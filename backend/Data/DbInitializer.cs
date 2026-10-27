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

            // Auto-migration: Đảm bảo cột [Session] tồn tại trên bảng [TradeSignals]
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TradeSignals]') AND name = 'Session') " +
                    "ALTER TABLE [dbo].[TradeSignals] ADD [Session] NVARCHAR(32) NULL;");

                await context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TradeSignals_Session' AND object_id = OBJECT_ID(N'[dbo].[TradeSignals]')) " +
                    "CREATE NONCLUSTERED INDEX [IX_TradeSignals_Session] ON [dbo].[TradeSignals] ([Session]);");
            }
            catch (Exception exMig)
            {
                logger.LogWarning("Auto-migration cột Session: {Message}", exMig.Message);
            }

            var count = await context.TradeSignals.CountAsync();
            if (count == 0)
            {
                logger.LogInformation("Database [TradeSignals] đang sạch sẽ (0 bản ghi). Sẵn sàng bắt đầu ghi nhận tín hiệu giao dịch mới thời gian thực.");
            }
            else
            {
                logger.LogInformation("SQL Server database initialized successfully with {Count} trade signal records.", count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the SQL Server database.");
            throw;
        }
    }
}
