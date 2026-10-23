import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription, interval } from 'rxjs';
import { CryptoApiService } from './services/crypto-api.service';
import { CoinSummary, CycleIndicator, FuturesAnalysis, KlineCandle, PredictionResult, SentimentData } from './models/crypto.model';
import { PriceChartComponent } from './components/price-chart/price-chart.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, PriceChartComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  private api = inject(CryptoApiService);

  coins = signal<CoinSummary[]>([]);
  selectedSymbol = signal<string>('BTCUSDT');
  selectedCoin = signal<CoinSummary | null>(null);
  cycleData = signal<CycleIndicator | null>(null);
  predictionData = signal<PredictionResult | null>(null);
  futuresData = signal<FuturesAnalysis | null>(null);
  sentiment = signal<SentimentData | null>(null);
  klines = signal<KlineCandle[]>([]);
  currentTimeframe = signal<string>('15m');
  isLoading = signal<boolean>(false);
  liveClock = signal<string>('');

  private streamSub: Subscription | null = null;
  private pollSub: Subscription | null = null;
  private clockInterval: any = null;

  ngOnInit(): void {
    this.loadInitialData();
    this.startClock();
    this.startBackgroundPolling();
  }

  ngOnDestroy(): void {
    this.stopLiveStream();
    if (this.pollSub) this.pollSub.unsubscribe();
    if (this.clockInterval) clearInterval(this.clockInterval);
  }

  private startClock(): void {
    this.liveClock.set(new Date().toLocaleTimeString('vi-VN'));
    this.clockInterval = setInterval(() => {
      this.liveClock.set(new Date().toLocaleTimeString('vi-VN'));
    }, 1000);
  }

  private startBackgroundPolling(): void {
    // Poll Futures FT data continuously every 4 seconds
    this.pollSub = interval(4000).subscribe(() => {
      this.api.getFuturesAnalysis(this.selectedSymbol(), this.currentTimeframe()).subscribe({
        next: (ft) => this.futuresData.set(ft),
        error: (err) => console.warn('Background FT poll error:', err)
      });
    });
  }

  loadInitialData(): void {
    this.loadCoins();
    this.loadSentiment();
    this.loadCoinDetails(this.selectedSymbol());
  }

  loadCoins(): void {
    this.api.getTopCoins().subscribe({
      next: (data) => {
        this.coins.set(data);
        const match = data.find(c => c.symbol === this.selectedSymbol());
        if (match) this.selectedCoin.set(match);
      },
      error: (err) => console.error('Error loading coins:', err)
    });
  }

  loadSentiment(): void {
    this.api.getSentiment().subscribe({
      next: (data) => this.sentiment.set(data),
      error: (err) => console.error('Error loading sentiment:', err)
    });
  }

  selectCoin(symbol: string): void {
    if (this.selectedSymbol() === symbol) return;
    this.selectedSymbol.set(symbol);
    const coin = this.coins().find(c => c.symbol === symbol) || null;
    this.selectedCoin.set(coin);
    this.loadCoinDetails(symbol);
  }

  loadCoinDetails(symbol: string): void {
    this.isLoading.set(true);

    // Load Klines for chart
    this.api.getKlines(symbol, this.currentTimeframe(), 120).subscribe({
      next: (candles) => {
        this.klines.set(candles);
        // Connect WebSocket live tick stream after history is loaded
        this.startLiveStream(symbol, this.currentTimeframe());
      },
      error: (err) => console.error('Error loading klines:', err)
    });

    // Load Cycle Analysis
    this.api.getCycleAnalysis(symbol).subscribe({
      next: (cycle) => this.cycleData.set(cycle),
      error: (err) => console.error('Error loading cycle data:', err)
    });

    // Load Predictions
    this.api.getPrediction(symbol).subscribe({
      next: (pred) => {
        this.predictionData.set(pred);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading prediction:', err);
        this.isLoading.set(false);
      }
    });

    // Load Futures (FT) Analysis with selected timeframe
    this.api.getFuturesAnalysis(symbol, this.currentTimeframe()).subscribe({
      next: (ft) => this.futuresData.set(ft),
      error: (err) => console.error('Error loading futures data:', err)
    });
  }

  startLiveStream(symbol: string, tf: string): void {
    this.stopLiveStream();

    this.streamSub = this.api.listenBinanceStream(symbol, tf).subscribe({
      next: (payload) => {
        const stream: string = payload.stream;
        const data: any = payload.data;
        const normSym = symbol.toLowerCase();

        // 1. Live Ticker for Active Coin
        if (stream === `${normSym}@ticker`) {
          const current = this.selectedCoin();
          if (current) {
            const price = parseFloat(data.c);
            const change = parseFloat(data.p);
            const changePct = parseFloat(data.P);
            const high = parseFloat(data.h);
            const low = parseFloat(data.l);
            const vol = parseFloat(data.v);
            const drawdown = current.athPrice > 0 ? Math.max(0, ((current.athPrice - price) / current.athPrice) * 100) : 0;

            this.selectedCoin.set({
              ...current,
              price,
              priceChange24h: change,
              priceChangePercent24h: changePct,
              high24h: high,
              low24h: low,
              volume24h: vol,
              athDrawdownPercent: Math.round(drawdown * 10) / 10
            });
          }
        }

        // 2. Live Candlestick for Chart
        else if (stream === `${normSym}@kline_${tf}`) {
          const k = data.k;
          const kTime = k.t;
          const kOpen = parseFloat(k.o);
          const kHigh = parseFloat(k.h);
          const kLow = parseFloat(k.l);
          const kClose = parseFloat(k.c);
          const kVol = parseFloat(k.v);

          const list = [...this.klines()];
          if (list.length > 0) {
            const last = list[list.length - 1];
            if (last.time === kTime) {
              list[list.length - 1] = {
                ...last,
                high: Math.max(last.high, kHigh),
                low: Math.min(last.low, kLow),
                close: kClose,
                volume: kVol
              };
            } else if (kTime > last.time) {
              list.push({ time: kTime, open: kOpen, high: kHigh, low: kLow, close: kClose, volume: kVol });
              if (list.length > 120) list.shift();
            }
            this.klines.set(list);
          }
        }

        // 3. Mini-ticker update for all coin tabs
        else if (stream === '!miniTicker@arr' && Array.isArray(data)) {
          const coinList = [...this.coins()];
          let changed = false;
          data.forEach(t => {
            const idx = coinList.findIndex(c => c.symbol === t.s);
            if (idx >= 0 && t.s !== symbol) {
              coinList[idx] = {
                ...coinList[idx],
                price: parseFloat(t.c)
              };
              changed = true;
            }
          });
          if (changed) this.coins.set(coinList);
        }
      },
      error: (err) => console.warn('Stream error, will retry on next action:', err)
    });
  }

  stopLiveStream(): void {
    if (this.streamSub) {
      this.streamSub.unsubscribe();
      this.streamSub = null;
    }
  }

  setTimeframe(tf: string): void {
    this.currentTimeframe.set(tf);
    this.loadCoinDetails(this.selectedSymbol());
  }

  formatCurrency(value?: number): string {
    if (value === undefined || value === null) return '$0.00';
    if (value >= 1000) {
      return '$' + value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    } else if (value >= 1) {
      return '$' + value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 });
    }
    return '$' + value.toFixed(6);
  }

  formatCompact(value?: number): string {
    if (!value) return '0';
    if (value >= 1e9) return (value / 1e9).toFixed(2) + 'B';
    if (value >= 1e6) return (value / 1e6).toFixed(2) + 'M';
    if (value >= 1e3) return (value / 1e3).toFixed(2) + 'K';
    return value.toFixed(2);
  }

  formatCompactUsd(value?: number): string {
    if (!value) return '$0.00';
    if (value >= 1e9) return '$' + (value / 1e9).toFixed(2) + ' Tỷ';
    if (value >= 1e6) return '$' + (value / 1e6).toFixed(2) + ' Triệu';
    return this.formatCurrency(value);
  }
}
