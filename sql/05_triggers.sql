USE TourismEcosystemDb;
GO

CREATE OR ALTER TRIGGER dbo.trg_Tours_PriceHistory
ON dbo.Tours
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(BasePrice) RETURN;

    INSERT dbo.TourPriceHistory (TourId, OldPrice, NewPrice, ChangedBy)
    SELECT i.TourId, d.BasePrice, i.BasePrice, i.UpdatedBy
    FROM inserted i
    JOIN deleted d ON d.TourId = i.TourId
    WHERE i.BasePrice <> d.BasePrice;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Tours_InsteadOfDelete
ON dbo.Tours
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE t
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = CAST(SESSION_CONTEXT(N'UserId') AS int)
    FROM dbo.Tours t
    JOIN deleted d ON d.TourId = t.TourId
    WHERE t.IsDeleted = 0;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Hotels_InsteadOfDelete
ON dbo.Hotels
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE h
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = CAST(SESSION_CONTEXT(N'UserId') AS int)
    FROM dbo.Hotels h
    JOIN deleted d ON d.HotelId = h.HotelId
    WHERE h.IsDeleted = 0;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Tourists_InsteadOfDelete
ON dbo.Tourists
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE t
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = CAST(SESSION_CONTEXT(N'UserId') AS int)
    FROM dbo.Tourists t
    JOIN deleted d ON d.TouristId = t.TouristId
    WHERE t.IsDeleted = 0;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_BookingTravelers_Sync
ON dbo.BookingTravelers
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1
               FROM (SELECT DISTINCT b.DepartureId
                     FROM inserted i
                     JOIN dbo.Bookings b ON b.BookingId = i.BookingId) x
               WHERE dbo.fn_GetAvailableSeats(x.DepartureId) < 0)
        THROW 50409, N'Перевищено місткість виїзду.', 1;

    UPDATE b
    SET TotalAmount = dbo.fn_CalcBookingTotal(b.BookingId)
    FROM dbo.Bookings b
    WHERE b.BookingId IN (SELECT BookingId FROM inserted UNION SELECT BookingId FROM deleted);
END
GO

CREATE OR ALTER TRIGGER dbo.trg_BookingExcursions_RecalcTotal
ON dbo.BookingExcursions
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE b
    SET TotalAmount = dbo.fn_CalcBookingTotal(b.BookingId)
    FROM dbo.Bookings b
    WHERE b.BookingId IN (SELECT BookingId FROM inserted UNION SELECT BookingId FROM deleted);
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Payments_ConfirmBooking
ON dbo.Payments
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE b
    SET Status = N'Confirmed', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = i.UserId
    FROM dbo.Bookings b
    JOIN (SELECT BookingId, MAX(COALESCE(UpdatedBy, CreatedBy)) AS UserId
          FROM inserted
          WHERE PaidAt IS NOT NULL
          GROUP BY BookingId) i ON i.BookingId = b.BookingId
    WHERE b.Status = N'Pending'
      AND b.TotalAmount > 0
      AND dbo.fn_GetBookingDebt(b.BookingId) <= 0;
END
GO
