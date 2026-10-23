export interface CoinSummary {
  symbol: string;
  name: string;
  baseAsset: string;
  iconUrl: string;
  price: number;
  priceChange24h: number;
  priceChangePercent24h: number;
  high24h: number;
  low24h: number;
  volume24h: number;
  quoteVolume24h: number;
  athPrice: number;
  athDate: string;
  athDrawdownPercent: number;
  category: string;
}

export interface KlineCandle {
  time: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface CycleIndicator {
  currentStage: string;
  stageDescription: string;
  daysSinceHalving: number;
  totalCycleDays: number;
  cycleProgressPercent: number;
  nextHalvingDate: string;
  daysToNextHalving: number;
  mayerMultiple: number;
  rsi14: number;
  sma50: number;
  sma200: number;
  distanceTo200SmaPercent: number;
  athDrawdownPercent: number;
  piCycleStatus: string;
  riskScore: number;
  riskLevel: string;
  historicalComparison: string;
  recommendationVietnamese: string;
}

export interface PriceTarget {
  low: number;
  expected: number;
  high: number;
  confidencePercent: number;
}

export interface ScenarioDetail {
  name: string;
  targetPrice: number;
  percentChange: number;
  probabilityPercent: number;
  triggerCondition: string;
  description: string;
}

export interface InstitutionalForecast {
  source: string;
  targetPrice: number;
  timeframe: string;
  rationale: string;
}

export interface PredictionResult {
  symbol: string;
  currentPrice: number;
  shortTerm30d: PriceTarget;
  midTerm90d: PriceTarget;
  oneYearTarget: PriceTarget;
  cyclePeakTarget: PriceTarget;
  scenarios: ScenarioDetail[];
  institutionalForecasts: InstitutionalForecast[];
  support1: number;
  support2: number;
  resistance1: number;
  resistance2: number;
  overallSentiment: string;
  strategicAdvice: string;
}

export interface SentimentData {
  fearGreedIndex: number;
  classification: string;
  classificationVietnamese: string;
  lastUpdated: string;
  history: Array<{
    value: number;
    classification: string;
    date: string;
  }>;
  marketPhaseNote: string;
}

export interface IctRsiEmaAnalysis {
  marketStructure: string;
  orderBlockType: string;
  orderBlockLow: number;
  orderBlockHigh: number;
  fvgType: string;
  fvgLow: number;
  fvgHigh: number;
  fvgStatus: string;
  liquiditySweep: string;
  oteFibZone: string;
  rsi14: number;
  rsiSentiment: string;
  rsiDivergence: string;
  ema20: number;
  ema50: number;
  ema200: number;
  emaTrend: string;
  confluenceSummary: string;
  setupAction: string;
  confidenceScore: number;
  optimalEntry: number;
  stopLoss: number;
  takeProfit1: number;
  takeProfit2: number;
  takeProfit3: number;
  riskRewardRatio: number;
  leverageRule: string;
  riskManagementAdvice: string;
}

export interface DailyBiasResult {
  d1Bias: string;
  d1BiasDescription: string;
  previousDayHigh: number;
  previousDayLow: number;
  pdhPdlSweepStatus: string;
  d1Ema20: number;
  d1Ema50: number;
  d1Rsi14: number;
  d1Structure: string;

  h4Bias: string;
  h4BiasDescription: string;
  h4Structure: string;
  h4KeyZone: string;
  h4Ema50: number;
  h4Rsi14: number;

  m15Bias: string;
  m15BiasDescription: string;
  m15Trigger: string;
  m15EntryZone: string;
  m15Ema20: number;
  m15Rsi14: number;

  alignmentScore: number;
  alignmentStatus: string;
  actionablePlan: string;
}

export interface FuturesAnalysis {
  symbol: string;
  markPrice: number;
  indexPrice: number;
  priceBasis: number;
  fundingRate: number;
  fundingRatePercent: number;
  annualizedFundingRate: number;
  fundingSentiment: string;
  nextFundingTime: string;
  longAccountPercent: number;
  shortAccountPercent: number;
  longShortRatio: number;
  longShortSentiment: string;
  openInterestCoin: number;
  openInterestUsd: number;
  openInterestInterpretation: string;
  longLiquidationCluster: number;
  shortLiquidationCluster: number;
  liquidationMapSummary: string;
  signalAction: string;
  entryZone: string;
  takeProfit1: number;
  takeProfit2: number;
  takeProfit3: number;
  stopLoss: number;
  recommendedLeverage: string;
  riskRewardRatio: string;
  tradingNotes: string;
  ictSetup?: IctRsiEmaAnalysis;
  dailyBias?: DailyBiasResult;
  selectedInterval?: string;
}


