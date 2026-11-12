using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace TourismEcosystem.Benchmarks;

public sealed class SqlStore(string connectionString)
{
    private const int UserId = 3;

    private async Task<SqlConnection> OpenAsync()
    {
        var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        return c;
    }

    public async Task LoadDimensionsAsync(DataGenerator gen)
    {
        await using (var c = await OpenAsync())
            await c.ExecuteAsync("INSERT dbo.Roles (Name) VALUES (N'Admin'), (N'Manager'), (N'Agent');");

        await BulkAsync("dbo.Users", [("UserId", typeof(int)), ("RoleId", typeof(int)), ("Email", typeof(string)), ("FullName", typeof(string))],
            [[1, 1, "admin@bench.example", "Administrator"], [2, 2, "manager@bench.example", "Manager"], [3, 3, "agent@bench.example", "Agent"]]);

        await BulkAsync("dbo.Countries", [("CountryId", typeof(int)), ("Name", typeof(string)), ("IsoCode", typeof(string))],
            DataGenerator.Countries.Select((c, i) => new object?[] { i + 1, c.Name, c.Iso }));

        await BulkAsync("dbo.Cities", [("CityId", typeof(int)), ("CountryId", typeof(int)), ("Name", typeof(string))],
            Enumerable.Range(1, DataGenerator.CityCount).Select(c => new object?[] { c, DataGenerator.CountryOf(c), DataGenerator.CityName(c) }));

        await BulkAsync("dbo.Guides", [("GuideId", typeof(int)), ("FullName", typeof(string)), ("Phone", typeof(string)), ("Languages", typeof(string)), ("ExperienceYears", typeof(byte))],
            Enumerable.Range(1, DataGenerator.GuideCount).Select(g => new object?[]
                { g, DataGenerator.GuideName(g), DataGenerator.GuidePhone(g), DataGenerator.GuideLanguages(g), (byte)(g % 20) }));

        await BulkAsync("dbo.Hotels",
            [("HotelId", typeof(int)), ("CityId", typeof(int)), ("Name", typeof(string)), ("Stars", typeof(byte)), ("Address", typeof(string)), ("CreatedBy", typeof(int))],
            Enumerable.Range(1, DataGenerator.HotelCount).Select(h => new object?[]
                { h, DataGenerator.CityOfHotel(h), DataGenerator.HotelName(h), (byte)DataGenerator.HotelStars(h), $"вул. Центральна, {h}", 2 }));

        await BulkAsync("dbo.Excursions",
            [("ExcursionId", typeof(int)), ("CityId", typeof(int)), ("GuideId", typeof(int)), ("Name", typeof(string)), ("Price", typeof(decimal)), ("DurationHours", typeof(decimal))],
            DataGenerator.Excursions.Select(e => new object?[] { e.Id, e.CityId, e.GuideId, e.Name, e.Price, e.DurationHours }));

        await BulkAsync("dbo.Tourists", [("TouristId", typeof(int)), ("FullName", typeof(string)), ("BirthDate", typeof(DateTime)), ("CreatedBy", typeof(int))],
            Enumerable.Range(1, gen.TouristCount).Select(t => new object?[] { t, DataGenerator.TouristName(t), DataGenerator.TouristBirthDate(t), 2 }));
    }

    public async Task LoadOffersAsync(IReadOnlyList<Offer> offers)
    {
        var tours = offers.GroupBy(o => o.TourId).Select(g => g.First()).ToList();

        await BulkAsync("dbo.Tours",
            [("TourId", typeof(int)), ("CountryId", typeof(int)), ("Title", typeof(string)), ("Description", typeof(string)), ("TourType", typeof(string)),
             ("DurationDays", typeof(byte)), ("BasePrice", typeof(decimal)), ("Currency", typeof(string)), ("CreatedBy", typeof(int))],
            tours.Select(t => new object?[] { t.TourId, t.CountryId, t.Title, t.Description, t.TourType, (byte)t.DurationDays, t.Price, t.Currency, 2 }));

        await BulkAsync("dbo.TourItinerary",
            [("TourId", typeof(int)), ("DayNo", typeof(byte)), ("CityId", typeof(int)), ("HotelId", typeof(int)), ("Description", typeof(string))],
            tours.SelectMany(t => t.Itinerary.Select(d => new object?[] { t.TourId, (byte)d.Day, d.CityId, d.Hotel?.Id, d.Description })), keepIdentity: false);

        await BulkAsync("dbo.TourAttributes",
            [("TourId", typeof(int)), ("AttrKey", typeof(string)), ("NumValue", typeof(decimal)), ("TextValue", typeof(string))],
            tours.SelectMany(t => t.Attributes.Select(kv => AttributeRow(t.TourId, kv.Key, kv.Value))));

        await BulkAsync("dbo.TourPriceHistory",
            [("TourId", typeof(int)), ("OldPrice", typeof(decimal)), ("NewPrice", typeof(decimal)), ("ChangedAt", typeof(DateTime)), ("ChangedBy", typeof(int))],
            tours.SelectMany(t => t.PriceHistory.Select(h => new object?[] { t.TourId, h.OldPrice, h.NewPrice, h.ChangedAt, 2 })), keepIdentity: false);

        await BulkAsync("dbo.TourDepartures",
            [("DepartureId", typeof(int)), ("TourId", typeof(int)), ("GuideId", typeof(int)), ("StartDate", typeof(DateTime)), ("EndDate", typeof(DateTime)),
             ("Capacity", typeof(short)), ("Status", typeof(string)), ("CreatedBy", typeof(int))],
            offers.Select(o => new object?[] { o.Id, o.TourId, o.Guide?.Id, o.StartDate, o.EndDate, (short)o.Capacity, o.Status, 2 }));

        await BulkAsync("dbo.Transfers",
            [("DepartureId", typeof(int)), ("TransportType", typeof(string)), ("Carrier", typeof(string)), ("FromCityId", typeof(int)), ("ToCityId", typeof(int)),
             ("DepartAt", typeof(DateTime)), ("ArriveAt", typeof(DateTime))],
            offers.SelectMany(o => o.Transfers.Select(x => new object?[] { o.Id, x.Type, x.Carrier, x.FromCityId, x.ToCityId, x.DepartAt, x.ArriveAt })), keepIdentity: false);

        await BulkAsync("dbo.Bookings",
            [("BookingId", typeof(int)), ("DepartureId", typeof(int)), ("LeadTouristId", typeof(int)), ("TotalAmount", typeof(decimal)), ("Status", typeof(string)),
             ("BookedAt", typeof(DateTime)), ("CreatedBy", typeof(int))],
            offers.SelectMany(o => o.Bookings.Select(b => new object?[]
                { b.BookingId, o.Id, b.TouristIds[0], o.Price * b.TouristIds.Length, "Confirmed", o.StartDate.AddDays(-30), UserId })));

        await BulkAsync("dbo.BookingTravelers", [("BookingId", typeof(int)), ("TouristId", typeof(int)), ("Price", typeof(decimal))],
            offers.SelectMany(o => o.Bookings.SelectMany(b => b.TouristIds.Select(t => new object?[] { b.BookingId, t, o.Price }))));

        await BulkAsync("dbo.Reviews",
            [("DepartureId", typeof(int)), ("TouristId", typeof(int)), ("Rating", typeof(byte)), ("Comment", typeof(string)), ("CreatedAt", typeof(DateTime))],
            offers.SelectMany(o => o.AllReviews.Select(r => new object?[] { o.Id, r.TouristId, (byte)r.Rating, r.Comment, r.CreatedAt })), keepIdentity: false);
    }

    public async Task FinishLoadAsync()
    {
        await using var c = await OpenAsync();
        const string sql = """
            DECLARE @s nvarchar(max) = N'';
            SELECT @s += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' WITH CHECK CHECK CONSTRAINT ALL;'
            FROM sys.tables;
            EXEC (@s);
            EXEC sp_updatestats;
            CHECKPOINT;
            """;
        await c.ExecuteAsync(sql, commandTimeout: 0);
    }

    private async Task BulkAsync(string table, (string Name, Type Type)[] columns, IEnumerable<object?[]> rows, bool keepIdentity = true)
    {
        using var dt = new DataTable();
        foreach (var (name, type) in columns) dt.Columns.Add(name, type);
        foreach (var row in rows) dt.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());

        var options = SqlBulkCopyOptions.TableLock | (keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default);
        using var bulk = new SqlBulkCopy(connectionString, options) { DestinationTableName = table, BulkCopyTimeout = 0 };
        foreach (var (name, _) in columns) bulk.ColumnMappings.Add(name, name);
        await bulk.WriteToServerAsync(dt);
    }

    private static object?[] AttributeRow(int tourId, string key, object value) => value switch
    {
        bool b => [tourId, key, b ? 1m : 0m, null],
        double d => [tourId, key, (decimal)d, null],
        _ => [tourId, key, null, value.ToString()]
    };

    public async Task<Offer?> GetOfferAsync(int id)
    {
        const string sql = """
            DECLARE @tid int = (SELECT TourId FROM dbo.TourDepartures WHERE DepartureId = @id);
            SELECT * FROM dbo.vw_OfferCards WHERE DepartureId = @id;
            SELECT i.DayNo AS Day, i.CityId, c.Name AS City, i.Description, i.HotelId, h.Name AS HotelName, h.Stars
            FROM dbo.TourItinerary i JOIN dbo.Cities c ON c.CityId = i.CityId LEFT JOIN dbo.Hotels h ON h.HotelId = i.HotelId
            WHERE i.TourId = @tid ORDER BY i.DayNo;
            SELECT TransportType AS Type, Carrier, FromCityId, ToCityId, DepartAt, ArriveAt
            FROM dbo.Transfers WHERE DepartureId = @id ORDER BY DepartAt;
            SELECT DISTINCT e.ExcursionId AS Id, e.Name, e.CityId, e.Price
            FROM dbo.TourItinerary i JOIN dbo.Excursions e ON e.CityId = i.CityId
            WHERE i.TourId = @tid AND e.IsDeleted = 0 ORDER BY e.ExcursionId;
            SELECT OldPrice, NewPrice, ChangedAt FROM dbo.TourPriceHistory WHERE TourId = @tid ORDER BY ChangedAt;
            SELECT AttrKey, NumValue, TextValue FROM dbo.TourAttributes WHERE TourId = @tid;
            SELECT TOP (3) TouristId, Rating, Comment, CreatedAt FROM dbo.Reviews WHERE DepartureId = @id ORDER BY CreatedAt DESC;
            """;
        await using var c = await OpenAsync();
        using var grid = await c.QueryMultipleAsync(sql, new { id });
        var h = await grid.ReadSingleOrDefaultAsync<OfferRow>();
        if (h is null) return null;

        var offer = h.ToOffer();
        offer.Itinerary = (await grid.ReadAsync<DayRow>()).Select(d => d.ToDay()).ToList();
        offer.Transfers = (await grid.ReadAsync<OfferTransfer>()).ToList();
        offer.Excursions = (await grid.ReadAsync<OfferExcursion>()).ToList();
        offer.PriceHistory = (await grid.ReadAsync<OfferPriceChange>()).ToList();
        offer.Attributes = (await grid.ReadAsync<(string Key, decimal? Num, string? Text)>())
            .ToDictionary(a => a.Key, a => (object)(a.Text ?? (object)(double)a.Num!.Value));
        offer.Reviews = (await grid.ReadAsync<(int TouristId, byte Rating, string? Comment, DateTime CreatedAt)>())
            .Select(r => new OfferReview { TouristId = r.TouristId, Rating = r.Rating, Comment = r.Comment, CreatedAt = r.CreatedAt }).ToList();
        return offer;
    }

    private const string SummarySelect = """
        SELECT TOP (@Take) d.DepartureId, t.Title, t.BasePrice, d.StartDate, c.Name AS Country, s.Available
        FROM dbo.TourDepartures d
        JOIN dbo.Tours t     ON t.TourId = d.TourId
        JOIN dbo.Countries c ON c.CountryId = t.CountryId
        CROSS APPLY (SELECT d.Capacity - COUNT(*) AS Available
                     FROM dbo.Bookings b JOIN dbo.BookingTravelers bt ON bt.BookingId = b.BookingId
                     WHERE b.DepartureId = d.DepartureId AND b.Status <> N'Cancelled') s
        WHERE t.IsDeleted = 0 AND d.Status = N'Open'
        """;

    public const string SearchFilter = """
         AND t.CountryId = @CountryId AND d.StartDate BETWEEN CAST(@From AS date) AND CAST(@To AS date)
         AND t.BasePrice <= @MaxPrice AND s.Available >= @Seats
        ORDER BY t.BasePrice, d.DepartureId
        """;

    public Task<long> SearchAsync(Args a) =>
        SummaryAsync(SummarySelect + SearchFilter, new { Take = 20, a.CountryId, a.From, a.To, a.MaxPrice, a.Seats });

    public Task<long> DeepPageAsync(Args a) => SummaryAsync(SummarySelect.Replace("TOP (@Take) ", "") + """
         AND t.CountryId = @CountryId AND d.StartDate BETWEEN CAST(@From AS date) AND CAST(@WideTo AS date)
        ORDER BY t.BasePrice, d.DepartureId
        OFFSET @Skip ROWS FETCH NEXT 20 ROWS ONLY
        """, new { a.CountryId, a.From, a.WideTo, Skip = Args.DeepPageOffset });

    public Task<long> RouteAsync(Args a) => SummaryAsync(SummarySelect + """
         AND EXISTS (SELECT 1 FROM dbo.TourItinerary i JOIN dbo.Hotels h ON h.HotelId = i.HotelId
                     WHERE i.TourId = t.TourId AND i.CityId = @CityId AND h.Stars >= 4)
        ORDER BY t.BasePrice, d.DepartureId
        """, new { Take = 20, a.CityId });

    public Task<long> AttributesAsync(Args a) => SummaryAsync(SummarySelect + """
         AND t.TourType = 'Beach'
         AND EXISTS (SELECT 1 FROM dbo.TourAttributes x WHERE x.TourId = t.TourId AND x.AttrKey = 'seaDistanceM' AND x.NumValue <= @SeaDistance)
         AND EXISTS (SELECT 1 FROM dbo.TourAttributes x WHERE x.TourId = t.TourId AND x.AttrKey = 'allInclusive' AND x.NumValue = 1)
        ORDER BY t.BasePrice, d.DepartureId
        """, new { Take = 20, SeaDistance = (decimal)a.SeaDistance });

    public async Task<long> ReportAsync(Args a)
    {
        const string sql = """
            SELECT t.CountryId, MONTH(d.StartDate) AS M, COUNT(*) AS Cnt, AVG(t.BasePrice) AS AvgPrice
            FROM dbo.TourDepartures d
            JOIN dbo.Tours t ON t.TourId = d.TourId
            WHERE t.IsDeleted = 0 AND d.Status = N'Open' AND t.TourType = @TourType AND d.StartDate >= CAST(@Now AS date)
            GROUP BY t.CountryId, MONTH(d.StartDate)
            """;
        await using var c = await OpenAsync();
        var rows = await c.QueryAsync<(int CountryId, int M, int Cnt, decimal AvgPrice)>(sql, new { a.TourType, DataGenerator.Now });
        return rows.Sum(r => (r.CountryId * 100L + r.M) * r.Cnt);
    }

    public async Task<long> InsertOfferAsync(Offer x)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<int>("dbo.sp_OfferCard_Create", new
        {
            x.CountryId, x.Title, x.Description, x.TourType, DurationDays = (byte)x.DurationDays, BasePrice = x.Price, x.Currency,
            GuideId = x.Guide?.Id, x.StartDate, Capacity = (short)x.Capacity,
            Itinerary = JsonSerializer.Serialize(x.Itinerary.Select(d => new { DayNo = d.Day, d.CityId, HotelId = d.Hotel?.Id, d.Description })),
            Attributes = JsonSerializer.Serialize(x.Attributes),
            PriceHistory = JsonSerializer.Serialize(x.PriceHistory),
            Transfers = JsonSerializer.Serialize(x.Transfers.Select(t => new { TransportType = t.Type, t.Carrier, t.FromCityId, t.ToCityId, t.DepartAt, t.ArriveAt })),
            UserId = 2
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task<long> BookAsync(int departureId, int[] touristIds)
    {
        await using var c = await OpenAsync();
        await using var tx = c.BeginTransaction();
        try
        {
            var bookingId = await c.ExecuteScalarAsync<int>("dbo.sp_Booking_Create",
                new { DepartureId = departureId, LeadTouristId = touristIds[0], UserId }, tx, commandType: CommandType.StoredProcedure);
            foreach (var t in touristIds.Skip(1))
                await c.ExecuteAsync("dbo.sp_Booking_AddTraveler",
                    new { BookingId = bookingId, TouristId = t, UserId }, tx, commandType: CommandType.StoredProcedure);
            await tx.CommitAsync();
            return departureId;
        }
        catch (SqlException e) when (e.Number is 50409 or 1205)
        {
            if (tx.Connection is not null) await tx.RollbackAsync();
            return 0;
        }
    }

    public Task<long> BookAsync(Args a) => BookAsync(a.BookDepartureId, a.TouristIds);

    public async Task<long> RenameHotelAsync(Args a)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteAsync("UPDATE dbo.Hotels SET Name = @Name, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = 2 WHERE HotelId = @HotelId",
            new { a.HotelId, Name = a.HotelNewName });
    }

    public async Task<long> UpdateTourPriceAsync(Args a)
    {
        await using var c = await OpenAsync();
        await c.ExecuteAsync("dbo.sp_Tour_UpdatePrice", new { a.TourId, a.NewPrice, RowVersion = (byte[]?)null, UserId = 2 },
            commandType: CommandType.StoredProcedure);
        return a.TourId;
    }

    public async Task<int> OverbookedAsync(IEnumerable<int> departureIds)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.TourDepartures WHERE DepartureId IN @ids AND dbo.fn_GetAvailableSeats(DepartureId) < 0",
            new { ids = departureIds.ToArray() });
    }

    public async Task<int> BookedSeatsAsync(IEnumerable<int> departureIds)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<int>(
            "SELECT SUM(Capacity - dbo.fn_GetAvailableSeats(DepartureId)) FROM dbo.TourDepartures WHERE DepartureId IN @ids",
            new { ids = departureIds.ToArray() });
    }

    public async Task CreateSearchViewsAsync(string scriptPath)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        await using var c = await OpenAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline))
            if (!string.IsNullOrWhiteSpace(batch))
                await c.ExecuteAsync(batch, commandTimeout: 0);
    }

    private const string ViewSummarySelect = """
        SELECT TOP (@Take) s.DepartureId, t.Title, s.BasePrice, s.StartDate, c.Name AS Country, s.Capacity - ISNULL(bs.BookedSeats, 0) AS Available
        FROM dbo.vw_OfferSearch s WITH (NOEXPAND)
        JOIN dbo.Tours t     ON t.TourId = s.TourId
        JOIN dbo.Countries c ON c.CountryId = s.CountryId
        LEFT JOIN dbo.vw_DepartureSeats bs WITH (NOEXPAND) ON bs.DepartureId = s.DepartureId
        WHERE s.Status = N'Open' AND s.CountryId = @CountryId
        """;

    public const string ViewSearchFilter = """
         AND s.StartDate BETWEEN CAST(@From AS date) AND CAST(@To AS date)
         AND s.BasePrice <= @MaxPrice AND s.Capacity - ISNULL(bs.BookedSeats, 0) >= @Seats
        ORDER BY s.BasePrice, s.DepartureId
        """;

    public Task<long> SearchViewAsync(Args a) =>
        SummaryAsync(ViewSummarySelect + ViewSearchFilter, new { Take = 20, a.CountryId, a.From, a.To, a.MaxPrice, a.Seats });

    public Task<long> DeepPageViewAsync(Args a) => SummaryAsync(ViewSummarySelect.Replace("TOP (@Take) ", "") + """
         AND s.StartDate BETWEEN CAST(@From AS date) AND CAST(@WideTo AS date)
        ORDER BY s.BasePrice, s.DepartureId
        OFFSET @Skip ROWS FETCH NEXT 20 ROWS ONLY
        """, new { a.CountryId, a.From, a.WideTo, Skip = Args.DeepPageOffset });

    private async Task<long> SummaryAsync(string sql, object args)
    {
        await using var c = await OpenAsync();
        var ids = await c.QueryAsync<int>(sql, args);
        return Args.ListChecksum(ids);
    }

    public async Task<(long LogicalReads, double Ms)> ExplainSearchAsync(Args a, bool view)
    {
        await using var c = await OpenAsync();
        long reads = 0;
        c.InfoMessage += (_, e) =>
        {
            foreach (Match m in Regex.Matches(e.Message, @"logical reads (\d+)")) reads += long.Parse(m.Groups[1].Value);
        };
        await c.ExecuteAsync("SET STATISTICS IO ON");
        var sw = Stopwatch.StartNew();
        await c.QueryAsync<int>(view ? ViewSummarySelect + ViewSearchFilter : SummarySelect + SearchFilter,
            new { Take = 20, a.CountryId, a.From, a.To, a.MaxPrice, a.Seats });
        return (reads, sw.Elapsed.TotalMilliseconds);
    }

    public async Task<(long DataKb, long IndexKb)> SizeAsync()
    {
        const string sql = """
            SELECT SUM(CASE WHEN i.index_id <= 1 THEN a.used_pages ELSE 0 END) * 8 AS DataKb,
                   SUM(CASE WHEN i.index_id > 1  THEN a.used_pages ELSE 0 END) * 8 AS IndexKb
            FROM sys.indexes i
            JOIN sys.partitions p       ON p.object_id = i.object_id AND p.index_id = i.index_id
            JOIN sys.allocation_units a ON a.container_id = p.partition_id
            WHERE OBJECT_NAME(i.object_id) IN ('TourDepartures','Tours','Countries','Cities','TourItinerary','Hotels','Guides',
                  'Transfers','Excursions','TourPriceHistory','TourAttributes','Reviews','Bookings','BookingTravelers','Tourists')
            """;
        await using var c = await OpenAsync();
        return await c.QuerySingleAsync<(long, long)>(sql);
    }

    public async Task<long> RowCountAsync()
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<long>("""
            SELECT SUM(p.rows) FROM sys.partitions p
            WHERE p.index_id <= 1 AND OBJECT_NAME(p.object_id) IN ('TourDepartures','Tours','Countries','Cities','TourItinerary','Hotels','Guides',
                  'Transfers','Excursions','TourPriceHistory','TourAttributes','Reviews','Bookings','BookingTravelers','Tourists')
            """);
    }

    private sealed class OfferRow
    {
        public int DepartureId { get; init; }
        public int TourId { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public string TourType { get; init; } = "";
        public int CountryId { get; init; }
        public string Country { get; init; } = "";
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public byte DurationDays { get; init; }
        public decimal Price { get; init; }
        public string Currency { get; init; } = "";
        public string Status { get; init; } = "";
        public short Capacity { get; init; }
        public int AvailableSeats { get; init; }
        public int? GuideId { get; init; }
        public string? GuideName { get; init; }
        public string? GuideLanguages { get; init; }
        public int ReviewsCount { get; init; }
        public decimal? AvgRating { get; init; }

        public Offer ToOffer() => new()
        {
            Id = DepartureId, TourId = TourId, Title = Title, Description = Description, TourType = TourType,
            CountryId = CountryId, Country = Country, StartDate = StartDate, EndDate = EndDate, DurationDays = DurationDays,
            Price = Price, Currency = Currency, Status = Status, Capacity = Capacity, AvailableSeats = AvailableSeats,
            Guide = GuideId is { } g ? new OfferGuide { Id = g, Name = GuideName ?? "", Languages = GuideLanguages ?? "" } : null,
            Rating = new OfferRating { Count = ReviewsCount, Avg = (double?)AvgRating }
        };
    }

    private sealed class DayRow
    {
        public byte Day { get; init; }
        public int CityId { get; init; }
        public string City { get; init; } = "";
        public string Description { get; init; } = "";
        public int? HotelId { get; init; }
        public string? HotelName { get; init; }
        public byte? Stars { get; init; }

        public OfferDay ToDay() => new()
        {
            Day = Day, CityId = CityId, City = City, Description = Description,
            Hotel = HotelId is { } h ? new OfferHotel { Id = h, Name = HotelName ?? "", Stars = Stars ?? 0 } : null
        };
    }
}
