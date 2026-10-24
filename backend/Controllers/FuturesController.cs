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
        decimal initialPrice = coin?.Price ?? 80800m;

        // 1. Lấy dữ liệu nến và phân tích kỹ thuật
        var klines = await _binanceService.GetKlinesAsync(symbol, interval, 120);
        var analysis = await _futuresService.GetFuturesAnalysisAsync(symbol, initialPrice, interval, klines);

        // 2. Giám sát giá live với các lệnh active: Dùng giá MarkPrice thật từ Binance Futures
        decimal trueLivePrice = analysis.MarkPrice > 0 ? analysis.MarkPrice : initialPrice;
        if (trueLivePrice > 0)
        {
            await _tradeHistoryService.EvaluateActiveSignalsAsync(symbol, trueLivePrice);
        }

        // 3. Chỉ khi KHÔNG còn lệnh active nào đang chạy và tín hiệu mới đạt chuẩn > 70% thì mới ghi nhận lệnh mới!
        var activeTrade = await _tradeHistoryService.GetActiveSignalAsync(symbol);
        if (activeTrade == null && analysis.IctSetup != null && analysis.IctSetup.ConfidenceScore > 70 && analysis.IctSetup.OptimalEntry > 0)
        {
            await _tradeHistoryService.RecordSignalIfEligibleAsync(symbol, interval, analysis.IctSetup, trueLivePrice);
        }

        return Ok(analysis);
    }

    [HttpGet("history")]
    public async Task<ActionResult<TradeHistorySummary>> GetTradeHistory(
        [FromQuery] string? symbol = null,
        [FromQuery] string? status = null)
    {
        var history = await _tradeHistoryService.GetHistorySummaryAsync(symbol, status);
        return Ok(history);
    }

    [HttpPost("history/reset")]
    public async Task<ActionResult> ResetTradeHistory()
    {
        await _tradeHistoryService.ResetHistoryAsync();
        return Ok(new { message = "Lịch sử tín hiệu lệnh đã được đặt lại thành công." });
    }
}
