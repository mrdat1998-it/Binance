using BinanceTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register HttpClient services
builder.Services.AddHttpClient<IBinanceService, BinanceService>();
builder.Services.AddHttpClient<ISentimentService, SentimentService>();
builder.Services.AddHttpClient<IFuturesService, FuturesService>();

// Register Domain Services
builder.Services.AddScoped<IMarketCycleService, MarketCycleService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();

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
