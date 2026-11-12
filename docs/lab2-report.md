# Lab 2 Report: NoSQL

**Topic:** Moving the public tour offer catalog of the Tourism Ecosystem to a document database, implementing the same data in SQL, comparing performance, and basic queries in a NoSQL database of another type.

**Author:** Albina Kara
**Databases:** MongoDB 8.0 (document), Neo4j 5.26 (graph), Microsoft SQL Server 2025 (relational)
**Code:** C# / .NET 10, MongoDB.Driver, Dapper, Cypher

## Contents
1. [Types of NoSQL databases](#1-types-of-nosql-databases)
2. [What moves to NoSQL and why](#2-what-moves-to-nosql-and-why)
3. [MongoDB implementation](#3-mongodb-implementation)
4. [The same data in SQL](#4-the-same-data-in-sql)
5. [Performance testing](#5-performance-testing)
6. [Neo4j: a NoSQL database of another type](#6-neo4j-a-nosql-database-of-another-type)
7. [Conclusions](#7-conclusions)
8. [Anticipated questions](#8-anticipated-questions)
9. [How to reproduce](#9-how-to-reproduce)

---

## 1. Types of NoSQL databases

| Type | Model | Examples | Good for |
|---|---|---|---|
| Key-value | key → value / data structure | Redis, DynamoDB | caches, counters, sessions, locks |
| Document | JSON/BSON documents with nesting | MongoDB, Couchbase | aggregates read as a whole, flexible schema |
| Wide-column | partition key → rows of columns | Cassandra, ScyllaDB | massive writes, time series, query-first models |
| Graph | nodes + relationships with properties | Neo4j, ArangoDB | paths, networks, recommendations |
| Time series | timestamped measurements | InfluxDB, TimescaleDB | metrics, telemetry |
| Search engine | inverted index | Elasticsearch | full-text search, facets |

Strengths, weaknesses and common traits (schema-on-read, denormalization, sharding, CAP/BASE) — [nosql-overview.md](nosql-overview.md).

## 2. What moves to NoSQL and why

**Stays in SQL:** bookings, travelers, payments, refunds, seat accounting rules and audit — money and obligations that need FKs, CHECK constraints, multi-table transactions and triggers (Lab 1).

**New requirement that makes NoSQL reasonable:** a **public catalog of tour offers**. A visitor searches departures by country, date window, budget and **free seats for the whole group**, by cities on the route with a hotel class, and by **type-specific attributes** (beach: distance to the sea, all inclusive; ski: lift pass, slopes; excursion: walking distance, tickets; cruise: ship, cabin class).

**Why a document database fits the catalog:**
1. **The catalog is read far more often than it changes** — store the offer ready-made.
2. **One offer is an aggregate of 14 SQL tables** and **one document** in MongoDB (mapping in section 4).
3. **The search filter is spread over 4+ tables in SQL**: country in `Tours`, dates and status in `TourDepartures`, free seats are an aggregate over `Bookings` + `BookingTravelers`. In the document they are plain fields next to each other.
4. **Attributes differ per tour type**: EAV with an `EXISTS` per filter in SQL vs a nested object with a wildcard index in MongoDB.

**Trade-offs accepted:** tour data (title, price, itinerary with hotel names, attributes) is duplicated in every departure; hotel and guide names are copied into thousands of documents; no FKs; the catalog is a derived projection of SQL (CQRS read model, eventually consistent). The free-seat counter in the document is updated together with a booking (scenario S8).

**Neo4j** covers a different kind of question — routes between cities and "tourists like you also booked" recommendations (section 6).

## 3. MongoDB implementation

Collection `offers`, one document per **tour departure**, `_id` = `DepartureId`. Full sample — [benchmarks/sample-offer.json](benchmarks/sample-offer.json).

```json
{
  "_id": 1, "tourId": 1,
  "title": "Гірськолижний тур: Туреччина, 8 дн.", "tourType": "Ski",
  "countryId": 1, "country": "Туреччина",
  "startDate": { "$date": "2026-10-05T00:00:00Z" }, "endDate": { "$date": "2026-10-12T00:00:00Z" },
  "durationDays": 8, "price": { "$numberDecimal": "21600" }, "currency": "UAH",
  "status": "Closed", "capacity": 40, "availableSeats": 16,
  "guide": { "_id": 87, "name": "Наталія Савченко (87)", "languages": "uk, en, it" },
  "itinerary": [ { "day": 1, "cityId": 8, "city": "Туреччина, місто 8", "description": "День 1: …",
                   "hotel": { "_id": 37, "name": "Гранд 8", "stars": 5 } }, … ],
  "transfers": [ { "type": "Flight", "carrier": "…", "fromCityId": 21, "toCityId": 8, "departAt": …, "arriveAt": … } ],
  "excursions": [ { "_id": 15, "name": "…", "cityId": 8, "price": … } ],
  "priceHistory": [ { "oldPrice": …, "newPrice": …, "changedAt": … } ],
  "rating": { "avg": 4.33, "count": 3 },
  "reviews": [ { "touristId": …, "rating": 5, "comment": "Гід супер", "createdAt": … } ],
  "attributes": { "liftPassIncluded": true, "slopeKm": 412, "skiRental": false }
}
```

(Values are generated test data in Ukrainian; field names are English.)

**Indexes** ([MongoStore.cs](../benchmarks/TourismEcosystem.Benchmarks/MongoStore.cs)), field order follows the ESR rule (Equality, Sort, Range):

| Index | Keys | Scenario |
|---|---|---|
| `_id` | `_id` | S1 card by Id |
| `ix_search` | `countryId, status, price, _id, startDate, availableSeats` | S2, S3 search: equality on country/status, sort by price, range on dates and seats checked in the index |
| `ix_route` | `itinerary.cityId, status, price, _id` (multikey) | S4 route via a city |
| `ix_type_price` | `tourType, status, price, _id` | S5 attributes + sort by price |
| `ix_attributes` | `attributes.$**` (wildcard) | arbitrary attribute filters |
| `ix_report` | `tourType, status, startDate, countryId, price` | S6 report (covered) |
| `ix_hotel` | `itinerary.hotel._id` | S9 rename hotel |
| `ix_tour` | `tourId` | S10 tour price change |

**Write durability:** benchmark writes use `WriteConcern(w: 1, journal: true)` — acknowledged only after reaching the journal, the same guarantee as a `COMMIT` in SQL Server.

**Booking in MongoDB** (S8) is two writes: `findOneAndUpdate({_id, status: "Open", availableSeats: {$gte: N}}, {$inc: {availableSeats: -N}})` — an atomic conditional decrement that cannot overbook — and then `insertOne` of a booking record into the `bookings` collection.

## 4. The same data in SQL

The offer is assembled from the Lab 1 tables plus one new table and one new column ([sql/08_catalog.sql](../sql/08_catalog.sql)):

| Document field | SQL |
|---|---|
| `_id`, `startDate`, `endDate`, `capacity`, `status` | `TourDepartures` |
| `tourId`, `title`, `description`, `durationDays`, `price`, `currency`, `tourType` | `Tours` (+ new column `TourType`) |
| `country` | `Countries` |
| `guide.*` | `Guides` |
| `itinerary[]` | `TourItinerary` → `Cities`, `Hotels` |
| `transfers[]` | `Transfers` |
| `excursions[]` | `Excursions` in the route cities |
| `priceHistory[]` | `TourPriceHistory` |
| `rating`, `reviews[]` | `Reviews` (aggregate + top 3) |
| `availableSeats` | `Capacity` − count of `BookingTravelers` of non-cancelled `Bookings` |
| `attributes{}` | **`TourAttributes(TourId, AttrKey, NumValue, TextValue)`** — new EAV table |

So **one document maps to 14 tables** (15 with `Tourists`, which the travelers reference).

- `vw_OfferCards` — card header (5 tables + seat and rating aggregates); collections are read with 6 more queries in the same batch.
- `sp_OfferCard_Create` — publishes a new tour with its first departure into 6 tables in one transaction; collections are passed as JSON (`OPENJSON`), so the statement text is constant and the plan is cached.
- Booking uses the **Lab 1 procedures** `sp_Booking_Create` + `sp_Booking_AddTraveler` in one transaction (seat check under `UPDLOCK`, triggers recalculate the total).
- Indexes for the same scenarios: `IX_Tours_Country_Price`, `IX_Tours_Type_Price`, `IX_TourDepartures_Tour_Search`, `IX_TourItinerary_City`, `IX_TourAttributes_Num/Text`, `IX_Reviews_Departure_Created`, plus all Lab 1 indexes (`IX_Bookings_Departure`, `PK_BookingTravelers`, …).
- Experiment: two **indexed (materialized) views** ([sql/bench_indexed_view.sql](../sql/bench_indexed_view.sql)) — `vw_OfferSearch` brings the search fields of `TourDepartures` and `Tours` into one index, `vw_DepartureSeats` keeps booked seats per departure (`COUNT_BIG` grouped). This is SQL's counterpart of denormalization and is measured separately (S2v, S3v, S7v, S8v).

## 5. Performance testing

### 5.1 Environment

| | |
|---|---|
| Host | AMD Ryzen 7 7840HS (8 cores / 16 threads), 27.8 GB RAM, NVMe SSD, Windows 11 |
| Containers | Docker Desktop (WSL2). **SQL Server and MongoDB get identical limits: 4 CPU, 2 GB RAM** |
| SQL Server | 2025 CU10 (17.0.5035.5), Developer edition, `MSSQL_MEMORY_LIMIT_MB = 1536` |
| MongoDB | 8.0.34, WiredTiger cache 1 GB |
| Client | .NET 10 console app on the host, connection pooling on both sides |

Both databases run in containers in the same VM, so virtualization overhead is the same for both. Neo4j was stopped during the benchmark.

### 5.2 Data

A deterministic generator ([DataGenerator.cs](../benchmarks/TourismEcosystem.Benchmarks/DataGenerator.cs), seed 42) produces offers that are written **identically** to both databases. Distributions are skewed like a real catalog:
- 40 countries with quadratic skew (the most popular ~16% of tours, the rarest ~1.3%), 10 cities per country, 5 hotels per city (2,000 hotels), 500 guides;
- 10 departures per tour; tour types: excursion 45%, beach 30%, ski 15%, cruise 10%; 3–12 days; 1–6 cities on the route; 0–3 price changes; 3 type-specific attributes;
- departure dates from 180 days in the past to a year ahead: past ones are `Closed` and have 0–5 reviews, 4% of future ones are `Cancelled`, 10% are sold out, the rest are 0–80% booked; capacity 10–40;
- bookings of 1–4 travelers; tourists = departures / 2.

**Three scales — 10k, 100k, 1M departures** — to show the trend, not one point.

| Scale | SQL Server rows | SQL Server data + indexes | MongoDB data + indexes |
|---|---:|---:|---:|
| 10k | 0.26M | 14.3 + 8.0 MB | 4.2 + 1.7 MB |
| 100k | 2.5M | 136.5 + 113.4 MB | 42.1 + 16.1 MB |
| 1M | **25.3M** | **1,361 + 912 MB** | **420 + 163 MB** |

At 1M the SQL working set (2.27 GB) exceeds its 1.5 GB buffer pool; MongoDB (0.58 GB compressed) fits into its 1 GB cache. Most SQL rows are seats: 13.6M `BookingTravelers` and 5.8M `Bookings`, then 2.2M `Transfers`, 0.8M `Reviews`, 0.75M itinerary days.

### 5.3 Methodology

- **Same queries, same answers.** Every read scenario is first run on 30 random parameter sets on both databases and the results are compared by a checksum over the returned Ids and their order (for S1 — over the whole card: itinerary hotels, transfers, excursions, attributes, price history, last reviews, free seats, price). **All read scenarios match 30/30 on all scales**, including the indexed-view variants.
- **Same parameters.** A seeded generator (seed 2026) gives both databases exactly the same sequence; countries are picked with the same skew as the data.
- **Warm-up** 10% of iterations, then N measured calls (500 for fast reads, 200 for searches, 30 for the report, 300 for inserts and bookings, 200 for price changes, 20 for renames).
- **Median and p95**, not only the mean.
- **Reads before writes**, so writes do not change the data the reads are checked against; indexed views are created after the base measurements because SQL Server Developer edition matches indexed views automatically even for queries on base tables.
- **Concurrency:** 16 clients for 10 s (throughput and latency), plus **C2** — 16 clients for 5 s racing for the last seats of 50 nearly sold-out departures, with a correctness check afterwards.
- **Plans checked:** `SET STATISTICS IO` on SQL Server and `explain('executionStats')` on MongoDB for the main search, on a popular and a rare country.
- **Not measured:** cold cache (cannot be reproduced reliably for MongoDB inside the VM; a production catalog is warm), replication and sharding (single node).
- **Run validity:** the host was close to its memory commit limit because of other applications (IDE, other services). The containers of another project were stopped before the runs; during the 1M run commit peaked at 54.6 of 56.9 GB and free RAM never went below 0.9 GB, so the databases did not page. Background activity on the host shows up as noise in some 10k tails (e.g. S8 p95 ~45–50 ms on both sides).

### 5.4 Scenarios

| # | Scenario | Why it is tested |
|---|---|---|
| S1 | Full offer card by Id | the most frequent catalog operation: 1 document vs 14 tables |
| S2 | Country + start date window (30 days) + max price + free seats ≥ N, sorted by price, top 20 | the main search; fields in 4 tables, seats are an aggregate |
| S3 | Country + 120-day window, page at offset 500 | deep pagination |
| S4 | Route through city X with a 4–5 star hotel there | array (multikey index) vs itinerary join |
| S5 | Beach tours: sea ≤ X m and all inclusive | nested attributes vs EAV |
| S6 | Report: open departures and average price by country and month | heavy aggregation, the classic SQL strength |
| S7 | Publish a new tour with a departure | 1 document vs 6 tables in a transaction |
| S8 | Book 1–3 seats | Lab 1 procedures with `UPDLOCK` + triggers vs conditional `$inc` + booking record |
| S9 | Rename a hotel | **the cost of denormalization**: 1 row vs thousands of documents |
| S10 | Change a tour price + history | 1 row + trigger vs `updateMany` over 10 departures |
| C1 | S1 and S2 with 16 clients | throughput under contention |
| C2 | 16 clients race for the last seats | correctness: no overbooking in either database |
| S2v, S3v, S7v, S8v | S2, S3, S7, S8 with the SQL indexed views | can SQL catch up by materializing, and at what write cost |

### 5.5 Results

Raw results: [10k](benchmarks/results-10000.md), [100k](benchmarks/results-100000.md), [1M](benchmarks/results-1000000.md) (CSV next to them).

**Median latency, ms (1 client).** "×" = SQL time / MongoDB time at 1M.

| Scenario | 10k SQL | 10k Mongo | 100k SQL | 100k Mongo | 1M SQL | 1M Mongo | 1M × |
|---|---:|---:|---:|---:|---:|---:|---:|
| S1 card by Id | 1.62 | 1.06 | 1.91 | 1.68 | 4.41 | 1.20 | 3.7 |
| S2 search | 2.22 | 1.32 | 3.85 | 1.68 | 7.97 | 2.27 | 3.5 |
| S3 deep page | 2.58 | 1.18 | 8.05 | 2.52 | **254.61** | **4.02** | **63** |
| S4 route via city | 1.87 | 1.06 | 3.17 | 1.18 | 34.78 | 1.45 | 24 |
| S5 attributes | 2.15 | 2.22 | 5.77 | 1.38 | 35.43 | 2.97 | 11.9 |
| S6 report | 5.52 | 4.57 | 16.92 | 13.74 | **24.25** | **56.10** | **0.43** |
| S7 publish offer | 4.46 | 2.89 | 4.08 | 2.24 | 5.60 | 2.50 | 2.2 |
| S8 book seats | 10.79 | 9.16 | 6.07 | 3.56 | 8.32 | 4.10 | 2.0 |
| S9 rename hotel | 4.01 | 5.72 | 2.53 | 6.75 | **3.07** | **24.29** | **0.13** |
| S10 tour price change | 5.21 | 5.42 | 2.72 | 3.48 | 3.99 | 6.12 | 0.65 |
| S2v search, SQL indexed views | 1.99 | 1.35 | 1.23 | 1.52 | 1.72 | 2.04 | 0.84 |
| S3v deep page, SQL indexed views | 1.76 | 2.52 | 9.48 | 3.24 | 3.62 | 3.85 | 0.94 |
| S7v publish, SQL with indexed views | 6.24 | 3.43 | 3.88 | 1.86 | 5.28 | 2.60 | 2.0 |
| S8v book, SQL with indexed views | 8.63 | 4.43 | 5.93 | 3.27 | 7.70 | 3.88 | 2.0 |

Tail latency at 1M (p95, ms): S3 SQL **1,041** vs MongoDB 12.9; S1 SQL 7.3 vs 2.1; S6 SQL 162 vs MongoDB 161; S10 MongoDB 41.5 vs SQL 5.1.

S9: MongoDB modifies on average 15 / 142 / 1,155 documents per call (10k / 100k / 1M); SQL Server updates 1 row. S10: MongoDB modifies 10 documents (all departures of the tour), SQL Server 1 row + 1 history row written by the trigger.

**Throughput under load, ops/s (16 clients).**

| Scenario | 10k SQL | 10k Mongo | 100k SQL | 100k Mongo | 1M SQL | 1M Mongo |
|---|---:|---:|---:|---:|---:|---:|
| S1 card by Id | 3,939 | 9,686 | 4,063 | 11,700 | 1,872 | **9,860** |
| S2 search | 2,765 | 4,233 | 608 | 3,275 | **278** | **1,271** |
| S2v search, SQL indexed views | 1,974 | 5,210 | 5,551 | 2,853 | **3,014** | **1,314** |
| C2 race for last seats (attempts/s) | 2,816 | 9,197 | 2,596 | 6,329 | 2,761 | 8,731 |

**C2 correctness** (all scales): **0 overbooked departures in both databases**; the change of booked seats equals the sum of accepted bookings exactly (e.g. 1M: SQL Server accepted 219 bookings / 301 seats, MongoDB 216 bookings / 301 seats; the rest of the attempts were rejected for lack of seats).

### 5.6 Analysis

**S1 — card by Id.** MongoDB reads one document by `_id`. SQL Server runs the header query (5 tables plus seat and rating aggregates) and 6 more queries for collections. Up to 100k everything is in memory and the gap is small (1.1–1.5×); at 1M it grows to 3.7× (and 5× in throughput) because the SQL working set no longer fits into the buffer pool.

**S2 — main search.** Plans at 1M for the same window, popular and rare country:

| Query | SQL Server | MongoDB |
|---|---|---|
| Popular country (Id 1) | 866 logical reads, 15.5 ms | `ix_search`: 304 keys, 20 documents, 1 ms |
| Rare country (Id 40) | 686 logical reads, 10.2 ms | `ix_search`: 268 keys, 20 documents, 1 ms |
| Same through indexed views | 151 / 136 logical reads, 1.6–1.8 ms | |

SQL walks tours of the country in price order (`IX_Tours_Country_Price`), looks up their departures in the date window and for every candidate counts booked seats in `Bookings` + `BookingTravelers`. MongoDB answers from one compound index: equality on country and status, the index order gives the price sort, dates and free seats are checked on index keys, so exactly 20 documents are fetched. Under load the difference grows: 278 vs 1,271 ops/s at 1M.

**S3 — deep page: the largest gap (63× median, 80× p95).** To return page 26 SQL Server needs the free seats for the output rows, and the optimizer computes them for all ~33,000 departures in the window: a parallel plan that hashes the whole `Bookings` (27,020 pages) and `BookingTravelers` (37,109 pages, 13.6M rows) tables — 10.8 s CPU in 2.9 s elapsed in a diagnostic run, 255 ms median in the benchmark. In MongoDB `availableSeats` is a stored field, so skipping 500 entries in `ix_search` costs 4 ms. This is the price of keeping a frequently read aggregate (free seats) normalized.

**S2v / S3v — SQL with indexed views catches up and overtakes under load.** `vw_DepartureSeats` stores booked seats per departure and `vw_OfferSearch` puts the search fields into one index. The search becomes as fast as MongoDB (1.72 vs 2.04 ms median) and under 16 clients SQL Server is **2.3× faster** (3,014 vs 1,314 ops/s at 1M); the deep page drops from 255 ms to 3.6 ms. **The gap in S2/S3 is caused by the data layout, not by "SQL vs NoSQL".** The price of the views is paid on writes: 9.4 s to build at 1M and maintenance on every insert or booking (at 100k and 1M S7v/S8v are within noise of S7/S8 because each write touches one row of each view; at 10k S7v is +40%, in the noisier run). One outlier: S3v at 100k (9.5 ms) — likely a different plan for the join with the seats view at that size; it did not reproduce at 1M and was not investigated further.

**S4, S5 — route and attributes.** S4 needs `TourItinerary` → `Hotels` for every tour in price order (`EXISTS` per row); MongoDB filters one array element with `$elemMatch` through the multikey `ix_route`. S5 needs two `EXISTS` into the EAV table per row; MongoDB uses `ix_type_price` and checks the nested attributes on 20-odd documents. Both stay at 1–3 ms in MongoDB on all scales, while SQL grows to 35 ms at 1M.

**S6 — report: SQL Server wins (2.3× at 1M).** SQL Server runs a parallel hash aggregation on 4 cores. MongoDB uses the covered `ix_report` but groups in a single thread. Tails are equal (~160 ms p95).

**S7, S8 — writes.** Publishing an offer is one document vs 6 tables with FK checks and ~15 index entries: MongoDB is about 2× faster on every scale. Booking in SQL is 2–4 procedure calls in one transaction with an `UPDLOCK`, seat count and two triggers; in MongoDB it is a conditional `$inc` plus one insert, both journaled: again ~2× faster. **Both are correct under contention** (C2): SQL Server serializes bookings on the departure row lock, MongoDB relies on the atomic single-document update with the condition `availableSeats ≥ N`. MongoDB handles ~3× more attempts per second in the race.

**S9, S10 — where denormalization hurts.** Renaming a hotel is one row in SQL (~3 ms on every scale). MongoDB has to rewrite the name in every itinerary that mentions the hotel: 15 → 142 → 1,155 documents, 5.7 → 6.8 → **24.3 ms** — it grows linearly with the data and is **8× slower** than SQL at 1M. A tour price change touches all 10 departures of the tour in MongoDB (and appends history to each), so it is 1.5× slower than the SQL procedure + trigger, with a heavier tail (41.5 vs 5.1 ms p95).

**Storage and load.** At 1M MongoDB takes 3.2× less space for data and 5.6× less for indexes (compression, no join keys, no per-row overhead of 15 tables, seats are one integer instead of ~10 rows). Loading took 213.7 s in SQL Server (bulk copy into 15 tables with indexes in place + FK validation) vs 73.4 s in MongoDB.

## 6. Neo4j: a NoSQL database of another type

Neo4j (graph) was used for basic queries on a tourism network: countries, cities, transfers between cities with type, time and price, hotels, tours with their day-by-day route, tourists and their bookings with ratings. The 20 queries cover constraints and indexes, `CREATE`/`UNWIND` loading, `MATCH … WHERE`, relationship properties, `OPTIONAL MATCH`, variable-length paths (all routes Kyiv → Santorini with up to 4 legs and their total price/time), `shortestPath`, the cheapest route by summed price, reachability ("tours reachable from Lviv in ≤ 2 legs"), aggregation, collaborative-filtering recommendations, `MERGE` (idempotent upsert), `SET`, `DELETE`, `REMOVE` and index statistics.

Commands — [neo4j/demo.cypher](../neo4j/demo.cypher), output — [neo4j/demo-output.txt](../neo4j/demo-output.txt), explanation of every query — [neo4j.md](neo4j.md).

## 7. Conclusions

1. **The offer catalog belongs in MongoDB.** At 1M, the normalized SQL schema is 3.5× slower on the main search, 24× on route search, 12× on attributes and 63× on deep pages (255 vs 4 ms median, 1,041 vs 13 ms p95); under load SQL drops to 278 searches/s vs 1,271. The card is read 3.7× faster, published and booked 2× faster, and stored in 3.9× less space.
2. **The difference comes from the data layout.** When SQL Server gets a denormalized layout through indexed views (search fields in one index, free seats precomputed), its search matches MongoDB and under load is 2.3× faster. The document wins because it matches the query, not because of the engine.
3. **Denormalization has a price.** Renaming a hotel is 8× slower in MongoDB at 1M and grows with the data; a tour price change touches every departure. SQL Server is better at heavy aggregation (2.3× on the report).
4. **Correctness under contention is achievable in both**: row locks + procedures in SQL, an atomic conditional update in MongoDB — 0 overbookings in every run.
5. **Resulting architecture:** SQL Server remains the source of truth for bookings and money (Lab 1); MongoDB is the read model for the public catalog, updated from SQL events; Neo4j answers route and recommendation questions over the network of cities, transfers and bookings.

## 8. Anticipated questions

**Why so much / so little data?** Three scales up to 1M departures = 25.3M rows in SQL (2.27 GB). 1M is where the SQL working set exceeds its 1.5 GB memory limit, so the effect of the storage model becomes visible; 10k and 100k show the trend. Larger scales would only measure disk I/O of the laptop.

**Why one document per departure and not per tour?** Visitors search and book departures (dates, free seats). A tour document with an array of departures would need `$elemMatch` on dates and seats and could not sort departures by price across tours with one index. The cost — tour data duplicated in 10 departures — is measured in S10.

**Why are the indexes different?** Each database got the best indexes for the same queries; identical definitions are impossible because in SQL the filter fields are in different tables. Plans were checked on both sides (section 5.6).

**How do you know both databases return the same thing?** Every read scenario is checked on 30 parameter sets with a checksum of the returned Ids and order (S1 — over the whole card). 30/30 on all scales.

**Is the booking comparison fair? MongoDB just decrements a counter.** MongoDB also stores a booking record (second journaled write), and both sides guarantee no overbooking — checked in C2. SQL does more (traveler rows, totals, audit) because it is the source of truth; that is the honest cost of the full transactional model.

**Why median and p95?** The median shows the typical call, p95 the tail a user notices; means are in the raw results.

**Why Docker for both and equal limits?** Same CPU/RAM and the same virtualization overhead; native SQL Server vs containerized MongoDB would not be comparable.

**Isn't MongoDB faster at writes because it does not wait for the disk?** No: `journal: true`, the same durability as `COMMIT`.

**Why are the indexed views measured separately and after the writes?** Developer edition uses indexed views automatically even for queries on base tables; creating them earlier would silently change the normalized results.

**Where does MongoDB lose?** S9 (renaming shared data, 8× and growing), S10 (price change across copies), S6 (heavy aggregation), and anything that needs integrity across entities.

## 9. How to reproduce

```powershell
docker compose up -d                       # MongoDB :27019, SQL Server :14334, Neo4j :7475/:7688
.\benchmarks\run.ps1                       # 10k, 100k, 1M (about 40 min); -Scales 10000 for a quick run
.\neo4j\run-demo.ps1                       # Neo4j basic queries
```

`run.ps1` recreates the SQL schema in the container (`deploy.ps1 -NoSeed`), the benchmark loads identical data into both databases, runs all scenarios and writes `docs/benchmarks/results-<scale>.md/.csv`. Close heavy applications before the 1M run: memory pressure on the host slows both databases down.
