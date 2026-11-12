# Neo4j: basic queries (item 6)

Neo4j is a **graph** database: data is stored as nodes and relationships, and both carry properties. A relationship is a stored pointer between two nodes, so following it costs the same regardless of the table size ("index-free adjacency"). This makes path and neighbourhood questions cheap, which are awkward in SQL (recursive CTEs over self-joins) and in MongoDB (`$graphLookup` without path costs).

Version: Neo4j 5.26 (Community, Docker image `neo4j:5`). Queries are written in **Cypher**. Commands — [neo4j/demo.cypher](../neo4j/demo.cypher), output — [neo4j/demo-output.txt](../neo4j/demo-output.txt). Run with `.\neo4j\run-demo.ps1` (the container is in `docker-compose.yml`, browser UI at http://localhost:7475, login `neo4j` / `Tourism_Graph1`).

## Graph model

```
(:Country)<-[:IN]-(:City)-[:TRANSFER {type, minutes, price}]->(:City)
(:Hotel {stars})-[:IN]->(:City)
(:Tour {type, price, days})-[:VISITS {day}]->(:City)
(:Tour)-[:STAYS_AT]->(:Hotel)
(:Tourist)-[:BOOKED {rating}]->(:Tour)
```

7 countries, 14 cities, 22 directed transfers (train, bus, flight, ferry) with travel time and price, 10 hotels, 6 tours, 12 tourists, 21 bookings.

## Queries

| # | Query | Cypher feature |
|---|---|---|
| — | Uniqueness constraints on `id` for every label, indexes on `City(name)` and `Hotel(stars)` | `CREATE CONSTRAINT`, `CREATE INDEX` |
| 1–3 | Load countries, cities, transfers, hotels, tours with itinerary, tourists, bookings | `UNWIND` a list + `MATCH` + `CREATE` nodes and relationships with properties |
| 4 | Count nodes per label and relationships per type | `labels()`, `type()`, `count(*)` |
| 5 | 4–5 star hotels in Italy and Greece | pattern over two hops `(Hotel)-[:IN]->(City)-[:IN]->(Country)`, `WHERE ... IN [...]` |
| 6 | Direct transfers from Kraków, cheapest first | relationship properties, `ORDER BY` |
| 7 | Day-by-day itinerary of a tour with hotels | relationship property `day`, `OPTIONAL MATCH` |
| 8 | All routes Kyiv → Santorini with up to 4 legs and no repeated city, with total price and time | variable-length path `[:TRANSFER*1..4]`, `nodes(p)`, `relationships(p)`, `reduce()` |
| 9 | Fewest changes Lviv → Hurghada | `shortestPath()` |
| 10 | Cheapest route Lviv → Hurghada | all paths + `reduce()` over `price`, `ORDER BY ... LIMIT` |
| 11 | Tours whose first day is reachable from Lviv in ≤ 2 legs | path + pattern back to `Tour` via `VISITS {day: 1}` |
| 12 | Bookings and average rating per tour | `OPTIONAL MATCH` + `count`, `avg`, `round` |
| 13 | "Tourists who booked *Перлини Італії* also booked…" | collaborative filtering over `Tour<-BOOKED-Tourist-BOOKED->Tour` |
| 14 | Personal recommendations for tourist 5 | the same plus `WHERE NOT (me)-[:BOOKED]->(rec)` |
| 15 | Add a transfer idempotently (run twice, created once) | `MERGE ... ON CREATE SET ... ON MATCH SET` |
| 16 | Raise prices of beach tours by 5% | `SET` |
| 17 | Remove a booking and a temporary property | `DELETE` relationship, `REMOVE` property |
| 18 | Tours nobody rated yet | `OPTIONAL MATCH` + `WITH ... WHERE count = 0` |
| 19 | Cities reachable from Kyiv in exactly 2 legs but not directly | fixed-length path `*2`, negative pattern |
| 20 | List of indexes with their read counters — proves the constraints' indexes and `City(name)` are used by the queries | `SHOW INDEXES YIELD ... readCount` |

## Why a graph for this part

Query 8 in SQL would be a recursive CTE that joins `Transfers` to itself once per leg, carries the visited cities in a string to avoid cycles and sums prices along the way; every extra leg multiplies the rows. In Cypher it is one pattern `(:City)-[:TRANSFER*1..4]->(:City)` with the cost computed by `reduce()`. Recommendations (13–14) are two hops over `BOOKED` relationships — no join tables, no self-joins.

The graph does not replace the other stores: payments stay in SQL Server, the searchable catalog lives in MongoDB. Neo4j holds the network (cities, transfers, who booked what), which is small compared with the catalog but is queried by paths.
