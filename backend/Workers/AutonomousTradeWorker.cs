using BinanceTracker.Api.Services;

namespace BinanceTracker.Api.Workers;

/// <summary>
/// Tiến trình ngầm tự động 24/7 (Autonomous Background Worker):
/// Hoạt động độc lập mà không cần bất kỳ ai mở trình duyệt Web.
/// Định kỳ tự động quét giá Binance Futures, đánh giá SL/TP các lệnh đang chạy,
/// và phân tích tín hiệu ICT SMC để ghi nhận lệnh mới vào SQL Server.
/// </summary>
public class AutonomousTradeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutonomousTradeWorker> _logger;
    private static readonly string[] MonitoredSymbols = { "BTC", "ETH", "BNB", "SOL", "NEAR" };

    public AutonomousTradeWorker(IServiceScopeFactory scopeFactory, ILogger<AutonomousTradeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(">>> [AUTONOMOUS WORKER] Đã khởi động dịch vụ quét ngầm tự động 24/7 (Độc lập, không phụ thuộc trình duyệt Web).");

        // Chờ 5 giây ban đầu để Database và API nạp cấu hình xong
        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var binanceService = scope.ServiceProvider.GetRequiredService<IBinanceService>();
                var futuresService = scope.ServiceProvider.GetRequiredService<IFuturesService>();
                var tradeHistoryService = scope.ServiceProvider.GetRequiredService<ITradeHistoryService>();

                foreach (var symbol in MonitoredSymbols)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        var coin = await binanceService.GetCoinSummaryAsync(symbol);
                        decimal initialPrice = coin?.Price ?? 0m;

                        var klines = await binanceService.GetKlinesAsync(symbol, "15m", 120);
                        var analysis = await futuresService.GetFuturesAnalysisAsync(symbol, initialPrice, "15m", klines);

                        decimal trueLivePrice = analysis.MarkPrice > 0 ? analysis.MarkPrice : initialPrice;
                        if (trueLivePrice > 0)
                        {
                            // 1. Tự động kiểm tra dính SL hoặc TP đối với các vị thế đang chạy
                            await tradeHistoryService.EvaluateActiveSignalsAsync(symbol, trueLivePrice);
                        }

                        // 2. Tự động phát hiện tín hiệu: Khi không có lệnh chạy và tín hiệu đạt chuẩn (>70% confidence)
                        var activeTrade = await tradeHistoryService.GetActiveSignalAsync(symbol);
                        if (activeTrade == null && analysis.IctSetup != null && analysis.IctSetup.ConfidenceScore > 70 && analysis.IctSetup.OptimalEntry > 0)
                        {
                            // Kiểm tra điều kiện phiên: Trong vùng chết (02:00 - 07:00), không mở lệnh trừ khi điểm hội tụ >= 82%
                            if (analysis.SessionInfo == null || analysis.SessionInfo.IsTradeAllowed || analysis.IctSetup.ConfidenceScore >= 82)
                            {
                                await tradeHistoryService.RecordSignalIfEligibleAsync(symbol, "15m", analysis.IctSetup, trueLivePrice);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("AutonomousTradeWorker: Lỗi khi xử lý {Symbol}: {Message}", symbol, ex.Message);
                    }

                    // Tạm nghỉ 1 giây giữa các coin để tối ưu kết nối
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutonomousTradeWorker: Lỗi trong chu kỳ quét thị trường ngầm.");
            }

            // Chu kỳ nghỉ 25 giây trước lượt quét kế tiếp
            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
        }
    }
}
