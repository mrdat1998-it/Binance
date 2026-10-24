import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CoinSummary, CycleIndicator, FuturesAnalysis, KlineCandle, PredictionResult, SentimentData, TradeHistorySummary } from '../models/crypto.model';

@Injectable({
  providedIn: 'root'
})
export class CryptoApiService {
  private http = inject(HttpClient);
  // Default to backend API port 5166 or relative /api
  private baseUrl = 'http://localhost:5166/api';

  getTopCoins(): Observable<CoinSummary[]> {
    return this.http.get<CoinSummary[]>(`${this.baseUrl}/coins`);
  }

  getCoin(symbol: string): Observable<CoinSummary> {
    return this.http.get<CoinSummary>(`${this.baseUrl}/coins/${symbol}`);
  }

  getKlines(symbol: string, interval = '1d', limit = 180): Observable<KlineCandle[]> {
    return this.http.get<KlineCandle[]>(`${this.baseUrl}/market/klines?symbol=${symbol}&interval=${interval}&limit=${limit}`);
  }

  getCycleAnalysis(symbol: string): Observable<CycleIndicator> {
    return this.http.get<CycleIndicator>(`${this.baseUrl}/cycle/${symbol}`);
  }

  getPrediction(symbol: string): Observable<PredictionResult> {
    return this.http.get<PredictionResult>(`${this.baseUrl}/prediction/${symbol}`);
  }

  getSentiment(): Observable<SentimentData> {
    return this.http.get<SentimentData>(`${this.baseUrl}/sentiment`);
  }

  getFuturesAnalysis(symbol: string, interval = '15m'): Observable<FuturesAnalysis> {
    return this.http.get<FuturesAnalysis>(`${this.baseUrl}/futures/${symbol}?interval=${interval}`);
  }

  getTradeHistory(symbol?: string, status?: string): Observable<TradeHistorySummary> {
    const params: string[] = [];
    if (symbol && symbol !== 'all') params.push(`symbol=${encodeURIComponent(symbol)}`);
    if (status && status !== 'all') params.push(`status=${encodeURIComponent(status)}`);
    const query = params.length > 0 ? `?${params.join('&')}` : '';
    return this.http.get<TradeHistorySummary>(`${this.baseUrl}/futures/history${query}`);
  }

  // Real-time tick-by-tick Binance WebSocket Stream
  listenBinanceStream(symbol: string, interval = '1d'): Observable<any> {
    return new Observable(observer => {
      const normSym = symbol.toLowerCase();
      const wsUrl = `wss://stream.binance.com:9443/stream?streams=${normSym}@ticker/${normSym}@kline_${interval}/!miniTicker@arr`;
      const ws = new WebSocket(wsUrl);

      ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          observer.next(data);
        } catch (e) {
          console.warn('WS parse error:', e);
        }
      };

      ws.onerror = (err) => observer.error(err);
      ws.onclose = () => observer.complete();

      return () => {
        try { ws.close(); } catch (e) {}
      };
    });
  }
}

