using BinanceTracker.Api.Models;
using BinanceTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CycleController : ControllerBase
{
    private readonly IBinanceService _binanceService;
    private readonly IMarketCycleService _cycleService;

    public CycleController(IBinanceService binanceService, IMarketCycleService cycleService)
    {
        _binanceService = binanceService;
        _cycleService = cycleService;
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<CycleIndicator>> GetCycleAnalysis(string symbol)
    {
        var summary = await _binanceService.GetCoinSummaryAsync(symbol);
        decimal price = summary?.Price ?? 50000m;
        var klines = await _binanceService.GetKlinesAsync(symbol, "1d", 200);

        var cycle = await _cycleService.AnalyzeCycleAsync(symbol, price, klines);

        // Enrich with actual drawdown
        if (summary != null)
        {
            cycle = cycle with { AthDrawdownPercent = summary.AthDrawdownPercent };
        }

        return Ok(cycle);
    }
}

