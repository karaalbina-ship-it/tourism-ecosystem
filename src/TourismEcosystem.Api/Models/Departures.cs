using System.ComponentModel.DataAnnotations;

namespace TourismEcosystem.Api.Models;

public sealed class DepartureView
{
    public int DepartureId { get; init; }
    public int TourId { get; init; }
    public string TourTitle { get; init; } = "";
    public int CountryId { get; init; }
    public string Country { get; init; } = "";
    public int? GuideId { get; init; }
    public string? GuideName { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public short Capacity { get; init; }
    public int AvailableSeats { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
    public byte[] RowVersion { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record DepartureRequest(int? GuideId, DateTime StartDate, [Range(1, 1000)] short Capacity);

public sealed record DepartureUpdateRequest(int? GuideId, [Range(1, 1000)] short Capacity, byte[]? RowVersion);
