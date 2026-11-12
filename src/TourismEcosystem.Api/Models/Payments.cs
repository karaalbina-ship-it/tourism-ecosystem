using System.ComponentModel.DataAnnotations;

namespace TourismEcosystem.Api.Models;

public sealed class PaymentView
{
    public int PaymentId { get; init; }
    public int BookingId { get; init; }
    public int LeadTouristId { get; init; }
    public string LeadTouristName { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public string Method { get; init; } = "";
    public bool IsPaid { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed class OverduePaymentView
{
    public int PaymentId { get; init; }
    public int BookingId { get; init; }
    public string TouristName { get; init; } = "";
    public string? TouristPhone { get; init; }
    public decimal Amount { get; init; }
    public DateTime DueDate { get; init; }
    public int DaysOverdue { get; init; }
}

public sealed record PaymentRequest(
    [Range(0.01, 1e10)] decimal Amount,
    DateTime DueDate,
    DateTime? PaidAt,
    [Required, RegularExpression("Cash|Card|Transfer")] string Method);

public sealed record MarkPaidRequest(DateTime? PaidAt);
