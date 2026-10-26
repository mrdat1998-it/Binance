-- ========================================================================================
-- BINANCE CYCLE INTELLIGENCE & AUTONOMOUS SMC TRACKER
-- FULL DATABASE CREATION & INITIALIZATION SCRIPT (SQL SERVER)
-- ========================================================================================
-- Hướng dẫn chạy:
-- Cách 1: Mở SQL Server Management Studio (SSMS) hoặc Azure Data Studio, dán toàn bộ nội dung file này và nhấn [F5] / [Execute].
-- Cách 2: Chạy lệnh nhanh bằng sqlcmd trên terminal:
--         sqlcmd -S . -U admin -P "shc@1234" -C -i database_setup.sql
-- Cách 3: Chạy script PowerShell tự động đi kèm:
--         .\setup_database.ps1
-- ========================================================================================

SET NOCOUNT ON;
PRINT N'========================================================================================';
PRINT N'>>> BẮT ĐẦU KHỞI TẠO CƠ SỞ DỮ LIỆU BINANCE TRACKER TRÊN MICROSOFT SQL SERVER...';
PRINT N'========================================================================================';
GO

-- 1. TẠO CƠ SỞ DỮ LIỆU [BinanceTracker] NẾU CHƯA TỒN TẠI
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'BinanceTracker')
BEGIN
    PRINT N'[1/4] Đang khởi tạo Database [BinanceTracker]...';
    CREATE DATABASE [BinanceTracker]
    COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT N'[1/4] Đã tạo thành công Database [BinanceTracker].';
END
ELSE
BEGIN
    PRINT N'[1/4] Database [BinanceTracker] đã tồn tại sẵn, bỏ qua bước tạo DB.';
END
GO

USE [BinanceTracker];
GO

-- 2. TẠO BẢNG [dbo].[TradeSignals] LƯU TRỮ LỊCH SỬ LỆNH SMC / ICT
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TradeSignals]') AND type in (N'U'))
BEGIN
    PRINT N'[2/4] Đang tạo Bảng [dbo].[TradeSignals]...';
    CREATE TABLE [dbo].[TradeSignals] (
        [Id]               NVARCHAR(64)    NOT NULL,
        [Symbol]           NVARCHAR(32)    NOT NULL,
        [SignalAction]     NVARCHAR(16)    NOT NULL,                       -- "LONG" hoặc "SHORT"
        [Timeframe]        NVARCHAR(16)    NOT NULL CONSTRAINT [DF_TradeSignals_Timeframe] DEFAULT ('15m'),
        [ConfidenceScore]  INT             NOT NULL CONSTRAINT [DF_TradeSignals_Confidence] DEFAULT (0),
        [EntryPrice]       DECIMAL(18, 8)  NOT NULL,
        [StopLoss]         DECIMAL(18, 8)  NOT NULL,
        [TakeProfit1]      DECIMAL(18, 8)  NOT NULL,
        [TakeProfit2]      DECIMAL(18, 8)  NOT NULL,
        [TakeProfit3]      DECIMAL(18, 8)  NOT NULL,
        [RiskRewardRatio]  DECIMAL(18, 4)  NOT NULL CONSTRAINT [DF_TradeSignals_RRR] DEFAULT (2.0),
        [StartTime]        DATETIME2(7)    NOT NULL,
        [EndTime]          DATETIME2(7)    NULL,
        [Status]           NVARCHAR(32)    NOT NULL CONSTRAINT [DF_TradeSignals_Status] DEFAULT ('ACTIVE'), -- "ACTIVE", "HIT_TP1", "HIT_TP2", "HIT_TP3", "HIT_SL", "CANCELLED"
        [ExitPrice]        DECIMAL(18, 8)  NULL,
        [PnlPercent]       DECIMAL(18, 4)  NULL,
        [Notes]            NVARCHAR(2000)  NULL,
        [Session]          NVARCHAR(32)    NULL,                           -- "NEW_YORK", "LONDON", "ASIA", "OFF_HOURS"
        [CreatedAt]        DATETIME2(7)    NOT NULL CONSTRAINT [DF_TradeSignals_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt]        DATETIME2(7)    NULL,

        CONSTRAINT [PK_TradeSignals] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT N'[2/4] Đã tạo thành công Bảng [dbo].[TradeSignals].';
END
ELSE
BEGIN
    PRINT N'[2/4] Bảng [dbo].[TradeSignals] đã tồn tại.';

    -- Đảm bảo có cột Session nếu là phiên bản cũ nâng cấp lên
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TradeSignals]') AND name = 'Session')
    BEGIN
        ALTER TABLE [dbo].[TradeSignals] ADD [Session] NVARCHAR(32) NULL;
        PRINT N'[2/4] Đã tự động thêm cột [Session] vào bảng.';
    END
END
GO

-- 3. TẠO CÁC CHỈ MỤC TỐI ƯU TRUY VẤN (INDEXES)
PRINT N'[3/4] Đang kiểm tra & khởi tạo các chỉ mục tốc độ cao (Indexes)...';
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TradeSignals_Symbol' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TradeSignals_Symbol] ON [dbo].[TradeSignals] ([Symbol] ASC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TradeSignals_Status' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TradeSignals_Status] ON [dbo].[TradeSignals] ([Status] ASC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TradeSignals_StartTime' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TradeSignals_StartTime] ON [dbo].[TradeSignals] ([StartTime] DESC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TradeSignals_Session' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TradeSignals_Session] ON [dbo].[TradeSignals] ([Session] ASC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TradeSignals_Symbol_Status' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TradeSignals_Symbol_Status] ON [dbo].[TradeSignals] ([Symbol] ASC, [Status] ASC);
END
GO

PRINT N'[3/4] Đã hoàn tất cấu hình toàn bộ Indexes tối ưu truy vấn.';
GO

-- 4. INSERT DỮ LIỆU BAN ĐẦU (SEED DATA NẾU CHƯA CÓ)
PRINT N'[4/4] Đang kiểm tra và đồng bộ dữ liệu hạt giống (Seed Data)...';
GO

-- Seed 1: TRD-NEAR201 (Win TP1)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-NEAR201')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-NEAR201', 'NEARUSDT', 'LONG', '15m', 81, 4.85000000, 4.71000000, 5.12000000, 5.28000000, 5.45000000, 2.5000, '2026-10-20T14:15:00', '2026-10-21T03:45:00', 'HIT_TP1', 5.12000000, 5.5700, N'Hội tụ 81% D1/H4 Bullish, breakout cản chéo và retest FVG $4.85 thành công. Chốt lời TP1 chuẩn xác (+5.57%).', 'NEW_YORK', '2026-10-20T14:15:00', '2026-10-21T03:45:00');
END
GO

-- Seed 2: TRD-SOL202 (Win TP2)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-SOL202')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-SOL202', 'SOLUSDT', 'SHORT', '1h', 79, 115.00000000, 118.50000000, 110.25000000, 108.50000000, 105.00000000, 2.5000, '2026-10-21T04:00:00', '2026-10-21T16:30:00', 'HIT_TP2', 108.50000000, 5.6500, N'Mô hình Bearish Quasimodo (QML) giảm giá trên H1 kết hợp phân kỳ âm RSI. Đã chốt lời TP2 thành công (+5.65%).', 'ASIA', '2026-10-21T04:00:00', '2026-10-21T16:30:00');
END
GO

-- Seed 3: TRD-BNB108 (Win TP2)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-BNB108')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-BNB108', 'BNBUSDT', 'LONG', '15m', 80, 592.00000000, 581.50000000, 607.75000000, 618.25000000, 630.00000000, 2.5000, '2026-10-21T12:43:26.6469985', '2026-10-22T02:43:26.6469985', 'HIT_TP2', 618.25000000, 4.4300, N'D1/H4 Golden Cross EMA20/50, nến BOS tăng rõ nét. Chốt lời TP2 xuất sắc (+4.43%).', 'NEW_YORK', '2026-10-21T12:43:26.6469985', '2026-10-22T02:43:26.6469985');
END
GO

-- Seed 4: TRD-SOL305 (Hit SL)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-SOL305')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-SOL305', 'SOLUSDT', 'LONG', '15m', 74, 178.50000000, 174.20000000, 184.95000000, 189.25000000, 194.00000000, 2.5000, '2026-10-22T00:43:26.6469985', '2026-10-22T05:43:26.6469985', 'HIT_SL', 174.20000000, -2.4100, N'Tin tức CPI ra bất ngờ quét thủng hỗ trợ M15. Kích hoạt Stop Loss kỷ luật tại $174.20 (-2.41%).', 'ASIA', '2026-10-22T00:43:26.6469985', '2026-10-22T05:43:26.6469985');
END
GO

-- Seed 5: TRD-ETH402 (Win TP1)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-ETH402')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-ETH402', 'ETHUSDT', 'SHORT', '1h', 82, 2740.00000000, 2815.00000000, 2627.50000000, 2552.50000000, 2470.00000000, 2.5000, '2026-10-22T20:43:26.6469985', '2026-10-23T10:43:26.6469985', 'HIT_TP1', 2627.50000000, 4.1100, N'Chạm vùng kháng cự Bearish FVG trên H1 và phân kỳ giảm RSI. Đạt chốt lời TP1 an toàn (+4.11%).', 'OFF_HOURS', '2026-10-22T20:43:26.6469985', '2026-10-23T10:43:26.6469985');
END
GO

-- Seed 6: TRD-BTC801 (Win TP2)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-BTC801')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-BTC801', 'BTCUSDT', 'LONG', '15m', 78, 81250.00000000, 79800.00000000, 83425.00000000, 84875.00000000, 86500.00000000, 2.5000, '2026-10-23T06:43:26.6469985', '2026-10-23T17:43:26.6469985', 'HIT_TP2', 84875.00000000, 4.4600, N'Hội tụ đủ D1/H4 Bullish, giá kiểm định Bullish Order Block. Đã chốt lời TP2 thành công (+4.46%).', 'ASIA', '2026-10-23T06:43:26.6469985', '2026-10-23T17:43:26.6469985');
END
GO

-- Seed 7: TRD-SOL603 (Active)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-SOL603')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-SOL603', 'SOLUSDT', 'SHORT', '15m', 74, 102.80000000, 104.90000000, 99.65000000, 97.55000000, 94.80000000, 2.5000, '2026-10-24T00:35:00', NULL, 'ACTIVE', NULL, NULL, N'Hội tụ 74%: SOL suy yếu dưới EMA50 và EMA200 M15 ($103.40), từ chối giá tại Bearish Order Block, RSI 36.5 tiếp tục đi xuống.', 'ASIA', '2026-10-24T00:35:00', '2026-10-24T00:35:00');
END
GO

-- Seed 8: TRD-895089 (Hit SL)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-895089')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-895089', 'BTCUSDT', 'SHORT', '15m', 75, 80946.41000000, 82352.86000000, 78836.74000000, 77430.28000000, 75601.90000000, 2.5000, '2026-10-24T00:43:26.6622218', '2026-10-24T01:04:37.4032332', 'HIT_SL', 82352.86000000, -1.7400, N'Dính SL tại $82,352.86. Lỗ: -1.74% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn).', 'ASIA', '2026-10-24T00:43:26.6622218', '2026-10-24T01:04:37.4032332');
END
GO

-- Seed 9: TRD-ETH901 (Win TP1)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-ETH901')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-ETH901', 'ETHUSDT', 'SHORT', '15m', 78, 2382.50000000, 2426.00000000, 2317.25000000, 2273.75000000, 2215.00000000, 2.5000, '2026-10-24T00:50:00', '2026-10-26T06:50:03.0762128', 'HIT_TP1', 2317.25000000, 2.7400, N'Đạt mục tiêu TP1 tại $2,317.25. Lợi nhuận: +2.74%. Khuyến nghị dời SL về Entry hòa vốn.', 'ASIA', '2026-10-24T00:50:00', '2026-10-26T06:50:03.0762181');
END
GO

-- Seed 10: TRD-NEAR501 (Win TP1)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-NEAR501')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-NEAR501', 'NEARUSDT', 'SHORT', '15m', 77, 4.68500000, 4.81500000, 4.49000000, 4.36000000, 4.20000000, 2.5000, '2026-10-24T01:00:00', '2026-10-26T06:49:32.7319532', 'HIT_TP1', 4.49000000, 4.1600, N'Đạt mục tiêu TP1 tại $4.49. Lợi nhuận: +4.16%. Khuyến nghị dời SL về Entry hòa vốn.', 'ASIA', '2026-10-24T01:00:00', '2026-10-26T06:49:32.7320383');
END
GO

-- Seed 11: TRD-BNB702 (Active)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-BNB702')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-BNB702', 'BNBUSDT', 'LONG', '15m', 76, 735.20000000, 722.50000000, 754.25000000, 766.95000000, 782.00000000, 2.5000, '2026-10-24T01:10:00', NULL, 'ACTIVE', NULL, NULL, N'Hội tụ 76%: BNB duy trì cấu trúc tăng vững chắc trên D1/H4, phản ứng bật mạnh tại Bullish Order Block $732.00 - $735.20, RSI > 52 phân kỳ tăng ẩn.', 'ASIA', '2026-10-24T01:10:00', '2026-10-24T01:10:00');
END
GO

-- Seed 12: TRD-AA4A6C (Active)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-AA4A6C')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-AA4A6C', 'BTCUSDT', 'SHORT', '15m', 76, 80764.64000000, 82641.18000000, 77949.83000000, 76073.29000000, 73633.79000000, 2.5000, '2026-10-24T01:48:55.7564178', NULL, 'ACTIVE', NULL, NULL, N'Hội tụ 76% (>70% tiêu chuẩn): 1) Đồng pha đa khung D1/H4 (BEARISH/BEARISH), 2) Áp lực bán dưới EMA50 (81,238.03), 3) Chạm cản Bearish Order Block (Khối lệnh đè phe Bán) và Bearish FVG (80,758.01 - 80,764.64), 4) Động lượng Yếu (RSI < 50 - Phe gấu kiểm soát) Không phát hiện phân kỳ.', 'ASIA', '2026-10-24T01:48:55.7564178', '2026-10-24T01:48:55.7564178');
END
GO

-- Seed 13: TRD-C284FC (Hit SL)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-C284FC')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-C284FC', 'NEARUSDT', 'SHORT', '15m', 80, 4.47000000, 4.55000000, 4.35000000, 4.27000000, 4.17000000, 2.5000, '2026-10-26T06:50:10.2926406', '2026-10-26T08:41:20.7270527', 'HIT_SL', 4.55000000, -1.7900, N'Dính SL tại $4.55. Lỗ: -1.79% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn).', 'ASIA', '2026-10-26T06:50:10.2926406', '2026-10-26T08:41:20.7271727');
END
GO

-- Seed 14: TRD-B11E90 (Active)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-B11E90')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-B11E90', 'ETHUSDT', 'SHORT', '15m', 80, 2321.19000000, 2367.49000000, 2251.74000000, 2205.44000000, 2145.25000000, 2.5000, '2026-10-26T06:50:40.0793476', NULL, 'ACTIVE', NULL, NULL, N'Hội tụ 80% (>70% tiêu chuẩn): 1) Đồng pha đa khung D1/H4 (BEARISH/BEARISH), 2) Áp lực bán dưới EMA50 (2,325.77), 3) Chạm cản Bearish Order Block (Khối lệnh đè phe Bán) và Bearish FVG (2,320.08 - 2,321.19), 4) Động lượng Tích cực (RSI > 50 - Phe bò kiểm soát) Phân kỳ Giảm Giá (Bearish Divergence: Giá tạo đỉnh cao hơn nhưng RSI yếu đi).', 'ASIA', '2026-10-26T06:50:40.0793476', '2026-10-26T06:50:40.0793476');
END
GO

-- Seed 15: TRD-0E1F15 (Active)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TradeSignals] WHERE [Id] = 'TRD-0E1F15')
BEGIN
    INSERT INTO [dbo].[TradeSignals] ([Id], [Symbol], [SignalAction], [Timeframe], [ConfidenceScore], [EntryPrice], [StopLoss], [TakeProfit1], [TakeProfit2], [TakeProfit3], [RiskRewardRatio], [StartTime], [EndTime], [Status], [ExitPrice], [PnlPercent], [Notes], [Session], [CreatedAt], [UpdatedAt])
    VALUES ('TRD-0E1F15', 'NEARUSDT', 'LONG', '15m', 71, 4.53000000, 4.44000000, 4.66000000, 4.76000000, 4.87000000, 2.6000, '2026-10-26T08:45:08.3340773', NULL, 'ACTIVE', NULL, NULL, N'Hội tụ 71% (>70% tiêu chuẩn): 1) Đồng pha đa khung D1/H4 (BEARISH/BEARISH), 2) Giá duy trì trên EMA50 (4.51), 3) Retest vùng Bullish Order Block (Khối lệnh gom phe Mua) và FVG (4.52 - 4.55), 4) Động lượng Tích cực (RSI > 50 - Phe bò kiểm soát) Phân kỳ Tăng Giá (Bullish Divergence: Giá tạo đáy thấp hơn nhưng RSI tạo đáy cao hơn), 5) Phiên Âu (London Open Killzone ⚡) (CAO ⚡ (London Judas Swing)).', 'LONDON', '2026-10-26T08:45:08.3340773', '2026-10-26T08:45:08.3340773');
END
GO

-- 5. BÁO CÁO KẾT QUẢ KHỞI TẠO
PRINT N'========================================================================================';
PRINT N'>>> HOÀN TẤT KHỞI TẠO CƠ SỞ DỮ LIỆU [BinanceTracker] THÀNH CÔNG!';
PRINT N'========================================================================================';

SELECT 
    COUNT(*) AS [TongSoLenh],
    SUM(CASE WHEN [Status] = 'ACTIVE' THEN 1 ELSE 0 END) AS [DangChay_Active],
    SUM(CASE WHEN [Status] LIKE 'HIT_TP%' THEN 1 ELSE 0 END) AS [Thang_TP],
    SUM(CASE WHEN [Status] = 'HIT_SL' THEN 1 ELSE 0 END) AS [Thua_SL]
FROM [dbo].[TradeSignals];
GO
