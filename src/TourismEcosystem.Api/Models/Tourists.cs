using System.ComponentModel.DataAnnotations;

namespace TourismEcosystem.Api.Models;

public sealed class TouristView
{
    public int TouristId { get; init; }
    public string FullName { get; init; } = "";
    public DateTime BirthDate { get; init; }
    public string? PassportNo { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record TouristRequest(
    [Required, StringLength(150)] string FullName,
    DateTime BirthDate,
    [StringLength(20)] string? PassportNo = null,
    [StringLength(20)] string? Phone = null,
    [EmailAddress, StringLength(255)] string? Email = null);
