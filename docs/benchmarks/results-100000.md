# Results: 100,000 departures (offers)

Date: 2026-11-12 15:20. Machine: 16 logical cores, .NET 10.0.12.
Databases in Docker (WSL2) with equal limits: 4 CPU, 2 GB RAM; MongoDB cache 1 GB, SQL Server memory 1.5 GB.

## Size and load

| DB | Rows / documents | Data, MB | Indexes, MB | Load time, s |
|---|---:|---:|---:|---:|
| SQL Server (15 tables) | 2,529,633 | 136.5 | 113.4 | 23.6 |
| MongoDB (1 collection) | 100,000 | 42.1 | 16.1 | 7.0 |

Load time is for reference only: SQL Server loads with indexes already in place (bulk copy + FK validation + statistics),
MongoDB uses insertMany without indexes and builds them afterwards.

## Checks: result equivalence, plans, race

- S1 Offer card by Id: 30/30 SQL and MongoDB results match
- S2 Search: country+dates+price+seats, top 20: 30/30 SQL and MongoDB results match
- S3 Deep page (skip 500): 30/30 SQL and MongoDB results match
- S4 Route via city with 4+ star hotel: 30/30 SQL and MongoDB results match
- S5 Beach: sea distance + all inclusive (EAV / nested): 30/30 SQL and MongoDB results match
- S6 Report: count and avg price by country and month: 30/30 SQL and MongoDB results match
- S2 plan, popular country (Id 1): SQL Server — 523 logical reads, 6.1 ms; MongoDB — index ix_search, 194 keys, 20 documents examined, 0 ms
- S2 plan, rare country (Id 40): SQL Server — 449 logical reads, 3.0 ms; MongoDB — index ix_search, 299 keys, 20 documents examined, 0 ms
- S9: MongoDB updates 142 documents per call on average, SQL Server — 1 Hotels row
- S10: MongoDB updates 10.0 documents per call on average, SQL Server — 1 Tours row + 1 history row (trigger)
- Indexed views build: 0.6 s
- S2v Search, SQL via indexed views: 30/30 SQL and MongoDB results match
- S3v Deep page, SQL via indexed views: 30/30 SQL and MongoDB results match
- S2 plan, popular country (Id 1): SQL Server via indexed views — 106 logical reads, 1.5 ms
- S2 plan, rare country (Id 40): SQL Server via indexed views — 99 logical reads, 1.6 ms
- C2 SQL Server: 215 bookings accepted (301 seats), 12,766 rejected; overbooked departures: 0; seat delta 301 = accepted seats 301: True
- C2 MongoDB: 214 bookings accepted (301 seats), 31,430 rejected; overbooked departures: 0; seat delta 301 = accepted seats 301: True

## Sequential calls (1 client)

| Scenario | DB | N | Median, ms | p95, ms | Mean, ms | ops/s |
|---|---|---:|---:|---:|---:|---:|
| S1 Offer card by Id | SQL Server | 500 | 1.91 | 2.43 | 1.94 | 512 |
| S1 Offer card by Id | MongoDB | 500 | 1.68 | 10.23 | 4.68 | 212 |
| S2 Search: country+dates+price+seats, top 20 | SQL Server | 500 | 3.85 | 10.36 | 4.69 | 213 |
| S2 Search: country+dates+price+seats, top 20 | MongoDB | 500 | 1.68 | 13.53 | 2.80 | 356 |
| S3 Deep page (skip 500) | SQL Server | 200 | 8.05 | 10.58 | 7.57 | 132 |
| S3 Deep page (skip 500) | MongoDB | 200 | 2.52 | 3.42 | 2.58 | 387 |
| S4 Route via city with 4+ star hotel | SQL Server | 200 | 3.17 | 7.51 | 3.75 | 266 |
| S4 Route via city with 4+ star hotel | MongoDB | 200 | 1.18 | 1.53 | 1.20 | 830 |
| S5 Beach: sea distance + all inclusive (EAV / nested) | SQL Server | 200 | 5.77 | 44.78 | 10.09 | 99 |
| S5 Beach: sea distance + all inclusive (EAV / nested) | MongoDB | 200 | 1.38 | 2.09 | 1.47 | 678 |
| S6 Report: count and avg price by country and month | SQL Server | 30 | 16.92 | 22.19 | 15.96 | 63 |
| S6 Report: count and avg price by country and month | MongoDB | 30 | 13.74 | 20.76 | 12.47 | 80 |
| S7 Insert new offer | SQL Server | 300 | 4.08 | 4.94 | 4.07 | 245 |
| S7 Insert new offer | MongoDB | 300 | 2.24 | 3.22 | 2.35 | 424 |
| S8 Book 1-3 seats | SQL Server | 300 | 6.07 | 37.86 | 9.01 | 111 |
| S8 Book 1-3 seats | MongoDB | 300 | 3.56 | 4.64 | 3.70 | 270 |
| S9 Rename hotel | SQL Server | 20 | 2.53 | 3.02 | 2.41 | 413 |
| S9 Rename hotel | MongoDB | 20 | 6.75 | 10.19 | 6.84 | 146 |
| S10 Tour price change + history | SQL Server | 200 | 2.72 | 3.94 | 2.78 | 359 |
| S10 Tour price change + history | MongoDB | 200 | 3.48 | 4.26 | 3.58 | 278 |
| S2v Search, SQL via indexed views | SQL Server | 500 | 1.23 | 10.82 | 3.02 | 330 |
| S2v Search, SQL via indexed views | MongoDB | 500 | 1.52 | 2.24 | 1.60 | 621 |
| S3v Deep page, SQL via indexed views | SQL Server | 200 | 9.48 | 13.19 | 9.98 | 100 |
| S3v Deep page, SQL via indexed views | MongoDB | 200 | 3.24 | 28.62 | 11.55 | 86 |
| S7v Insert offer, SQL with indexed views | SQL Server | 300 | 3.88 | 4.77 | 3.93 | 254 |
| S7v Insert offer, SQL with indexed views | MongoDB | 300 | 1.86 | 2.83 | 2.09 | 478 |
| S8v Book seats, SQL with indexed views | SQL Server | 300 | 5.93 | 7.68 | 5.99 | 167 |
| S8v Book seats, SQL with indexed views | MongoDB | 300 | 3.27 | 4.42 | 3.37 | 296 |

## Concurrent load

| Scenario | DB | Mode | Operations | ops/s | Median, ms | p95, ms |
|---|---|---|---:|---:|---:|---:|
| S1 | SQL Server | 16 threads | 40634 | 4063 | 2.87 | 5.12 |
| S1 | MongoDB | 16 threads | 116997 | 11700 | 1.21 | 2.58 |
| S2 | SQL Server | 16 threads | 6080 | 608 | 13.40 | 67.67 |
| S2 | MongoDB | 16 threads | 32753 | 3275 | 3.31 | 11.35 |
| S2v | SQL Server (indexed views) | 16 threads | 55506 | 5551 | 2.54 | 4.03 |
| S2v | MongoDB | 16 threads | 28530 | 2853 | 3.58 | 14.10 |
| C2 race for last seats | SQL Server | 16 threads | 12981 | 2596 | 4.83 | 8.91 |
| C2 race for last seats | MongoDB | 16 threads | 31644 | 6329 | 1.86 | 4.71 |
