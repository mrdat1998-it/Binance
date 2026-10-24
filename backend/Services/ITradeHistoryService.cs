using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public interface ITradeHistoryService
{
    Task<TradeHistorySummary> GetHistorySummaryAsync(string? symbol = null);
    Task<TradeSignalRecord?> GetActiveSignalAsync(string symbol);
    Task<TradeSignalRecord?> RecordSignalIfEligibleAsync(string symbol, string interval, IctRsiEmaAnalysis? ict, decimal currentPrice);
    Task EvaluateActiveSignalsAsync(string symbol, decimal currentPrice);
    Task ResetHistoryAsync();
}

