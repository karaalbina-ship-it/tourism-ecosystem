namespace TourismEcosystem.Benchmarks;

public sealed record Schedule(DateTime StartDate, string Status, int Capacity, int Booked)
{
    public int Available => Capacity - Booked;
}

public sealed record Excursion(int Id, int CityId, int GuideId, string Name, decimal Price, decimal DurationHours);

public sealed class DataGenerator(int departures, int seed = 42)
{
    public const int CitiesPerCountry = 10;
    public const int HotelsPerCity = 5;
    public const int GuideCount = 500;
    public const int DeparturesPerTour = 10;
    public const int OriginCityId = 21;
    public static readonly DateTime Now = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly (string Name, string Iso)[] Countries =
    [
        ("Туреччина", "TR"), ("Єгипет", "EG"), ("Україна", "UA"), ("Іспанія", "ES"), ("Італія", "IT"),
        ("Греція", "GR"), ("Хорватія", "HR"), ("Чорногорія", "ME"), ("Болгарія", "BG"), ("Польща", "PL"),
        ("Чехія", "CZ"), ("Австрія", "AT"), ("Франція", "FR"), ("Португалія", "PT"), ("Німеччина", "DE"),
        ("Угорщина", "HU"), ("Румунія", "RO"), ("Словаччина", "SK"), ("Словенія", "SI"), ("Кіпр", "CY"),
        ("Мальта", "MT"), ("ОАЕ", "AE"), ("Таїланд", "TH"), ("Шрі-Ланка", "LK"), ("Мальдіви", "MV"),
        ("Індонезія", "ID"), ("В'єтнам", "VN"), ("Японія", "JP"), ("Грузія", "GE"), ("Вірменія", "AM"),
        ("Марокко", "MA"), ("Туніс", "TN"), ("Йорданія", "JO"), ("Ісландія", "IS"), ("Норвегія", "NO"),
        ("Швеція", "SE"), ("Фінляндія", "FI"), ("Данія", "DK"), ("Нідерланди", "NL"), ("Велика Британія", "GB")
    ];

    public static readonly string[] TourTypes = ["Excursion", "Beach", "Ski", "Cruise"];
    private static readonly double[] TypeWeights = [0.45, 0.30, 0.15, 0.10];
    private static readonly string[] TypeTitles = ["Екскурсійний тур", "Пляжний відпочинок", "Гірськолижний тур", "Круїз"];
    private static readonly string[] HotelNames = ["Панорама", "Гранд", "Бриз", "Оазис", "Рив'єра"];
    private static readonly string[] ExcursionNames =
        ["Оглядова екскурсія містом", "Музей історії", "Гастротур", "Нічне місто", "Замок і околиці", "Морська прогулянка"];
    private static readonly string[] FirstNames = ["Андрій", "Олена", "Іван", "Марія", "Тарас", "Ірина", "Петро", "Наталія", "Сергій", "Оксана"];
    private static readonly string[] LastNames = ["Мельник", "Шевченко", "Бондар", "Коваль", "Ткаченко", "Кравець", "Лисенко", "Мороз", "Савченко", "Поліщук"];
    private static readonly string[] Languages = ["uk, en", "uk, en, de", "uk, en, es", "uk, en, it", "uk, en, fr", "uk, pl"];
    private static readonly string[] Ships = ["Aurora", "Costa Serena", "MSC Lirica", "Celestyal Crystal", "Norwegian Jade"];
    private static readonly string[] Cabins = ["Inside", "Ocean", "Balcony", "Suite"];
    private static readonly string[] Comments = ["Чудовий тур", "Все сподобалось", "Гід супер", "Готель міг би бути кращим", null!];

    public static readonly Excursion[] Excursions = BuildExcursions();
    private static readonly ILookup<int, Excursion> ExcursionsByCity = Excursions.ToLookup(e => e.CityId);

    public int Departures => departures;
    public int TourCount => (departures + DeparturesPerTour - 1) / DeparturesPerTour;
    public int TouristCount => Math.Max(1_000, departures / 2);

    public static int CountryCount => Countries.Length;
    public static int CityCount => CountryCount * CitiesPerCountry;
    public static int HotelCount => CityCount * HotelsPerCity;
    public static int CountryOf(int cityId) => (cityId - 1) / CitiesPerCountry + 1;
    public static string CityName(int cityId) => $"{Countries[CountryOf(cityId) - 1].Name}, місто {(cityId - 1) % CitiesPerCountry + 1}";
    public static int HotelId(int cityId, int k) => (cityId - 1) * HotelsPerCity + k + 1;
    public static int CityOfHotel(int hotelId) => (hotelId - 1) / HotelsPerCity + 1;
    public static string HotelName(int hotelId) => $"{HotelNames[(hotelId - 1) % HotelsPerCity]} {CityOfHotel(hotelId)}";
    public static int HotelStars(int hotelId) => 2 + hotelId * 7 % 4;
    public static string GuideName(int guideId) => $"{FirstNames[guideId % 10]} {LastNames[guideId / 10 % 10]} ({guideId})";
    public static string GuidePhone(int guideId) => $"+38063{guideId:D7}";
    public static string GuideLanguages(int guideId) => Languages[guideId % Languages.Length];
    public static string TouristName(int touristId) => $"{FirstNames[touristId % 10]} {LastNames[touristId / 10 % 10]} ({touristId})";
    public static DateTime TouristBirthDate(int touristId) => new DateTime(1950, 1, 1).AddDays(touristId * 37 % 20_000);
    public static int TourOf(int departureId) => (departureId - 1) / DeparturesPerTour + 1;
    public static int SkewedCountry(Random r) => (int)(CountryCount * Math.Pow(r.NextDouble(), 2)) + 1;

    private static int Hash(int a, int b) => (int)(((long)a * 1_000_003 + b) % int.MaxValue);

    private static Excursion[] BuildExcursions()
    {
        var list = new List<Excursion>();
        for (var city = 1; city <= CityCount; city++)
            for (var k = 0; k < city % 4; k++)
            {
                var id = list.Count + 1;
                list.Add(new Excursion(id, city, id * 13 % GuideCount + 1, ExcursionNames[(city + k) % ExcursionNames.Length],
                    300m + id * 37 % 28 * 100m, 1.5m + id % 9 * 0.5m));
            }
        return [.. list];
    }

    public Schedule ScheduleOf(int departureId)
    {
        var r = new Random(Hash(seed * 7 + 1, departureId));
        var start = Now.AddDays(-180 + r.Next(545));
        var capacity = r.Next(10, 41);
        var x = r.NextDouble();
        var y = r.NextDouble();
        if (start < Now) return new Schedule(start, "Closed", capacity, (int)Math.Round(capacity * (0.5 + y * 0.5)));
        if (x < 0.04) return new Schedule(start, "Cancelled", capacity, 0);
        return new Schedule(start, "Open", capacity, x < 0.14 ? capacity : (int)Math.Round(capacity * y * 0.8));
    }

    public bool IsBookable(int departureId)
    {
        var s = ScheduleOf(departureId);
        return s.Status == "Open" && s.StartDate >= Now.AddDays(60) && s.Available > 0;
    }

    public IEnumerable<Offer> Offers(int from, int count)
    {
        Offer? tour = null;
        for (var id = from; id < from + count; id++)
        {
            var tourId = TourOf(id);
            if (tour is null || tour.TourId != tourId) tour = CreateTour(tourId, new Random(Hash(seed * 3 + 2, tourId)));
            yield return CreateDeparture(tour, id, ScheduleOf(id), new Random(Hash(seed * 13 + 3, id)));
        }
    }

    public Offer CreateNewOffer(int departureId, int tourId)
    {
        var r = new Random(Hash(seed * 17 + 5, departureId));
        var tour = CreateTour(tourId, r);
        var schedule = new Schedule(Now.AddDays(30 + r.Next(300)), "Open", r.Next(10, 41), 0);
        return CreateDeparture(tour, departureId, schedule, r);
    }

    private Offer CreateTour(int tourId, Random r)
    {
        var countryId = SkewedCountry(r);
        var typeIdx = Pick(r, TypeWeights);
        var type = TourTypes[typeIdx];
        var (minDays, maxDays, minCities, maxCities, perDay) = type switch
        {
            "Beach" => (5, 12, 1, 2, 2_500m),
            "Ski" => (5, 9, 1, 2, 3_500m),
            "Cruise" => (6, 12, 3, 6, 4_500m),
            _ => (3, 10, 2, 5, 1_800m)
        };
        var duration = r.Next(minDays, maxDays + 1);
        var cityCount = Math.Min(duration, r.Next(minCities, maxCities + 1));
        var cities = Enumerable.Range(0, CitiesPerCountry).OrderBy(_ => r.Next()).Take(cityCount)
            .Select(k => (countryId - 1) * CitiesPerCountry + k + 1).ToArray();

        var itinerary = new List<OfferDay>();
        for (var day = 1; day <= duration; day++)
        {
            var cityId = cities[(day - 1) * cityCount / duration];
            var hotelId = HotelId(cityId, (int)((uint)Hash(tourId, cityId) % HotelsPerCity));
            itinerary.Add(new OfferDay
            {
                Day = day, CityId = cityId, City = CityName(cityId), Description = $"День {day}: {CityName(cityId)}",
                Hotel = day == duration ? null : new OfferHotel { Id = hotelId, Name = HotelName(hotelId), Stars = HotelStars(hotelId) }
            });
        }

        var countryFactor = 0.7m + countryId % 7 * 0.12m;
        var price = Math.Round(duration * perDay * countryFactor * (0.8m + (decimal)r.NextDouble() * 0.5m) / 100m) * 100m;
        var created = Now.AddDays(-400 + r.Next(200));
        var history = new List<OfferPriceChange>();
        var current = price;
        for (var n = r.Next(4); n > 0; n--)
        {
            var old = Math.Round(current * (1.03m + (decimal)r.NextDouble() * 0.07m) / 100m) * 100m;
            history.Insert(0, new OfferPriceChange { OldPrice = old, NewPrice = current, ChangedAt = created.AddDays(n * 15) });
            current = old;
        }

        return new Offer
        {
            TourId = tourId,
            Title = $"{TypeTitles[typeIdx]}: {Countries[countryId - 1].Name}, {duration} дн.",
            Description = r.NextDouble() < 0.3 ? "Ранне бронювання зі знижкою, трансфер включено." : null,
            TourType = type,
            CountryId = countryId,
            Country = Countries[countryId - 1].Name,
            DurationDays = duration,
            Price = price,
            Itinerary = itinerary,
            PriceHistory = history,
            Excursions = cities.SelectMany(c => ExcursionsByCity[c]).OrderBy(e => e.Id)
                .Select(e => new OfferExcursion { Id = e.Id, Name = e.Name, CityId = e.CityId, Price = e.Price }).ToList(),
            Attributes = Attributes(r, type)
        };
    }

    private Offer CreateDeparture(Offer tour, int id, Schedule s, Random r)
    {
        var guideId = r.Next(1, GuideCount + 1);
        var offer = new Offer
        {
            Id = id, TourId = tour.TourId, Title = tour.Title, Description = tour.Description, TourType = tour.TourType,
            CountryId = tour.CountryId, Country = tour.Country, DurationDays = tour.DurationDays, Price = tour.Price,
            StartDate = s.StartDate, EndDate = s.StartDate.AddDays(tour.DurationDays - 1),
            Status = s.Status, Capacity = s.Capacity, AvailableSeats = s.Available,
            Guide = new OfferGuide { Id = guideId, Name = GuideName(guideId), Languages = GuideLanguages(guideId) },
            Itinerary = tour.Itinerary, PriceHistory = tour.PriceHistory, Excursions = tour.Excursions, Attributes = tour.Attributes
        };

        var stops = new List<int> { OriginCityId };
        foreach (var d in tour.Itinerary)
            if (d.CityId != stops[^1]) stops.Add(d.CityId);
        var crossBorder = tour.CountryId != CountryOf(OriginCityId);
        for (var i = 0; i + 1 < stops.Count && offer.Transfers.Count < 3; i++)
        {
            var depart = s.StartDate.AddDays(i == 0 ? 0 : tour.Itinerary.First(d => d.CityId == stops[i + 1]).Day - 1).AddHours(6 + r.Next(12));
            var type = i == 0 && crossBorder ? "Flight" : tour.TourType == "Cruise" ? "Ferry" : r.NextDouble() < 0.5 ? "Bus" : "Train";
            offer.Transfers.Add(new OfferTransfer
            {
                Type = type, Carrier = $"{type} Line {r.Next(1, 21)}", FromCityId = stops[i], ToCityId = stops[i + 1],
                DepartAt = depart, ArriveAt = depart.AddMinutes(60 + r.Next(480))
            });
        }

        var seat = 0;
        for (var k = 0; seat < s.Booked; k++)
        {
            var size = Math.Min(s.Booked - seat, r.Next(1, 5));
            offer.Bookings.Add(new GenBooking(id * 64 + k,
                Enumerable.Range(seat, size).Select(n => (int)(((long)id * 40 + n) % TouristCount) + 1).ToArray()));
            seat += size;
        }

        if (s.Status == "Closed" && offer.EndDate < Now)
        {
            var travelers = offer.Bookings.SelectMany(b => b.TouristIds).ToArray();
            var count = Math.Min(travelers.Length, r.Next(6));
            for (var i = 0; i < count; i++)
                offer.AllReviews.Add(new OfferReview
                {
                    TouristId = travelers[i], Rating = 5 - (int)(Math.Pow(r.NextDouble(), 2) * 3),
                    Comment = Comments[r.Next(Comments.Length)], CreatedAt = offer.EndDate.AddDays(i + 1).AddMinutes(r.Next(1440))
                });
            offer.Rating = new OfferRating
            {
                Count = count, Avg = count == 0 ? null : Math.Round(offer.AllReviews.Average(x => (double)x.Rating), 2)
            };
            offer.Reviews = offer.AllReviews.OrderByDescending(x => x.CreatedAt).Take(3).ToList();
        }
        return offer;
    }

    private static Dictionary<string, object> Attributes(Random r, string type) => type switch
    {
        "Beach" => new() { ["seaDistanceM"] = (double)(50 + r.Next(40) * 50), ["allInclusive"] = r.NextDouble() < 0.5, ["poolCount"] = (double)r.Next(0, 5) },
        "Ski" => new() { ["liftPassIncluded"] = r.NextDouble() < 0.6, ["slopeKm"] = (double)r.Next(20, 601), ["skiRental"] = r.NextDouble() < 0.5 },
        "Cruise" => new() { ["ship"] = Ships[r.Next(Ships.Length)], ["cabinClass"] = Cabins[r.Next(Cabins.Length)], ["portsCount"] = (double)r.Next(3, 11) },
        _ => new() { ["walkingKmPerDay"] = (double)r.Next(2, 16), ["ticketsIncluded"] = r.NextDouble() < 0.5, ["groupMax"] = (double)r.Next(10, 31) }
    };

    public static decimal TypicalPrice(Random r, int countryId) =>
        Math.Round(7 * 2_500m * (0.7m + countryId % 7 * 0.12m) * (0.8m + (decimal)r.NextDouble() * 0.7m) / 100m) * 100m;

    private static int Pick(Random r, double[] weights)
    {
        var x = r.NextDouble();
        for (var i = 0; i < weights.Length; i++)
        {
            if (x < weights[i]) return i;
            x -= weights[i];
        }
        return weights.Length - 1;
    }
}
