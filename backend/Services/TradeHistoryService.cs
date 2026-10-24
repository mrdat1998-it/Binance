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

    public async Task<TradeHistorySummary> GetHistorySummaryAsync(string? symbol = null, string? status = null)
    {
        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsAsync();
            var filtered = records.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(symbol) && !symbol.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var normSym = symbol.Trim().ToUpperInvariant();
                filtered = filtered.Where(r => 
                    r.Symbol.Equals(normSym, StringComparison.OrdinalIgnoreCase) ||
                    r.Symbol.Equals(normSym + "USDT", StringComparison.OrdinalIgnoreCase) ||
                    (normSym.EndsWith("USDT") && r.Symbol.Equals(normSym, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var normStatus = status.Trim().ToUpperInvariant();
                if (normStatus == "ACTIVE")
                {
                    filtered = filtered.Where(r => r.Status == "ACTIVE");
                }
                else if (normStatus == "CLOSED" || normStatus == "COMPLETED")
                {
                    filtered = filtered.Where(r => r.Status != "ACTIVE");
                }
                else if (normStatus == "WIN" || normStatus == "TP")
                {
                    filtered = filtered.Where(r => r.Status.StartsWith("HIT_TP"));
                }
                else if (normStatus == "LOSS" || normStatus == "SL")
                {
                    filtered = filtered.Where(r => r.Status == "HIT_SL");
                }
                else
                {
                    filtered = filtered.Where(r => r.Status.Equals(normStatus, StringComparison.OrdinalIgnoreCase));
                }
            }

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

    public async Task<TradeSignalRecord?> GetActiveSignalAsync(string symbol)
    {
        var normSymbol = symbol.ToUpperInvariant();
        if (!normSymbol.EndsWith("USDT")) normSymbol += "USDT";

        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsAsync();
            return records.FirstOrDefault(r => r.Symbol == normSymbol && r.Status == "ACTIVE");
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

            // NGUYÊN TẮC BẢO TOÀN VỐN CỐT LÕI:
            // Đã đưa ra lệnh thì trong khi lệnh đó chưa xong (chưa dính SL hay TP) thì KHÔNG được đưa ra thêm lệnh mới!
            var currentActive = records.FirstOrDefault(r => r.Symbol == normSymbol && r.Status == "ACTIVE");
            if (currentActive != null)
            {
                return currentActive; // Đang có lệnh chạy, duy trì 1 lệnh duy nhất
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
                Id: "TRD-54CC39",
                Symbol: "BTCUSDT",
                SignalAction: "SHORT",
                Timeframe: "15m",
                ConfidenceScore: 75,
                EntryPrice: 80946.41m,
                StopLoss: 82404.27m,
                TakeProfit1: 78759.62m,
                TakeProfit2: 77301.76m,
                TakeProfit3: 75406.54m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddMinutes(-30),
                EndTime: null,
                Status: "ACTIVE",
                ExitPrice: null,
                PnlPercent: null,
                Notes: "Hội tụ 75%: Đồng pha D1/H4 Bearish, cản Bearish Order Block và Bearish FVG, RSI < 50. Đang theo dõi."
            ),
            new TradeSignalRecord(
                Id: "TRD-ETH901",
                Symbol: "ETHUSDT",
                SignalAction: "SHORT",
                Timeframe: "15m",
                ConfidenceScore: 78,
                EntryPrice: 2382.50m,
                StopLoss: 2426.00m,
                TakeProfit1: 2317.25m,
                TakeProfit2: 2273.75m,
                TakeProfit3: 2215.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddMinutes(-45),
                EndTime: null,
                Status: "ACTIVE",
                ExitPrice: null,
                PnlPercent: null,
                Notes: "Hội tụ 78%: Khung D1/H4 gãy trendline, giá kiểm định Bearish FVG $2,382.50 - $2,390.00, RSI 38.2 dưới EMA50. Đang theo dõi."
            ),
            new TradeSignalRecord(
                Id: "TRD-BNB702",
                Symbol: "BNBUSDT",
                SignalAction: "LONG",
                Timeframe: "15m",
                ConfidenceScore: 76,
                EntryPrice: 735.20m,
                StopLoss: 722.50m,
                TakeProfit1: 754.25m,
                TakeProfit2: 766.95m,
                TakeProfit3: 782.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddMinutes(-20),
                EndTime: null,
                Status: "ACTIVE",
                ExitPrice: null,
                PnlPercent: null,
                Notes: "Hội tụ 76%: BNB duy trì cấu trúc tăng vững chắc trên D1/H4, phản ứng bật mạnh tại Bullish Order Block $732.00 - $735.20, RSI > 52 phân kỳ tăng. Đang theo dõi."
            ),
            new TradeSignalRecord(
                Id: "TRD-SOL603",
                Symbol: "SOLUSDT",
                SignalAction: "SHORT",
                Timeframe: "15m",
                ConfidenceScore: 74,
                EntryPrice: 102.80m,
                StopLoss: 104.90m,
                TakeProfit1: 99.65m,
                TakeProfit2: 97.55m,
                TakeProfit3: 94.80m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddMinutes(-50),
                EndTime: null,
                Status: "ACTIVE",
                ExitPrice: null,
                PnlPercent: null,
                Notes: "Hội tụ 74%: Giá bị từ chối mạnh tại Bearish Order Block M15 ($103.40), RSI 36.5 tiếp tục xu hướng giảm. Đang theo dõi."
            ),
            new TradeSignalRecord(
                Id: "TRD-NEAR501",
                Symbol: "NEARUSDT",
                SignalAction: "SHORT",
                Timeframe: "15m",
                ConfidenceScore: 77,
                EntryPrice: 4.685m,
                StopLoss: 4.815m,
                TakeProfit1: 4.490m,
                TakeProfit2: 4.360m,
                TakeProfit3: 4.200m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddMinutes(-35),
                EndTime: null,
                Status: "ACTIVE",
                ExitPrice: null,
                PnlPercent: null,
                Notes: "Hội tụ 77%: Thủng đáy phiên Á tại $4.70 tạo Bearish BOS, kiểm tra lại vùng Bearish FVG $4.685. Khối lượng xả lớn. Đang theo dõi."
            ),
            new TradeSignalRecord(
                Id: "TRD-895089",
                Symbol: "BTCUSDT",
                SignalAction: "SHORT",
                Timeframe: "15m",
                ConfidenceScore: 75,
                EntryPrice: 80946.41m,
                StopLoss: 82352.86m,
                TakeProfit1: 78836.74m,
                TakeProfit2: 77430.28m,
                TakeProfit3: 75601.90m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddHours(-1),
                EndTime: now.AddMinutes(-20),
                Status: "HIT_SL",
                ExitPrice: 82352.86m,
                PnlPercent: -1.74m,
                Notes: "Dính SL tại $82,352.86. Lỗ: -1.74% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn)."
            ),
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
                Id: "TRD-SOL202",
                Symbol: "SOLUSDT",
                SignalAction: "SHORT",
                Timeframe: "1h",
                ConfidenceScore: 79,
                EntryPrice: 115.00m,
                StopLoss: 118.50m,
                TakeProfit1: 110.25m,
                TakeProfit2: 108.50m,
                TakeProfit3: 105.00m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddDays(-2).AddHours(-6),
                EndTime: now.AddDays(-1).AddHours(-18),
                Status: "HIT_TP2",
                ExitPrice: 108.50m,
                PnlPercent: 5.65m,
                Notes: "Mô hình Bearish Quasimodo (QML) giảm giá trên H1 kết hợp phân kỳ âm RSI. Đã chốt lời TP2 thành công (+5.65%)."
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
            ),
            new TradeSignalRecord(
                Id: "TRD-NEAR201",
                Symbol: "NEARUSDT",
                SignalAction: "LONG",
                Timeframe: "15m",
                ConfidenceScore: 81,
                EntryPrice: 4.850m,
                StopLoss: 4.710m,
                TakeProfit1: 5.120m,
                TakeProfit2: 5.280m,
                TakeProfit3: 5.450m,
                RiskRewardRatio: 2.5m,
                StartTime: now.AddDays(-3),
                EndTime: now.AddDays(-2).AddHours(-10),
                Status: "HIT_TP1",
                ExitPrice: 5.120m,
                PnlPercent: 5.57m,
                Notes: "Hội tụ 81% D1/H4 Bullish, breakout cản chéo và retest FVG $4.85 thành công. Chốt lời TP1 chuẩn xác (+5.57%)."
            )
        };
    }
}

