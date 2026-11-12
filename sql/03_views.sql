USE TourismEcosystemDb;
GO

CREATE OR ALTER VIEW dbo.vw_ActiveTourists
AS
SELECT t.TouristId, t.FullName, t.BirthDate, t.PassportNo, t.Phone, t.Email,
       t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy
FROM dbo.Tourists t
WHERE t.IsDeleted = 0;
GO

CREATE OR ALTER VIEW dbo.vw_ActiveTours
AS
SELECT t.TourId, t.Title, t.Description, t.CountryId, c.Name AS Country,
       t.DurationDays, t.BasePrice, t.Currency, t.RowVersion,
       rv.AvgRating, ISNULL(rv.ReviewsCount, 0) AS ReviewsCount,
       t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy
FROM dbo.Tours t
JOIN dbo.Countries c ON c.CountryId = t.CountryId
OUTER APPLY (SELECT CAST(AVG(CAST(r.Rating AS decimal(3,2))) AS decimal(3,2)) AS AvgRating, COUNT(*) AS ReviewsCount
             FROM dbo.Reviews r
             JOIN dbo.TourDepartures d ON d.DepartureId = r.DepartureId
             WHERE d.TourId = t.TourId) rv
WHERE t.IsDeleted = 0;
GO

CREATE OR ALTER VIEW dbo.vw_DepartureDetails
AS
SELECT d.DepartureId, d.TourId, t.Title AS TourTitle, t.CountryId, c.Name AS Country,
       d.GuideId, g.FullName AS GuideName,
       d.StartDate, d.EndDate, d.Capacity,
       dbo.fn_GetAvailableSeats(d.DepartureId) AS AvailableSeats,
       t.BasePrice AS Price, t.Currency, d.Status, d.RowVersion,
       CAST(t.IsDeleted AS bit) AS TourDeleted,
       d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
FROM dbo.TourDepartures d
JOIN dbo.Tours t     ON t.TourId = d.TourId
JOIN dbo.Countries c ON c.CountryId = t.CountryId
LEFT JOIN dbo.Guides g ON g.GuideId = d.GuideId;
GO

CREATE OR ALTER VIEW dbo.vw_UpcomingDepartures
AS
SELECT *
FROM dbo.vw_DepartureDetails
WHERE Status = N'Open' AND TourDeleted = 0 AND StartDate > CAST(SYSUTCDATETIME() AS date);
GO

CREATE OR ALTER VIEW dbo.vw_BookingDetails
AS
SELECT b.BookingId, b.DepartureId, d.TourId, t.Title AS TourTitle, d.StartDate, d.EndDate,
       b.LeadTouristId, lt.FullName AS LeadTouristName,
       ISNULL(tr.TravelersCount, 0) AS TravelersCount,
       b.TotalAmount, ISNULL(paid.PaidAmount, 0) AS PaidAmount,
       b.TotalAmount - ISNULL(paid.PaidAmount, 0) AS Debt,
       b.RefundAmount, b.Status, b.BookedAt, b.CancelledAt,
       b.CreatedAt, b.CreatedBy, b.UpdatedAt, b.UpdatedBy
FROM dbo.Bookings b
JOIN dbo.TourDepartures d ON d.DepartureId = b.DepartureId
JOIN dbo.Tours t          ON t.TourId = d.TourId
JOIN dbo.Tourists lt      ON lt.TouristId = b.LeadTouristId
OUTER APPLY (SELECT COUNT(*) AS TravelersCount FROM dbo.BookingTravelers x WHERE x.BookingId = b.BookingId) tr
OUTER APPLY (SELECT SUM(p.Amount) AS PaidAmount
             FROM dbo.Payments p
             WHERE p.BookingId = b.BookingId AND p.PaidAt IS NOT NULL) paid;
GO

CREATE OR ALTER VIEW dbo.vw_PaymentsByBooking
AS
SELECT p.PaymentId, p.BookingId, b.LeadTouristId, t.FullName AS LeadTouristName,
       p.Amount, p.DueDate, p.PaidAt, p.Method,
       CAST(CASE WHEN p.PaidAt IS NULL THEN 0 ELSE 1 END AS bit) AS IsPaid,
       p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy
FROM dbo.Payments p
JOIN dbo.Bookings b ON b.BookingId = p.BookingId
JOIN dbo.Tourists t ON t.TouristId = b.LeadTouristId;
GO

CREATE OR ALTER VIEW dbo.vw_OverduePayments
AS
SELECT p.PaymentId, p.BookingId, t.FullName AS TouristName, t.Phone AS TouristPhone,
       p.Amount, p.DueDate,
       DATEDIFF(day, p.DueDate, CAST(SYSUTCDATETIME() AS date)) AS DaysOverdue
FROM dbo.Payments p
JOIN dbo.Bookings b ON b.BookingId = p.BookingId
JOIN dbo.Tourists t ON t.TouristId = b.LeadTouristId
WHERE p.PaidAt IS NULL
  AND p.DueDate < CAST(SYSUTCDATETIME() AS date)
  AND b.Status = N'Pending';
GO

CREATE OR ALTER VIEW dbo.vw_GuidePerformance
AS
SELECT g.GuideId, g.FullName AS GuideName, g.Languages, g.ExperienceYears,
       ISNULL(dp.DeparturesCount, 0) AS DeparturesCount,
       ISNULL(tr.TouristsServed, 0) AS TouristsServed,
       rv.AvgRating, ISNULL(rv.ReviewsCount, 0) AS ReviewsCount
FROM dbo.Guides g
OUTER APPLY (SELECT COUNT(*) AS DeparturesCount
             FROM dbo.TourDepartures d
             WHERE d.GuideId = g.GuideId AND d.Status <> N'Cancelled') dp
OUTER APPLY (SELECT COUNT(*) AS TouristsServed
             FROM dbo.TourDepartures d
             JOIN dbo.Bookings b         ON b.DepartureId = d.DepartureId AND b.Status <> N'Cancelled'
             JOIN dbo.BookingTravelers x ON x.BookingId = b.BookingId
             WHERE d.GuideId = g.GuideId AND d.EndDate < CAST(SYSUTCDATETIME() AS date)) tr
OUTER APPLY (SELECT CAST(AVG(CAST(r.Rating AS decimal(3,2))) AS decimal(3,2)) AS AvgRating, COUNT(*) AS ReviewsCount
             FROM dbo.Reviews r
             JOIN dbo.TourDepartures d ON d.DepartureId = r.DepartureId
             WHERE d.GuideId = g.GuideId) rv
WHERE g.IsDeleted = 0;
GO

CREATE OR ALTER FUNCTION dbo.fn_DeparturesByCountry (@CountryId int)
RETURNS TABLE
AS
RETURN SELECT * FROM dbo.vw_UpcomingDepartures WHERE CountryId = @CountryId;
GO

CREATE OR ALTER FUNCTION dbo.fn_DeparturesByGuide (@GuideId int)
RETURNS TABLE
AS
RETURN SELECT * FROM dbo.vw_DepartureDetails WHERE GuideId = @GuideId;
GO
