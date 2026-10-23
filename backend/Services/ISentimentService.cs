using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public interface ISentimentService
{
    Task<SentimentData> GetMarketSentimentAsync();
}

