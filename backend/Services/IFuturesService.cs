using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public interface IFuturesService
{
    Task<FuturesAnalysis> GetFuturesAnalysisAsync(string symbol, decimal fallbackPrice, string interval = "15m", List<KlineCandle>? klines = null);
}
