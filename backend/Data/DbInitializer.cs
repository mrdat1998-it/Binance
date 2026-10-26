using System.Text.Json;
using BinanceTracker.Api.Data.Entities;
using BinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BinanceTracker.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeDatabaseAsync(AppDbContext context, IWebHostEnvironment env, ILogger logger)
    {
        try
        {
            // Ensure the SQL Server database and all tables are created
            await context.Database.EnsureCreatedAsync();

            // Auto-migration: Đảm bảo cột [Session] tồn tại trên bảng [TradeSignals] và backfill dữ liệu cũ
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TradeSignals]') AND name = 'Session') " +
                    "ALTER TABLE [dbo].[TradeSignals] ADD [Session] NVARCHAR(32) NULL;");

                await context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TradeSignals_Session' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]')) " +
                    "CREATE NONCLUSTERED INDEX [IX_TradeSignals_Session] ON [dbo].[TradeSignals] ([Session]);");

                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE [dbo].[TradeSignals] " +
                    "SET [Session] = CASE " +
                    "   WHEN DATEPART(hour, DATEADD(hour, 7, [StartTime])) >= 19 OR DATEPART(hour, DATEADD(hour, 7, [StartTime])) < 2 THEN 'NEW_YORK' " +
                    "   WHEN DATEPART(hour, DATEADD(hour, 7, [StartTime])) >= 14 THEN 'LONDON' " +
                    "   WHEN DATEPART(hour, DATEADD(hour, 7, [StartTime])) >= 7 THEN 'ASIA' " +
                    "   ELSE 'OFF_HOURS' " +
                    "END " +
                    "WHERE [Session] IS NULL;");

                logger.LogInformation("Auto-migration cột Session và backfill thành công.");
            }
            catch (Exception exMig)
            {
                logger.LogWarning("Auto-migration cột Session: {Message}", exMig.Message);
            }

            // Check if any records already exist in the database
            if (!await context.TradeSignals.AnyAsync())
            {
                logger.LogInformation("Database is empty. Checking for initial data to seed...");

                var jsonPath = Path.Combine(env.ContentRootPath, "Data", "trade_history.json");
                bool seededFromJson = false;

                if (File.Exists(jsonPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(jsonPath);
                        var records = JsonSerializer.Deserialize<List<TradeSignalRecord>>(json);
                        if (records != null && records.Count > 0)
                        {
                            var entities = records.Select(TradeSignalEntity.FromRecord).ToList();
                            await context.TradeSignals.AddRangeAsync(entities);
                            await context.SaveChangesAsync();
                            seededFromJson = true;
                            logger.LogInformation("Successfully migrated {Count} trade records from JSON into SQL Server database.", entities.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not load records from trade_history.json. Falling back to default seed.");
                    }
                }

                if (!seededFromJson)
                {
                    var defaultSeeds = GetDefaultSeeds();
                    await context.TradeSignals.AddRangeAsync(defaultSeeds);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} default trade records into SQL Server database.", defaultSeeds.Count);
                }
            }
            else
            {
                var count = await context.TradeSignals.CountAsync();
                logger.LogInformation("SQL Server database initialized successfully with {Count} trade signal records.", count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the SQLite database.");
            throw;
        }
    }

    private static List<TradeSignalEntity> GetDefaultSeeds()
    {
        var now = DateTime.UtcNow;
        return new List<TradeSignalEntity>
        {
            new()
            {
                Id = "TRD-AA4A6C",
                Symbol = "BTCUSDT",
                SignalAction = "SHORT",
                Timeframe = "15m",
                ConfidenceScore = 76,
                EntryPrice = 80764.64m,
                StopLoss = 82641.18m,
                TakeProfit1 = 77949.83m,
                TakeProfit2 = 76073.29m,
                TakeProfit3 = 73633.79m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddMinutes(-10),
                EndTime = null,
                Status = "ACTIVE",
                Notes = "Hội tụ 76%: Đồng pha D1/H4 Bearish, cản Bearish Order Block ($81,400) và Bearish FVG, RSI < 50. Đang theo dõi.",
                CreatedAt = now.AddMinutes(-10)
            },
            new()
            {
                Id = "TRD-ETH901",
                Symbol = "ETHUSDT",
                SignalAction = "SHORT",
                Timeframe = "15m",
                ConfidenceScore = 78,
                EntryPrice = 2382.50m,
                StopLoss = 2426.00m,
                TakeProfit1 = 2317.25m,
                TakeProfit2 = 2273.75m,
                TakeProfit3 = 2215.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddMinutes(-45),
                EndTime = null,
                Status = "ACTIVE",
                Notes = "Hội tụ 78%: Khung D1/H4 gãy trendline tăng, giá test vùng Bearish FVG $2,382.50 - $2,390.00, RSI 38.2 nằm dưới 50. Áp lực phe gấu chi phối.",
                CreatedAt = now.AddMinutes(-45)
            },
            new()
            {
                Id = "TRD-BNB702",
                Symbol = "BNBUSDT",
                SignalAction = "LONG",
                Timeframe = "15m",
                ConfidenceScore = 76,
                EntryPrice = 735.20m,
                StopLoss = 722.50m,
                TakeProfit1 = 754.25m,
                TakeProfit2 = 766.95m,
                TakeProfit3 = 782.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddMinutes(-20),
                EndTime = null,
                Status = "ACTIVE",
                Notes = "Hội tụ 76%: BNB duy trì cấu trúc tăng vững chắc trên D1/H4, phản ứng bật mạnh tại Bullish Order Block $732.00 - $735.20, RSI > 52 phân kỳ tăng ẩn.",
                CreatedAt = now.AddMinutes(-20)
            },
            new()
            {
                Id = "TRD-SOL603",
                Symbol = "SOLUSDT",
                SignalAction = "SHORT",
                Timeframe = "15m",
                ConfidenceScore = 74,
                EntryPrice = 102.80m,
                StopLoss = 104.90m,
                TakeProfit1 = 99.65m,
                TakeProfit2 = 97.55m,
                TakeProfit3 = 94.80m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddMinutes(-50),
                EndTime = null,
                Status = "ACTIVE",
                Notes = "Hội tụ 74%: SOL suy yếu dưới EMA50 và EMA200 M15 ($103.40), từ chối giá tại Bearish Order Block, RSI 36.5 tiếp tục đi xuống.",
                CreatedAt = now.AddMinutes(-50)
            },
            new()
            {
                Id = "TRD-NEAR501",
                Symbol = "NEARUSDT",
                SignalAction = "SHORT",
                Timeframe = "15m",
                ConfidenceScore = 77,
                EntryPrice = 4.685m,
                StopLoss = 4.815m,
                TakeProfit1 = 4.490m,
                TakeProfit2 = 4.360m,
                TakeProfit3 = 4.200m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddMinutes(-35),
                EndTime = null,
                Status = "ACTIVE",
                Notes = "Hội tụ 77%: NEAR thủng đáy phiên Á tại $4.70 tạo Bearish BOS, chạm vùng Bearish FVG test lại $4.685, khối lượng bán tăng đột biến.",
                CreatedAt = now.AddMinutes(-35)
            },
            new()
            {
                Id = "TRD-895089",
                Symbol = "BTCUSDT",
                SignalAction = "SHORT",
                Timeframe = "15m",
                ConfidenceScore = 75,
                EntryPrice = 80946.41m,
                StopLoss = 82352.86m,
                TakeProfit1 = 78836.74m,
                TakeProfit2 = 77430.28m,
                TakeProfit3 = 75601.90m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddHours(-1),
                EndTime = now.AddMinutes(-20),
                Status = "HIT_SL",
                ExitPrice = 82352.86m,
                PnlPercent = -1.74m,
                Notes = "Dính SL tại $82,352.86. Lỗ: -1.74% (Tuân thủ kỷ luật cắt lỗ bảo toàn vốn).",
                CreatedAt = now.AddHours(-1),
                UpdatedAt = now.AddMinutes(-20)
            },
            new()
            {
                Id = "TRD-BTC801",
                Symbol = "BTCUSDT",
                SignalAction = "LONG",
                Timeframe = "15m",
                ConfidenceScore = 78,
                EntryPrice = 81250.00m,
                StopLoss = 79800.00m,
                TakeProfit1 = 83425.00m,
                TakeProfit2 = 84875.00m,
                TakeProfit3 = 86500.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddHours(-18),
                EndTime = now.AddHours(-7),
                Status = "HIT_TP2",
                ExitPrice = 84875.00m,
                PnlPercent = 4.46m,
                Notes = "Hội tụ đủ D1/H4 Bullish, giá kiểm định Bullish Order Block. Đã chốt lời TP2 thành công (+4.46%).",
                CreatedAt = now.AddHours(-18),
                UpdatedAt = now.AddHours(-7)
            },
            new()
            {
                Id = "TRD-ETH402",
                Symbol = "ETHUSDT",
                SignalAction = "SHORT",
                Timeframe = "1h",
                ConfidenceScore = 82,
                EntryPrice = 2740.00m,
                StopLoss = 2815.00m,
                TakeProfit1 = 2627.50m,
                TakeProfit2 = 2552.50m,
                TakeProfit3 = 2470.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddDays(-1).AddHours(-4),
                EndTime = now.AddHours(-14),
                Status = "HIT_TP1",
                ExitPrice = 2627.50m,
                PnlPercent = 4.11m,
                Notes = "Chạm vùng kháng cự Bearish FVG trên H1 và phân kỳ giảm RSI. Đạt chốt lời TP1 an toàn (+4.11%).",
                CreatedAt = now.AddDays(-1).AddHours(-4),
                UpdatedAt = now.AddHours(-14)
            },
            new()
            {
                Id = "TRD-SOL202",
                Symbol = "SOLUSDT",
                SignalAction = "SHORT",
                Timeframe = "1h",
                ConfidenceScore = 79,
                EntryPrice = 115.00m,
                StopLoss = 118.50m,
                TakeProfit1 = 110.25m,
                TakeProfit2 = 108.50m,
                TakeProfit3 = 105.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddDays(-2).AddHours(-6),
                EndTime = now.AddDays(-1).AddHours(-18),
                Status = "HIT_TP2",
                ExitPrice = 108.50m,
                PnlPercent = 5.65m,
                Notes = "Mô hình Bearish Quasimodo (QML) giảm giá trên H1 kết hợp phân kỳ âm RSI. Đã chốt lời TP2 thành công (+5.65%).",
                CreatedAt = now.AddDays(-2).AddHours(-6),
                UpdatedAt = now.AddDays(-1).AddHours(-18)
            },
            new()
            {
                Id = "TRD-SOL305",
                Symbol = "SOLUSDT",
                SignalAction = "LONG",
                Timeframe = "15m",
                ConfidenceScore = 74,
                EntryPrice = 178.50m,
                StopLoss = 174.20m,
                TakeProfit1 = 184.95m,
                TakeProfit2 = 189.25m,
                TakeProfit3 = 194.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddDays(-2),
                EndTime = now.AddDays(-1).AddHours(-19),
                Status = "HIT_SL",
                ExitPrice = 174.20m,
                PnlPercent = -2.41m,
                Notes = "Tin tức CPI ra bất ngờ quét thủng hỗ trợ M15. Kích hoạt Stop Loss kỷ luật tại $174.20 (-2.41%).",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-1).AddHours(-19)
            },
            new()
            {
                Id = "TRD-BNB108",
                Symbol = "BNBUSDT",
                SignalAction = "LONG",
                Timeframe = "15m",
                ConfidenceScore = 80,
                EntryPrice = 592.00m,
                StopLoss = 581.50m,
                TakeProfit1 = 607.75m,
                TakeProfit2 = 618.25m,
                TakeProfit3 = 630.00m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddDays(-2).AddHours(-12),
                EndTime = now.AddDays(-1).AddHours(-22),
                Status = "HIT_TP2",
                ExitPrice = 618.25m,
                PnlPercent = 4.43m,
                Notes = "D1/H4 Golden Cross EMA20/50, nến BOS tăng rõ nét. Chốt lời TP2 xuất sắc (+4.43%).",
                CreatedAt = now.AddDays(-2).AddHours(-12),
                UpdatedAt = now.AddDays(-1).AddHours(-22)
            },
            new()
            {
                Id = "TRD-NEAR201",
                Symbol = "NEARUSDT",
                SignalAction = "LONG",
                Timeframe = "15m",
                ConfidenceScore = 81,
                EntryPrice = 4.850m,
                StopLoss = 4.710m,
                TakeProfit1 = 5.120m,
                TakeProfit2 = 5.280m,
                TakeProfit3 = 5.450m,
                RiskRewardRatio = 2.5m,
                StartTime = now.AddDays(-3),
                EndTime = now.AddDays(-2).AddHours(-10),
                Status = "HIT_TP1",
                ExitPrice = 5.120m,
                PnlPercent = 5.57m,
                Notes = "Hội tụ 81% D1/H4 Bullish, breakout cản chéo và retest FVG $4.85 thành công. Chốt lời TP1 chuẩn xác (+5.57%).",
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-2).AddHours(-10)
            }
        };
    }
}

