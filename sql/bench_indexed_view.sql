USE TourismEcosystemDb;
GO
CREATE OR ALTER VIEW dbo.vw_OfferSearch
WITH SCHEMABINDING
AS
SELECT d.DepartureId, d.TourId, t.CountryId, t.TourType, d.Status, d.StartDate, d.Capacity, t.BasePrice
FROM dbo.TourDepartures d
JOIN dbo.Tours t ON t.TourId = d.TourId
WHERE t.IsDeleted = 0;
GO
CREATE UNIQUE CLUSTERED INDEX CX_OfferSearch ON dbo.vw_OfferSearch(DepartureId);
CREATE NONCLUSTERED INDEX IX_OfferSearch_Search
    ON dbo.vw_OfferSearch(CountryId, Status, BasePrice, DepartureId) INCLUDE (StartDate, Capacity, TourId);
GO
CREATE OR ALTER VIEW dbo.vw_DepartureSeats
WITH SCHEMABINDING
AS
SELECT b.DepartureId, COUNT_BIG(*) AS BookedSeats
FROM dbo.Bookings b
JOIN dbo.BookingTravelers bt ON bt.BookingId = b.BookingId
WHERE b.Status <> N'Cancelled'
GROUP BY b.DepartureId;
GO
CREATE UNIQUE CLUSTERED INDEX CX_DepartureSeats ON dbo.vw_DepartureSeats(DepartureId);
GO
