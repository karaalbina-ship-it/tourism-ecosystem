MATCH (n) DETACH DELETE n;

CREATE CONSTRAINT country_id IF NOT EXISTS FOR (c:Country) REQUIRE c.id IS UNIQUE;
CREATE CONSTRAINT city_id IF NOT EXISTS FOR (c:City) REQUIRE c.id IS UNIQUE;
CREATE CONSTRAINT hotel_id IF NOT EXISTS FOR (h:Hotel) REQUIRE h.id IS UNIQUE;
CREATE CONSTRAINT tour_id IF NOT EXISTS FOR (t:Tour) REQUIRE t.id IS UNIQUE;
CREATE CONSTRAINT tourist_id IF NOT EXISTS FOR (t:Tourist) REQUIRE t.id IS UNIQUE;
CREATE INDEX city_name IF NOT EXISTS FOR (c:City) ON (c.name);
CREATE INDEX hotel_stars IF NOT EXISTS FOR (h:Hotel) ON (h.stars);

RETURN '--- 1. Create countries and cities (UNWIND + CREATE)' AS step;
UNWIND [
  {id: 1, name: 'Україна'}, {id: 2, name: 'Польща'}, {id: 3, name: 'Туреччина'}, {id: 4, name: 'Єгипет'},
  {id: 5, name: 'Італія'}, {id: 6, name: 'Греція'}, {id: 7, name: 'Іспанія'}
] AS row
CREATE (:Country {id: row.id, name: row.name});

UNWIND [
  {id: 1, name: 'Київ', country: 1}, {id: 2, name: 'Львів', country: 1},
  {id: 3, name: 'Варшава', country: 2}, {id: 4, name: 'Краків', country: 2},
  {id: 5, name: 'Стамбул', country: 3}, {id: 6, name: 'Анталія', country: 3},
  {id: 7, name: 'Каїр', country: 4}, {id: 8, name: 'Хургада', country: 4},
  {id: 9, name: 'Рим', country: 5}, {id: 10, name: 'Флоренція', country: 5}, {id: 11, name: 'Венеція', country: 5},
  {id: 12, name: 'Афіни', country: 6}, {id: 13, name: 'Санторіні', country: 6},
  {id: 14, name: 'Барселона', country: 7}
] AS row
MATCH (country:Country {id: row.country})
CREATE (:City {id: row.id, name: row.name})-[:IN]->(country);

RETURN '--- 2. Create transfers between cities (relationships with properties)' AS step;
UNWIND [
  [1, 2, 'Train', 300, 900], [2, 1, 'Train', 300, 900], [1, 3, 'Bus', 900, 1800], [2, 4, 'Bus', 420, 1200],
  [3, 9, 'Flight', 150, 4500], [3, 12, 'Flight', 170, 5200], [3, 5, 'Flight', 165, 4800], [3, 14, 'Flight', 210, 6100],
  [4, 9, 'Flight', 140, 4100], [4, 6, 'Flight', 185, 5600], [4, 8, 'Flight', 260, 7200], [4, 12, 'Flight', 160, 4900],
  [5, 6, 'Flight', 80, 2100], [5, 7, 'Flight', 120, 3900], [7, 8, 'Bus', 360, 700],
  [9, 10, 'Train', 95, 1300], [10, 11, 'Train', 125, 1500], [11, 9, 'Train', 230, 1900],
  [12, 13, 'Ferry', 300, 2300], [13, 12, 'Ferry', 300, 2300], [9, 12, 'Flight', 135, 3600], [14, 9, 'Flight', 125, 3300]
] AS t
MATCH (a:City {id: t[0]}), (b:City {id: t[1]})
CREATE (a)-[:TRANSFER {type: t[2], minutes: t[3], price: t[4]}]->(b);

RETURN '--- 3. Create hotels, tours (VISITS with day), tourists, bookings and reviews' AS step;
UNWIND [
  {id: 1, name: 'Bosphorus View', stars: 4, city: 5}, {id: 2, name: 'Lara Palace', stars: 5, city: 6},
  {id: 3, name: 'Red Sea Resort', stars: 5, city: 8}, {id: 4, name: 'Roma Centro', stars: 3, city: 9},
  {id: 5, name: 'Firenze Palace', stars: 4, city: 10}, {id: 6, name: 'Canal Grande', stars: 4, city: 11},
  {id: 7, name: 'Plaka Inn', stars: 3, city: 12}, {id: 8, name: 'Aegean Blue', stars: 5, city: 13},
  {id: 9, name: 'Rambla Hotel', stars: 4, city: 14}, {id: 10, name: 'Старий Львів', stars: 3, city: 2}
] AS row
MATCH (c:City {id: row.city})
CREATE (:Hotel {id: row.id, name: row.name, stars: row.stars})-[:IN]->(c);

UNWIND [
  {id: 1, title: 'Перлини Італії', type: 'Excursion', price: 42000, days: 8, route: [[9, 1, 4], [10, 4, 5], [11, 7, 6]]},
  {id: 2, title: 'Грецькі острови', type: 'Beach', price: 38000, days: 7, route: [[12, 1, 7], [13, 3, 8]]},
  {id: 3, title: 'Стамбул і Анталія', type: 'Beach', price: 31000, days: 9, route: [[5, 1, 1], [6, 4, 2]]},
  {id: 4, title: 'Єгипет: Каїр і Червоне море', type: 'Beach', price: 35000, days: 8, route: [[7, 1, null], [8, 3, 3]]},
  {id: 5, title: 'Барселона і Рим', type: 'Excursion', price: 47000, days: 7, route: [[14, 1, 9], [9, 5, 4]]},
  {id: 6, title: 'Вихідні у Львові', type: 'Excursion', price: 6500, days: 3, route: [[2, 1, 10]]}
] AS row
CREATE (t:Tour {id: row.id, title: row.title, type: row.type, price: row.price, days: row.days})
WITH t, row
UNWIND row.route AS stop
MATCH (c:City {id: stop[0]})
CREATE (t)-[:VISITS {day: stop[1]}]->(c)
WITH t, stop
WHERE stop[2] IS NOT NULL
MATCH (h:Hotel {id: stop[2]})
MERGE (t)-[:STAYS_AT]->(h);

UNWIND range(1, 12) AS i
CREATE (:Tourist {id: i, name: 'Турист ' + i, city: CASE WHEN i % 3 = 0 THEN 'Львів' ELSE 'Київ' END});

UNWIND [
  [1, 1, 5], [1, 2, 4], [2, 1, 4], [2, 5, 5], [3, 2, 5], [3, 3, 4], [4, 1, 5], [4, 5, 4], [4, 2, null],
  [5, 3, 3], [5, 4, null], [6, 4, null], [7, 2, 5], [7, 3, 4], [8, 1, 3], [8, 2, 4], [9, 6, 5], [10, 6, 4],
  [10, 1, null], [11, 5, 5], [12, 3, null]
] AS b
MATCH (p:Tourist {id: b[0]}), (t:Tour {id: b[1]})
CREATE (p)-[:BOOKED {rating: b[2]}]->(t);

RETURN '--- 4. Count nodes by label and relationships by type' AS step;
MATCH (n) RETURN labels(n)[0] AS label, count(*) AS nodes ORDER BY label;
MATCH ()-[r]->() RETURN type(r) AS relationship, count(*) AS relationships ORDER BY relationship;

RETURN '--- 5. MATCH + WHERE: 4-5 star hotels in Italy and Greece' AS step;
MATCH (h:Hotel)-[:IN]->(c:City)-[:IN]->(country:Country)
WHERE country.name IN ['Італія', 'Греція'] AND h.stars >= 4
RETURN country.name AS country, c.name AS city, h.name AS hotel, h.stars AS stars
ORDER BY stars DESC, hotel;

RETURN '--- 6. Direct transfers from Kraków, cheapest first' AS step;
MATCH (:City {name: 'Краків'})-[t:TRANSFER]->(to:City)
RETURN to.name AS destination, t.type AS type, t.minutes AS minutes, t.price AS price
ORDER BY price;

RETURN '--- 7. Itinerary of a tour (relationship property day)' AS step;
MATCH (t:Tour {title: 'Перлини Італії'})-[v:VISITS]->(c:City)
OPTIONAL MATCH (t)-[:STAYS_AT]->(h:Hotel)-[:IN]->(c)
RETURN v.day AS day, c.name AS city, h.name AS hotel
ORDER BY day;

RETURN '--- 8. Routes Kyiv -> Santorini with at most 4 legs, no city twice (variable-length path)' AS step;
MATCH p = (:City {name: 'Київ'})-[:TRANSFER*1..4]->(:City {name: 'Санторіні'})
WHERE all(n IN nodes(p) WHERE single(m IN nodes(p) WHERE m = n))
RETURN [n IN nodes(p) | n.name] AS route,
       length(p) AS legs,
       reduce(total = 0, r IN relationships(p) | total + r.price) AS price,
       reduce(total = 0, r IN relationships(p) | total + r.minutes) AS minutes
ORDER BY price
LIMIT 5;

RETURN '--- 9. shortestPath by number of legs: Lviv -> Hurghada' AS step;
MATCH p = shortestPath((:City {name: 'Львів'})-[:TRANSFER*]->(:City {name: 'Хургада'}))
RETURN [n IN nodes(p) | n.name] AS route, length(p) AS legs,
       reduce(total = 0, r IN relationships(p) | total + r.price) AS price;

RETURN '--- 10. Cheapest route Lviv -> Hurghada (all paths, sum of price)' AS step;
MATCH p = (:City {name: 'Львів'})-[:TRANSFER*1..5]->(:City {name: 'Хургада'})
WHERE all(n IN nodes(p) WHERE single(m IN nodes(p) WHERE m = n))
WITH p, reduce(total = 0, r IN relationships(p) | total + r.price) AS price
RETURN [n IN nodes(p) | n.name] AS route, length(p) AS legs, price
ORDER BY price
LIMIT 3;

RETURN '--- 11. Which tours can be reached from Lviv with at most 2 legs' AS step;
MATCH (:City {name: 'Львів'})-[:TRANSFER*1..2]->(c:City)<-[v:VISITS {day: 1}]-(t:Tour)
RETURN DISTINCT t.title AS tour, c.name AS startCity, t.price AS price
ORDER BY price;

RETURN '--- 12. Aggregation: bookings and average rating per tour' AS step;
MATCH (t:Tour)
OPTIONAL MATCH (p:Tourist)-[b:BOOKED]->(t)
RETURN t.title AS tour, count(p) AS bookings, round(avg(b.rating), 2) AS avgRating
ORDER BY bookings DESC, tour;

RETURN '--- 13. Recommendation: tourists who booked "Перлини Італії" also booked' AS step;
MATCH (:Tour {title: 'Перлини Італії'})<-[:BOOKED]-(p:Tourist)-[:BOOKED]->(other:Tour)
WHERE other.title <> 'Перлини Італії'
RETURN other.title AS alsoBooked, count(DISTINCT p) AS tourists
ORDER BY tourists DESC, alsoBooked;

RETURN '--- 14. Personal recommendation for tourist 5: tours booked by similar tourists, not booked yet' AS step;
MATCH (me:Tourist {id: 5})-[:BOOKED]->(:Tour)<-[:BOOKED]-(similar:Tourist)-[:BOOKED]->(rec:Tour)
WHERE NOT (me)-[:BOOKED]->(rec)
RETURN rec.title AS recommended, count(DISTINCT similar) AS score
ORDER BY score DESC, recommended
LIMIT 3;

RETURN '--- 15. MERGE (upsert): add a new transfer, run twice — created only once' AS step;
MATCH (a:City {name: 'Львів'}), (b:City {name: 'Варшава'})
MERGE (a)-[t:TRANSFER {type: 'Train'}]->(b)
ON CREATE SET t.minutes = 600, t.price = 1400, t.created = true
ON MATCH SET t.created = false
RETURN a.name AS from, b.name AS to, t.price AS price, t.created AS created;
MATCH (a:City {name: 'Львів'}), (b:City {name: 'Варшава'})
MERGE (a)-[t:TRANSFER {type: 'Train'}]->(b)
ON CREATE SET t.minutes = 600, t.price = 1400, t.created = true
ON MATCH SET t.created = false
RETURN a.name AS from, b.name AS to, t.price AS price, t.created AS created;

RETURN '--- 16. SET: raise the price of all Beach tours by 5%' AS step;
MATCH (t:Tour {type: 'Beach'})
SET t.price = toInteger(round(t.price * 1.05))
RETURN t.title AS tour, t.price AS newPrice
ORDER BY tour;

RETURN '--- 17. DELETE a relationship and REMOVE a property' AS step;
MATCH (:Tourist {id: 12})-[b:BOOKED]->(:Tour {id: 3})
DELETE b
RETURN 'booking deleted' AS result;
MATCH ()-[t:TRANSFER]->() WHERE t.created IS NOT NULL
REMOVE t.created
RETURN count(t) AS cleanedTransfers;

RETURN '--- 18. Tours nobody rated yet (OPTIONAL MATCH + IS NULL)' AS step;
MATCH (t:Tour)
OPTIONAL MATCH (:Tourist)-[b:BOOKED]->(t) WHERE b.rating IS NOT NULL
WITH t, count(b) AS rated
WHERE rated = 0
RETURN t.title AS tour;

RETURN '--- 19. Cities reachable from Kyiv in exactly 2 legs, not directly' AS step;
MATCH (kyiv:City {name: 'Київ'})-[:TRANSFER*2]->(c:City)
WHERE c <> kyiv AND NOT (kyiv)-[:TRANSFER]->(c)
RETURN DISTINCT c.name AS city
ORDER BY city;

RETURN '--- 20. Indexes and how many times each was read (counter since the database start)' AS step;
SHOW INDEXES YIELD name, labelsOrTypes, properties, type, readCount
WHERE type <> 'LOOKUP'
RETURN name, labelsOrTypes, properties, type, readCount ORDER BY name;
