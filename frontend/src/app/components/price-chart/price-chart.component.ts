import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, ElementRef, ViewChild, AfterViewInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KlineCandle, IctRsiEmaAnalysis } from '../../models/crypto.model';

@Component({
  selector: 'app-price-chart',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './price-chart.component.html',
  styleUrls: ['./price-chart.component.scss']
})
export class PriceChartComponent implements AfterViewInit, OnChanges {
  @Input() klines: KlineCandle[] = [];
  @Input() symbol: string = 'BTCUSDT';
  @Input() ictSetup: IctRsiEmaAnalysis | null = null;
  @Input() timeframe: string = '15m';
  @Output() timeframeChange = new EventEmitter<string>();

  @ViewChild('chartCanvas', { static: false }) canvasRef!: ElementRef<HTMLCanvasElement>;

  timeframes = ['5m', '15m', '1h', '4h', '1d', '1w'];
  hoveredCandle: KlineCandle | null = null;
  hoveredDate: string = '';
  hoveredIndex: number = -1;

  // Interactive 4-directional Panning & Zooming
  isDragging: boolean = false;
  startMouseX: number = 0;
  startMouseY: number = 0;
  panCandleOffset: number = 0; // Horizontal shift (Trái / Phải để xem nến quá khứ / hiện tại)
  panPriceOffsetInPixels: number = 0; // Vertical shift (Trên / Dưới để dịch chuyển mức giá)
  zoomScale: number = 1.0; // Zoom ratio (0.25x - 4.0x)
  isUserPanned: boolean = false;

  // Touch support
  private touchStartX: number = 0;
  private touchStartY: number = 0;

  onSelectTimeframe(tf: string): void {
    if (this.timeframe === tf) return;
    this.timeframe = tf;
    this.timeframeChange.emit(tf);
  }

  ngAfterViewInit(): void {
    this.render();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['klines'] || changes['ictSetup']) && this.canvasRef) {
      this.render();
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    this.render();
  }

  // Reset chart view to default zoom and position
  resetChartView(): void {
    this.panCandleOffset = 0;
    this.panPriceOffsetInPixels = 0;
    this.zoomScale = 1.0;
    this.isUserPanned = false;
    this.render();
  }

  // --- Mouse Drag & Panning Handlers ---
  onMouseDown(e: MouseEvent): void {
    if (!this.canvasRef) return;
    this.isDragging = true;
    this.startMouseX = e.clientX;
    this.startMouseY = e.clientY;
  }

  @HostListener('window:mouseup')
  onMouseUp(): void {
    if (this.isDragging) {
      this.isDragging = false;
    }
  }

  @HostListener('window:mousemove', ['$event'])
  onWindowMouseMove(e: MouseEvent): void {
    if (!this.canvasRef || !this.klines || this.klines.length === 0) return;

    if (this.isDragging) {
      const dx = e.clientX - this.startMouseX;
      const dy = e.clientY - this.startMouseY;
      this.startMouseX = e.clientX;
      this.startMouseY = e.clientY;

      this.isUserPanned = true;

      const rect = this.canvasRef.nativeElement.getBoundingClientRect();
      const paddingLeft = 10;
      const paddingRight = 75;
      const chartWidth = rect.width - paddingLeft - paddingRight;

      const totalCandles = this.klines.length;
      const viewCount = Math.max(12, Math.min(totalCandles, Math.round(100 / this.zoomScale)));
      const slotWidth = chartWidth / viewCount;

      // Kéo Trái / Phải: dx > 0 kéo sang phải -> panCandleOffset tăng (dịch lùi về nến cũ)
      this.panCandleOffset += (dx / slotWidth);
      const maxOffset = Math.max(0, totalCandles - viewCount);
      this.panCandleOffset = Math.max(0, Math.min(maxOffset, this.panCandleOffset));

      // Kéo Trên / Dưới: dy > 0 kéo xuống -> panPriceOffsetInPixels tăng (giá dịch xuống)
      this.panPriceOffsetInPixels += dy;

      this.render();
    } else {
      const rect = this.canvasRef.nativeElement.getBoundingClientRect();
      if (e.clientX >= rect.left && e.clientX <= rect.right && e.clientY >= rect.top && e.clientY <= rect.bottom) {
        this.handleCanvasHover(e, rect);
      }
    }
  }

  // Wheel Zoom (Phóng to / Thu nhỏ)
  onWheel(e: WheelEvent): void {
    e.preventDefault();
    const zoomFactor = e.deltaY < 0 ? 1.15 : 0.87;
    this.zoomScale = Math.max(0.25, Math.min(4.0, this.zoomScale * zoomFactor));
    this.isUserPanned = true;
    this.render();
  }

  onDoubleClick(): void {
    this.resetChartView();
  }

  // Touch Support
  onTouchStart(e: TouchEvent): void {
    if (e.touches.length === 1) {
      this.isDragging = true;
      this.touchStartX = e.touches[0].clientX;
      this.touchStartY = e.touches[0].clientY;
    }
  }

  onTouchMove(e: TouchEvent): void {
    if (this.isDragging && e.touches.length === 1 && this.canvasRef && this.klines.length > 0) {
      const dx = e.touches[0].clientX - this.touchStartX;
      const dy = e.touches[0].clientY - this.touchStartY;
      this.touchStartX = e.touches[0].clientX;
      this.touchStartY = e.touches[0].clientY;

      this.isUserPanned = true;
      const rect = this.canvasRef.nativeElement.getBoundingClientRect();
      const paddingLeft = 10;
      const paddingRight = 75;
      const chartWidth = rect.width - paddingLeft - paddingRight;

      const totalCandles = this.klines.length;
      const viewCount = Math.max(12, Math.min(totalCandles, Math.round(100 / this.zoomScale)));
      const slotWidth = chartWidth / viewCount;

      this.panCandleOffset += (dx / slotWidth);
      const maxOffset = Math.max(0, totalCandles - viewCount);
      this.panCandleOffset = Math.max(0, Math.min(maxOffset, this.panCandleOffset));
      this.panPriceOffsetInPixels += dy;

      this.render();
    }
  }

  onTouchEnd(): void {
    this.isDragging = false;
  }

  onMouseLeave(): void {
    if (!this.isDragging) {
      this.hoveredCandle = null;
      this.hoveredIndex = -1;
      this.render();
    }
  }

  private handleCanvasHover(e: MouseEvent, rect: DOMRect): void {
    if (!this.klines || this.klines.length === 0) return;
    const mouseX = e.clientX - rect.left;
    const paddingLeft = 10;
    const paddingRight = 75;
    const chartWidth = rect.width - paddingLeft - paddingRight;

    const totalCandles = this.klines.length;
    const viewCount = Math.max(12, Math.min(totalCandles, Math.round(100 / this.zoomScale)));
    const slotWidth = chartWidth / viewCount;

    const maxOffset = Math.max(0, totalCandles - viewCount);
    const clampedOffset = Math.max(0, Math.min(maxOffset, this.panCandleOffset));
    const endIndex = Math.round(totalCandles - clampedOffset);
    const startIndex = Math.max(0, endIndex - viewCount);
    const visibleCandles = this.klines.slice(startIndex, endIndex);

    const idx = Math.floor((mouseX - paddingLeft) / slotWidth);
    if (idx >= 0 && idx < visibleCandles.length) {
      this.hoveredIndex = idx;
      this.hoveredCandle = visibleCandles[idx];
      const d = new Date(this.hoveredCandle.time);
      this.hoveredDate = d.toLocaleDateString('vi-VN') + ' ' + ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2);
      this.render();
    }
  }

  render(): void {
    if (!this.canvasRef) return;
    const canvas = this.canvasRef.nativeElement;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;

    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);

    const width = rect.width;
    const height = rect.height;
    ctx.clearRect(0, 0, width, height);

    if (!this.klines || this.klines.length === 0) {
      ctx.fillStyle = '#848e9c';
      ctx.font = '14px Plus Jakarta Sans';
      ctx.textAlign = 'center';
      ctx.fillText('Đang tải dữ liệu biểu đồ...', width / 2, height / 2);
      return;
    }

    const paddingLeft = 10;
    const paddingRight = 75;
    const paddingTop = 30;
    const paddingBottom = 35;
    const chartWidth = width - paddingLeft - paddingRight;
    const chartHeight = height - paddingTop - paddingBottom;

    // Calculate visible candle window based on zoomScale and panCandleOffset
    const totalCandles = this.klines.length;
    const viewCount = Math.max(12, Math.min(totalCandles, Math.round(100 / this.zoomScale)));
    const maxOffset = Math.max(0, totalCandles - viewCount);
    this.panCandleOffset = Math.max(0, Math.min(maxOffset, this.panCandleOffset));

    const endIndex = Math.round(totalCandles - this.panCandleOffset);
    const startIndex = Math.max(0, endIndex - viewCount);
    const visibleCandles = this.klines.slice(startIndex, endIndex);

    if (visibleCandles.length === 0) return;

    let minPrice = Infinity;
    let maxPrice = -Infinity;
    let maxVol = 0;

    visibleCandles.forEach(c => {
      if (c.low < minPrice) minPrice = c.low;
      if (c.high > maxPrice) maxPrice = c.high;
      if (c.volume > maxVol) maxVol = c.volume;
    });

    const rawRange = Math.max(0.0001, maxPrice - minPrice);
    minPrice = Math.max(0, minPrice - rawRange * 0.04);
    maxPrice = maxPrice + rawRange * 0.04;
    const fullRange = maxPrice - minPrice;

    // Apply Vertical Panning offset (Trên / Dưới)
    const pricePerPixel = fullRange / chartHeight;
    const priceShift = this.panPriceOffsetInPixels * pricePerPixel;
    minPrice += priceShift;
    maxPrice += priceShift;

    const priceToY = (p: number) => paddingTop + (1 - (p - minPrice) / (maxPrice - minPrice)) * chartHeight;

    // Grid lines & price labels
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
    ctx.fillStyle = '#848e9c';
    ctx.font = '11px JetBrains Mono';
    ctx.textAlign = 'left';
    ctx.lineWidth = 1;

    const steps = 5;
    for (let i = 0; i <= steps; i++) {
      const y = paddingTop + (chartHeight / steps) * i;
      const p = maxPrice - ((maxPrice - minPrice) / steps) * i;

      ctx.beginPath();
      ctx.moveTo(paddingLeft, y);
      ctx.lineTo(width - paddingRight, y);
      ctx.stroke();

      const label = p >= 1000 ? '$' + Math.round(p).toLocaleString() : '$' + p.toFixed(2);
      ctx.fillText(label, width - paddingRight + 8, y + 4);
    }

    // Candles
    const n = visibleCandles.length;
    const slotWidth = chartWidth / n;
    const candleWidth = Math.max(2, slotWidth * 0.65);

    visibleCandles.forEach((c, i) => {
      const x = paddingLeft + i * slotWidth + slotWidth / 2;
      const openY = priceToY(c.open);
      const closeY = priceToY(c.close);
      const highY = priceToY(c.high);
      const lowY = priceToY(c.low);

      const isUp = c.close >= c.open;
      const color = isUp ? '#0ecb81' : '#f6465d';

      // Volume bar
      const volH = maxVol > 0 ? (c.volume / maxVol) * (chartHeight * 0.20) : 0;
      const volY = height - paddingBottom - volH;
      ctx.fillStyle = isUp ? 'rgba(14, 203, 129, 0.2)' : 'rgba(246, 70, 93, 0.2)';
      ctx.fillRect(x - candleWidth / 2, volY, candleWidth, volH);

      // Wick
      ctx.strokeStyle = color;
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.moveTo(x, highY);
      ctx.lineTo(x, lowY);
      ctx.stroke();

      // Body
      ctx.fillStyle = color;
      const bodyTop = Math.min(openY, closeY);
      const bodyHeight = Math.max(1.5, Math.abs(closeY - openY));
      ctx.fillRect(x - candleWidth / 2, bodyTop, candleWidth, bodyHeight);
    });

    // EMA Ribbon: calculated across total dataset, sliced for visible slice
    this.drawEMA(ctx, 20, '#f0b90b', 1.6, paddingLeft, slotWidth, priceToY, startIndex, n);
    this.drawEMA(ctx, 50, '#2962ff', 1.8, paddingLeft, slotWidth, priceToY, startIndex, n);
    this.drawEMA(ctx, 200, '#a855f7', 2.2, paddingLeft, slotWidth, priceToY, startIndex, n);

    // ICT Overlays
    if (this.ictSetup) {
      const ict = this.ictSetup;

      // 1. Order Block (OB)
      if (ict.orderBlockHigh > 0 && ict.orderBlockLow > 0) {
        const obTop = priceToY(Math.max(ict.orderBlockHigh, ict.orderBlockLow));
        const obBot = priceToY(Math.min(ict.orderBlockHigh, ict.orderBlockLow));
        const obH = Math.max(3, obBot - obTop);
        const isBull = (ict.orderBlockType || '').includes('Bullish');

        ctx.fillStyle = isBull ? 'rgba(14, 203, 129, 0.12)' : 'rgba(246, 70, 93, 0.12)';
        ctx.strokeStyle = isBull ? 'rgba(14, 203, 129, 0.55)' : 'rgba(246, 70, 93, 0.55)';
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 3]);
        ctx.fillRect(paddingLeft, obTop, chartWidth, obH);
        ctx.strokeRect(paddingLeft, obTop, chartWidth, obH);
        ctx.setLineDash([]);

        ctx.fillStyle = isBull ? '#0ecb81' : '#f6465d';
        ctx.font = 'bold 10px JetBrains Mono';
        ctx.fillText(`OB (${isBull ? 'Bullish' : 'Bearish'}): $${ict.orderBlockLow.toLocaleString()} - $${ict.orderBlockHigh.toLocaleString()}`, paddingLeft + 12, obTop + 13);
      }

      // 2. Fair Value Gap (FVG)
      if (ict.fvgHigh > 0 && ict.fvgLow > 0 && Math.abs(ict.fvgHigh - ict.fvgLow) > 0.001) {
        const fvgTop = priceToY(Math.max(ict.fvgHigh, ict.fvgLow));
        const fvgBot = priceToY(Math.min(ict.fvgHigh, ict.fvgLow));
        const fvgH = Math.max(3, fvgBot - fvgTop);

        ctx.fillStyle = 'rgba(240, 185, 11, 0.10)';
        ctx.strokeStyle = 'rgba(240, 185, 11, 0.5)';
        ctx.lineWidth = 1;
        ctx.setLineDash([5, 2]);
        ctx.fillRect(paddingLeft, fvgTop, chartWidth, fvgH);
        ctx.strokeRect(paddingLeft, fvgTop, chartWidth, fvgH);
        ctx.setLineDash([]);

        ctx.fillStyle = '#f0b90b';
        ctx.font = 'bold 10px JetBrains Mono';
        ctx.fillText(`FVG Gap: $${ict.fvgLow.toLocaleString()} - $${ict.fvgHigh.toLocaleString()}`, paddingLeft + 12, fvgTop + 13);
      }

      // 3. Trade Setup Levels (Entry, SL, TP1, TP2) - CHỈ VẼ KHI HỘI TỤ ĐỦ ĐIỀU KIỆN > 70%
      if (ict.optimalEntry > 0 && ict.confidenceScore > 70) {
        const entryY = priceToY(ict.optimalEntry);
        this.drawTradeLevelLine(ctx, entryY, '#00e5ff', `ENTRY: $${ict.optimalEntry.toLocaleString()}`, paddingLeft, width - paddingRight);

        if (ict.stopLoss > 0) {
          const slY = priceToY(ict.stopLoss);
          this.drawTradeLevelLine(ctx, slY, '#f6465d', `SL: $${ict.stopLoss.toLocaleString()}`, paddingLeft, width - paddingRight);
        }

        if (ict.takeProfit1 > 0) {
          const tp1Y = priceToY(ict.takeProfit1);
          this.drawTradeLevelLine(ctx, tp1Y, '#0ecb81', `TP1: $${ict.takeProfit1.toLocaleString()}`, paddingLeft, width - paddingRight);
        }

        if (ict.takeProfit2 > 0) {
          const tp2Y = priceToY(ict.takeProfit2);
          this.drawTradeLevelLine(ctx, tp2Y, '#0ecb81', `TP2: $${ict.takeProfit2.toLocaleString()}`, paddingLeft, width - paddingRight);
        }
      } else if (ict.confidenceScore <= 70) {
        ctx.save();
        ctx.fillStyle = 'rgba(240, 185, 11, 0.85)';
        ctx.font = 'bold 10px JetBrains Mono';
        ctx.textAlign = 'right';
        ctx.fillText(`[ĐỘ HỘI TỤ ${ict.confidenceScore}% < 70% • CHỜ TÍN HIỆU ĐỦ ĐIỀU KIỆN]`, width - paddingRight - 10, paddingTop + 20);
        ctx.restore();
      }
    }

    // Time Axis Labels
    ctx.fillStyle = '#848e9c';
    ctx.font = '10px JetBrains Mono';
    ctx.textAlign = 'center';
    const labelInterval = Math.max(1, Math.floor(n / 6));

    for (let i = 0; i < n; i += labelInterval) {
      const x = paddingLeft + i * slotWidth + slotWidth / 2;
      const d = new Date(visibleCandles[i].time);
      const dateStr = (d.getMonth() + 1) + '/' + d.getDate() + ' ' + ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2);
      ctx.fillText(dateStr, x, height - 12);
    }

    // Crosshair if hovered
    if (this.hoveredIndex >= 0 && this.hoveredIndex < n && !this.isDragging) {
      const hCandle = visibleCandles[this.hoveredIndex];
      const hX = paddingLeft + this.hoveredIndex * slotWidth + slotWidth / 2;
      const hY = priceToY(hCandle.close);

      ctx.save();
      ctx.strokeStyle = 'rgba(240, 185, 11, 0.4)';
      ctx.setLineDash([4, 4]);
      ctx.lineWidth = 1;

      // Vertical line
      ctx.beginPath();
      ctx.moveTo(hX, paddingTop);
      ctx.lineTo(hX, height - paddingBottom);
      ctx.stroke();

      // Horizontal line
      ctx.beginPath();
      ctx.moveTo(paddingLeft, hY);
      ctx.lineTo(width - paddingRight, hY);
      ctx.stroke();
      ctx.restore();
    }
  }

  private drawEMA(
    ctx: CanvasRenderingContext2D,
    period: number,
    color: string,
    strokeWidth: number,
    paddingLeft: number,
    slotWidth: number,
    priceToY: (p: number) => number,
    startIndex: number,
    visibleCount: number
  ): void {
    if (!this.klines || this.klines.length < period) return;

    const emaVals = new Float64Array(this.klines.length);
    const k = 2 / (period + 1);
    let ema = 0;

    for (let i = 0; i < period; i++) {
      ema += this.klines[i].close;
    }
    ema /= period;
    emaVals[period - 1] = ema;

    for (let i = period; i < this.klines.length; i++) {
      ema = this.klines[i].close * k + ema * (1 - k);
      emaVals[i] = ema;
    }

    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = strokeWidth;
    ctx.beginPath();
    let started = false;

    for (let i = 0; i < visibleCount; i++) {
      const fullIdx = startIndex + i;
      if (fullIdx >= period - 1 && emaVals[fullIdx] !== undefined) {
        const x = paddingLeft + i * slotWidth + slotWidth / 2;
        const y = priceToY(emaVals[fullIdx]);
        if (!started) {
          ctx.moveTo(x, y);
          started = true;
        } else {
          ctx.lineTo(x, y);
        }
      }
    }
    ctx.stroke();
    ctx.restore();
  }

  private drawTradeLevelLine(ctx: CanvasRenderingContext2D, y: number, color: string, label: string, xStart: number, xEnd: number): void {
    if (isNaN(y) || y < 5 || y > 5000) return;
    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.3;
    ctx.setLineDash([5, 3]);
    ctx.beginPath();
    ctx.moveTo(xStart, y);
    ctx.lineTo(xEnd, y);
    ctx.stroke();
    ctx.setLineDash([]);

    ctx.fillStyle = color;
    ctx.font = 'bold 10px JetBrains Mono';
    ctx.textAlign = 'right';
    ctx.fillText(label, xEnd - 6, y - 4);
    ctx.restore();
  }
}
