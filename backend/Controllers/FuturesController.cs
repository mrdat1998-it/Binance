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
    private readonly ITradeHistoryService _tradeHistoryService;

    public FuturesController(
        IFuturesService futuresService,
        IBinanceService binanceService,
        ITradeHistoryService tradeHistoryService)
    {
        _futuresService = futuresService;
        _binanceService = binanceService;
        _tradeHistoryService = tradeHistoryService;
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

        // Evaluate active signals against current live price
        await _tradeHistoryService.EvaluateActiveSignalsAsync(symbol, fallbackPrice);

        // Record signal if confluence score > 70%
        if (analysis.IctSetup != null && analysis.IctSetup.ConfidenceScore > 70)
        {
            await _tradeHistoryService.RecordSignalIfEligibleAsync(symbol, interval, analysis.IctSetup, fallbackPrice);
        }

        return Ok(analysis);
    }

    [HttpGet("history")]
    public async Task<ActionResult<TradeHistorySummary>> GetTradeHistory([FromQuery] string? symbol = null)
    {
        var history = await _tradeHistoryService.GetHistorySummaryAsync(symbol);
        return Ok(history);
    }

    [HttpPost("history/reset")]
    public async Task<ActionResult> ResetTradeHistory()
    {
        await _tradeHistoryService.ResetHistoryAsync();
        return Ok(new { message = "Lịch sử tín hiệu lệnh đã được đặt lại thành công." });
    }
}
