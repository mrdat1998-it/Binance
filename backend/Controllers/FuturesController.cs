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

        // 1. Giám sát giá live với các lệnh active trước: Kiểm tra đã chạm SL hay TP chưa
        await _tradeHistoryService.EvaluateActiveSignalsAsync(symbol, fallbackPrice);

        // 2. Lấy phân tích (nếu còn lệnh active thì FuturesService khóa giữ nguyên vị thế active, không phát lệnh mới)
        var klines = await _binanceService.GetKlinesAsync(symbol, interval, 120);
        var analysis = await _futuresService.GetFuturesAnalysisAsync(symbol, fallbackPrice, interval, klines);

        // 3. Chỉ khi KHÔNG còn lệnh active nào đang chạy và tín hiệu mới đạt chuẩn > 70% thì mới ghi nhận lệnh mới!
        var activeTrade = await _tradeHistoryService.GetActiveSignalAsync(symbol);
        if (activeTrade == null && analysis.IctSetup != null && analysis.IctSetup.ConfidenceScore > 70 && analysis.IctSetup.OptimalEntry > 0)
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
