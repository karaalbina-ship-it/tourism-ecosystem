# NoSQL for the Tourism Ecosystem

## 1. Types of NoSQL databases

| Type | Data model | Examples | Strengths | Weaknesses |
|---|---|---|---|---|
| Key-value | key → value (string, hash, list, set, sorted set) | Redis, Memcached, DynamoDB | O(1) access by key, TTL, atomic counters, very low latency | No queries by value, size limited by RAM (Redis), weak relationship model |
| Document | JSON/BSON documents with nested objects and arrays | MongoDB, Couchbase, CosmosDB | The whole aggregate in one document (no JOINs), flexible schema, indexes on nested fields and arrays | Data duplication, shared data must be updated in many documents, weak cross-collection integrity |
| Wide-column | partition key → rows of columns, sorted by clustering key | Cassandra, ScyllaDB, HBase | Massive write throughput, linear horizontal scaling, time series | Queries only by partition key, "query-first" modelling, no JOINs, no ad hoc aggregation |
| Graph | nodes + relationships, both with properties | Neo4j, ArangoDB, Neptune | Traversals of any depth (paths, recommendations), relationships are stored, not computed | Large aggregations, horizontal scaling of one graph |
| Time series | timestamped measurements | InfluxDB, TimescaleDB | Compression, downsampling, time windows | Time series only |
| Search engine | inverted index | Elasticsearch, OpenSearch | Full-text search, facets, geo search | Not a primary store, eventual consistency |

Common NoSQL traits compared with relational databases:
- the schema is defined by the application (schema-on-read) rather than by the database;
- data is modelled around queries (denormalization) instead of normalized tables joined at read time;
- horizontal scaling (sharding, replication) is part of the design;
- consistency trade-offs (CAP/BASE): many systems are eventually consistent by default; MongoDB gives atomic single-document writes and multi-document transactions on replica sets, Neo4j is fully transactional (ACID) on one instance.

## 2. Should part of the data move to NoSQL?

### What stays in SQL
Bookings, payments, refunds, seat accounting and audit (Lab 1) are **money and obligations to customers**. They need FKs, CHECK constraints, transactions spanning several tables (booking + travelers + payment), triggers and exact reporting. The relational model is the right tool here and nothing of it moves.

### What moves to MongoDB: the public tour offer catalog
New application requirement: a **public catalog of offers** — website visitors search for a departure by country, date window, budget, free seats for their group, cities on the route and hotel class, and filter by **type-specific attributes**:
- beach: distance to the sea, all inclusive, number of pools;
- ski: lift pass included, slope length, ski rental;
- excursion: walking distance per day, entry tickets included, group size;
- cruise: ship, cabin class, number of ports.

Arguments **for** a document database:
1. **Workload profile.** The catalog is read thousands of times more often than it changes (a departure is published once, its price changes a few times). It pays to keep the offer ready-made instead of assembling it on every view.
2. **The offer is an aggregate of ~14 tables.** In SQL one offer card is `TourDepartures` + `Tours` + `Countries` + `TourItinerary` + `Cities` + `Hotels` + `Guides` + `Transfers` + `Excursions` + `TourPriceHistory` + `TourAttributes` + `Reviews` + `Bookings` + `BookingTravelers` (free seats). In MongoDB it is one document with nested `itinerary[]` (city + hotel per day), `transfers[]`, `excursions[]`, `priceHistory[]`, `reviews[]`, `rating`, `guide` and `attributes{}`.
3. **The main search spans 4+ tables in SQL.** Country lives in `Tours`, dates and status in `TourDepartures`, free seats are an aggregate over `Bookings` + `BookingTravelers`. No single SQL index can serve the whole filter plus the sort by price. In a document all these fields sit together and one compound index answers the query.
4. **Flexible attributes.** Each tour type has its own attributes and new ones appear with new products. In SQL this is either an EAV table (`TourAttributes(TourId, AttrKey, NumValue, TextValue)`) with an `EXISTS` per filter, or an `ALTER TABLE` per attribute. In MongoDB it is a nested object with a wildcard index `attributes.$**`.
5. **Independent scaling.** The catalog can be replicated or sharded (e.g. by country) separately from the transactional database that handles payments.

Arguments **against** (accepted trade-offs):
1. **Duplication.** Tour title, price, itinerary, hotel names and the guide are copied into every departure document. Renaming a hotel means `updateMany` over thousands of documents instead of one row; changing a tour price touches all its departures. Both are measured.
2. **No FKs.** Integrity of the catalog is guaranteed by the process that builds it, not by the database.
3. **Synchronization.** SQL is the source of truth; the catalog is a derived projection (CQRS read model) updated from SQL changes (outbox / CDC). A visitor may see a new price a few seconds later — acceptable for a catalog, not for payments.
4. **Heavy aggregation** (reports by country and month) is supported by the aggregation pipeline, but SQL Server with parallel plans is expected to be at least as good — measured as well.

**Conclusion:** the offer card projection moves to MongoDB as a read model. Bookings and money stay in SQL Server.

### Neo4j (graph) — routes and recommendations
A different kind of question appears in tourism: *how do I get from my city to the tour start, with how many changes, and what is the cheapest way?*, *what do tourists like me also book?*. These are path and neighbourhood queries over a network of cities, transfers, tours and tourists. In SQL they need recursive CTEs over a self-joined transfers table; in a graph database they are a single `MATCH` over stored relationships. Neo4j is used for item 6 — [neo4j.md](neo4j.md).
