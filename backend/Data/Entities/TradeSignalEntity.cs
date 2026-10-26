using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BinanceTracker.Api.Models;

namespace BinanceTracker.Api.Data.Entities;

[Table("TradeSignals")]
public class TradeSignalEntity
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string SignalAction { get; set; } = string.Empty; // "LONG" or "SHORT"

    [Required]
    [MaxLength(16)]
    public string Timeframe { get; set; } = "15m";

    public int ConfidenceScore { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal EntryPrice { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal StopLoss { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal TakeProfit1 { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal TakeProfit2 { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal TakeProfit3 { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RiskRewardRatio { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE", "HIT_TP1", "HIT_TP2", "HIT_TP3", "HIT_SL", "CANCELLED"

    [Column(TypeName = "decimal(18,8)")]
    public decimal? ExitPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? PnlPercent { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(32)]
    public string? Session { get; set; } // "NEW_YORK", "LONDON", "ASIA", "OFF_HOURS"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public TradeSignalRecord ToRecord() => new(
        Id: Id,
        Symbol: Symbol,
        SignalAction: SignalAction,
        Timeframe: Timeframe,
        ConfidenceScore: ConfidenceScore,
        EntryPrice: EntryPrice,
        StopLoss: StopLoss,
        TakeProfit1: TakeProfit1,
        TakeProfit2: TakeProfit2,
        TakeProfit3: TakeProfit3,
        RiskRewardRatio: RiskRewardRatio,
        StartTime: StartTime.Kind == DateTimeKind.Utc ? StartTime : DateTime.SpecifyKind(StartTime, DateTimeKind.Utc),
        EndTime: EndTime.HasValue ? (EndTime.Value.Kind == DateTimeKind.Utc ? EndTime.Value : DateTime.SpecifyKind(EndTime.Value, DateTimeKind.Utc)) : null,
        Status: Status,
        ExitPrice: ExitPrice,
        PnlPercent: PnlPercent,
        Notes: Notes ?? string.Empty,
        Session: !string.IsNullOrWhiteSpace(Session) ? Session : DetermineSessionFromTime(StartTime)
    );

    public static string DetermineSessionFromTime(DateTime timeUtc)
    {
        var vnTime = timeUtc.Kind == DateTimeKind.Utc ? timeUtc.AddHours(7) : timeUtc;
        int hour = vnTime.Hour;
        if (hour >= 19 || hour < 2) return "NEW_YORK";
        if (hour >= 14) return "LONDON";
        if (hour >= 7) return "ASIA";
        return "OFF_HOURS";
    }

    public static TradeSignalEntity FromRecord(TradeSignalRecord r) => new()
    {
        Id = r.Id,
        Symbol = r.Symbol,
        SignalAction = r.SignalAction,
        Timeframe = r.Timeframe,
        ConfidenceScore = r.ConfidenceScore,
        EntryPrice = r.EntryPrice,
        StopLoss = r.StopLoss,
        TakeProfit1 = r.TakeProfit1,
        TakeProfit2 = r.TakeProfit2,
        TakeProfit3 = r.TakeProfit3,
        RiskRewardRatio = r.RiskRewardRatio,
        StartTime = r.StartTime,
        EndTime = r.EndTime,
        Status = r.Status,
        ExitPrice = r.ExitPrice,
        PnlPercent = r.PnlPercent,
        Notes = r.Notes,
        Session = r.Session ?? DetermineSessionFromTime(r.StartTime),
        CreatedAt = r.StartTime,
        UpdatedAt = r.EndTime ?? r.StartTime
    };
}
