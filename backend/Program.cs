using BinanceTracker.Api.Data;
using BinanceTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register SQLite Database Context (ACID Relational Storage)
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "binance_tracker.db");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Register HttpClient services
builder.Services.AddHttpClient<IBinanceService, BinanceService>();
builder.Services.AddHttpClient<ISentimentService, SentimentService>();
builder.Services.AddHttpClient<IFuturesService, FuturesService>();

// Register Domain Services
builder.Services.AddScoped<IMarketCycleService, MarketCycleService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<ITradeHistoryService, TradeHistoryService>();

// Configure CORS for Angular frontend (both localhost:4200 and any dev port)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDev", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Initialize SQLite database and seed initial trade history
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbInitializer.InitializeDatabaseAsync(dbContext, app.Environment, logger);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAllDev");

// Static files support (for serving built Angular dist if needed)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Fallback to index.html for SPA routing if deployed together
app.MapFallbackToFile("index.html");

app.Run();
