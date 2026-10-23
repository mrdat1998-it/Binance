using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public interface IBinanceService
{
    Task<List<CoinSummary>> GetTopCoinsAsync();
    Task<CoinSummary?> GetCoinSummaryAsync(string symbol);
    Task<List<KlineCandle>> GetKlinesAsync(string symbol, string interval = "1d", int limit = 180);
    List<CoinInfo> GetSupportedCoins();
}

