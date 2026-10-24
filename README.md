# Binance Cycle Intelligence & Price Predictor 🚀

Hệ thống theo dõi thông tin tiền mã hóa, phân tích chu kỳ Halving và dự đoán xu hướng tương lai dựa trên dữ liệu chính thống từ Binance Public API, Alternative.me và các tổ chức tài chính hàng đầu thế giới (VanEck, Standard Chartered, Bernstein, Ark Invest).

---

## 🌟 Tính Năng Nổi Bật

1. **Theo Dõi Giá & Luồng Dữ Liệu Thời Gian Thực (Live Tick-by-Tick Streaming)**:
   - **Binance WebSocket Trực Tiếp (`wss://stream.binance.com:9443`)**: Kết nối luồng dữ liệu song song không độ trễ (0ms latency), không cần API key.
   - **Cập nhật Nến thời gian thực (Live Candlestick)**: Cây nến đang hình thành trên biểu đồ tự động co giãn giá đỉnh, đáy, đóng cửa và khối lượng theo từng lệnh khớp (tick) trên sàn Binance.
   - **Hiệu ứng Nháy Giá Xanh/Đỏ (Flash Animation)**: Giá coin nhấp nháy xanh khi vừa có lệnh mua đẩy giá lên và đỏ khi có lệnh bán ép giá xuống.
   - **Đồng hồ thời gian thực (Real-time Clock)**: Đồng bộ từng giây và tự động cập nhật toàn bộ thanh cuộn danh sách các đồng coin.
   - **Tự động làm mới dữ liệu phái sinh (Futures FT)**: Quét liên tục mỗi 4 giây để cập nhật tỷ lệ Funding Rate, Open Interest và tỷ lệ Long/Short.


2. **Biểu Đồ Kỹ Thuật Tương Tác (TradingView Lightweight Charts)**:
   - Biểu đồ nến Candlestick chuẩn sàn giao dịch tài chính chuyên nghiệp.
   - Tích hợp đường trung bình động MA (SMA 50, SMA 200).
   - Tùy chỉnh khung thời gian: `1H`, `4H`, `1D`, `1W`.

3. **Phân Tích Thay Đổi Chu Kỳ (Crypto Cycle Analysis)**:
   - **Mô hình Chu kỳ Halving 4 năm (Bitcoin Halving Model)**: Theo dõi tiến độ chu kỳ (% hoàn thành), số ngày đã qua kể từ Halving 20/04/2024, đếm ngược ngày đến đợt Halving tiếp theo (~Tháng 04/2028).
   - **Xác định Giai đoạn Chu kỳ (Cycle Phase)**:
     - Giai đoạn Tích luỹ & Tái tích luỹ (*Re-accumulation*)
     - Giai đoạn Tăng trưởng Bùng nổ (*Parabolic Markup / Bull Run*)
     - Giai đoạn Phân phối & Rủi ro Đỉnh (*Distribution*)
     - Giai đoạn Điều chỉnh & Suy thoái (*Bear Market / Reset*)
   - **Chỉ số Kỹ thuật Chu kỳ chuyên sâu**:
     - **Bội số Mayer (Mayer Multiple)**: So sánh giá hiện tại với đường 200-Day SMA (<0.8 gom hàng, >2.4 đỉnh chu kỳ).
     - **Chỉ số Sức mạnh Tương đối RSI (14D)**: Phát hiện vùng quá bán / quá mua chu kỳ.
     - **Khoảng cách tới đường 200-Week / 200-Day SMA**.
     - **Chỉ báo Đỉnh Pi Cycle (Pi Cycle Top Indicator)**.
     - **Điểm Rủi Ro Chu Kỳ (Risk Score 1 - 10)**.

4. **Dự Đoán Tương Lai & Kịch Bản Giá (Future Projections & Scenarios)**:
   - **Dự báo 4 mốc thời gian**: Ngắn hạn (30 ngày), Trung hạn (90 ngày), Dài hạn (1 năm), và Đỉnh chu kỳ Halving (*Cycle Peak Projection*).
   - **Mô hình Định lượng**: Hồi quy Lũy thừa (*Power Law Corridor*) và Dải Fibonacci Mở rộng (*Fibonacci Extensions* 1.618 - 2.618).
   - **3 Kịch bản Chu kỳ**:
     - 🟢 *Kịch bản Lạc quan (Bullish Supercycle)*: Tỷ lệ xác suất, điều kiện kích hoạt vĩ mô & mục tiêu giá.
     - 🟡 *Kịch bản Cơ sở (Base Case)*: Tỷ lệ xác suất & vùng giá cân bằng chu kỳ.
     - 🔴 *Kịch bản Phòng thủ (Bearish Stress Case)*: Vùng hỗ trợ cứng 200W SMA.
   - **Tổng Hợp Dự Báo Từ Các Nguồn Chính Thống**: Bảng đối chiếu mục tiêu và luận điểm phân tích từ:
     - **VanEck Digital Assets**
     - **Standard Chartered Bank**
     - **Bernstein Institutional**
     - **Ark Invest (Cathie Wood)**
     - **Binance Research**
   - **Khuyến nghị & Chiến lược Đầu tư (DCA / Take Profit / Stoploss)**.

5. **Chỉ Số Tâm Lý Thị Trường (Fear & Greed Index)**:
   - Kết nối trực tiếp API của Alternative.me với lịch sử 7 ngày gần nhất.

6. **Phân Tích Hợp Đồng Tương Lai & Phái Sinh (Binance Futures - FT)**:
   - Dữ liệu trực tiếp thời gian thực từ **Binance Futures API** (`fapi.binance.com`).
   - **Tỷ lệ Phí Cấp Vốn (Funding Rate 8H & APR quy đổi theo năm)**: Phát hiện sớm tâm lý quá nhiệt phe Long hoặc nguy cơ bẫy Short Squeeze khi Funding âm sâu.
   - **Tỷ lệ Tài Khoản Long / Short (Long/Short Account Ratio)**: Biểu đồ trực quan so sánh % vị thế Long vs % vị thế Short của toàn bộ các tài khoản giao dịch.
   - **Khối lượng Hợp đồng Mở (Open Interest - OI)**: Theo dõi tổng lượng tiền đòn bẩy đang lưu thông tính bằng USD và số lượng Coin.
   - **Giá Đánh Dấu (Mark Price) vs Giá Chỉ Số (Index Price)** & Độ lệch Basis.
   - **Bản Đồ Cụm Thanh Lý Hợp Đồng (Liquidation Heatmap)**: Tính toán vùng tập trung thanh lý của các lệnh đòn bẩy 20x, 30x, 50x phía trên và phía dưới.
   - **Tín Hiệu Chiến Lược FT Đề Xuất (Futures Trade Setup)**:
     - Tín hiệu: `LONG (Canh hồi)` / `SHORT (Canh đỉnh)` / `QUAN SÁT`
     - Vùng vào lệnh (Entry Zone), 3 mốc chốt lời (TP1, TP2, TP3), mức dừng lỗ nghiêm ngặt (Stop Loss)
     - Đòn bẩy khuyến nghị an toàn (3x - 10x) và tỷ lệ Lợi nhuận / Rủi ro (R:R Ratio).


---

## 🛠️ Kiến Trúc Hệ Thống

Dự án được xây dựng theo kiến trúc C# .NET 9 hiện đại tích hợp sẵn Web Dashboard và Worker quét ngầm 24/7:
```
D:\2026\Binance\
└── backend/                       # C# ASP.NET Core Web API (.NET 9.0)
    ├── Controllers/               # API Endpoints
    │   ├── CoinsController.cs     # /api/coins (Dữ liệu giá 24h thời gian thực)
    │   ├── MarketController.cs    # /api/market/klines (Dữ liệu nến biểu đồ)
    │   ├── CycleController.cs     # /api/cycle/{symbol} (Phân tích chu kỳ Halving)
    │   ├── PredictionController.cs# /api/prediction/{symbol} (Dự đoán giá tương lai)
    │   ├── SentimentController.cs # /api/sentiment (Chỉ số Fear & Greed)
    │   └── FuturesController.cs   # /api/futures (Phân tích phái sinh & Lịch sử lệnh)
    ├── Data/                      # Entity Framework Core 9 + SQL Server
    │   ├── AppDbContext.cs        # Kết nối CSDL SQL Server (Database: BinanceTracker)
    │   ├── DbInitializer.cs       # Khởi tạo bảng và seed dữ liệu chuẩn ban đầu
    │   └── Entities/              # TradeSignalEntity (Lưu trữ và giám sát lệnh)
    ├── Workers/                   # Background Services 24/7
    │   └── AutonomousTradeWorker.cs # Tự động quét Binance, kiểm tra SL/TP & ghi nhận lệnh độc lập
    ├── Models/                    # DTOs & Domain Models
    │   └── CryptoModels.cs
    ├── Services/                  # Business Logic & External APIs
    │   ├── BinanceService.cs      # Kết nối Binance Public API
    │   ├── FuturesService.cs      # Phân tích Binance Futures & SMC ICT
    │   ├── TradeHistoryService.cs # Quản lý lịch sử lệnh, chống trùng lặp & thống kê PnL
    │   ├── MarketCycleService.cs  # Thuật toán tính toán chu kỳ & Halving
    │   ├── PredictionService.cs   # Thuật toán dự báo tương lai & kịch bản
    │   └── SentimentService.cs    # Kết nối Alternative.me API
    ├── wwwroot/                   # Web Dashboard hoàn chỉnh phục vụ trực tiếp tại cổng 5166
    │   └── index.html             # Giao diện TradingView, nến tương tác, selector coin, nhật ký lệnh
    └── Program.cs                 # Cấu hình DI, Background Worker & Static SPA Serving
```

---

## 🚀 Hướng Dẫn Khởi Chạy

Toàn bộ hệ thống Web Dashboard đã được tích hợp trực tiếp vào Backend tại cổng `5166`:

1. Mở PowerShell trong thư mục dự án:
```powershell
cd D:\2026\Binance\backend
dotnet run --urls=http://localhost:5166
```

2. Mở trình duyệt web và truy cập:
👉 **[http://localhost:5166](http://localhost:5166)**

Giao diện sẽ hiển thị đầy đủ biểu đồ nến TradingView, bảng chọn coin, phân tích chu kỳ Halving, dự đoán kịch bản tương lai, tín hiệu ICT SMC và bảng lưu trữ giám sát lệnh SL/TP theo thời gian thực (Giờ Việt Nam GMT+7). Hệ thống có sẵn Worker chạy ngầm 24/7 tự động giám sát thị trường ngay cả khi bạn tắt trình duyệt.

