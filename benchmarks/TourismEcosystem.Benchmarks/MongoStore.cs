using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace TourismEcosystem.Benchmarks;

public sealed class MongoStore
{
    private readonly IMongoDatabase _db;
    private readonly IMongoCollection<Offer> _offers;
    private readonly IMongoCollection<Offer> _offersJournaled;
    private readonly IMongoCollection<BsonDocument> _bookingsJournaled;

    static MongoStore()
    {
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        ConventionRegistry.Register("camel",
            new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true) }, _ => true);
    }

    public MongoStore(string connectionString, string database = "tourism")
    {
        _db = new MongoClient(connectionString).GetDatabase(database);
        _offers = _db.GetCollection<Offer>("offers");
        var journaled = new WriteConcern(1, journal: true);
        _offersJournaled = _offers.WithWriteConcern(journaled);
        _bookingsJournaled = _db.GetCollection<BsonDocument>("bookings").WithWriteConcern(journaled);
    }

    public async Task ResetAsync()
    {
        await _db.DropCollectionAsync("offers");
        await _db.DropCollectionAsync("bookings");
    }

    public Task LoadOffersAsync(IReadOnlyList<Offer> offers) =>
        _offers.InsertManyAsync(offers, new InsertManyOptions { IsOrdered = false });

    public Task CreateIndexesAsync()
    {
        var k = Builders<Offer>.IndexKeys;
        return _offers.Indexes.CreateManyAsync(
        [
            new(k.Ascending(x => x.CountryId).Ascending(x => x.Status).Ascending(x => x.Price).Ascending(x => x.Id)
                 .Ascending(x => x.StartDate).Ascending(x => x.AvailableSeats), new CreateIndexOptions { Name = "ix_search" }),
            new(k.Ascending("itinerary.cityId").Ascending(x => x.Status).Ascending(x => x.Price).Ascending(x => x.Id),
                new CreateIndexOptions { Name = "ix_route" }),
            new(k.Ascending(x => x.TourType).Ascending(x => x.Status).Ascending(x => x.Price).Ascending(x => x.Id),
                new CreateIndexOptions { Name = "ix_type_price" }),
            new(k.Wildcard("attributes"), new CreateIndexOptions { Name = "ix_attributes" }),
            new(k.Ascending(x => x.TourType).Ascending(x => x.Status).Ascending(x => x.StartDate).Ascending(x => x.CountryId)
                 .Ascending(x => x.Price), new CreateIndexOptions { Name = "ix_report" }),
            new(k.Ascending("itinerary.hotel._id"), new CreateIndexOptions { Name = "ix_hotel" }),
            new(k.Ascending(x => x.TourId), new CreateIndexOptions { Name = "ix_tour" })
        ]);
    }

    private static readonly FilterDefinitionBuilder<Offer> F = Builders<Offer>.Filter;
    private static readonly SortDefinition<Offer> ByPrice = Builders<Offer>.Sort.Ascending(x => x.Price).Ascending(x => x.Id);
    private static readonly ProjectionDefinition<Offer> Summary = Builders<Offer>.Projection
        .Include(x => x.Title).Include(x => x.Price).Include(x => x.StartDate).Include(x => x.Country).Include(x => x.AvailableSeats);
    private static readonly FilterDefinition<Offer> Open = F.Eq(x => x.Status, "Open");

    public async Task<Offer?> GetOfferAsync(int id) =>
        await _offers.Find(F.Eq(x => x.Id, id)).FirstOrDefaultAsync();

    private static FilterDefinition<Offer> SearchFilter(Args a) =>
        F.Eq(x => x.CountryId, a.CountryId) & Open & F.Gte(x => x.StartDate, a.From) & F.Lte(x => x.StartDate, a.To)
        & F.Lte(x => x.Price, a.MaxPrice) & F.Gte(x => x.AvailableSeats, a.Seats);

    public Task<long> SearchAsync(Args a) => SummaryAsync(SearchFilter(a), skip: 0);

    public Task<long> DeepPageAsync(Args a) => SummaryAsync(
        F.Eq(x => x.CountryId, a.CountryId) & Open & F.Gte(x => x.StartDate, a.From) & F.Lte(x => x.StartDate, a.WideTo),
        skip: Args.DeepPageOffset);

    public Task<long> RouteAsync(Args a) => SummaryAsync(
        Open & F.ElemMatch<BsonDocument>("itinerary",
            new BsonDocument { { "cityId", a.CityId }, { "hotel.stars", new BsonDocument("$gte", 4) } }), skip: 0);

    public Task<long> AttributesAsync(Args a) => SummaryAsync(
        F.Eq(x => x.TourType, "Beach") & Open
        & F.Lte("attributes.seaDistanceM", (double)a.SeaDistance) & F.Eq("attributes.allInclusive", true), skip: 0);

    public async Task<long> ReportAsync(Args a)
    {
        var rows = await _offers.Aggregate()
            .Match(F.Eq(x => x.TourType, a.TourType) & Open & F.Gte(x => x.StartDate, DataGenerator.Now))
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument { { "c", "$countryId" }, { "m", new BsonDocument("$month", "$startDate") } } },
                { "cnt", new BsonDocument("$sum", 1) },
                { "avgPrice", new BsonDocument("$avg", "$price") }
            })
            .ToListAsync();
        return rows.Sum(r => (r["_id"]["c"].AsInt32 * 100L + r["_id"]["m"].AsInt32) * r["cnt"].AsInt32);
    }

    public async Task<long> InsertOfferAsync(Offer offer)
    {
        await _offersJournaled.InsertOneAsync(offer);
        return offer.Id;
    }

    public async Task<long> BookAsync(int departureId, int[] touristIds)
    {
        var updated = await _offersJournaled.FindOneAndUpdateAsync(
            F.Eq(x => x.Id, departureId) & Open & F.Gte(x => x.AvailableSeats, touristIds.Length),
            Builders<Offer>.Update.Inc(x => x.AvailableSeats, -touristIds.Length),
            new FindOneAndUpdateOptions<Offer> { Projection = Builders<Offer>.Projection.Include(x => x.Id) });
        if (updated is null) return 0;
        await _bookingsJournaled.InsertOneAsync(new BsonDocument
        {
            { "departureId", departureId }, { "touristIds", new BsonArray(touristIds) },
            { "seats", touristIds.Length }, { "createdAt", DateTime.UtcNow }
        });
        return departureId;
    }

    public Task<long> BookAsync(Args a) => BookAsync(a.BookDepartureId, a.TouristIds);

    public async Task<long> RenameHotelAsync(Args a)
    {
        var r = await _offersJournaled.UpdateManyAsync(F.Eq("itinerary.hotel._id", a.HotelId),
            Builders<Offer>.Update.Set("itinerary.$[x].hotel.name", a.HotelNewName),
            new UpdateOptions { ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("x.hotel._id", a.HotelId))] });
        return r.ModifiedCount;
    }

    public async Task<long> UpdateTourPriceAsync(Args a)
    {
        var update = new BsonDocument("$set", new BsonDocument
        {
            { "priceHistory", new BsonDocument("$concatArrays", new BsonArray
                {
                    "$priceHistory",
                    new BsonArray { new BsonDocument { { "oldPrice", "$price" }, { "newPrice", new Decimal128(a.NewPrice) }, { "changedAt", DateTime.UtcNow } } }
                }) },
            { "price", new Decimal128(a.NewPrice) }
        });
        var r = await _offersJournaled.UpdateManyAsync(F.Eq(x => x.TourId, a.TourId), new PipelineUpdateDefinition<Offer>(new[] { update }));
        return r.ModifiedCount;
    }

    public async Task<(long Overbooked, long BookedSeats)> SeatsAsync(IEnumerable<int> departureIds)
    {
        var docs = await _offers.Find(F.In(x => x.Id, departureIds))
            .Project<Offer>(Builders<Offer>.Projection.Include(x => x.Capacity).Include(x => x.AvailableSeats)).ToListAsync();
        return (docs.Count(d => d.AvailableSeats < 0), docs.Sum(d => (long)(d.Capacity - d.AvailableSeats)));
    }

    private async Task<long> SummaryAsync(FilterDefinition<Offer> filter, int skip)
    {
        var docs = await _offers.Find(filter).Sort(ByPrice).Skip(skip).Limit(20).Project<Offer>(Summary).ToListAsync();
        return Args.ListChecksum(docs.Select(d => d.Id));
    }

    public async Task<(long Keys, long Docs, long Ms, string Plan)> ExplainSearchAsync(Args a)
    {
        var filter = SearchFilter(a).Render(new RenderArgs<Offer>(_offers.DocumentSerializer, _offers.Settings.SerializerRegistry));
        var explain = await _db.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "explain", new BsonDocument
                {
                    { "find", "offers" }, { "filter", filter },
                    { "sort", new BsonDocument { { "price", 1 }, { "_id", 1 } } }, { "limit", 20 }
                } },
            { "verbosity", "executionStats" }
        });
        var s = explain["executionStats"];
        var plan = explain["queryPlanner"]["winningPlan"].ToString() ?? "";
        var index = System.Text.RegularExpressions.Regex.Match(plan, "\"indexName\" : \"([^\"]+)\"").Groups[1].Value;
        return (s["totalKeysExamined"].ToInt64(), s["totalDocsExamined"].ToInt64(), s["executionTimeMillis"].ToInt64(), index);
    }

    public async Task<(long DataKb, long IndexKb)> SizeAsync()
    {
        await _db.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("fsync", 1));
        var s = await _db.RunCommandAsync<BsonDocument>(new BsonDocument("collStats", "offers"));
        return (s["storageSize"].ToInt64() / 1024, s["totalIndexSize"].ToInt64() / 1024);
    }
}
