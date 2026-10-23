using BinanceTracker.Api.Models;
using BinanceTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly IBinanceService _binanceService;

    public MarketController(IBinanceService binanceService)
    {
        _binanceService = binanceService;
    }

    [HttpGet("klines")]
    public async Task<ActionResult<List<KlineCandle>>> GetKlines(
        [FromQuery] string symbol = "BTCUSDT",
        [FromQuery] string interval = "1d",
        [FromQuery] int limit = 180)
    {
        var klines = await _binanceService.GetKlinesAsync(symbol, interval, limit);
        return Ok(klines);
    }
}

