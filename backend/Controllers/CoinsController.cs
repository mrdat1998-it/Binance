using BinanceTracker.Api.Models;
using BinanceTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoinsController : ControllerBase
{
    private readonly IBinanceService _binanceService;

    public CoinsController(IBinanceService binanceService)
    {
        _binanceService = binanceService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CoinSummary>>> GetTopCoins()
    {
        var coins = await _binanceService.GetTopCoinsAsync();
        return Ok(coins);
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<CoinSummary>> GetCoin(string symbol)
    {
        var coin = await _binanceService.GetCoinSummaryAsync(symbol);
        if (coin == null)
        {
            return NotFound(new { message = $"Coin {symbol} không tìm thấy" });
        }
        return Ok(coin);
    }

    [HttpGet("supported")]
    public ActionResult<List<CoinInfo>> GetSupportedCoins()
    {
        return Ok(_binanceService.GetSupportedCoins());
    }
}

