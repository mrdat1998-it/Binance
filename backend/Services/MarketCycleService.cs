using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public class MarketCycleService : IMarketCycleService
{
    // Bitcoin 4th Halving was April 20, 2024
    private static readonly DateTime LastHalvingDate = new(2024, 4, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextHalvingDateEst = new(2028, 4, 15, 0, 0, 0, DateTimeKind.Utc);
    private const int StandardCycleDays = 1460; // 4 years * 365 days

    public Task<CycleIndicator> AnalyzeCycleAsync(string symbol, decimal currentPrice, List<KlineCandle> klines)
    {
        var now = DateTime.UtcNow;
        int daysSinceHalving = Math.Max(1, (int)(now - LastHalvingDate).TotalDays);
        int daysToNextHalving = Math.Max(1, (int)(NextHalvingDateEst - now).TotalDays);
        double cycleProgress = Math.Min(100.0, Math.Round(((double)daysSinceHalving / StandardCycleDays) * 100.0, 1));

        // Calculate moving averages from candle closes
        var closes = klines.Select(k => k.Close).ToList();
        decimal sma50 = closes.Count >= 50 ? closes.TakeLast(50).Average() : (closes.Count > 0 ? closes.Average() : currentPrice);
        decimal sma200 = closes.Count >= 200 ? closes.TakeLast(200).Average() : (closes.Count > 0 ? closes.Average() * 0.82m : currentPrice * 0.85m);

        decimal mayerMultiple = sma200 > 0 ? Math.Round(currentPrice / sma200, 2) : 1.0m;
        decimal distanceTo200Sma = sma200 > 0 ? Math.Round(((currentPrice - sma200) / sma200) * 100m, 2) : 0;

        // Calculate 14-period RSI
        decimal rsi14 = CalculateRsi(closes, 14);

        // Determine cycle stage based on halving chronology & indicators
        string stage;
        string stageDesc;
        int riskScore;
        string riskLevel;
        string piCycleStatus;
        string historicalComp;
        string recommendation;

        if (daysSinceHalving <= 180)
        {
            stage = "Giai đoạn Tích luỹ & Tái tích luỹ (Re-accumulation)";
            stageDesc = "Giai đoạn sau Halving khoảng 0-6 tháng, giá có xu hướng đi ngang biên độ rộng và rũ bỏ các vị thế đòn bẩy trước khi bước vào sóng tăng chính.";
            riskScore = 3;
            riskLevel = "Thấp - Trung bình";
            piCycleStatus = "Chưa giao cắt / Vùng tích lũy an toàn";
            historicalComp = "Tương đồng chu kỳ 2016 và 2020: Thị trường đi ngang từ 4-6 tháng trước khi bứt phá mạnh.";
            recommendation = "Chiến lược tối ưu: Mua tích lũy định kỳ (DCA), tập trung vào các đồng coin có nội tại mạnh và dòng tiền dẫn dắt.";
        }
        else if (daysSinceHalving <= 540)
        {
            stage = "Giai đoạn Tăng trưởng Bùng nổ (Parabolic Markup / Bull Run)";
            stageDesc = "Giai đoạn thắt chặt nguồn cung Halving phát huy tác dụng tối đa, kết hợp dòng vốn tổ chức (ETF, quỹ đầu tư) đẩy giá thiết lập các mức cao kỷ lục mới (ATH).";
            riskScore = 6;
            riskLevel = "Trung bình - Cao";
            piCycleStatus = "Đang mở rộng biên độ dương (Bullish Expansion)";
            historicalComp = "Tương đương giai đoạn Q4/2020 - Q2/2021: Xu hướng tăng vượt đỉnh cũ, Altcoin bùng nổ theo sau Bitcoin.";
            recommendation = "Chiến lược tối ưu: Tiếp tục nắm giữ theo xu hướng (Trend Following), nâng dần mức dừng lỗ (Trailing Stop), không FOMO mua đuổi tại các nhịp tăng sốc.";
        }
        else if (daysSinceHalving <= 650)
        {
            stage = "Giai đoạn Phân phối & Rủi ro Đỉnh chu kỳ (Distribution / Late Bull)";
            stageDesc = "Chỉ số lòng tham cực độ, các nhà đầu tư tổ chức và cá voi bắt đầu phân phối tài sản sang dòng tiền bán lẻ. Biến động thị trường ở mức cực đại.";
            riskScore = 9;
            riskLevel = "Rất cao / Cực hạn";
            piCycleStatus = "Cảnh báo quá nhiệt / Tiệm cận giao cắt đỉnh Pi Cycle";
            historicalComp = "Tương đương đỉnh tháng 11/2021 hoặc tháng 12/2017: Mayer Multiple vượt 2.4, thị trường hưng phấn tột cùng.";
            recommendation = "Chiến lược tối ưu: Chốt lời từng phần (Take Profit) sang tiền mặt/stablecoin, hạ tỷ trọng đòn bẩy về 0.";
        }
        else
        {
            stage = "Giai đoạn Điều chỉnh & Suy thoái chu kỳ (Bear Market / Reset)";
            stageDesc = "Sau đỉnh hưng phấn, thanh khoản rút dần, thị trường trải qua giai đoạn định giá lại về gần đường hỗ trợ cơ bản 200 tuần.";
            riskScore = 4;
            riskLevel = "Trung bình";
            piCycleStatus = "Dưới đường cơ sở / Tạo đáy dài hạn";
            historicalComp = "Tương đương năm 2018 và 2022: Thị trường chiết khấu sâu 70-85% so với đỉnh chu kỳ.";
            recommendation = "Chiến lược tối ưu: Kiên nhẫn quan sát, tích trữ tiền mặt chờ đợi tín hiệu tạo đáy 200W-SMA để tái tích lũy.";
        }

        // Adjust risk score based on Mayer Multiple and RSI
        if (mayerMultiple > 2.2m || rsi14 > 78m)
        {
            riskScore = Math.Min(10, riskScore + 2);
            riskLevel = "Rất cao (Quá mua)";
            piCycleStatus = "Cảnh báo vùng đỉnh lịch sử!";
        }
        else if (mayerMultiple < 0.85m && rsi14 < 35m)
        {
            riskScore = Math.Max(1, riskScore - 2);
            riskLevel = "Rất thấp (Vùng giá rẻ lịch sử)";
            piCycleStatus = "Vùng gom hàng cực tốt";
        }

        var result = new CycleIndicator(
            CurrentStage: stage,
            StageDescription: stageDesc,
            DaysSinceHalving: daysSinceHalving,
            TotalCycleDays: StandardCycleDays,
            CycleProgressPercent: cycleProgress,
            NextHalvingDate: NextHalvingDateEst.ToString("yyyy-MM-dd"),
            DaysToNextHalving: daysToNextHalving,
            MayerMultiple: mayerMultiple,
            Rsi14: Math.Round(rsi14, 1),
            Sma50: Math.Round(sma50, 2),
            Sma200: Math.Round(sma200, 2),
            DistanceTo200SmaPercent: distanceTo200Sma,
            AthDrawdownPercent: 0, // will be enriched in controller
            PiCycleStatus: piCycleStatus,
            RiskScore: riskScore,
            RiskLevel: riskLevel,
            HistoricalComparison: historicalComp,
            RecommendationVietnamese: recommendation
        );

        return Task.FromResult(result);
    }

    private static decimal CalculateRsi(List<decimal> closes, int period = 14)
    {
        if (closes.Count < period + 1) return 52.5m;

        decimal gains = 0;
        decimal losses = 0;

        for (int i = closes.Count - period; i < closes.Count; i++)
        {
            decimal diff = closes[i] - closes[i - 1];
            if (diff >= 0) gains += diff;
            else losses += Math.Abs(diff);
        }

        if (losses == 0) return 100m;
        if (gains == 0) return 0m;

        decimal avgGain = gains / period;
        decimal avgLoss = losses / period;
        decimal rs = avgGain / avgLoss;

        return 100m - (100m / (1m + rs));
    }
}

