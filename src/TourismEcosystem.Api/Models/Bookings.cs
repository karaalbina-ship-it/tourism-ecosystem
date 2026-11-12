using System.ComponentModel.DataAnnotations;

namespace TourismEcosystem.Api.Models;

public sealed class BookingView
{
    public int BookingId { get; init; }
    public int DepartureId { get; init; }
    public int TourId { get; init; }
    public string TourTitle { get; init; } = "";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int LeadTouristId { get; init; }
    public string LeadTouristName { get; init; } = "";
    public int TravelersCount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal Debt { get; init; }
    public decimal? RefundAmount { get; init; }
    public string Status { get; init; } = "";
    public DateTime BookedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record ExcursionRequest(int ExcursionId, [Range(1, 50)] byte Persons);

public sealed record TravelerRequest(int TouristId);

public sealed record BookingRequest(
    int DepartureId,
    int[]? TouristIds,
    TouristRequest[]? NewTourists,
    ExcursionRequest[]? Excursions,
    PaymentRequest? Payment);

public sealed record BookingResult(int BookingId, int[] TouristIds, int? PaymentId);

public sealed record CancelResult(decimal RefundAmount);
