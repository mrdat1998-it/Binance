using BinanceTracker.Api.Models;
using BinanceTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionController : ControllerBase
{
    private readonly IBinanceService _binanceService;
    private readonly IMarketCycleService _cycleService;
    private readonly IPredictionService _predictionService;

    public PredictionController(
        IBinanceService binanceService,
        IMarketCycleService cycleService,
        IPredictionService predictionService)
    {
        _binanceService = binanceService;
        _cycleService = cycleService;
        _predictionService = predictionService;
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<PredictionResult>> GetPrediction(string symbol)
    {
        var summary = await _binanceService.GetCoinSummaryAsync(symbol);
        decimal price = summary?.Price ?? 50000m;
        var klines = await _binanceService.GetKlinesAsync(symbol, "1d", 200);
        var cycle = await _cycleService.AnalyzeCycleAsync(symbol, price, klines);

        var prediction = await _predictionService.GeneratePredictionAsync(symbol, price, klines, cycle);
        return Ok(prediction);
    }
}

