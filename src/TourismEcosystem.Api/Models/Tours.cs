using System.ComponentModel.DataAnnotations;

namespace TourismEcosystem.Api.Models;

public sealed class TourView
{
    public int TourId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public int CountryId { get; init; }
    public string Country { get; init; } = "";
    public byte DurationDays { get; init; }
    public decimal BasePrice { get; init; }
    public string Currency { get; init; } = "";
    public byte[] RowVersion { get; init; } = [];
    public decimal? AvgRating { get; init; }
    public int ReviewsCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record TourRequest(
    int CountryId,
    [Required, StringLength(200)] string Title,
    string? Description,
    [Range(1, 60)] byte DurationDays,
    [Range(0.01, 1e10)] decimal BasePrice,
    [StringLength(3, MinimumLength = 3)] string Currency = "UAH");

public sealed record UpdatePriceRequest([Range(0.01, 1e10)] decimal NewPrice, byte[]? RowVersion);
