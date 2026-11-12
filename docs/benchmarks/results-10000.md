# Results: 10,000 departures (offers)

Date: 2026-11-12 15:18. Machine: 16 logical cores, .NET 10.0.12.
Databases in Docker (WSL2) with equal limits: 4 CPU, 2 GB RAM; MongoDB cache 1 GB, SQL Server memory 1.5 GB.

## Size and load

| DB | Rows / documents | Data, MB | Indexes, MB | Load time, s |
|---|---:|---:|---:|---:|
| SQL Server (15 tables) | 257,261 | 14.3 | 8.0 | 2.8 |
| MongoDB (1 collection) | 10,000 | 4.2 | 1.7 | 1.3 |

Load time is for reference only: SQL Server loads with indexes already in place (bulk copy + FK validation + statistics),
MongoDB uses insertMany without indexes and builds them afterwards.

## Checks: result equivalence, plans, race

- S1 Offer card by Id: 30/30 SQL and MongoDB results match
- S2 Search: country+dates+price+seats, top 20: 30/30 SQL and MongoDB results match
- S3 Deep page (skip 500): 30/30 SQL and MongoDB results match
- S4 Route via city with 4+ star hotel: 30/30 SQL and MongoDB results match
- S5 Beach: sea distance + all inclusive (EAV / nested): 30/30 SQL and MongoDB results match
- S6 Report: count and avg price by country and month: 30/30 SQL and MongoDB results match
- S2 plan, popular country (Id 1): SQL Server — 1,021 logical reads, 6.1 ms; MongoDB — index ix_search, 287 keys, 20 documents examined, 0 ms
- S2 plan, rare country (Id 40): SQL Server — 339 logical reads, 4.8 ms; MongoDB — index ix_search, 71 keys, 4 documents examined, 0 ms
- S9: MongoDB updates 15 documents per call on average, SQL Server — 1 Hotels row
- S10: MongoDB updates 10.0 documents per call on average, SQL Server — 1 Tours row + 1 history row (trigger)
- Indexed views build: 0.1 s
- S2v Search, SQL via indexed views: 30/30 SQL and MongoDB results match
- S3v Deep page, SQL via indexed views: 30/30 SQL and MongoDB results match
- S2 plan, popular country (Id 1): SQL Server via indexed views — 102 logical reads, 1.7 ms
- S2 plan, rare country (Id 40): SQL Server via indexed views — 26 logical reads, 1.2 ms
- C2 SQL Server: 190 bookings accepted (268 seats), 13,891 rejected; overbooked departures: 0; seat delta 268 = accepted seats 268: True
- C2 MongoDB: 188 bookings accepted (268 seats), 45,798 rejected; overbooked departures: 0; seat delta 268 = accepted seats 268: True

## Sequential calls (1 client)

| Scenario | DB | N | Median, ms | p95, ms | Mean, ms | ops/s |
|---|---|---:|---:|---:|---:|---:|
| S1 Offer card by Id | SQL Server | 500 | 1.62 | 2.40 | 1.71 | 581 |
| S1 Offer card by Id | MongoDB | 500 | 1.06 | 1.69 | 1.14 | 867 |
| S2 Search: country+dates+price+seats, top 20 | SQL Server | 500 | 2.22 | 18.66 | 4.01 | 248 |
| S2 Search: country+dates+price+seats, top 20 | MongoDB | 500 | 1.32 | 2.00 | 1.37 | 728 |
| S3 Deep page (skip 500) | SQL Server | 200 | 2.58 | 5.25 | 2.92 | 341 |
| S3 Deep page (skip 500) | MongoDB | 200 | 1.18 | 2.11 | 1.35 | 739 |
| S4 Route via city with 4+ star hotel | SQL Server | 200 | 1.87 | 2.57 | 1.94 | 512 |
| S4 Route via city with 4+ star hotel | MongoDB | 200 | 1.06 | 1.59 | 1.12 | 892 |
| S5 Beach: sea distance + all inclusive (EAV / nested) | SQL Server | 200 | 2.15 | 20.97 | 4.15 | 240 |
| S5 Beach: sea distance + all inclusive (EAV / nested) | MongoDB | 200 | 2.22 | 5.41 | 2.97 | 336 |
| S6 Report: count and avg price by country and month | SQL Server | 30 | 5.52 | 6.30 | 5.30 | 188 |
| S6 Report: count and avg price by country and month | MongoDB | 30 | 4.57 | 5.95 | 4.56 | 219 |
| S7 Insert new offer | SQL Server | 300 | 4.46 | 5.41 | 4.52 | 220 |
| S7 Insert new offer | MongoDB | 300 | 2.89 | 4.80 | 3.01 | 332 |
| S8 Book 1-3 seats | SQL Server | 300 | 10.79 | 50.31 | 16.22 | 62 |
| S8 Book 1-3 seats | MongoDB | 300 | 9.16 | 43.73 | 15.44 | 65 |
| S9 Rename hotel | SQL Server | 20 | 4.01 | 9.17 | 4.65 | 214 |
| S9 Rename hotel | MongoDB | 20 | 5.72 | 26.97 | 9.89 | 101 |
| S10 Tour price change + history | SQL Server | 200 | 5.21 | 30.47 | 10.03 | 100 |
| S10 Tour price change + history | MongoDB | 200 | 5.42 | 11.25 | 6.07 | 164 |
| S2v Search, SQL via indexed views | SQL Server | 500 | 1.99 | 2.66 | 2.04 | 487 |
| S2v Search, SQL via indexed views | MongoDB | 500 | 1.35 | 12.48 | 2.56 | 390 |
| S3v Deep page, SQL via indexed views | SQL Server | 200 | 1.76 | 3.04 | 1.84 | 541 |
| S3v Deep page, SQL via indexed views | MongoDB | 200 | 2.52 | 4.28 | 2.65 | 375 |
| S7v Insert offer, SQL with indexed views | SQL Server | 300 | 6.24 | 17.51 | 10.92 | 91 |
| S7v Insert offer, SQL with indexed views | MongoDB | 300 | 3.43 | 6.42 | 3.87 | 258 |
| S8v Book seats, SQL with indexed views | SQL Server | 300 | 8.63 | 47.27 | 12.08 | 83 |
| S8v Book seats, SQL with indexed views | MongoDB | 300 | 4.43 | 7.02 | 4.46 | 224 |

## Concurrent load

| Scenario | DB | Mode | Operations | ops/s | Median, ms | p95, ms |
|---|---|---|---:|---:|---:|---:|
| S1 | SQL Server | 16 threads | 39391 | 3939 | 2.92 | 5.29 |
| S1 | MongoDB | 16 threads | 96859 | 9686 | 1.44 | 3.11 |
| S2 | SQL Server | 16 threads | 27653 | 2765 | 5.18 | 10.37 |
| S2 | MongoDB | 16 threads | 42327 | 4233 | 2.51 | 8.51 |
| S2v | SQL Server (indexed views) | 16 threads | 19737 | 1974 | 4.77 | 27.89 |
| S2v | MongoDB | 16 threads | 52103 | 5210 | 2.32 | 5.43 |
| C2 race for last seats | SQL Server | 16 threads | 14081 | 2816 | 4.52 | 6.63 |
| C2 race for last seats | MongoDB | 16 threads | 45986 | 9197 | 1.58 | 2.86 |
