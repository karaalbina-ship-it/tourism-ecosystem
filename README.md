# TourismEcosystem

Tourism ecosystem: tours with itineraries, departures with guides and transfers, hotels, tourists, bookings, excursions, payments and reviews.

- **Lab 1** — relational database on MS SQL Server (20 tables, procedures, functions, triggers, views, indexes) and an ASP.NET Core Web API with the Repository + Unit of Work patterns. Report: [docs/lab1-report.md](docs/lab1-report.md).
- **Lab 2** — tour offer catalog in MongoDB, the same catalog in SQL, performance comparison on 10k / 100k / 1M departures, basic queries in Neo4j. Report: [docs/lab2-report.md](docs/lab2-report.md).

![ER diagram](docs/er-diagram.webp)

## Tech stack

| | |
|---|---|
| DBMS | Microsoft SQL Server 2025 (LocalDB for Lab 1, Docker container for the Lab 2 benchmarks) |
| Application | .NET 10, ASP.NET Core Web API, Dapper, Swagger UI |
| Tests | xUnit (integration, on LocalDB), SQL checks in `sql/99_tests.sql` |
| NoSQL (Lab 2) | MongoDB 8.0, Neo4j 5 in Docker Compose |

## Quick start (Lab 1)

Requirements: Windows, .NET SDK 10, SQL Server LocalDB, `sqlcmd`.

```powershell
.\deploy.ps1 -Test                                                  # create the DB, load data, run 13 SQL checks
dotnet test                                                         # 6 Unit of Work integration tests
dotnet run --project src\TourismEcosystem.Api --launch-profile http
```

Swagger UI: http://localhost:5186/swagger. Write requests need the `X-User-Id` header (1–5, users from the test data).

## Structure

```
docs/
  lab1-report.md            Lab 1 report
  lab2-report.md            Lab 2 report
  requirements.md           system requirements
  schema.dbml               ER schema for dbdiagram.io
  er-diagram.webp           ER diagram
  nosql-overview.md         Lab 2: NoSQL types, what moves to NoSQL and why
  neo4j.md                  Lab 2: basic Neo4j queries
  benchmarks/               Lab 2: raw benchmark results, sample document
sql/
  00–07                     database, schema, functions, views, procedures, triggers, indexes, data
  08_catalog.sql            Lab 2: SQL side of the catalog (TourAttributes EAV, vw_OfferCards, sp_OfferCard_Create)
  99_tests.sql              business logic checks
  bench_indexed_view.sql    Lab 2: indexed views for the performance experiment
src/TourismEcosystem.Api/   Web API: Controllers, Data (Unit of Work, Repositories), Models
tests/TourismEcosystem.Tests/ integration tests
benchmarks/                 Lab 2: data generator and SQL Server vs MongoDB benchmark
neo4j/                      Lab 2: Neo4j demo queries and output
deploy.ps1                  database deployment
docker-compose.yml          Lab 2: MongoDB, SQL Server, Neo4j
```

## Lab 1: assignment checklist

| Item | Implementation |
|---|---|
| 1. Database schema | [docs/er-diagram.webp](docs/er-diagram.webp), [docs/schema.dbml](docs/schema.dbml) |
| 2. ≥ 15 entities | 20 tables — [sql/01_schema.sql](sql/01_schema.sql) |
| 3. Requirements, soft delete, audit | [docs/requirements.md](docs/requirements.md); soft delete on 5 tables, `UpdatedAt/UpdatedBy` on 6 tables |
| 4. Implementation in a DBMS | MS SQL Server, scripts in [sql/](sql), [deploy.ps1](deploy.ps1) |
| 5. ≥ 10 server-side objects | 37: 7 functions, 8 views, 15 procedures, 7 triggers |
| 6. ≥ 2 index types | clustered, unique, filtered, composite + INCLUDE, columnstore — [sql/06_indexes.sql](sql/06_indexes.sql) |
| 7. Repository + Unit of Work | [src/TourismEcosystem.Api/Data](src/TourismEcosystem.Api/Data); demo — `POST /api/bookings` |

Details — in the [report](docs/lab1-report.md).

## Lab 2: NoSQL

Additional requirement: Docker Desktop.

```powershell
docker compose up -d                 # MongoDB :27019, SQL Server :14334, Neo4j :7475/:7688
.\benchmarks\run.ps1                 # benchmark on 10k / 100k / 1M departures (-Scales 10000 for a quick run)
.\neo4j\run-demo.ps1                 # basic Neo4j queries
```

| Item | Where |
|---|---|
| 1–2. NoSQL types, what moves and why | [docs/nosql-overview.md](docs/nosql-overview.md) |
| 3. MongoDB: `offers` collection | [MongoStore.cs](benchmarks/TourismEcosystem.Benchmarks/MongoStore.cs), sample document [sample-offer.json](docs/benchmarks/sample-offer.json) |
| 4. The same in SQL (14 tables) | [sql/08_catalog.sql](sql/08_catalog.sql), [SqlStore.cs](benchmarks/TourismEcosystem.Benchmarks/SqlStore.cs) |
| 5. Performance testing | [docs/lab2-report.md](docs/lab2-report.md), raw results in [docs/benchmarks/](docs/benchmarks) |
| 6. Neo4j (graph) | [docs/neo4j.md](docs/neo4j.md), [neo4j/demo.cypher](neo4j/demo.cypher), [neo4j/demo-output.txt](neo4j/demo-output.txt) |
