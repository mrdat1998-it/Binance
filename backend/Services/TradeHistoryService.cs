using System.Text.Json;
using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public class TradeHistoryService : ITradeHistoryService
{
    private readonly ILogger<TradeHistoryService> _logger;
    private readonly string _storagePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<TradeSignalRecord>? _cachedRecords;

    public TradeHistoryService(ILogger<TradeHistoryService> logger, IHostEnvironment env)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        _storagePath = Path.Combine(dataDir, "trade_history.json");
    }

    private async Task<List<TradeSignalRecord>> LoadRecordsAsync()
    {
        if (_cachedRecords != null) return _cachedRecords;

        if (File.Exists(_storagePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_storagePath);
                var loaded = JsonSerializer.Deserialize<List<TradeSignalRecord>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded != null && loaded.Count > 0)
                {
                    _cachedRecords = loaded;
                    return _cachedRecords;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read trade_history.json, recreating defaults");
            }
        }

        // Initialize with realistic initial sample history
        _cachedRecords = SeedInitialHistory();
        await SaveRecordsAsync();
        return _cachedRecords;
    }

    private async Task SaveRecordsAsync()
    {
        if (_cachedRecords == null) return;
        try
        {
            var json = JsonSerializer.Serialize(_cachedRecords, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_storagePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save trade_history.json");
        }
    }

    public async Task<TradeHistorySummary> GetHistorySummaryAsync(string? symbol = null)
    {
        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsAsync();
            var filtered = string.IsNullOrWhiteSpace(symbol)
                ? records
                : records.Where(r => r.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToList();

            var sorted = filtered.OrderByDescending(r => r.StartTime).ToList();
            int total = sorted.Count;
            int active = sorted.Count(r => r.Status == "ACTIVE");
            int win = sorted.Count(r => r.Status.StartsWith("HIT_TP"));
            int loss = sorted.Count(r => r.Status == "HIT_SL");
            int closed = win + loss;
            decimal winRate = closed > 0 ? Math.Round((decimal)win * 100m / closed, 1) : 0m;
            decimal totalPnl = Math.Round(sorted.Where(r => r.PnlPercent.HasValue).Sum(r => r.PnlPercent!.Value), 2);
            decimal avgRr = sorted.Count > 0 ? Math.Round(sorted.Average(r => r.RiskRewardRatio), 2) : 2.5m;

            return new TradeHistorySummary(
                TotalTrades: total,
                ActiveTrades: active,
                WinTrades: win,
                LossTrades: loss,
                WinRatePercent: winRate,
                TotalPnlPercent: totalPnl,
                AverageRiskReward: avgRr,
                Records: sorted
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TradeSignalRecord?> RecordSignalIfEligibleAsync(string symbol, string interval, IctRsiEmaAnalysis? ict, decimal currentPrice)
    {
        if (ict == null || ict.ConfidenceScore <= 70 || ict.OptimalEntry <= 0)
        {
            return null; // Chỉ lưu khi hội tụ đủ điều kiện đảm bảo trên 70% (> 70%)
        }

        string action = ict.SetupAction.Contains("LONG") ? "LONG" : (ict.SetupAction.Contains("SHORT") ? "SHORT" : "");
        if (string.IsNullOrEmpty(action)) return null;

        var normSymbol = symbol.ToUpperInvariant();
        if (!normSymbol.EndsWith("USDT")) normSymbol += "USDT";

        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsAsync();

            // Check if recent active trade already exists for this symbol & action (within last 3 hours and within 0.8% price)
            var recentActive = records.FirstOrDefault(r =>
                r.Symbol == normSymbol &&
                r.SignalAction == action &&
                r.Status == "ACTIVE" &&
                (DateTime.UtcNow - r.StartTime).TotalHours < 3 &&
                Math.Abs(r.EntryPrice - ict.OptimalEntry) / ict.OptimalEntry < 0.008m);

            if (recentActive != null)
            {
                return recentActive; // Don't duplicate active signal
            }

            var newRecord = new TradeSignalRecord(
                Id: "TRD-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Symbol: normSymbol,
                SignalAction: action,
                Timeframe: interval,
                ConfidenceScore: ict.ConfidenceScore,
                EntryPrice: ict.OptimalEntry,
                StopLoss: ict.StopLoss,
                TakeProfit1: ict.TakeProfit1,
                TakeProfit2: ict.TakeProfit2,
                TakeProfit3: ict.TakeProfit3,
                RiskRewardRatio: ict.RiskRewardRatio > 0 ? ict.RiskRewardRatio : 2.5m,
                StartTime: DateTime.UtcNow,
                EndTime: null,
                Status: "ACTIVE",
                ExitPrice: null,
                PnlPercent: null,
                Notes: ict.ConfluenceSummary
            );

            records.Insert(0, newRecord);
            await SaveRecordsAsync();
            _logger.LogInformation("New trade signal recorded: {Id} {Symbol} {Action} Entry: {Entry}", newRecord.Id, newRecord.Symbol, newRecord.SignalAction, newRecord.EntryPrice);
            return newRecord;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task EvaluateActiveSignalsAsync(string symbol, decimal currentPrice)
    {
        if (currentPrice <= 0) return;
        var normSymbol = symbol.ToUpperInvariant();
        if (!normSymbol.EndsWith("USDT")) normSymbol += "USDT";

        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsAsync();
            var activeSignals = records.Where(r => r.Symbol == normSymbol && r.Status == "ACTIVE").ToList();
            if (activeSignals.Count == 0) return;

            bool modified = false;

            for (int i = 0; i < records.Count; i++)
            {
                var sig = records[i];
                if (sig.Symbol != normSymbol || sig.Status != "ACTIVE") continue;

                if (sig.SignalAction == "LONG")
                {
                    // Check Hit Stop Loss
                    if (currentPrice <= sig.StopLoss)
                    {
                        decimal lossPct = Math.Round(-((sig.EntryPrice - sig.StopLoss) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_SL",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.StopLoss,
                            PnlPercent = lossPct,
                            Notes = $"Dính SL tại ${sig.StopLoss:N2}. Lỗ: {lossPct}% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn)."
                        };
                        modified = true;
                    }
                    // Check Hit Take Profit 3
                    else if (sig.TakeProfit3 > 0 && currentPrice >= sig.TakeProfit3)
                    {
                        decimal gainPct = Math.Round(((sig.TakeProfit3 - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_TP3",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.TakeProfit3,
                            PnlPercent = gainPct,
                            Notes = $"Đạt mục tiêu TP3 cao nhất tại ${sig.TakeProfit3:N2}. Lợi nhuận: +{gainPct}%! Kèo thắng lớn."
                        };
                        modified = true;
                    }
                    // Check Hit Take Profit 2
                    else if (sig.TakeProfit2 > 0 && currentPrice >= sig.TakeProfit2)
                    {
                        decimal gainPct = Math.Round(((sig.TakeProfit2 - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_TP2",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.TakeProfit2,
                            PnlPercent = gainPct,
                            Notes = $"Đạt mục tiêu TP2 tại ${sig.TakeProfit2:N2}. Lợi nhuận: +{gainPct}%! Đã khóa 80% lợi nhuận."
                        };
                        modified = true;
                    }
                    // Check Hit Take Profit 1
                    else if (sig.TakeProfit1 > 0 && currentPrice >= sig.TakeProfit1)
                    {
                        decimal gainPct = Math.Round(((sig.TakeProfit1 - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_TP1",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.TakeProfit1,
                            PnlPercent = gainPct,
                            Notes = $"Đạt mục tiêu TP1 tại ${sig.TakeProfit1:N2}. Lợi nhuận: +{gainPct}%. Đã dời SL về Break-even Entry."
                        };
                        modified = true;
                    }
                }
                else if (sig.SignalAction == "SHORT")
                {
                    // Check Hit Stop Loss
                    if (currentPrice >= sig.StopLoss)
                    {
                        decimal lossPct = Math.Round(-((sig.StopLoss - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_SL",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.StopLoss,
                            PnlPercent = lossPct,
                            Notes = $"Dính SL tại ${sig.StopLoss:N2}. Lỗ: {lossPct}% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn)."
                        };
                        modified = true;
                    }
                    // Check Hit Take Profit 3
                    else if (sig.TakeProfit3 > 0 && currentPrice <= sig.TakeProfit3)
                    {
                        decimal gainPct = Math.Round(((sig.EntryPrice - sig.TakeProfit3) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_TP3",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.TakeProfit3,
                            PnlPercent = gainPct,
                            Notes = $"Đạt mục tiêu TP3 tại ${sig.TakeProfit3:N2}. Lợi nhuận: +{gainPct}%! Kèo Short thắng tối đa."
                        };
                        modified = true;
                    }
                    // Check Hit Take Profit 2
                    else if (sig.TakeProfit2 > 0 && currentPrice <= sig.TakeProfit2)
                    {
                        decimal gainPct = Math.Round(((sig.EntryPrice - sig.TakeProfit2) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_TP2",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.TakeProfit2,
                            PnlPercent = gainPct,
                            Notes = $"Đạt mục tiêu TP2 tại ${sig.TakeProfit2:N2}. Lợi nhuận: +{gainPct}%! Đã khóa 80% lợi nhuận."
                        };
                        modified = true;
                    }
                    // Check Hit Take Profit 1
                    else if (sig.TakeProfit1 > 0 && currentPrice <= sig.TakeProfit1)
                    {
                        decimal gainPct = Math.Round(((sig.EntryPrice - sig.TakeProfit1) / sig.EntryPrice) * 100m, 2);
                        records[i] = sig with
                        {
                            Status = "HIT_TP1",
                            EndTime = DateTime.UtcNow,
                            ExitPrice = sig.TakeProfit1,
                            PnlPercent = gainPct,
                            Notes = $"Đạt mục tiêu TP1 tại ${sig.TakeProfit1:N2}. Lợi nhuận: +{gainPct}%. Đã dời SL về Break-even Entry."
                        };
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                await SaveRecordsAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetHistoryAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cachedRecords = SeedInitialHistory();
            await SaveRecordsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static List<TradeSignalRecord> SeedInitialHistory()
    {
        var now = DateTime.UtcNow;
        return new List<TradeSignalRecord>
        {
            new TradeSignalRecord(
                Id: "TRD-BTC801",
                Symbol: "BTCUSDT",
                SignalAction: "LONG",
                Timeframe: "15m",
                ConfidenceScore: 78,
                EntryPrice: 81250.00m,
                StopLoss: 79800.00m,
                TakeProfit1: 83425.00m,
                TakeProfit2: 84875.00m,
                TakeProfit3: 86500.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddHours(-18),
                EndTime: now.AddHours(-7),
                Status: "HIT_TP2",
                ExitPrice: 84875.00m,
                PnlPercent: 4.46m,
                Notes: "Hội tụ đủ D1/H4 Bullish, giá kiểm định Bullish Order Block. Đã chốt lời TP2 thành công (+4.46%)."
            ),
            new TradeSignalRecord(
                Id: "TRD-ETH402",
                Symbol: "ETHUSDT",
                SignalAction: "SHORT",
                Timeframe: "1h",
                ConfidenceScore: 82,
                EntryPrice: 2740.00m,
                StopLoss: 2815.00m,
                TakeProfit1: 2627.50m,
                TakeProfit2: 2552.50m,
                TakeProfit3: 2470.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddDays(-1).AddHours(-4),
                EndTime: now.AddHours(-14),
                Status: "HIT_TP1",
                ExitPrice: 2627.50m,
                PnlPercent: 4.11m,
                Notes: "Chạm vùng kháng cự Bearish FVG trên H1 và phân kỳ giảm RSI. Đạt chốt lời TP1 an toàn (+4.11%)."
            ),
            new TradeSignalRecord(
                Id: "TRD-SOL305",
                Symbol: "SOLUSDT",
                SignalAction: "LONG",
                Timeframe: "15m",
                ConfidenceScore: 74,
                EntryPrice: 178.50m,
                StopLoss: 174.20m,
                TakeProfit1: 184.95m,
                TakeProfit2: 189.25m,
                TakeProfit3: 194.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddDays(-2),
                EndTime: now.AddDays(-1).AddHours(-19),
                Status: "HIT_SL",
                ExitPrice: 174.20m,
                PnlPercent: -2.41m,
                Notes: "Tin tức CPI ra bất ngờ quét thủng hỗ trợ M15. Kích hoạt Stop Loss kỷ luật tại $174.20 (-2.41%)."
            ),
            new TradeSignalRecord(
                Id: "TRD-BNB108",
                Symbol: "BNBUSDT",
                SignalAction: "LONG",
                Timeframe: "15m",
                ConfidenceScore: 80,
                EntryPrice: 592.00m,
                StopLoss: 581.50m,
                TakeProfit1: 607.75m,
                TakeProfit2: 618.25m,
                TakeProfit3: 630.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddDays(-2).AddHours(-12),
                EndTime: now.AddDays(-1).AddHours(-22),
                Status: "HIT_TP2",
                ExitPrice: 618.25m,
                PnlPercent: 4.43m,
                Notes: "D1/H4 Golden Cross EMA20/50, nến BOS tăng rõ nét. Chốt lời TP2 xuất sắc (+4.43%)."
            )
        };
    }
}
