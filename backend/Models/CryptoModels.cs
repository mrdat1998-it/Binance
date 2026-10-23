namespace BinanceTracker.Api.Models;

public record CoinInfo(
    string Symbol,
    string Name,
    string BaseAsset,
    string QuoteAsset,
    string IconUrl,
    decimal AthPrice,
    string AthDate,
    string Category,
    string Description
);

public record CoinSummary(
    string Symbol,
    string Name,
    string BaseAsset,
    string IconUrl,
    decimal Price,
    decimal PriceChange24h,
    decimal PriceChangePercent24h,
    decimal High24h,
    decimal Low24h,
    decimal Volume24h,
    decimal QuoteVolume24h,
    decimal AthPrice,
    string AthDate,
    decimal AthDrawdownPercent,
    string Category
);

public record KlineCandle(
    long Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
);

public record CycleIndicator(
    string CurrentStage,
    string StageDescription,
    int DaysSinceHalving,
    int TotalCycleDays,
    double CycleProgressPercent,
    string NextHalvingDate,
    int DaysToNextHalving,
    decimal MayerMultiple,
    decimal Rsi14,
    decimal Sma50,
    decimal Sma200,
    decimal DistanceTo200SmaPercent,
    decimal AthDrawdownPercent,
    string PiCycleStatus,
    int RiskScore, // 1 to 10
    string RiskLevel, // Thấp, Trung bình, Cao, Cực cao
    string HistoricalComparison,
    string RecommendationVietnamese
);

public record PriceTarget(
    decimal Low,
    decimal Expected,
    decimal High,
    int ConfidencePercent
);

public record ScenarioDetail(
    string Name,
    decimal TargetPrice,
    decimal PercentChange,
    int ProbabilityPercent,
    string TriggerCondition,
    string Description
);

public record InstitutionalForecast(
    string Source,
    decimal TargetPrice,
    string Timeframe,
    string Rationale
);

public record PredictionResult(
    string Symbol,
    decimal CurrentPrice,
    PriceTarget ShortTerm30d,
    PriceTarget MidTerm90d,
    PriceTarget OneYearTarget,
    PriceTarget CyclePeakTarget,
    List<ScenarioDetail> Scenarios,
    List<InstitutionalForecast> InstitutionalForecasts,
    decimal Support1,
    decimal Support2,
    decimal Resistance1,
    decimal Resistance2,
    string OverallSentiment,
    string StrategicAdvice
);

public record SentimentData(
    int FearGreedIndex,
    string Classification,
    string ClassificationVietnamese,
    string LastUpdated,
    List<SentimentHistoryItem> History,
    string MarketPhaseNote
);

public record SentimentHistoryItem(
    int Value,
    string Classification,
    string Date
);

public record DailyBiasResult(
    // 1. D1 BIAS TỔNG
    string D1Bias, // "BULLISH", "BEARISH", "NEUTRAL"
    string D1BiasDescription,
    decimal PreviousDayHigh,
    decimal PreviousDayLow,
    string PdhPdlSweepStatus,
    decimal D1Ema20,
    decimal D1Ema50,
    decimal D1Rsi14,
    string D1Structure,

    // 2. H4 BIAS TRUNG HẠN
    string H4Bias, // "BULLISH", "BEARISH", "NEUTRAL"
    string H4BiasDescription,
    string H4Structure,
    string H4KeyZone,
    decimal H4Ema50,
    decimal H4Rsi14,

    // 3. M15 BIAS VÀO LỆNH (EXECUTION)
    string M15Bias, // "BULLISH", "BEARISH", "NEUTRAL"
    string M15BiasDescription,
    string M15Trigger,
    string M15EntryZone,
    decimal M15Ema20,
    decimal M15Rsi14,

    // 4. ĐỒNG THUẬN ĐA KHUNG THỜI GIAN
    int AlignmentScore, // 30% - 100%
    string AlignmentStatus,
    string ActionablePlan
);

public record FuturesAnalysis(
    string Symbol,
    decimal MarkPrice,
    decimal IndexPrice,
    decimal PriceBasis,
    decimal FundingRate,
    decimal FundingRatePercent,
    decimal AnnualizedFundingRate,
    string FundingSentiment,
    string NextFundingTime,
    decimal LongAccountPercent,
    decimal ShortAccountPercent,
    decimal LongShortRatio,
    string LongShortSentiment,
    decimal OpenInterestCoin,
    decimal OpenInterestUsd,
    string OpenInterestInterpretation,
    decimal LongLiquidationCluster,
    decimal ShortLiquidationCluster,
    string LiquidationMapSummary,
    string SignalAction, // LONG, SHORT, NEUTRAL
    string EntryZone,
    decimal TakeProfit1,
    decimal TakeProfit2,
    decimal TakeProfit3,
    decimal StopLoss,
    string RecommendedLeverage,
    string RiskRewardRatio,
    string TradingNotes,
    IctRsiEmaAnalysis? IctSetup,
    DailyBiasResult? DailyBias = null,
    string SelectedInterval = "15m"
);

public record IctRsiEmaAnalysis(
    string MarketStructure,
    string OrderBlockType,
    decimal OrderBlockLow,
    decimal OrderBlockHigh,
    string FvgType,
    decimal FvgLow,
    decimal FvgHigh,
    string FvgStatus,
    string LiquiditySweep,
    string OteFibZone,
    decimal Rsi14,
    string RsiSentiment,
    string RsiDivergence,
    decimal Ema20,
    decimal Ema50,
    decimal Ema200,
    string EmaTrend,
    string ConfluenceSummary,
    string SetupAction, // MỞ LỆNH LONG (MUA) / MỞ LỆNH SHORT (BÁN)
    int ConfidenceScore,
    decimal OptimalEntry,
    decimal StopLoss,
    decimal TakeProfit1,
    decimal TakeProfit2,
    decimal TakeProfit3,
    decimal RiskRewardRatio,
    string LeverageRule,
    string RiskManagementAdvice
);



