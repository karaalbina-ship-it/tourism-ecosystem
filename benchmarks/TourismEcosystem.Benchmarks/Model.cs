using MongoDB.Bson.Serialization.Attributes;

namespace TourismEcosystem.Benchmarks;

public sealed class Offer
{
    [BsonId] public int Id { get; set; }
    public int TourId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string TourType { get; set; } = "";
    public int CountryId { get; set; }
    public string Country { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "UAH";
    public string Status { get; set; } = "";
    public int Capacity { get; set; }
    public int AvailableSeats { get; set; }
    public OfferGuide? Guide { get; set; }
    public List<OfferDay> Itinerary { get; set; } = [];
    public List<OfferTransfer> Transfers { get; set; } = [];
    public List<OfferExcursion> Excursions { get; set; } = [];
    public List<OfferPriceChange> PriceHistory { get; set; } = [];
    public OfferRating Rating { get; set; } = new();
    public List<OfferReview> Reviews { get; set; } = [];
    public Dictionary<string, object> Attributes { get; set; } = [];

    [BsonIgnore] public List<GenBooking> Bookings { get; set; } = [];
    [BsonIgnore] public List<OfferReview> AllReviews { get; set; } = [];

    public long Checksum() =>
        Id + Itinerary.Count * 10L + Transfers.Count * 100L + Excursions.Count * 1_000L + Attributes.Count * 10_000L
        + PriceHistory.Count * 100_000L + Rating.Count * 7L + AvailableSeats * 13L + (long)Price
        + Reviews.Sum(r => (long)r.TouristId) + Itinerary.Sum(d => (long)(d.Hotel?.Id ?? 0))
        + Excursions.Sum(e => (long)e.Id) + (Guide?.Id ?? 0);
}

public sealed class OfferGuide
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Languages { get; set; } = "";
}

public sealed class OfferDay
{
    public int Day { get; set; }
    public int CityId { get; set; }
    public string City { get; set; } = "";
    public string Description { get; set; } = "";
    public OfferHotel? Hotel { get; set; }
}

public sealed class OfferHotel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Stars { get; set; }
}

public sealed class OfferTransfer
{
    public string Type { get; set; } = "";
    public string Carrier { get; set; } = "";
    public int FromCityId { get; set; }
    public int ToCityId { get; set; }
    public DateTime DepartAt { get; set; }
    public DateTime ArriveAt { get; set; }
}

public sealed class OfferExcursion
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CityId { get; set; }
    public decimal Price { get; set; }
}

public sealed class OfferPriceChange
{
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class OfferRating
{
    public double? Avg { get; set; }
    public int Count { get; set; }
}

public sealed class OfferReview
{
    public int TouristId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record GenBooking(int BookingId, int[] TouristIds);
