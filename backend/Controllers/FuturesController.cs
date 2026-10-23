using BinanceTracker.Api.Models;
using BinanceTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FuturesController : ControllerBase
{
    private readonly IFuturesService _futuresService;
    private readonly IBinanceService _binanceService;

    public FuturesController(IFuturesService futuresService, IBinanceService binanceService)
    {
        _futuresService = futuresService;
        _binanceService = binanceService;
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<FuturesAnalysis>> GetFuturesAnalysis(
        string symbol,
        [FromQuery] string interval = "15m")
    {
        var coin = await _binanceService.GetCoinSummaryAsync(symbol);
        decimal fallbackPrice = coin?.Price ?? 50000m;
        var klines = await _binanceService.GetKlinesAsync(symbol, interval, 120);

        var analysis = await _futuresService.GetFuturesAnalysisAsync(symbol, fallbackPrice, interval, klines);
        return Ok(analysis);
    }
}
