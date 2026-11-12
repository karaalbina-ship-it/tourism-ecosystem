# Tourism Ecosystem — Requirements

## Roles
- **Admin** — manages reference data (countries, cities, room types) and users.
- **Manager** — manages tours, departures, hotels, guides; oversees payments and reports.
- **Agent** — registers tourists, creates bookings, accepts payments.
- **Tourist** (external) — a traveler on a booking.
- **Guide** (external) — leads tour departures and excursions.

## Functional requirements
1. Geography reference: countries and cities.
2. Hotels in cities with a star rating and room offers (room type, capacity, price per night).
3. Tours: destination country, duration, base price, day-by-day itinerary (city and hotel for each day).
4. Tour departures: start and end dates, capacity, assigned guide, transfers (flight, bus, train, ferry) between cities.
5. Every tour price change is recorded in the price history automatically.
6. A booking is made for an open upcoming departure. It has a lead tourist and may include several travelers. Seats are limited by the departure capacity.
7. The traveler price is calculated automatically: children under 12 pay 50% of the tour price. Optional excursions can be added to a booking (price × persons).
8. The booking total is recalculated automatically when travelers or excursions change.
9. Booking payments: payment schedule and actual payment, debt and overdue payment tracking. A booking is confirmed automatically once fully paid.
10. Cancellation refund depends on the days left before the start: 30+ days — 100%, 14–29 — 50%, 7–13 — 25%, under 7 — 0%. Cancelled bookings free their seats.
11. A tourist can review a departure (rating 1–5) only after it ended and only if they traveled on it.
12. Reports: active tours, upcoming departures with free seats, booking details with debt, overdue payments, guide performance.

## Non-functional requirements
- **Soft delete**: tourists, guides, hotels, tours and excursions are never physically deleted — `IsDeleted = 1`, `DeletedAt`, `DeletedBy` are set instead. A plain `DELETE` on Tourists/Hotels/Tours is turned into a soft delete by a trigger.
- **Audit**: Tourists, Hotels, Tours, TourDepartures, Bookings and Payments store `CreatedAt/CreatedBy` and `UpdatedAt/UpdatedBy`.
- **Optimistic locking** of the tour price and the departure via `rowversion`.
- **Concurrency**: seat checks lock the departure row (`UPDLOCK`), so two parallel bookings cannot overbook it.
- The application accesses the database **only** through stored procedures (writes) and views / table-valued functions (reads).
- Several related changes (tourists → booking → travelers → excursions → payment) run atomically in one transaction (Unit of Work).
