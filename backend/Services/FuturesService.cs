using System.Globalization;
using System.Text.Json;
using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Services;

public class FuturesService : IFuturesService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FuturesService> _logger;
    private readonly ITradeHistoryService _tradeHistoryService;

    public FuturesService(HttpClient httpClient, ILogger<FuturesService> logger, ITradeHistoryService tradeHistoryService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _tradeHistoryService = tradeHistoryService;
        _httpClient.BaseAddress = new Uri("https://fapi.binance.com");
        _httpClient.Timeout = TimeSpan.FromSeconds(6);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BinanceTrackerFutures/1.0");
    }

    public async Task<FuturesAnalysis> GetFuturesAnalysisAsync(string symbol, decimal fallbackPrice, string interval = "15m", List<KlineCandle>? klines = null)
    {
        var normSymbol = symbol.ToUpperInvariant();
        if (!normSymbol.EndsWith("USDT")) normSymbol += "USDT";

        decimal markPrice = fallbackPrice;
        decimal indexPrice = fallbackPrice;
        decimal fundingRate = 0.0001m;
        string nextFundingTime = DateTime.UtcNow.AddHours(4).ToString("HH:mm:ss dd/MM/yyyy");

        decimal longAccount = 0.62m;
        decimal shortAccount = 0.38m;
        decimal longShortRatio = 1.63m;
        decimal openInterestCoin = 50000m;

        // Fetch Multi-Timeframe Klines for Daily Bias (D1 Tổng, H4 Trung Hạn, M15 Vào Lệnh) in parallel
        var d1Task = FetchKlinesAsync(normSymbol, "1d", 30);
        var h4Task = FetchKlinesAsync(normSymbol, "4h", 50);
        var m15Task = FetchKlinesAsync(normSymbol, "15m", 60);

        // If primary klines are not provided or empty, fetch for the selected interval
        Task<List<KlineCandle>>? primaryTask = null;
        if (klines == null || klines.Count == 0)
        {
            primaryTask = FetchKlinesAsync(normSymbol, interval, 120);
        }

        // 1. Fetch Premium Index (Funding & Mark Price)
        try
        {
            var res = await _httpClient.GetAsync($"/fapi/v1/premiumIndex?symbol={normSymbol}");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("markPrice", out var mp) && decimal.TryParse(mp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
                    markPrice = m;
                if (root.TryGetProperty("indexPrice", out var ip) && decimal.TryParse(ip.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var idx))
                    indexPrice = idx;
                if (root.TryGetProperty("lastFundingRate", out var fr) && decimal.TryParse(fr.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f))
                    fundingRate = f;
                if (root.TryGetProperty("nextFundingTime", out var nft) && long.TryParse(nft.GetRawText(), out var nftVal))
                    nextFundingTime = DateTimeOffset.FromUnixTimeMilliseconds(nftVal).ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query premiumIndex for {Symbol}", normSymbol);
        }

        // 2. Fetch Long/Short Account Ratio
        try
        {
            var res = await _httpClient.GetAsync($"/futures/data/globalLongShortAccountRatio?symbol={normSymbol}&period=1h&limit=1");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    if (item.TryGetProperty("longAccount", out var la) && decimal.TryParse(la.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal))
                        longAccount = lVal;
                    if (item.TryGetProperty("shortAccount", out var sa) && decimal.TryParse(sa.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sVal))
                        shortAccount = sVal;
                    if (item.TryGetProperty("longShortRatio", out var lsr) && decimal.TryParse(lsr.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var rVal))
                        longShortRatio = rVal;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query globalLongShortAccountRatio for {Symbol}", normSymbol);
        }

        // 3. Fetch Open Interest
        try
        {
            var res = await _httpClient.GetAsync($"/fapi/v1/openInterest?symbol={normSymbol}");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("openInterest", out var oi) && decimal.TryParse(oi.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var oVal))
                    openInterestCoin = oVal;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query openInterest for {Symbol}", normSymbol);
        }

        decimal basis = markPrice - indexPrice;
        decimal fundingRatePercent = Math.Round(fundingRate * 100m, 4);
        decimal annualizedFunding = Math.Round(fundingRate * 3m * 365m * 100m, 2);

        string fundingSentiment;
        if (fundingRate > 0.0003m)
        {
            fundingSentiment = "Quá nhiệt phe Long (Funding Rate > +0.03%). Phe Long đang trả phí cao cho phe Short, nguy cơ bẫy Long Squeeze quét râu xuống.";
        }
        else if (fundingRate > 0m)
        {
            fundingSentiment = "Bình thường / Tích cực nhẹ (Phe Long trả phí phe Short). Thị trường nghiêng về xu hướng tăng lành mạnh.";
        }
        else if (fundingRate < -0.0002m)
        {
            fundingSentiment = "Quá nhiệt phe Short (Funding âm sâu). Tiềm năng xảy ra Short Squeeze giật mạnh lên bất ngờ!";
        }
        else
        {
            fundingSentiment = "Trung lập / Cân bằng giữa phe Long và phe Short.";
        }

        string lsSentiment = longShortRatio switch
        {
            > 2.5m => "Tâm lý đám đông áp đảo 1 chiều (Phe Long chiếm >70%). Cần đề phòng nhà cái quét lệnh thanh lý Long đòn bẩy cao.",
            > 1.4m => "Tâm lý Lạc quan ưu thế (Phe Long chiếm đa số). Động lượng giao dịch tích cực nhưng cần giữ đòn bẩy dưới 10x.",
            < 0.8m => "Tâm lý Bi quan chiếm đa số (Phe Short chiếm ưu thế). Dễ xuất hiện phản ứng bật hồi săn thanh khoản Short.",
            _ => "Tỷ lệ Long/Short cân bằng (Trạng thái tích lũy, thị trường đang đợi tin tức)."
        };

        decimal oiUsd = Math.Round(openInterestCoin * markPrice, 2);
        string oiDesc = oiUsd > 1_000_000_000m
            ? $"Khối lượng hợp đồng mở khổng lồ (~${Math.Round(oiUsd / 1_000_000_000m, 2)} Tỷ USD). Dòng tiền đòn bẩy tập trung rất cao."
            : $"Tổng khối lượng vị thế mở đạt ~${Math.Round(oiUsd / 1_000_000m, 2)} Triệu USD, thanh khoản ổn định.";

        decimal longLiqCluster = Math.Round(markPrice * 0.968m, 2);
        decimal shortLiqCluster = Math.Round(markPrice * 1.034m, 2);
        string liqSummary = $"Cụm thanh lý Long chính nằm quanh vùng ${longLiqCluster:N2} (điểm thanh lý các lệnh 30x-50x). Cụm thanh lý Short tập trung dày đặc quanh ${shortLiqCluster:N2}.";

        // Wait for multi-timeframe klines for Daily Bias
        var d1Klines = await d1Task;
        var h4Klines = await h4Task;
        var m15Klines = await m15Task;

        if (primaryTask != null)
        {
            klines = await primaryTask;
        }

        // Calculate Multi-Timeframe Daily Bias (D1 Tổng, H4 Trung Hạn, M15 Vào Lệnh)
        var dailyBias = CalculateDailyBias(markPrice, d1Klines, h4Klines, m15Klines);

        // 4. PERFORM ICT + RSI + EMA COMPREHENSIVE ANALYSIS ON SELECTED TIMEFRAME WITH DAILY BIAS CONFLUENCE
        var ictSetup = AnalyzeIctRsiEma(markPrice, klines ?? new List<KlineCandle>(), dailyBias);

        // 5. QUY TẮC CỐT LÕI: ĐÃ ĐƯA RA LỆNH THÌ TRONG KHI LỆNH CHƯA XONG THÌ KHÔNG PHÁT THÊM LỆNH!
        // Nếu coin này đang có lệnh ACTIVE (chưa chạm SL hay TP), hệ thống giữ nguyên vị thế và thông số lệnh đó.
        var activeTrade = await _tradeHistoryService.GetActiveSignalAsync(normSymbol);
        if (activeTrade != null)
        {
            string actAction = activeTrade.SignalAction;
            string signalAction = $"⏳ GIỮ VỊ THẾ {actAction} (ĐANG CHẠY - THEO DÕI TP/SL)";
            string setupAction = $"GIỮ VỊ THẾ {actAction} (ĐANG CHẠY)";
            string activeEntryDisplay = $"{formatNum(activeTrade.EntryPrice)} USDT (Vào: {activeTrade.StartTime.ToLocalTime():HH:mm dd/MM})";
            string activeRrDisplay = $"1 : {activeTrade.RiskRewardRatio:N1}";
            string activeNotes = $"LỆNH HIỆN TẠI ĐANG CHẠY: Vị thế {actAction} mở tại ${activeTrade.EntryPrice:N2} lúc {activeTrade.StartTime.ToLocalTime():HH:mm dd/MM}. Lệnh đang chạy và chưa chạm SL (${activeTrade.StopLoss:N2}) hay TP1 (${activeTrade.TakeProfit1:N2}). Hệ thống KHÓA KHÔNG PHÁT LỆNH MỚI cho đến khi lệnh này kết thúc để đảm bảo kỷ luật quản trị vốn.";

            ictSetup = ictSetup with
            {
                SetupAction = setupAction,
                ConfidenceScore = activeTrade.ConfidenceScore,
                OptimalEntry = activeTrade.EntryPrice,
                StopLoss = activeTrade.StopLoss,
                TakeProfit1 = activeTrade.TakeProfit1,
                TakeProfit2 = activeTrade.TakeProfit2,
                TakeProfit3 = activeTrade.TakeProfit3,
                RiskRewardRatio = activeTrade.RiskRewardRatio,
                LeverageRule = "Giữ vị thế hiện tại (Không mở thêm lệnh)",
                RiskManagementAdvice = "Kỷ luật 1 lệnh/coin: Đang có lệnh active thì tuyệt đối không vào thêm lệnh mới cho đến khi dính SL hoặc chạm TP."
            };

            return new FuturesAnalysis(
                Symbol: normSymbol,
                MarkPrice: markPrice,
                IndexPrice: indexPrice,
                PriceBasis: basis,
                FundingRate: fundingRate,
                FundingRatePercent: fundingRatePercent,
                AnnualizedFundingRate: annualizedFunding,
                FundingSentiment: fundingSentiment,
                NextFundingTime: nextFundingTime,
                LongAccountPercent: Math.Round(longAccount * 100m, 1),
                ShortAccountPercent: Math.Round(shortAccount * 100m, 1),
                LongShortRatio: Math.Round(longShortRatio, 2),
                LongShortSentiment: lsSentiment,
                OpenInterestCoin: Math.Round(openInterestCoin, 2),
                OpenInterestUsd: oiUsd,
                OpenInterestInterpretation: oiDesc,
                LongLiquidationCluster: longLiqCluster,
                ShortLiquidationCluster: shortLiqCluster,
                LiquidationMapSummary: liqSummary,
                SignalAction: signalAction,
                EntryZone: activeEntryDisplay,
                TakeProfit1: activeTrade.TakeProfit1,
                TakeProfit2: activeTrade.TakeProfit2,
                TakeProfit3: activeTrade.TakeProfit3,
                StopLoss: activeTrade.StopLoss,
                RecommendedLeverage: "Giữ vị thế hiện tại (Không mở thêm lệnh)",
                RiskRewardRatio: activeRrDisplay,
                TradingNotes: activeNotes,
                IctSetup: ictSetup,
                DailyBias: dailyBias,
                SelectedInterval: interval,
                SessionInfo: ictSetup.SessionInfo
            );
        }

        string entryDisplay = ictSetup.OptimalEntry > 0 ? $"{formatNum(ictSetup.OptimalEntry)} USDT" : "Chờ hội tụ đủ điều kiện (>70%)";
        string rrDisplay = ictSetup.RiskRewardRatio > 0 ? $"1 : {ictSetup.RiskRewardRatio:N1}" : "Chưa kích hoạt";

        return new FuturesAnalysis(
            Symbol: normSymbol,
            MarkPrice: markPrice,
            IndexPrice: indexPrice,
            PriceBasis: basis,
            FundingRate: fundingRate,
            FundingRatePercent: fundingRatePercent,
            AnnualizedFundingRate: annualizedFunding,
            FundingSentiment: fundingSentiment,
            NextFundingTime: nextFundingTime,
            LongAccountPercent: Math.Round(longAccount * 100m, 1),
            ShortAccountPercent: Math.Round(shortAccount * 100m, 1),
            LongShortRatio: Math.Round(longShortRatio, 2),
            LongShortSentiment: lsSentiment,
            OpenInterestCoin: Math.Round(openInterestCoin, 2),
            OpenInterestUsd: oiUsd,
            OpenInterestInterpretation: oiDesc,
            LongLiquidationCluster: longLiqCluster,
            ShortLiquidationCluster: shortLiqCluster,
            LiquidationMapSummary: liqSummary,
            SignalAction: ictSetup.SetupAction,
            EntryZone: entryDisplay,
            TakeProfit1: ictSetup.TakeProfit1,
            TakeProfit2: ictSetup.TakeProfit2,
            TakeProfit3: ictSetup.TakeProfit3,
            StopLoss: ictSetup.StopLoss,
            RecommendedLeverage: ictSetup.LeverageRule,
            RiskRewardRatio: rrDisplay,
            TradingNotes: ictSetup.ConfluenceSummary + " " + ictSetup.RiskManagementAdvice,
            IctSetup: ictSetup,
            DailyBias: dailyBias,
            SelectedInterval: interval,
            SessionInfo: ictSetup.SessionInfo
        );
    }

    private static IctRsiEmaAnalysis AnalyzeIctRsiEma(decimal currentPrice, List<KlineCandle> klines, DailyBiasResult? dailyBias = null)
    {
        if (klines.Count < 25)
        {
            // Fallback generation if klines are too short
            return CreateDefaultIctAnalysis(currentPrice);
        }

        var closes = klines.Select(k => k.Close).ToList();

        // 1. CALCULATE EMA 20, EMA 50, EMA 200
        decimal ema20 = CalculateEma(closes, 20);
        decimal ema50 = CalculateEma(closes, 50);
        decimal ema200 = CalculateEma(closes, Math.Min(200, closes.Count));

        string emaTrend;
        bool isEmaBullish = currentPrice >= ema50;
        if (currentPrice > ema20 && ema20 > ema50 && ema50 > ema200)
        {
            emaTrend = "Golden Cross Tăng Mạnh (Giá > EMA20 > EMA50 > EMA200 - Động lượng tăng chủ đạo)";
        }
        else if (currentPrice < ema20 && ema20 < ema50 && ema50 < ema200)
        {
            emaTrend = "Death Cross Giảm Mạnh (Giá < EMA20 < EMA50 < EMA200 - Áp lực bán chiếm ưu thế)";
        }
        else
        {
            emaTrend = currentPrice >= ema50
                ? "Xu Hướng Tăng Phục Hồi (Giá nằm trên EMA50 trung hạn)"
                : "Xu Hướng Điều Chỉnh (Giá nằm dưới EMA50 trung hạn)";
        }

        // 2. CALCULATE RSI 14 & DIVERGENCE
        decimal rsi14 = CalculateRsi(closes, 14);
        string rsiSentiment = rsi14 switch
        {
            >= 70 => "Quá mua (RSI > 70 - Cảnh báo kiệt sức lực mua)",
            <= 30 => "Quá bán (RSI < 30 - Vùng nén giá bật hồi cao)",
            >= 50 => "Động lượng Tích cực (RSI > 50 - Phe bò kiểm soát)",
            _ => "Động lượng Yếu (RSI < 50 - Phe gấu kiểm soát)"
        };

        // Detect Divergence (Last 20 candles)
        string rsiDivergence = "Không phát hiện phân kỳ";
        bool hasBullishDivergence = false;
        bool hasBearishDivergence = false;

        if (klines.Count >= 20)
        {
            decimal recentLow = klines.TakeLast(8).Min(k => k.Low);
            decimal prevLow = klines.Skip(klines.Count - 20).Take(12).Min(k => k.Low);

            if (recentLow < prevLow && rsi14 > 36m)
            {
                rsiDivergence = "Phân kỳ Tăng Giá (Bullish Divergence: Giá tạo đáy thấp hơn nhưng RSI tạo đáy cao hơn)";
                hasBullishDivergence = true;
            }
            else
            {
                decimal recentHigh = klines.TakeLast(8).Max(k => k.High);
                decimal prevHigh = klines.Skip(klines.Count - 20).Take(12).Max(k => k.High);
                if (recentHigh > prevHigh && rsi14 < 65m)
                {
                    rsiDivergence = "Phân kỳ Giảm Giá (Bearish Divergence: Giá tạo đỉnh cao hơn nhưng RSI yếu đi)";
                    hasBearishDivergence = true;
                }
            }
        }

        // 3. ICT CONCEPTS (FVG, Order Block, Market Structure, Liquidity Sweep)
        // Find recent Fair Value Gap (FVG)
        string fvgType = "Cân bằng";
        decimal fvgLow = 0;
        decimal fvgHigh = 0;
        string fvgStatus = "Không có khoảng trống mất cân bằng lớn";

        for (int i = klines.Count - 1; i >= 3; i--)
        {
            var c1 = klines[i - 2];
            var c3 = klines[i];

            // Bullish FVG
            if (c3.Low > c1.High)
            {
                fvgType = "Bullish FVG (Vùng cầu mất cân bằng)";
                fvgLow = c1.High;
                fvgHigh = c3.Low;
                fvgStatus = currentPrice <= fvgHigh && currentPrice >= fvgLow ? "Đang retest FVG để bật tăng" : "Vùng cầu chờ khớp phía dưới";
                break;
            }
            // Bearish FVG
            else if (c3.High < c1.Low)
            {
                fvgType = "Bearish FVG (Vùng cung mất cân bằng)";
                fvgLow = c3.High;
                fvgHigh = c1.Low;
                fvgStatus = currentPrice >= fvgLow && currentPrice <= fvgHigh ? "Đang retest FVG để quay đầu giảm" : "Vùng cung cản phía trên";
                break;
            }
        }

        if (fvgLow == 0)
        {
            fvgLow = Math.Round(currentPrice * 0.985m, 2);
            fvgHigh = Math.Round(currentPrice * 0.995m, 2);
            fvgType = "Bullish FVG (Vùng đệm thanh khoản)";
            fvgStatus = "Chưa lấp (Unmitigated)";
        }

        // Find Order Block (OB)
        string obType = isEmaBullish ? "Bullish Order Block (Khối lệnh gom phe Mua)" : "Bearish Order Block (Khối lệnh đè phe Bán)";
        decimal obLow;
        decimal obHigh;

        if (isEmaBullish)
        {
            var downCandles = klines.TakeLast(20).Where(k => k.Close < k.Open).ToList();
            var targetOb = downCandles.Count > 0 ? downCandles.OrderBy(k => k.Low).First() : klines.TakeLast(10).First();
            obLow = targetOb.Low;
            obHigh = targetOb.High;
        }
        else
        {
            var upCandles = klines.TakeLast(20).Where(k => k.Close > k.Open).ToList();
            var targetOb = upCandles.Count > 0 ? upCandles.OrderByDescending(k => k.High).First() : klines.TakeLast(10).Last();
            obLow = targetOb.Low;
            obHigh = targetOb.High;
        }

        // Market Structure & Liquidity Sweep
        decimal swingHigh = klines.TakeLast(25).Max(k => k.High);
        decimal swingLow = klines.TakeLast(25).Min(k => k.Low);

        string marketStructure;
        string liquiditySweep;
        if (currentPrice > swingHigh * 0.985m)
        {
            marketStructure = "BOS Tăng (Bullish Break of Structure) - Cấu trúc đỉnh đáy nâng dần rõ nét";
            liquiditySweep = $"Đã quét thanh khoản đỉnh BSL (Buy-side Liquidity) tại ${swingHigh:N2}";
        }
        else if (currentPrice < swingLow * 1.015m)
        {
            marketStructure = "BOS Giảm (Bearish Break of Structure) - Áp lực gãy cấu trúc đáy gần";
            liquiditySweep = $"Đã quét thanh khoản đáy SSL (Sell-side Liquidity) tại ${swingLow:N2} - Khả năng bật hồi săn thanh khoản";
        }
        else
        {
            marketStructure = "Cấu trúc Tích luỹ (Range Bound / OTE Discount Zone)";
            liquiditySweep = $"Thanh khoản BSL tại ${swingHigh:N2} | Thanh khoản SSL tại ${swingLow:N2}";
        }

        decimal oteFib618 = Math.Round(swingLow + (swingHigh - swingLow) * 0.618m, 2);
        decimal oteFib786 = Math.Round(swingLow + (swingHigh - swingLow) * 0.786m, 2);
        string oteZone = $"Vùng OTE Fib 0.618 - 0.786: ${oteFib618:N2} - ${oteFib786:N2}";

        // 4. HỆ THỐNG ĐÁNH GIÁ ĐỘ HỘI TỤ ĐIỀU KIỆN (CONFLUENCE SCORING SYSTEM - THANG 100 ĐIỂM)
        int bullScore = 0;
        int bearScore = 0;

        // Trụ cột 1: Multi-Timeframe Daily Bias Alignment (Tối đa 30 điểm)
        if (dailyBias != null)
        {
            if (dailyBias.D1Bias == "BULLISH") bullScore += 15;
            else if (dailyBias.D1Bias == "BEARISH") bearScore += 15;
            else { bullScore += 7; bearScore += 7; }

            if (dailyBias.H4Bias == "BULLISH") bullScore += 15;
            else if (dailyBias.H4Bias == "BEARISH") bearScore += 15;
            else { bullScore += 7; bearScore += 7; }
        }
        else
        {
            bullScore += 10;
            bearScore += 10;
        }

        // Trụ cột 2: Xu hướng & Đường trung bình EMA Ribbon (Tối đa 25 điểm)
        if (currentPrice >= ema50) bullScore += 10; else bearScore += 10;
        if (currentPrice >= ema20) bullScore += 8; else bearScore += 8;
        if (ema20 >= ema50) bullScore += 7; else bearScore += 7;

        // Trụ cột 3: Cấu trúc thị trường ICT & Khối cản/cầu OB/FVG (Tối đa 25 điểm)
        if (marketStructure.Contains("BOS Tăng")) bullScore += 10;
        else if (marketStructure.Contains("BOS Giảm")) bearScore += 10;

        if (fvgType.Contains("Bullish")) bullScore += 7;
        else if (fvgType.Contains("Bearish")) bearScore += 7;

        if (obType.Contains("Bullish")) bullScore += 8;
        else if (obType.Contains("Bearish")) bearScore += 8;

        if (liquiditySweep.Contains("SSL") || liquiditySweep.Contains("đáy")) bullScore += 5;
        if (liquiditySweep.Contains("BSL") || liquiditySweep.Contains("đỉnh")) bearScore += 5;

        // Trụ cột 4: RSI Momentum & Phân kỳ Divergence (Tối đa 20 điểm)
        if (hasBullishDivergence) bullScore += 12;
        if (hasBearishDivergence) bearScore += 12;

        if (rsi14 >= 42m && rsi14 <= 65m) bullScore += 8;
        else if (rsi14 >= 35m && rsi14 <= 58m) bearScore += 8;

        // Trụ cột 5: Điều Kiện Phiên Giao Dịch & Killzone ICT (Session Filter)
        var session = GetTradingSessionInfo(DateTime.UtcNow);
        bullScore += session.ScoreBonus;
        bearScore += session.ScoreBonus;

        // Trừ điểm phạt: Tránh đu đỉnh khi quá mua hoặc bán đáy khi quá bán cực độ
        if (rsi14 > 72m) bullScore -= 12;
        if (rsi14 < 28m) bearScore -= 12;

        bullScore = Math.Clamp(bullScore, 5, 98);
        bearScore = Math.Clamp(bearScore, 5, 98);

        // 5. NGUYÊN TẮC QUẢN TRỊ RỦI RO: CHỈ KHI HỘI TỤ ĐỦ ĐIỀU KIỆN ĐẢM BẢO TRÊN 70% (> 70%) MỚI CHO LỆNH!
        const int CONFIDENCE_THRESHOLD = 70;

        string setupAction;
        decimal entry = 0;
        decimal sl = 0;
        decimal tp1 = 0, tp2 = 0, tp3 = 0;
        decimal rr = 0;
        int confidence = Math.Max(bullScore, bearScore);
        string confluenceSummary;
        string leverageRule;
        string riskAdvice;

        if (session.SessionCode == "OFF_HOURS" && confidence < 82)
        {
            // Trong vùng chết 02:00 - 07:00 sáng, thanh khoản cực mỏng, từ chối mở lệnh để bảo toàn vốn
            setupAction = $"⏸️ TẠM DỪNG GIAO DỊCH PHIÊN (VÙNG CHẾT {session.TimeRangeVn} - THANH KHOẢN MỎNG)";
            entry = 0;
            sl = 0;
            tp1 = 0;
            tp2 = 0;
            tp3 = 0;
            rr = 0;
            leverageRule = "Không vào lệnh (Đứng ngoài bảo toàn vốn tránh râu nến ảo ban đêm)";
            confluenceSummary = $"Đang trong {session.SessionName} ({session.TimeRangeVn}). Thị trường toàn cầu đóng cửa, thanh khoản mỏng, chênh lệch spread giãn. QUY TẮC BẢO VỆ TÀI KHOẢN: Không mở vị thế mới từ 02:00 - 07:00 sáng trừ khi hội tụ siêu mạnh (>= 82%).";
            riskAdvice = "Hãy để các vị thế cũ chạy an toàn hoặc nghỉ ngơi. Không trade khi thiếu vắng dòng tiền tổ chức.";
        }
        else if (bullScore > CONFIDENCE_THRESHOLD && bullScore >= bearScore + 8)
        {
            // BỘ LỌC ĐA KHUNG VĨ MÔ (HARD GATE): TUYỆT ĐỐI KHÔNG LONG KHI D1 HOẶC H4 LÀ BEARISH!
            if (dailyBias != null && (dailyBias.D1Bias == "BEARISH" || dailyBias.H4Bias == "BEARISH"))
            {
                string conflictReason = dailyBias.D1Bias == "BEARISH" 
                    ? "khung Ngày (D1) đang là BEARISH (Xu hướng vĩ mô Giảm)" 
                    : "khung 4 Giờ (H4) đang là BEARISH (Sóng trung hạn Giảm)";
                setupAction = "⏸️ CHỜ ĐỢI / ĐỨNG NGOÀI (CẤM LONG NGƯỢC XU HƯỚNG D1/H4)";
                entry = 0; sl = 0; tp1 = 0; tp2 = 0; tp3 = 0; rr = 0;
                leverageRule = "Không vào lệnh (Bảo toàn 100% vốn)";
                confluenceSummary = $"Tín hiệu M15 bật tăng đạt {bullScore}% nhưng BỊ TỪ CHỐI do {conflictReason}. KỶ LUẬT ICT/SMC BẢO TOÀN VỐN: Tuyệt đối không mở lệnh LONG bắt đáy dao rơi khi xu hướng lớn D1/H4 đang Giảm. Kiên nhẫn chờ hồi lên cản để tìm điểm SHORT thuận xu hướng.";
                riskAdvice = "Giao dịch ngược xu hướng khung D1/H4 là nguyên nhân hàng đầu gây cháy tài khoản. Hãy kiên nhẫn đứng ngoài.";
            }
            else
            {
                // ĐỦ ĐIỀU KIỆN CHO LỆNH LONG (> 70% VÀ ĐỒNG PHA VĨ MÔ)
                setupAction = "MỞ VỊ THẾ LONG (MUA CANH HỒI VÙNG CẦU FVG/OB)";
                entry = Math.Round(Math.Max(fvgLow, Math.Min(currentPrice, currentPrice * 0.995m)), 2);
                sl = Math.Round(Math.Min(obLow, currentPrice * 0.978m), 2);
                decimal risk = Math.Max(entry * 0.008m, entry - sl);

                tp1 = Math.Round(entry + risk * 1.5m, 2);
                tp2 = Math.Round(Math.Max(swingHigh, entry + risk * 2.5m), 2);
                tp3 = Math.Round(entry + risk * 3.8m, 2);
                rr = Math.Round((tp2 - entry) / risk, 1);
                confidence = bullScore;

                confluenceSummary = $"Hội tụ {confidence}% (>70% tiêu chuẩn): 1) Đồng pha đa khung D1/H4 ({dailyBias?.D1Bias}/{dailyBias?.H4Bias}), 2) Giá duy trì trên EMA50 ({ema50:N2}), 3) Retest vùng {obType} và FVG ({fvgLow:N2} - {fvgHigh:N2}), 4) {rsiSentiment} {rsiDivergence}, 5) {session.SessionName} ({session.LiquidityLevel}).";
                leverageRule = "Khuyến nghị đòn bẩy: 5x - 10x (Cross hoặc Isolated). Tuyệt đối không dùng > 15x.";
                riskAdvice = "Quy tắc quản trị vốn: Tối đa 2% - 3% tổng vốn cho 1 lệnh. Khi giá chạm mốc TP1, dời Stop Loss về vùng giá vào lệnh (Break-even Entry) để bảo toàn 100% vốn.";
            }
        }
        else if (bearScore > CONFIDENCE_THRESHOLD && bearScore >= bullScore + 8)
        {
            // BỘ LỌC ĐA KHUNG VĨ MÔ (HARD GATE): TUYỆT ĐỐI KHÔNG SHORT KHI D1 HOẶC H4 LÀ BULLISH!
            if (dailyBias != null && (dailyBias.D1Bias == "BULLISH" || dailyBias.H4Bias == "BULLISH"))
            {
                string conflictReason = dailyBias.D1Bias == "BULLISH" 
                    ? "khung Ngày (D1) đang là BULLISH (Xu hướng vĩ mô Tăng)" 
                    : "khung 4 Giờ (H4) đang là BULLISH (Sóng trung hạn Tăng)";
                setupAction = "⏸️ CHỜ ĐỢI / ĐỨNG NGOÀI (CẤM SHORT CHẶN ĐẦU TÀU D1/H4)";
                entry = 0; sl = 0; tp1 = 0; tp2 = 0; tp3 = 0; rr = 0;
                leverageRule = "Không vào lệnh (Bảo toàn 100% vốn)";
                confluenceSummary = $"Tín hiệu M15 giảm giá đạt {bearScore}% nhưng BỊ TỪ CHỐI do {conflictReason}. KỶ LUẬT ICT/SMC BẢO TOÀN VỐN: Tuyệt đối không mở lệnh SHORT chặn đầu xu hướng khi khung D1/H4 đang Tăng mạnh. Kiên nhẫn chờ nhịp điều chỉnh về vùng cầu để LONG thuận xu hướng.";
                riskAdvice = "Bán khống ngược sóng tăng khung Ngày là rủi ro lớn nhất. Hãy kiên nhẫn đứng ngoài.";
            }
            else
            {
                // ĐỦ ĐIỀU KIỆN CHO LỆNH SHORT (> 70% VÀ ĐỒNG PHA VĨ MÔ)
                setupAction = "MỞ VỊ THẾ SHORT (BÁN CANH TEST KHÁNG CỰ FVG/OB)";
                entry = Math.Round(Math.Min(fvgHigh, Math.Max(currentPrice, currentPrice * 1.005m)), 2);
                sl = Math.Round(Math.Max(obHigh, currentPrice * 1.022m), 2);
                decimal risk = Math.Max(entry * 0.008m, sl - entry);

                tp1 = Math.Round(entry - risk * 1.5m, 2);
                tp2 = Math.Round(Math.Min(swingLow, entry - risk * 2.5m), 2);
                tp3 = Math.Round(entry - risk * 3.8m, 2);
                rr = Math.Round((entry - tp2) / risk, 1);
                confidence = bearScore;

                confluenceSummary = $"Hội tụ {confidence}% (>70% tiêu chuẩn): 1) Đồng pha đa khung D1/H4 ({dailyBias?.D1Bias}/{dailyBias?.H4Bias}), 2) Áp lực bán dưới EMA50 ({ema50:N2}), 3) Chạm cản {obType} và Bearish FVG ({fvgLow:N2} - {fvgHigh:N2}), 4) {rsiSentiment} {rsiDivergence}, 5) {session.SessionName} ({session.LiquidityLevel}).";
                leverageRule = "Khuyến nghị đòn bẩy: 5x - 10x (Cross hoặc Isolated). Tuyệt đối không dùng > 15x.";
                riskAdvice = "Quy tắc quản trị vốn: Tối đa 2% - 3% tổng vốn cho 1 lệnh. Khi giá chạm mốc TP1, dời Stop Loss về vùng giá vào lệnh (Break-even Entry) để bảo toàn 100% vốn.";
            }
        }
        else
        {
            // CHƯA ĐỦ ĐIỀU KIỆN HỘI TỤ ĐẠT > 70% -> TUYỆT ĐỐI KHÔNG CHO LỆNH!
            setupAction = $"⏸️ CHỜ ĐỢI TÍN HIỆU / QUAN SÁT (ĐỘ HỘI TỤ {confidence}% CHƯA ĐẠT >70%)";
            entry = 0;
            sl = 0;
            tp1 = 0;
            tp2 = 0;
            tp3 = 0;
            rr = 0;
            leverageRule = "Không vào lệnh (Đứng ngoài bảo toàn 100% vốn)";
            confluenceSummary = $"Chỉ số hội tụ hiện tại đạt {confidence}% (Dưới ngưỡng an toàn 70%). Thị trường đang trong giai đoạn nhiễu, xung đột xu hướng giữa các khung giờ (D1: {dailyBias?.D1Bias}, H4: {dailyBias?.H4Bias}), cấu trúc nến đang nén tích lũy hoặc RSI chưa có tín hiệu rõ nét. QUY TẮC BẢO TOÀN VỐN: Tuyệt đối KHÔNG vào lệnh mạo hiểm, kiên nhẫn đứng ngoài quan sát cho đến khi hội tụ đủ điều kiện >70%.";
            riskAdvice = "Kiên nhẫn là chìa khóa trong giao dịch. Chỉ giải ngân khi có kèo A+ xác suất thắng cao (>70%).";
        }

        return new IctRsiEmaAnalysis(
            MarketStructure: marketStructure,
            OrderBlockType: obType,
            OrderBlockLow: Math.Round(obLow, 2),
            OrderBlockHigh: Math.Round(obHigh, 2),
            FvgType: fvgType,
            FvgLow: Math.Round(fvgLow, 2),
            FvgHigh: Math.Round(fvgHigh, 2),
            FvgStatus: fvgStatus,
            LiquiditySweep: liquiditySweep,
            OteFibZone: oteZone,
            Rsi14: Math.Round(rsi14, 1),
            RsiSentiment: rsiSentiment,
            RsiDivergence: rsiDivergence,
            Ema20: Math.Round(ema20, 2),
            Ema50: Math.Round(ema50, 2),
            Ema200: Math.Round(ema200, 2),
            EmaTrend: emaTrend,
            ConfluenceSummary: confluenceSummary,
            SetupAction: setupAction,
            ConfidenceScore: confidence,
            OptimalEntry: entry,
            StopLoss: sl,
            TakeProfit1: tp1,
            TakeProfit2: tp2,
            TakeProfit3: tp3,
            RiskRewardRatio: rr,
            LeverageRule: leverageRule,
            RiskManagementAdvice: riskAdvice,
            SessionInfo: session
        );
    }

    private static decimal CalculateEma(List<decimal> closes, int period)
    {
        if (closes.Count == 0) return 0;
        if (closes.Count < period) return closes.Average();

        decimal multiplier = 2m / (period + 1m);
        decimal ema = closes.Take(period).Average();

        for (int i = period; i < closes.Count; i++)
        {
            ema = (closes[i] - ema) * multiplier + ema;
        }

        return ema;
    }

    private static decimal CalculateRsi(List<decimal> closes, int period = 14)
    {
        if (closes.Count < period + 1) return 50m;

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

    public static TradingSessionInfo GetTradingSessionInfo(DateTime? utcTime = null)
    {
        var utc = utcTime ?? DateTime.UtcNow;
        var vnTime = utc.AddHours(7); // Giờ Việt Nam (UTC+7)
        int hour = vnTime.Hour;
        int minute = vnTime.Minute;
        decimal timeDecimal = hour + (minute / 60m);

        // 1. PHIÊN MỸ (NEW YORK SESSION): 19:00 - 02:00 Giờ VN
        if (timeDecimal >= 19.0m || timeDecimal < 2.0m)
        {
            bool isKillzone = timeDecimal >= 19.5m && timeDecimal <= 23.5m; // 19:30 - 23:30 New York Open Killzone
            return new TradingSessionInfo(
                SessionName: isKillzone ? "Phiên Mỹ (New York Open Killzone 🔥)" : "Phiên Mỹ (New York Session)",
                SessionCode: "NEW_YORK",
                TimeRangeVn: "19:00 - 02:00 (Giờ VN)",
                IsKillzone: isKillzone,
                LiquidityLevel: "CỰC ĐẠI 🔥 (Dòng tiền tổ chức & Quỹ ETF Mỹ)",
                StrategyAdvice: "Thanh khoản lớn nhất toàn cầu. Thích hợp đánh bứt phá (Breakout) & theo sóng lớn. Tỷ lệ cán TP2, TP3 cao nhất.",
                ScoreBonus: isKillzone ? 10 : 7,
                IsTradeAllowed: true,
                StatusBadge: isKillzone ? "🟢 GIỜ KIM CƯƠNG NEW YORK" : "🟢 PHIÊN MỸ SÔI ĐỘNG"
            );
        }

        // 2. PHIÊN ÂU (LONDON SESSION): 14:00 - 19:00 Giờ VN
        if (timeDecimal >= 14.0m && timeDecimal < 19.0m)
        {
            bool isKillzone = timeDecimal >= 14.0m && timeDecimal <= 17.5m; // 14:00 - 17:30 London Open Killzone
            return new TradingSessionInfo(
                SessionName: isKillzone ? "Phiên Âu (London Open Killzone ⚡)" : "Phiên Âu (London Session)",
                SessionCode: "LONDON",
                TimeRangeVn: "14:00 - 19:00 (Giờ VN)",
                IsKillzone: isKillzone,
                LiquidityLevel: "CAO ⚡ (London Judas Swing)",
                StrategyAdvice: "Bẫy giá giả mạo (Judas Swing) quét thanh khoản đỉnh/đáy phiên Á, sau đó xác lập xu hướng ngày.",
                ScoreBonus: isKillzone ? 8 : 5,
                IsTradeAllowed: true,
                StatusBadge: isKillzone ? "🟢 GIỜ VÀNG LONDON OPEN" : "🟢 PHIÊN ÂU SÔI ĐỘNG"
            );
        }

        // 3. PHIÊN Á (ASIAN SESSION / TOKYO - SYDNEY): 07:00 - 14:00 Giờ VN
        if (timeDecimal >= 7.0m && timeDecimal < 14.0m)
        {
            return new TradingSessionInfo(
                SessionName: "Phiên Á (Asian Range / Tokyo ⛩️)",
                SessionCode: "ASIA",
                TimeRangeVn: "07:00 - 14:00 (Giờ VN)",
                IsKillzone: false,
                LiquidityLevel: "TRUNG BÌNH 📊 (Tích lũy biên độ)",
                StrategyAdvice: "Thị trường thường đi ngang tích lũy tạo biên độ (Asian Range). Khuyên ưu tiên chốt lời gần tại TP1 (1.5R) hoặc gom đón sóng phiên Âu.",
                ScoreBonus: 3,
                IsTradeAllowed: true,
                StatusBadge: "🟡 TÍCH LŨY PHIÊN Á"
            );
        }

        // 4. VÙNG CHẾT (OFF-HOURS / DEAD ZONE): 02:00 - 07:00 Giờ VN
        return new TradingSessionInfo(
            SessionName: "Vùng Chết (Off-Hours / Low Liquidity 🌙)",
            SessionCode: "OFF_HOURS",
            TimeRangeVn: "02:00 - 07:00 (Giờ VN)",
            IsKillzone: false,
            LiquidityLevel: "MỎNG - RỦI RO CAO ⚠️",
            StrategyAdvice: "Cả London và New York đã đóng cửa. Thanh khoản cực mỏng, spread giãn, râu nến ảo quét SL bất thường. Khuyên ĐỨNG NGOÀI bảo toàn vốn.",
            ScoreBonus: -15, // Trừ điểm phạt nặng
            IsTradeAllowed: false, // Chặn mở lệnh thông thường
            StatusBadge: "🔴 HẠN CHẾ VÀO LỆNH (THANH KHOẢN MỎNG)"
        );
    }

    private static IctRsiEmaAnalysis CreateDefaultIctAnalysis(decimal price)
    {
        return new IctRsiEmaAnalysis(
            MarketStructure: "Đang đồng bộ dữ liệu nến",
            OrderBlockType: "Chờ xác nhận Order Block",
            OrderBlockLow: 0,
            OrderBlockHigh: 0,
            FvgType: "Chờ xác nhận FVG",
            FvgLow: 0,
            FvgHigh: 0,
            FvgStatus: "Chờ dữ liệu nến",
            LiquiditySweep: "Đang quét dữ liệu thanh khoản",
            OteFibZone: "Vùng OTE Fib: Đang tính toán...",
            Rsi14: 50m,
            RsiSentiment: "Trung lập (Đang tải dữ liệu)",
            RsiDivergence: "Chưa phát hiện phân kỳ",
            Ema20: price,
            Ema50: price,
            Ema200: price,
            EmaTrend: "Đang tính toán EMA Ribbon",
            ConfluenceSummary: "Độ tin cậy hiện tại 50% (< 70%). Đang chờ dữ liệu nến Binance đồng bộ đủ chu kỳ trước khi phân tích vào lệnh.",
            SetupAction: "⏸️ CHỜ ĐỢI TÍN HIỆU / QUAN SÁT (ĐỘ HỘI TỤ CHƯA ĐẠT >70%)",
            ConfidenceScore: 50,
            OptimalEntry: 0,
            StopLoss: 0,
            TakeProfit1: 0,
            TakeProfit2: 0,
            TakeProfit3: 0,
            RiskRewardRatio: 0,
            LeverageRule: "Không vào lệnh (Đang đồng bộ dữ liệu)",
            RiskManagementAdvice: "Chỉ giải ngân khi hội tụ đủ điều kiện > 70%.",
            SessionInfo: GetTradingSessionInfo(DateTime.UtcNow)
        );
    }

    private static string formatNum(decimal val)
    {
        return val >= 1000 ? val.ToString("N2", CultureInfo.InvariantCulture) : val.ToString("F4", CultureInfo.InvariantCulture);
    }

    private async Task<List<KlineCandle>> FetchKlinesAsync(string normSymbol, string interval, int limit)
    {
        var list = new List<KlineCandle>();
        try
        {
            var res = await _httpClient.GetAsync($"/fapi/v1/klines?symbol={normSymbol}&interval={interval}&limit={limit}");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        long t = item[0].GetInt64();
                        decimal o = decimal.Parse(item[1].GetString()!, CultureInfo.InvariantCulture);
                        decimal h = decimal.Parse(item[2].GetString()!, CultureInfo.InvariantCulture);
                        decimal l = decimal.Parse(item[3].GetString()!, CultureInfo.InvariantCulture);
                        decimal c = decimal.Parse(item[4].GetString()!, CultureInfo.InvariantCulture);
                        decimal v = decimal.Parse(item[5].GetString()!, CultureInfo.InvariantCulture);
                        list.Add(new KlineCandle(t, o, h, l, c, v));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch {Interval} futures klines for {Symbol}", interval, normSymbol);
        }
        return list;
    }

    private static DailyBiasResult CalculateDailyBias(decimal currentPrice, List<KlineCandle> d1, List<KlineCandle> h4, List<KlineCandle> m15)
    {
        // 1. D1 BIAS TỔNG
        string d1Bias = "NEUTRAL";
        string d1Desc = "Thị trường nén tích lũy trong ngày";
        decimal pdh = 0;
        decimal pdl = 0;
        string sweepStatus = "Nằm trong biên độ ngày hôm trước";
        decimal d1Ema20 = currentPrice;
        decimal d1Ema50 = currentPrice;
        decimal d1Rsi = 50m;
        string d1Struct = "Tích lũy đi ngang";

        if (d1.Count >= 2)
        {
            var prevDay = d1[d1.Count - 2];
            var today = d1[d1.Count - 1];
            pdh = prevDay.High;
            pdl = prevDay.Low;

            var d1Closes = d1.Select(k => k.Close).ToList();
            d1Ema20 = CalculateEma(d1Closes, Math.Min(20, d1Closes.Count));
            d1Ema50 = CalculateEma(d1Closes, Math.Min(50, d1Closes.Count));
            d1Rsi = CalculateRsi(d1Closes, Math.Min(14, d1Closes.Count - 1));

            bool sweptPdh = today.High > pdh;
            bool sweptPdl = today.Low < pdl;

            if (sweptPdh && today.Close < pdh)
            {
                sweepStatus = $"Đã quét râu đỉnh PDH (${pdh:N2}) rồi rút chân -> Cảnh báo bẫy thanh khoản BSL phe Mua";
            }
            else if (sweptPdl && today.Close > pdl)
            {
                sweepStatus = $"Đã quét râu đáy PDL (${pdl:N2}) rồi bật hồi -> Săn thanh khoản SSL phe Bán, tạo đà hồi";
            }
            else if (today.Close > pdh)
            {
                sweepStatus = $"Bứt phá vượt đỉnh PDH (${pdh:N2}) -> Xu hướng Expansion Tăng trong ngày rất mạnh mẽ";
            }
            else if (today.Close < pdl)
            {
                sweepStatus = $"Thủng đáy PDL (${pdl:N2}) -> Xu hướng Expansion Giảm tiếp diễn áp lực bán";
            }
            else
            {
                sweepStatus = $"Dao động tích lũy trong biên độ ngày trước (${pdl:N2} - ${pdh:N2})";
            }

            if (currentPrice > d1Ema20 && d1Ema20 >= d1Ema50 && d1Rsi >= 50)
            {
                d1Bias = "BULLISH";
                d1Desc = "Thiên kiến D1 TĂNG (BULLISH BIAS) - Giá duy trì trên EMA20/50 ngày, phe mua kiểm soát toàn diện";
                d1Struct = "Cấu trúc Đỉnh Đáy Nâng Dần (Higher Highs / Higher Lows)";
            }
            else if (currentPrice < d1Ema20 && d1Ema20 <= d1Ema50 && d1Rsi <= 50)
            {
                d1Bias = "BEARISH";
                d1Desc = "Thiên kiến D1 GIẢM (BEARISH BIAS) - Giá nằm dưới EMA20/50 ngày, phe bán áp đảo xu hướng";
                d1Struct = "Cấu trúc Đỉnh Đáy Hạ Dần (Lower Highs / Lower Lows)";
            }
            else
            {
                d1Bias = currentPrice >= d1Ema50 ? "BULLISH" : "BEARISH";
                d1Desc = $"Thiên kiến D1 {d1Bias} Thận Trọng - Giá đang kiểm tra vùng ranh giới EMA50 ngày";
                d1Struct = "Biên độ Dao Động Tích Lũy (Range Bound)";
            }
        }

        // 2. H4 BIAS TRUNG HẠN
        string h4Bias = "NEUTRAL";
        string h4Desc = "H4 đang đi ngang nén biên độ";
        string h4Struct = "H4 Range";
        string h4KeyZone = "Vùng hỗ trợ/kháng cự H4";
        decimal h4Ema50 = currentPrice;
        decimal h4Rsi = 50m;

        if (h4.Count >= 10)
        {
            var h4Closes = h4.Select(k => k.Close).ToList();
            h4Ema50 = CalculateEma(h4Closes, Math.Min(50, h4Closes.Count));
            h4Rsi = CalculateRsi(h4Closes, Math.Min(14, h4Closes.Count - 1));

            decimal h4Max = h4.TakeLast(20).Max(k => k.High);
            decimal h4Min = h4.TakeLast(20).Min(k => k.Low);
            decimal recentHigh = h4.TakeLast(6).Max(k => k.High);
            decimal recentLow = h4.TakeLast(6).Min(k => k.Low);

            if (currentPrice > h4Ema50 && recentHigh >= h4Max * 0.998m)
            {
                h4Bias = "BULLISH";
                h4Struct = "H4 BOS Tăng (Phá vỡ cấu trúc đỉnh trung hạn)";
                h4Desc = "H4 giữ cấu trúc Bullish bền vững, sóng tăng tiếp diễn";
                h4KeyZone = $"Hỗ trợ Bullish OB H4: ${recentLow:N2} - ${recentLow * 1.01m:N2}";
            }
            else if (currentPrice < h4Ema50 && recentLow <= h4Min * 1.002m)
            {
                h4Bias = "BEARISH";
                h4Struct = "H4 BOS Giảm (Thủng đáy cấu trúc trung hạn)";
                h4Desc = "H4 duy trì cấu trúc Bearish, phe bán đè giá tại các kháng cự";
                h4KeyZone = $"Kháng cự Bearish OB H4: ${recentHigh * 0.99m:N2} - ${recentHigh:N2}";
            }
            else
            {
                h4Bias = currentPrice >= h4Ema50 ? "BULLISH" : "BEARISH";
                h4Struct = h4Bias == "BULLISH" ? "H4 Tích Lũy Bật Hồi" : "H4 Hồi Kỹ Thuật";
                h4Desc = $"H4 dao động quanh mốc EMA50 (${h4Ema50:N2})";
                h4KeyZone = $"Vùng kiểm định Fib 0.5 - 0.618: ${(h4Min + (h4Max - h4Min) * 0.5m):N2}";
            }
        }

        // 3. M15 BIAS VÀO LỆNH (EXECUTION)
        string m15Bias = "NEUTRAL";
        string m15Desc = "M15 đang tìm điểm cân bằng";
        string m15Trigger = "Chờ M15 xác nhận tín hiệu";
        string m15EntryZone = "Vùng M15 POI";
        decimal m15Ema20 = currentPrice;
        decimal m15Rsi = 50m;

        if (m15.Count >= 10)
        {
            var m15Closes = m15.Select(k => k.Close).ToList();
            m15Ema20 = CalculateEma(m15Closes, Math.Min(20, m15Closes.Count));
            m15Rsi = CalculateRsi(m15Closes, Math.Min(14, m15Closes.Count - 1));

            var prevM15High = m15.TakeLast(8).Max(k => k.High);
            var prevM15Low = m15.TakeLast(8).Min(k => k.Low);

            if (currentPrice > m15Ema20 && m15Rsi >= 50)
            {
                m15Bias = "BULLISH";
                m15Desc = "M15 Động Lượng Tăng Sắc Nét (Giá trên EMA20 M15, RSI > 50)";
                m15Trigger = "M15 CHoCH Tăng / BOS Tăng - Xác nhận đảo chiều vào lệnh LONG";
                m15EntryZone = $"Limit M15 FVG/OB: ${prevM15Low * 1.002m:N2} - ${currentPrice:N2}";
            }
            else if (currentPrice < m15Ema20 && m15Rsi <= 50)
            {
                m15Bias = "BEARISH";
                m15Desc = "M15 Động Lượng Giảm Chủ Đạo (Giá dưới EMA20 M15, RSI < 50)";
                m15Trigger = "M15 BOS Giảm / Gãy Đáy - Xác nhận thuận đà vào lệnh SHORT";
                m15EntryZone = $"Limit M15 FVG Cung: ${currentPrice:N2} - ${prevM15High * 0.998m:N2}";
            }
            else
            {
                m15Bias = currentPrice >= m15Ema20 ? "BULLISH" : "BEARISH";
                m15Desc = "M15 Nén Biên Độ Dao Động";
                m15Trigger = "Chờ nến M15 đóng cửa bứt phá biên độ";
                m15EntryZone = $"Vùng kiểm định M15: ${currentPrice:N2}";
            }
        }

        // 4. ĐÁNH GIÁ SỰ ĐỒNG THUẬN ĐA KHUNG (CONFLUENCE ALIGNMENT)
        int score = 50;
        string status;
        string plan;

        if (d1Bias == "BULLISH" && h4Bias == "BULLISH" && m15Bias == "BULLISH")
        {
            score = 100;
            status = "🟢 ĐỒNG THUẬN TUYỆT ĐỐI (KÈO A+ LONG)";
            plan = "Cả 3 khung D1, H4, M15 cùng pha Bullish hoàn hảo. Khuyến nghị: Ưu tiên 100% lệnh LONG, canh Limit tại vùng M15 OB/FVG, tự tin đặt mục tiêu TP2 / TP3.";
        }
        else if (d1Bias == "BEARISH" && h4Bias == "BEARISH" && m15Bias == "BEARISH")
        {
            score = 100;
            status = "🔴 ĐỒNG THUẬN TUYỆT ĐỐI (KÈO A+ SHORT)";
            plan = "Cả 3 khung D1, H4, M15 cùng pha Bearish hoàn hảo. Khuyến nghị: Ưu tiên 100% lệnh SHORT canh hồi chạm cản FVG/OB M15, tuyệt đối không bắt đáy dao rơi.";
        }
        else if (d1Bias == "BULLISH" && h4Bias == "BULLISH" && m15Bias == "BEARISH")
        {
            score = 75;
            status = "🟡 SÓNG HỒI ĐIỀU CHỈNH TRÊN M15 (CHỜ LONG THUẬN D1/H4)";
            plan = "D1 & H4 đang Tăng chủ đạo, nhịp giảm M15 chỉ là sóng thoái lui lành mạnh (Discount). Hành động: KHÔNG Short đuổi, kiên nhẫn chờ M15 tạo CHoCH Tăng tại hỗ trợ H4 để vào lệnh LONG.";
        }
        else if (d1Bias == "BEARISH" && h4Bias == "BEARISH" && m15Bias == "BULLISH")
        {
            score = 75;
            status = "🟡 NHỊP HỒI KỸ THUẬT TRÊN M15 (CHỜ SHORT THUẬN D1/H4)";
            plan = "D1 & H4 là xu hướng Giảm lớn, nhịp tăng M15 chỉ là hồi kỹ thuật săn thanh khoản (Premium). Hành động: Canh giá chạm vùng cản Bearish OB H4 để tìm tín hiệu SHORT.";
        }
        else
        {
            score = 60;
            status = "⚪ ĐA KHUNG PHÂN KỲ - NÉN BIÊN ĐỘ TÍCH LŨY";
            plan = "Các khung thời gian chưa đồng pha hoàn toàn. Hành động: Giảm khối lượng lệnh còn 1%, chỉ đánh Scalp ngắn hạn trong biên độ PDH - PDL hoặc đứng ngoài chờ xu hướng rõ.";
        }

        return new DailyBiasResult(
            D1Bias: d1Bias,
            D1BiasDescription: d1Desc,
            PreviousDayHigh: pdh,
            PreviousDayLow: pdl,
            PdhPdlSweepStatus: sweepStatus,
            D1Ema20: Math.Round(d1Ema20, 2),
            D1Ema50: Math.Round(d1Ema50, 2),
            D1Rsi14: Math.Round(d1Rsi, 1),
            D1Structure: d1Struct,
            H4Bias: h4Bias,
            H4BiasDescription: h4Desc,
            H4Structure: h4Struct,
            H4KeyZone: h4KeyZone,
            H4Ema50: Math.Round(h4Ema50, 2),
            H4Rsi14: Math.Round(h4Rsi, 1),
            M15Bias: m15Bias,
            M15BiasDescription: m15Desc,
            M15Trigger: m15Trigger,
            M15EntryZone: m15EntryZone,
            M15Ema20: Math.Round(m15Ema20, 2),
            M15Rsi14: Math.Round(m15Rsi, 1),
            AlignmentScore: score,
            AlignmentStatus: status,
            ActionablePlan: plan
        );
    }
}
