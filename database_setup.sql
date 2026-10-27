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

-- 4. TRẠNG THÁI BẢNG DỮ LIỆU
PRINT N'[4/4] Bảng [dbo].[TradeSignals] hoàn tất và sẵn sàng ghi nhận tín hiệu giao dịch mới thời gian thực từ bây giờ.';
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
