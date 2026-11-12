using System.Diagnostics;
using System.Text;

namespace TourismEcosystem.Benchmarks;

public sealed record Args(
    int DepartureId, int CountryId, DateTime From, DateTime To, DateTime WideTo, decimal MaxPrice, int Seats,
    int CityId, int SeaDistance, string TourType, int BookDepartureId, int[] TouristIds,
    int HotelId, string HotelNewName, int TourId, decimal NewPrice)
{
    public const int DeepPageOffset = 500;

    public static Args Next(Random r, DataGenerator gen)
    {
        var countryId = DataGenerator.SkewedCountry(r);
        var from = DataGenerator.Now.AddDays(r.Next(0, 300));
        int book;
        do book = r.Next(1, gen.Departures + 1); while (!gen.IsBookable(book));
        var tourists = new HashSet<int>();
        var travelers = r.Next(1, 4);
        while (tourists.Count < travelers) tourists.Add(r.Next(1, gen.TouristCount + 1));

        return new Args(
            r.Next(1, gen.Departures + 1), countryId, from, from.AddDays(30), from.AddDays(120),
            DataGenerator.TypicalPrice(r, countryId), r.Next(1, 5),
            (countryId - 1) * DataGenerator.CitiesPerCountry + r.Next(DataGenerator.CitiesPerCountry) + 1,
            100 + r.Next(11) * 50, DataGenerator.TourTypes[r.Next(DataGenerator.TourTypes.Length)],
            book, [.. tourists],
            r.Next(1, DataGenerator.HotelCount + 1), $"Hotel {r.Next(1_000_000)}",
            r.Next(1, gen.TourCount + 1), r.Next(50, 1_000) * 100m);
    }

    public static long ListChecksum(IEnumerable<int> ids)
    {
        long sum = 0, pos = 1;
        foreach (var id in ids) sum += id * pos++;
        return sum;
    }
}

public sealed record Result(string Scenario, string Engine, string Mode, int Count, double MedianMs, double P95Ms, double MeanMs, double OpsPerSec);

public sealed class Bench(SqlStore sql, MongoStore mongo, DataGenerator gen, string indexedViewScript)
{
    private const int Seed = 2026;
    private readonly List<Result> _results = [];
    private readonly List<string> _checks = [];

    private sealed record Scenario(string Id, string Title, int Warmup, int Iterations,
        Func<Args, Task<long>> Sql, Func<Args, Task<long>> Mongo, bool Verify = true);

    public async Task RunAsync()
    {
        Scenario[] reads =
        [
            new("S1", "Offer card by Id", 50, 500,
                async a => (await sql.GetOfferAsync(a.DepartureId))?.Checksum() ?? 0,
                async a => (await mongo.GetOfferAsync(a.DepartureId))?.Checksum() ?? 0),
            new("S2", "Search: country+dates+price+seats, top 20", 50, 500, sql.SearchAsync, mongo.SearchAsync),
            new("S3", $"Deep page (skip {Args.DeepPageOffset})", 20, 200, sql.DeepPageAsync, mongo.DeepPageAsync),
            new("S4", "Route via city with 4+ star hotel", 20, 200, sql.RouteAsync, mongo.RouteAsync),
            new("S5", "Beach: sea distance + all inclusive (EAV / nested)", 20, 200, sql.AttributesAsync, mongo.AttributesAsync),
            new("S6", "Report: count and avg price by country and month", 3, 30, sql.ReportAsync, mongo.ReportAsync)
        ];
        foreach (var s in reads) await RunScenarioAsync(s);
        await ExplainAsync(view: false);

        _results.Add(await ThroughputAsync("S1", "SQL Server", reads[0].Sql));
        _results.Add(await ThroughputAsync("S1", "MongoDB", reads[0].Mongo));
        _results.Add(await ThroughputAsync("S2", "SQL Server", reads[1].Sql));
        _results.Add(await ThroughputAsync("S2", "MongoDB", reads[1].Mongo));

        var sqlGen = new DataGenerator(gen.Departures, seed: 777);
        var mongoGen = new DataGenerator(gen.Departures, seed: 777);
        int sqlNext = gen.Departures + 1, mongoNext = gen.Departures + 1;
        int sqlTour = gen.TourCount + 1, mongoTour = gen.TourCount + 1;
        long renamedDocs = 0, renameCalls = 0, repricedDocs = 0, repriceCalls = 0;
        Scenario[] writes =
        [
            new("S7", "Insert new offer", 30, 300,
                _ => sql.InsertOfferAsync(sqlGen.CreateNewOffer(sqlNext++, sqlTour++)),
                _ => mongo.InsertOfferAsync(mongoGen.CreateNewOffer(mongoNext++, mongoTour++)), Verify: false),
            new("S8", "Book 1-3 seats", 30, 300, sql.BookAsync, mongo.BookAsync, Verify: false),
            new("S9", "Rename hotel", 2, 20, sql.RenameHotelAsync,
                async a => { var n = await mongo.RenameHotelAsync(a); renamedDocs += n; renameCalls++; return n; }, Verify: false),
            new("S10", "Tour price change + history", 20, 200, sql.UpdateTourPriceAsync,
                async a => { var n = await mongo.UpdateTourPriceAsync(a); repricedDocs += n; repriceCalls++; return n; }, Verify: false)
        ];
        foreach (var s in writes) await RunScenarioAsync(s);
        _checks.Add($"S9: MongoDB updates {renamedDocs / Math.Max(1, renameCalls):N0} documents per call on average, SQL Server — 1 Hotels row");
        _checks.Add($"S10: MongoDB updates {repricedDocs / (double)Math.Max(1, repriceCalls):F1} documents per call on average, SQL Server — 1 Tours row + 1 history row (trigger)");

        Console.WriteLine("-- Creating indexed views vw_OfferSearch, vw_DepartureSeats");
        var started = Stopwatch.StartNew();
        await sql.CreateSearchViewsAsync(indexedViewScript);
        _checks.Add($"Indexed views build: {started.Elapsed.TotalSeconds:F1} s");
        Scenario[] withView =
        [
            new("S2v", "Search, SQL via indexed views", 50, 500, sql.SearchViewAsync, mongo.SearchAsync),
            new("S3v", "Deep page, SQL via indexed views", 20, 200, sql.DeepPageViewAsync, mongo.DeepPageAsync),
            writes[0] with { Id = "S7v", Title = "Insert offer, SQL with indexed views" },
            writes[1] with { Id = "S8v", Title = "Book seats, SQL with indexed views" }
        ];
        foreach (var s in withView) await RunScenarioAsync(s);
        await ExplainAsync(view: true);
        _results.Add(await ThroughputAsync("S2v", "SQL Server (indexed views)", sql.SearchViewAsync));
        _results.Add(await ThroughputAsync("S2v", "MongoDB", mongo.SearchAsync));

        await RaceAsync();
    }

    private async Task RunScenarioAsync(Scenario s)
    {
        var name = $"{s.Id} {s.Title}";
        Console.WriteLine($"-- {name}");
        if (s.Verify) await VerifyAsync(s, name);
        _results.Add(await MeasureAsync(name, "SQL Server", s.Warmup, s.Iterations, s.Sql));
        _results.Add(await MeasureAsync(name, "MongoDB", s.Warmup, s.Iterations, s.Mongo));
    }

    private async Task VerifyAsync(Scenario s, string name)
    {
        var r = new Random(Seed + 1);
        int ok = 0, total = Math.Min(30, s.Iterations);
        for (var i = 0; i < total; i++)
        {
            var a = Args.Next(r, gen);
            if (await s.Sql(a) == await s.Mongo(a)) ok++;
        }
        _checks.Add($"{name}: {ok}/{total} SQL and MongoDB results match");
        Console.WriteLine($"   result check: {ok}/{total}");
    }

    private async Task ExplainAsync(bool view)
    {
        var baseArgs = Args.Next(new Random(7), gen) with
        {
            From = DataGenerator.Now.AddDays(30), To = DataGenerator.Now.AddDays(60), MaxPrice = 1_000_000m, Seats = 2
        };
        foreach (var (label, country) in new[] { ("popular country (Id 1)", 1), ("rare country (Id 40)", DataGenerator.CountryCount) })
        {
            var a = baseArgs with { CountryId = country };
            await sql.ExplainSearchAsync(a, view);
            var (reads, ms) = await sql.ExplainSearchAsync(a, view);
            if (view)
            {
                _checks.Add($"S2 plan, {label}: SQL Server via indexed views — {reads:N0} logical reads, {ms:F1} ms");
                continue;
            }
            var (keys, docs, mongoMs, index) = await mongo.ExplainSearchAsync(a);
            _checks.Add($"S2 plan, {label}: SQL Server — {reads:N0} logical reads, {ms:F1} ms; MongoDB — index {index}, {keys:N0} keys, {docs:N0} documents examined, {mongoMs} ms");
        }
    }

    private async Task RaceAsync()
    {
        Console.WriteLine("-- C2 race for the last seats");
        var hot = Enumerable.Range(1, gen.Departures).Where(d => gen.IsBookable(d) && gen.ScheduleOf(d).Available <= 8).Take(50).ToArray();
        var sqlBefore = await sql.BookedSeatsAsync(hot);
        var (_, mongoBefore) = await mongo.SeatsAsync(hot);

        var (sqlOk, sqlSeats, sqlResult) = await RaceOnceAsync("SQL Server", sql.BookAsync, hot);
        var (mongoOk, mongoSeats, mongoResult) = await RaceOnceAsync("MongoDB", mongo.BookAsync, hot);
        _results.Add(sqlResult);
        _results.Add(mongoResult);

        var sqlOver = await sql.OverbookedAsync(hot);
        var sqlAfter = await sql.BookedSeatsAsync(hot);
        var (mongoOver, mongoAfter) = await mongo.SeatsAsync(hot);
        _checks.Add($"C2 SQL Server: {sqlOk:N0} bookings accepted ({sqlSeats} seats), {sqlResult.Count - sqlOk:N0} rejected; overbooked departures: {sqlOver}; seat delta {sqlAfter - sqlBefore} = accepted seats {sqlSeats}: {sqlAfter - sqlBefore == sqlSeats}");
        _checks.Add($"C2 MongoDB: {mongoOk:N0} bookings accepted ({mongoSeats} seats), {mongoResult.Count - mongoOk:N0} rejected; overbooked departures: {mongoOver}; seat delta {mongoAfter - mongoBefore} = accepted seats {mongoSeats}: {mongoAfter - mongoBefore == mongoSeats}");
    }

    private static async Task<(int Ok, int Seats, Result Result)> RaceOnceAsync(string engine, Func<int, int[], Task<long>> book, int[] hot,
        int workers = 16, int seconds = 5)
    {
        int ok = 0, seats = 0;
        var stopAt = Stopwatch.GetTimestamp() + seconds * Stopwatch.Frequency;
        var lists = await Task.WhenAll(Enumerable.Range(0, workers).Select(async w =>
        {
            var r = new Random(Seed + 500 + w);
            var list = new List<double>();
            while (Stopwatch.GetTimestamp() < stopAt)
            {
                var n = r.Next(1, 3);
                var tourists = Enumerable.Range(0, n).Select(i => (w * 1_000 + list.Count * 3 + i) % 900 + 1).Distinct().ToArray();
                var start = Stopwatch.GetTimestamp();
                if (await book(hot[r.Next(hot.Length)], tourists) != 0)
                {
                    Interlocked.Increment(ref ok);
                    Interlocked.Add(ref seats, tourists.Length);
                }
                list.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            return list;
        }));
        var times = lists.SelectMany(x => x).ToArray();
        var result = Summarize("C2 race for last seats", engine, $"{workers} threads", times, times.Length / (double)seconds);
        Console.WriteLine($"   {engine,-22} {result.OpsPerSec,8:F0} ops/s, accepted {ok}, seats {seats}");
        return (ok, seats, result);
    }

    private async Task<Result> MeasureAsync(string scenario, string engine, int warmup, int iterations, Func<Args, Task<long>> op)
    {
        var r = new Random(Seed);
        for (var i = 0; i < warmup; i++) await op(Args.Next(r, gen));

        var times = new double[iterations];
        var total = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var a = Args.Next(r, gen);
            var start = Stopwatch.GetTimestamp();
            await op(a);
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        total.Stop();
        var result = Summarize(scenario, engine, "sequential", times, iterations / total.Elapsed.TotalSeconds);
        Console.WriteLine($"   {engine,-22} median {result.MedianMs,8:F2} ms   p95 {result.P95Ms,8:F2} ms");
        return result;
    }

    private async Task<Result> ThroughputAsync(string scenario, string engine, Func<Args, Task<long>> op,
        int workers = 16, int seconds = 10)
    {
        var stopAt = Stopwatch.GetTimestamp() + seconds * Stopwatch.Frequency;
        var lists = await Task.WhenAll(Enumerable.Range(0, workers).Select(async w =>
        {
            var r = new Random(Seed + 100 + w);
            var list = new List<double>();
            while (Stopwatch.GetTimestamp() < stopAt)
            {
                var a = Args.Next(r, gen);
                var start = Stopwatch.GetTimestamp();
                await op(a);
                list.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            return list;
        }));
        var times = lists.SelectMany(x => x).ToArray();
        var result = Summarize(scenario, engine, $"{workers} threads", times, times.Length / (double)seconds);
        Console.WriteLine($"   {scenario} {engine,-26} {workers} threads: {result.OpsPerSec,8:F0} ops/s, p95 {result.P95Ms:F2} ms");
        return result;
    }

    private static Result Summarize(string scenario, string engine, string mode, double[] times, double opsPerSec)
    {
        Array.Sort(times);
        return new Result(scenario, engine, mode, times.Length,
            times[times.Length / 2], times[(int)Math.Ceiling(times.Length * 0.95) - 1], times.Average(), opsPerSec);
    }

    public string ToMarkdown(string header)
    {
        var sb = new StringBuilder(header);
        sb.AppendLine("\n## Checks: result equivalence, plans, race\n");
        foreach (var c in _checks) sb.AppendLine($"- {c}");

        sb.AppendLine("\n## Sequential calls (1 client)\n");
        sb.AppendLine("| Scenario | DB | N | Median, ms | p95, ms | Mean, ms | ops/s |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var x in _results.Where(x => x.Mode == "sequential"))
            sb.AppendLine($"| {x.Scenario} | {x.Engine} | {x.Count} | {x.MedianMs:F2} | {x.P95Ms:F2} | {x.MeanMs:F2} | {x.OpsPerSec:F0} |");

        sb.AppendLine("\n## Concurrent load\n");
        sb.AppendLine("| Scenario | DB | Mode | Operations | ops/s | Median, ms | p95, ms |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
        foreach (var x in _results.Where(x => x.Mode != "sequential"))
            sb.AppendLine($"| {x.Scenario} | {x.Engine} | {x.Mode} | {x.Count} | {x.OpsPerSec:F0} | {x.MedianMs:F2} | {x.P95Ms:F2} |");
        return sb.ToString();
    }

    public string ToCsv() => string.Join("\n",
        _results.Select(x => $"{gen.Departures};{x.Scenario};{x.Engine};{x.Mode};{x.Count};{x.MedianMs:F3};{x.P95Ms:F3};{x.MeanMs:F3};{x.OpsPerSec:F1}"));
}
