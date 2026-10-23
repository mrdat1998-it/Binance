using BinanceTracker.Api.Models;
using BinanceTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SentimentController : ControllerBase
{
    private readonly ISentimentService _sentimentService;

    public SentimentController(ISentimentService sentimentService)
    {
        _sentimentService = sentimentService;
    }

    [HttpGet]
    public async Task<ActionResult<SentimentData>> GetSentiment()
    {
        var data = await _sentimentService.GetMarketSentimentAsync();
        return Ok(data);
    }
}

