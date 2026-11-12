USE TourismEcosystemDb;
GO

CREATE OR ALTER FUNCTION dbo.fn_CalcTravelerPrice (@BasePrice decimal(12,2), @BirthDate date, @OnDate date)
RETURNS decimal(12,2)
AS
BEGIN
    DECLARE @Age int = DATEDIFF(year, @BirthDate, @OnDate)
                     - CASE WHEN DATEADD(year, DATEDIFF(year, @BirthDate, @OnDate), @BirthDate) > @OnDate THEN 1 ELSE 0 END;
    RETURN CASE WHEN @Age < 12 THEN ROUND(@BasePrice * 0.5, 2) ELSE @BasePrice END;
END
GO

CREATE OR ALTER FUNCTION dbo.fn_CalcRefund (@PaidAmount decimal(12,2), @StartDate date, @CancelDate date)
RETURNS decimal(12,2)
AS
BEGIN
    DECLARE @Days int = DATEDIFF(day, @CancelDate, @StartDate);
    RETURN ROUND(ISNULL(@PaidAmount, 0) * CASE WHEN @Days >= 30 THEN 1.00
                                              WHEN @Days >= 14 THEN 0.50
                                              WHEN @Days >= 7  THEN 0.25
                                              ELSE 0 END, 2);
END
GO

CREATE OR ALTER FUNCTION dbo.fn_GetAvailableSeats (@DepartureId int)
RETURNS int
AS
BEGIN
    RETURN (SELECT d.Capacity - (SELECT COUNT(*)
                                 FROM dbo.BookingTravelers bt
                                 JOIN dbo.Bookings b ON b.BookingId = bt.BookingId
                                 WHERE b.DepartureId = d.DepartureId AND b.Status <> N'Cancelled')
            FROM dbo.TourDepartures d
            WHERE d.DepartureId = @DepartureId);
END
GO

CREATE OR ALTER FUNCTION dbo.fn_CalcBookingTotal (@BookingId int)
RETURNS decimal(12,2)
AS
BEGIN
    RETURN ISNULL((SELECT SUM(Price) FROM dbo.BookingTravelers WHERE BookingId = @BookingId), 0)
         + ISNULL((SELECT SUM(UnitPrice * Persons) FROM dbo.BookingExcursions WHERE BookingId = @BookingId), 0);
END
GO

CREATE OR ALTER FUNCTION dbo.fn_GetBookingDebt (@BookingId int)
RETURNS decimal(12,2)
AS
BEGIN
    RETURN (SELECT b.TotalAmount - ISNULL((SELECT SUM(p.Amount)
                                           FROM dbo.Payments p
                                           WHERE p.BookingId = b.BookingId AND p.PaidAt IS NOT NULL), 0)
            FROM dbo.Bookings b
            WHERE b.BookingId = @BookingId);
END
GO
