using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public interface IPredictionService
{
    Task<PredictionResult> GeneratePredictionAsync(string symbol, decimal currentPrice, List<KlineCandle> klines, CycleIndicator cycle);
}

