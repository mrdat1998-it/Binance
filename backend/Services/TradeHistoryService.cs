using BinanceTracker.Api.Data;
using BinanceTracker.Api.Data.Entities;
using BinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BinanceTracker.Api.Services;

public class TradeHistoryService : ITradeHistoryService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TradeHistoryService> _logger;

    public TradeHistoryService(AppDbContext dbContext, ILogger<TradeHistoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TradeHistorySummary> GetHistorySummaryAsync(string? symbol = null, string? status = null, string? session = null)
    {
        try
        {
            var query = _dbContext.TradeSignals.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(symbol) && !symbol.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var normSym = symbol.Trim().ToUpperInvariant();
                query = query.Where(r => 
                    r.Symbol == normSym || 
                    r.Symbol == normSym + "USDT" ||
                    (normSym.EndsWith("USDT") && r.Symbol == normSym));
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var normStatus = status.Trim().ToUpperInvariant();
                if (normStatus == "ACTIVE")
                {
                    query = query.Where(r => r.Status == "ACTIVE");
                }
                else if (normStatus == "CLOSED" || normStatus == "COMPLETED")
                {
                    query = query.Where(r => r.Status != "ACTIVE");
                }
                else if (normStatus == "WIN" || normStatus == "TP")
                {
                    query = query.Where(r => r.Status.StartsWith("HIT_TP"));
                }
                else if (normStatus == "LOSS" || normStatus == "SL")
                {
                    query = query.Where(r => r.Status == "HIT_SL");
                }
                else
                {
                    query = query.Where(r => r.Status == normStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(session) && !session.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var normSession = session.Trim().ToUpperInvariant();
                query = query.Where(r => r.Session == normSession);
            }

            var entities = await query.OrderByDescending(r => r.StartTime).ToListAsync();
            var records = entities.Select(e => e.ToRecord()).ToList();

            int total = records.Count;
            int active = records.Count(r => r.Status == "ACTIVE");
            int win = records.Count(r => r.Status.StartsWith("HIT_TP"));
            int loss = records.Count(r => r.Status == "HIT_SL");
            int closed = win + loss;
            decimal winRate = closed > 0 ? Math.Round((decimal)win * 100m / closed, 1) : 0m;
            decimal totalPnl = Math.Round(records.Where(r => r.PnlPercent.HasValue).Sum(r => r.PnlPercent!.Value), 2);
            decimal avgRr = records.Count > 0 ? Math.Round(records.Average(r => r.RiskRewardRatio), 2) : 2.5m;

            return new TradeHistorySummary(
                TotalTrades: total,
                ActiveTrades: active,
                WinTrades: win,
                LossTrades: loss,
                WinRatePercent: winRate,
                TotalPnlPercent: totalPnl,
                AverageRiskReward: avgRr,
                Records: records
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying trade history from database.");
            return new TradeHistorySummary(0, 0, 0, 0, 0, 0, 2.5m, new List<TradeSignalRecord>());
        }
    }

    public async Task<TradeSignalRecord?> GetActiveSignalAsync(string symbol)
    {
        var normSymbol = NormalizeSymbol(symbol);

        try
        {
            var entity = await _dbContext.TradeSignals
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Symbol == normSymbol && r.Status == "ACTIVE");

            return entity?.ToRecord();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active signal for {Symbol} from database.", normSymbol);
            return null;
        }
    }

    public async Task<TradeSignalRecord?> RecordSignalIfEligibleAsync(
        string symbol,
        string interval,
        IctRsiEmaAnalysis? ict,
        decimal currentPrice)
    {
        if (ict == null || ict.ConfidenceScore < 78 || ict.OptimalEntry <= 0 || !ict.SetupAction.StartsWith("MỞ VỊ THẾ"))
        {
            return null;
        }

        var normSymbol = NormalizeSymbol(symbol);
        string action = ict.SetupAction.Contains("LONG", StringComparison.OrdinalIgnoreCase) ? "LONG" : "SHORT";

        try
        {
            // NGUYÊN TẮC BẢO TOÀN VỐN CỐT LÕI:
            // Đã đưa ra lệnh thì trong khi lệnh đó chưa xong (chưa dính SL hay TP) thì KHÔNG được đưa ra thêm lệnh mới!
            var currentActive = await _dbContext.TradeSignals
                .FirstOrDefaultAsync(r => r.Symbol == normSymbol && r.Status == "ACTIVE");

            if (currentActive != null)
            {
                return currentActive.ToRecord(); // Đang có vị thế chạy, giữ nguyên
            }

            // Chống mở lệnh trùng lặp / Re-entry Spam:
            // Nếu có lệnh vừa kết thúc trong vòng 45 phút cùng chiều và giá Entry lệch < 0.6%, không tạo lệnh mới
            var recentClosed = await _dbContext.TradeSignals
                .Where(r => r.Symbol == normSymbol && r.SignalAction == action && r.Status != "ACTIVE")
                .OrderByDescending(r => r.EndTime ?? r.StartTime)
                .FirstOrDefaultAsync();

            if (recentClosed != null)
            {
                var timeSinceClose = DateTime.UtcNow - (recentClosed.EndTime ?? recentClosed.StartTime);
                if (timeSinceClose.TotalMinutes < 45)
                {
                    decimal priceDiffPct = Math.Abs(recentClosed.EntryPrice - ict.OptimalEntry) / ict.OptimalEntry * 100m;
                    if (priceDiffPct < 0.6m)
                    {
                        _logger.LogInformation("Database: Bỏ qua tạo lệnh trùng cho {Symbol} {Action} vì lệnh {RecentId} vừa đóng cách đây {Min:N0} phút tại vùng giá tương tự ({Entry}).",
                            normSymbol, action, recentClosed.Id, timeSinceClose.TotalMinutes, recentClosed.EntryPrice);
                        return null;
                    }
                }
            }

            // Điều kiện phiên: Không mở lệnh trong Vùng Chết (02:00 - 07:00) trừ khi điểm hội tụ >= 82%
            if (ict.SessionInfo != null && !ict.SessionInfo.IsTradeAllowed && ict.ConfidenceScore < 82)
            {
                _logger.LogInformation("Database: Bỏ qua tạo lệnh cho {Symbol} vì đang trong {Session} ({Advice}).",
                    normSymbol, ict.SessionInfo.SessionName, ict.SessionInfo.StrategyAdvice);
                return null;
            }

            var id = $"TRD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var now = DateTime.UtcNow;
            var sessionCode = ict.SessionInfo?.SessionCode ?? TradeSignalEntity.DetermineSessionFromTime(now);

            var entity = new TradeSignalEntity
            {
                Id = id,
                Symbol = normSymbol,
                SignalAction = action,
                Timeframe = interval,
                ConfidenceScore = ict.ConfidenceScore,
                EntryPrice = ict.OptimalEntry,
                StopLoss = ict.StopLoss,
                TakeProfit1 = ict.TakeProfit1,
                TakeProfit2 = ict.TakeProfit2,
                TakeProfit3 = ict.TakeProfit3,
                RiskRewardRatio = ict.RiskRewardRatio > 0 ? ict.RiskRewardRatio : 2.5m,
                StartTime = now,
                EndTime = null,
                Status = "ACTIVE",
                ExitPrice = null,
                PnlPercent = null,
                Notes = ict.ConfluenceSummary,
                Session = sessionCode,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _dbContext.TradeSignals.AddAsync(entity);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Database: Recorded new ACTIVE signal {Id} for {Symbol} {Action} at Entry={Entry}", 
                id, normSymbol, action, ict.OptimalEntry);

            return entity.ToRecord();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording trade signal for {Symbol} into database.", normSymbol);
            return null;
        }
    }

    public async Task EvaluateActiveSignalsAsync(string symbol, decimal currentPrice)
    {
        if (currentPrice <= 0) return;

        var normSymbol = NormalizeSymbol(symbol);

        try
        {
            var activeSignals = await _dbContext.TradeSignals
                .Where(r => r.Symbol == normSymbol && r.Status == "ACTIVE")
                .ToListAsync();

            if (activeSignals.Count == 0) return;

            bool modified = false;

            foreach (var sig in activeSignals)
            {
                if (sig.SignalAction == "LONG")
                {
                    // Check Hit Stop Loss
                    if (currentPrice <= sig.StopLoss)
                    {
                        decimal lossPct = Math.Round(-((sig.EntryPrice - sig.StopLoss) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_SL";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.StopLoss;
                        sig.PnlPercent = lossPct;
                        sig.Notes = $"Dính SL tại ${sig.StopLoss:N2}. Lỗ: {lossPct}% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn).";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogWarning("Database: Order {Id} ({Symbol} LONG) HIT STOP LOSS at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                    // Check Hit Take Profit 3
                    else if (sig.TakeProfit3 > 0 && currentPrice >= sig.TakeProfit3)
                    {
                        decimal gainPct = Math.Round(((sig.TakeProfit3 - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_TP3";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.TakeProfit3;
                        sig.PnlPercent = gainPct;
                        sig.Notes = $"Đạt mục tiêu TP3 cao nhất tại ${sig.TakeProfit3:N2}. Lợi nhuận: +{gainPct}%! Kèo thắng lớn.";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogInformation("Database: Order {Id} ({Symbol} LONG) HIT TP3 at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                    // Check Hit Take Profit 2
                    else if (sig.TakeProfit2 > 0 && currentPrice >= sig.TakeProfit2)
                    {
                        decimal gainPct = Math.Round(((sig.TakeProfit2 - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_TP2";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.TakeProfit2;
                        sig.PnlPercent = gainPct;
                        sig.Notes = $"Đạt mục tiêu TP2 tại ${sig.TakeProfit2:N2}. Lợi nhuận: +{gainPct}%! Đã khóa 80% lợi nhuận.";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogInformation("Database: Order {Id} ({Symbol} LONG) HIT TP2 at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                    // Check Hit Take Profit 1
                    else if (sig.TakeProfit1 > 0 && currentPrice >= sig.TakeProfit1)
                    {
                        decimal gainPct = Math.Round(((sig.TakeProfit1 - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_TP1";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.TakeProfit1;
                        sig.PnlPercent = gainPct;
                        sig.Notes = $"Đạt mục tiêu TP1 tại ${sig.TakeProfit1:N2}. Lợi nhuận: +{gainPct}%. Khuyến nghị dời SL về Entry hòa vốn.";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogInformation("Database: Order {Id} ({Symbol} LONG) HIT TP1 at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                }
                else if (sig.SignalAction == "SHORT")
                {
                    // Check Hit Stop Loss
                    if (currentPrice >= sig.StopLoss)
                    {
                        decimal lossPct = Math.Round(-((sig.StopLoss - sig.EntryPrice) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_SL";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.StopLoss;
                        sig.PnlPercent = lossPct;
                        sig.Notes = $"Dính SL tại ${sig.StopLoss:N2}. Lỗ: {lossPct}% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn).";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogWarning("Database: Order {Id} ({Symbol} SHORT) HIT STOP LOSS at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                    // Check Hit Take Profit 3
                    else if (sig.TakeProfit3 > 0 && currentPrice <= sig.TakeProfit3)
                    {
                        decimal gainPct = Math.Round(((sig.EntryPrice - sig.TakeProfit3) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_TP3";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.TakeProfit3;
                        sig.PnlPercent = gainPct;
                        sig.Notes = $"Đạt mục tiêu TP3 cao nhất tại ${sig.TakeProfit3:N2}. Lợi nhuận: +{gainPct}%! Kèo thắng lớn.";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogInformation("Database: Order {Id} ({Symbol} SHORT) HIT TP3 at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                    // Check Hit Take Profit 2
                    else if (sig.TakeProfit2 > 0 && currentPrice <= sig.TakeProfit2)
                    {
                        decimal gainPct = Math.Round(((sig.EntryPrice - sig.TakeProfit2) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_TP2";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.TakeProfit2;
                        sig.PnlPercent = gainPct;
                        sig.Notes = $"Đạt mục tiêu TP2 tại ${sig.TakeProfit2:N2}. Lợi nhuận: +{gainPct}%! Đã khóa 80% lợi nhuận.";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogInformation("Database: Order {Id} ({Symbol} SHORT) HIT TP2 at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                    // Check Hit Take Profit 1
                    else if (sig.TakeProfit1 > 0 && currentPrice <= sig.TakeProfit1)
                    {
                        decimal gainPct = Math.Round(((sig.EntryPrice - sig.TakeProfit1) / sig.EntryPrice) * 100m, 2);
                        sig.Status = "HIT_TP1";
                        sig.EndTime = DateTime.UtcNow;
                        sig.ExitPrice = sig.TakeProfit1;
                        sig.PnlPercent = gainPct;
                        sig.Notes = $"Đạt mục tiêu TP1 tại ${sig.TakeProfit1:N2}. Lợi nhuận: +{gainPct}%. Khuyến nghị dời SL về Entry hòa vốn.";
                        sig.UpdatedAt = DateTime.UtcNow;
                        modified = true;
                        _logger.LogInformation("Database: Order {Id} ({Symbol} SHORT) HIT TP1 at ${Price}", sig.Id, sig.Symbol, currentPrice);
                    }
                }
            }

            if (modified)
            {
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating active signals in database for {Symbol}.", normSymbol);
        }
    }

    public async Task ResetHistoryAsync()
    {
        try
        {
            _dbContext.TradeSignals.RemoveRange(_dbContext.TradeSignals);
            await _dbContext.SaveChangesAsync();

            await DbInitializer.InitializeDatabaseAsync(
                _dbContext, 
                new MockEnv(), 
                _logger);

            _logger.LogInformation("Database: Reset trade history completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting trade history in database.");
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        var norm = symbol.Trim().ToUpperInvariant();
        if (!norm.EndsWith("USDT")) norm += "USDT";
        return norm;
    }

    private class MockEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "BinanceTracker.Api";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Development";
    }
}
