using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public interface IMarketCycleService
{
    Task<CycleIndicator> AnalyzeCycleAsync(string symbol, decimal currentPrice, List<KlineCandle> klines);
}

