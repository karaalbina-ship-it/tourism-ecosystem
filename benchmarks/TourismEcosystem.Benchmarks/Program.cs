using System.Diagnostics;
using System.Globalization;
using MongoDB.Bson;
using TourismEcosystem.Benchmarks;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var departures = args.Length > 0 ? int.Parse(args[0]) : 10_000;

var sqlCs = Environment.GetEnvironmentVariable("BENCH_SQL")
    ?? "Server=localhost,14334;Database=TourismEcosystemDb;User Id=sa;Password=Tourism_Bench1;TrustServerCertificate=True;Max Pool Size=200";
var mongoCs = Environment.GetEnvironmentVariable("BENCH_MONGO") ?? "mongodb://localhost:27019";

var sql = new SqlStore(sqlCs);
var mongo = new MongoStore(mongoCs);

Console.WriteLine($"== Loading {departures:N0} departures (offers)");
var gen = new DataGenerator(departures);
var sqlLoad = new Stopwatch();
var mongoLoad = new Stopwatch();

await mongo.ResetAsync();
sqlLoad.Start();
await sql.LoadDimensionsAsync(gen);
sqlLoad.Stop();

const int batch = 20_000;
for (var from = 1; from <= departures; from += batch)
{
    var offers = gen.Offers(from, Math.Min(batch, departures - from + 1)).ToList();
    sqlLoad.Start(); await sql.LoadOffersAsync(offers); sqlLoad.Stop();
    mongoLoad.Start(); await mongo.LoadOffersAsync(offers); mongoLoad.Stop();
    Console.Write($"\r   {from + offers.Count - 1:N0} / {departures:N0}");
}
Console.WriteLine();

sqlLoad.Start(); await sql.FinishLoadAsync(); sqlLoad.Stop();
mongoLoad.Start(); await mongo.CreateIndexesAsync(); mongoLoad.Stop();

var (sqlData, sqlIndex) = await sql.SizeAsync();
var (mongoData, mongoIndex) = await mongo.SizeAsync();
var sqlRows = await sql.RowCountAsync();
Console.WriteLine($"   SQL: {sqlLoad.Elapsed.TotalSeconds:F1} s, {sqlRows:N0} rows, {sqlData / 1024.0:F1} MB data + {sqlIndex / 1024.0:F1} MB indexes");
Console.WriteLine($"   Mongo: {mongoLoad.Elapsed.TotalSeconds:F1} s, {mongoData / 1024.0:F1} MB data + {mongoIndex / 1024.0:F1} MB indexes");

Offer? sample = null;
for (var id = 1; id <= Math.Min(departures, 500) && sample is null; id++)
    if (await mongo.GetOfferAsync(id) is { Rating.Count: > 0 } o) sample = o;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var bench = new Bench(sql, mongo, gen, Path.Combine(repoRoot, "sql", "bench_indexed_view.sql"));
await bench.RunAsync();

var header = $"""
    # Results: {departures:N0} departures (offers)

    Date: {DateTime.Now:yyyy-MM-dd HH:mm}. Machine: {Environment.ProcessorCount} logical cores, .NET {Environment.Version}.
    Databases in Docker (WSL2) with equal limits: 4 CPU, 2 GB RAM; MongoDB cache 1 GB, SQL Server memory 1.5 GB.

    ## Size and load

    | DB | Rows / documents | Data, MB | Indexes, MB | Load time, s |
    |---|---:|---:|---:|---:|
    | SQL Server (15 tables) | {sqlRows:N0} | {sqlData / 1024.0:F1} | {sqlIndex / 1024.0:F1} | {sqlLoad.Elapsed.TotalSeconds:F1} |
    | MongoDB (1 collection) | {departures:N0} | {mongoData / 1024.0:F1} | {mongoIndex / 1024.0:F1} | {mongoLoad.Elapsed.TotalSeconds:F1} |

    Load time is for reference only: SQL Server loads with indexes already in place (bulk copy + FK validation + statistics),
    MongoDB uses insertMany without indexes and builds them afterwards.

    """;

var outDir = Path.Combine(repoRoot, "docs", "benchmarks");
Directory.CreateDirectory(outDir);
await File.WriteAllTextAsync(Path.Combine(outDir, $"results-{departures}.md"), bench.ToMarkdown(header));
await File.WriteAllTextAsync(Path.Combine(outDir, $"results-{departures}.csv"),
    "departures;scenario;engine;mode;count;median_ms;p95_ms;mean_ms;ops_per_sec\n" + bench.ToCsv());
if (sample is not null)
    await File.WriteAllTextAsync(Path.Combine(outDir, "sample-offer.json"),
        sample.ToBsonDocument().ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true }));
Console.WriteLine($"== Saved to {Path.GetFullPath(outDir)}");
