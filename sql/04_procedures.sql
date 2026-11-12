USE TourismEcosystemDb;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tourist_Create
    @FullName nvarchar(150), @BirthDate date, @PassportNo nvarchar(20) = NULL,
    @Phone nvarchar(20) = NULL, @Email nvarchar(255) = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF @BirthDate > CAST(SYSUTCDATETIME() AS date)
        THROW 50400, N'Дата народження не може бути в майбутньому.', 1;

    INSERT dbo.Tourists (FullName, BirthDate, PassportNo, Phone, Email, CreatedBy)
    VALUES (@FullName, @BirthDate, @PassportNo, @Phone, @Email, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS TouristId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tourist_Update
    @TouristId int, @FullName nvarchar(150), @BirthDate date, @PassportNo nvarchar(20) = NULL,
    @Phone nvarchar(20) = NULL, @Email nvarchar(255) = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF @BirthDate > CAST(SYSUTCDATETIME() AS date)
        THROW 50400, N'Дата народження не може бути в майбутньому.', 1;

    UPDATE dbo.Tourists
    SET FullName = @FullName, BirthDate = @BirthDate, PassportNo = @PassportNo, Phone = @Phone, Email = @Email,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE TouristId = @TouristId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Туриста не знайдено.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tourist_SoftDelete
    @TouristId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1
               FROM dbo.BookingTravelers bt
               JOIN dbo.Bookings b       ON b.BookingId = bt.BookingId
               JOIN dbo.TourDepartures d ON d.DepartureId = b.DepartureId
               WHERE bt.TouristId = @TouristId AND b.Status <> N'Cancelled'
                 AND d.EndDate >= CAST(SYSUTCDATETIME() AS date))
        THROW 50409, N'Турист має активне бронювання.', 1;

    UPDATE dbo.Tourists
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
    WHERE TouristId = @TouristId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Туриста не знайдено.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tour_Create
    @CountryId int, @Title nvarchar(200), @Description nvarchar(max) = NULL,
    @DurationDays tinyint, @BasePrice decimal(12,2), @Currency char(3) = 'UAH', @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Countries WHERE CountryId = @CountryId)
        THROW 50404, N'Країну не знайдено.', 1;

    INSERT dbo.Tours (CountryId, Title, Description, DurationDays, BasePrice, Currency, CreatedBy)
    VALUES (@CountryId, @Title, @Description, @DurationDays, @BasePrice, @Currency, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS TourId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tour_UpdatePrice
    @TourId int, @NewPrice decimal(12,2), @RowVersion binary(8) = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Tours
    SET BasePrice = @NewPrice, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE TourId = @TourId AND IsDeleted = 0
      AND (@RowVersion IS NULL OR RowVersion = @RowVersion);

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Tours WHERE TourId = @TourId AND IsDeleted = 0)
            THROW 50404, N'Тур не знайдено.', 1;
        THROW 50409, N'Тур змінено іншим користувачем.', 1;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tour_SoftDelete
    @TourId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1
               FROM dbo.TourDepartures d
               JOIN dbo.Bookings b ON b.DepartureId = d.DepartureId
               WHERE d.TourId = @TourId AND d.Status = N'Open' AND b.Status <> N'Cancelled'
                 AND d.EndDate >= CAST(SYSUTCDATETIME() AS date))
        THROW 50409, N'Тур має активні бронювання.', 1;

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Tour_SoftDelete;

        UPDATE dbo.Tours
        SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
        WHERE TourId = @TourId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
            THROW 50404, N'Тур не знайдено.', 1;

        UPDATE dbo.TourDepartures
        SET Status = N'Cancelled', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
        WHERE TourId = @TourId AND Status = N'Open';

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Tour_SoftDelete;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Departure_Create
    @TourId int, @GuideId int = NULL, @StartDate date, @Capacity smallint, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Duration tinyint = (SELECT DurationDays FROM dbo.Tours WHERE TourId = @TourId AND IsDeleted = 0);
    IF @Duration IS NULL
        THROW 50404, N'Тур не знайдено.', 1;
    IF @StartDate <= CAST(SYSUTCDATETIME() AS date)
        THROW 50400, N'Дата виїзду має бути в майбутньому.', 1;

    DECLARE @EndDate date = DATEADD(day, @Duration - 1, @StartDate);

    IF @GuideId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Guides WHERE GuideId = @GuideId AND IsDeleted = 0)
            THROW 50404, N'Гіда не знайдено.', 1;
        IF EXISTS (SELECT 1 FROM dbo.TourDepartures
                   WHERE GuideId = @GuideId AND Status <> N'Cancelled'
                     AND StartDate <= @EndDate AND EndDate >= @StartDate)
            THROW 50409, N'Гід зайнятий на інший виїзд у ці дати.', 1;
    END

    INSERT dbo.TourDepartures (TourId, GuideId, StartDate, EndDate, Capacity, CreatedBy)
    VALUES (@TourId, @GuideId, @StartDate, @EndDate, @Capacity, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS DepartureId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Departure_Update
    @DepartureId int, @GuideId int = NULL, @Capacity smallint, @RowVersion binary(8) = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @StartDate date, @EndDate date, @Booked int;
    SELECT @StartDate = StartDate, @EndDate = EndDate,
           @Booked = Capacity - dbo.fn_GetAvailableSeats(DepartureId)
    FROM dbo.TourDepartures
    WHERE DepartureId = @DepartureId AND Status = N'Open';

    IF @StartDate IS NULL
        THROW 50404, N'Відкритий виїзд не знайдено.', 1;
    IF @Capacity < @Booked
        THROW 50409, N'Місткість менша за кількість заброньованих місць.', 1;
    IF @GuideId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Guides WHERE GuideId = @GuideId AND IsDeleted = 0)
        THROW 50404, N'Гіда не знайдено.', 1;
    IF @GuideId IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.TourDepartures
                                        WHERE GuideId = @GuideId AND DepartureId <> @DepartureId
                                          AND Status <> N'Cancelled'
                                          AND StartDate <= @EndDate AND EndDate >= @StartDate)
        THROW 50409, N'Гід зайнятий на інший виїзд у ці дати.', 1;

    UPDATE dbo.TourDepartures
    SET GuideId = @GuideId, Capacity = @Capacity, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE DepartureId = @DepartureId AND (@RowVersion IS NULL OR RowVersion = @RowVersion);

    IF @@ROWCOUNT = 0
        THROW 50409, N'Виїзд змінено іншим користувачем.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_Create
    @DepartureId int, @LeadTouristId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @BookingId int, @StartDate date, @BasePrice decimal(12,2), @Status nvarchar(10), @BirthDate date;

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Booking_Create;

        SELECT @StartDate = d.StartDate, @BasePrice = t.BasePrice, @Status = d.Status
        FROM dbo.TourDepartures d WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.Tours t ON t.TourId = d.TourId
        WHERE d.DepartureId = @DepartureId AND t.IsDeleted = 0;

        IF @StartDate IS NULL
            THROW 50404, N'Виїзд не знайдено.', 1;
        IF @Status <> N'Open' OR @StartDate <= CAST(SYSUTCDATETIME() AS date)
            THROW 50409, N'Виїзд закритий для бронювання.', 1;

        SELECT @BirthDate = BirthDate FROM dbo.Tourists WHERE TouristId = @LeadTouristId AND IsDeleted = 0;
        IF @BirthDate IS NULL
            THROW 50404, N'Туриста не знайдено.', 1;
        IF dbo.fn_GetAvailableSeats(@DepartureId) < 1
            THROW 50409, N'Немає вільних місць.', 1;

        INSERT dbo.Bookings (DepartureId, LeadTouristId, CreatedBy)
        VALUES (@DepartureId, @LeadTouristId, @UserId);
        SET @BookingId = SCOPE_IDENTITY();

        INSERT dbo.BookingTravelers (BookingId, TouristId, Price)
        VALUES (@BookingId, @LeadTouristId, dbo.fn_CalcTravelerPrice(@BasePrice, @BirthDate, @StartDate));

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Booking_Create;
        THROW;
    END CATCH

    SELECT @BookingId AS BookingId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_AddTraveler
    @BookingId int, @TouristId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DepartureId int, @StartDate date, @BasePrice decimal(12,2), @Status nvarchar(10), @BirthDate date;

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Booking_AddTraveler;

        SELECT @DepartureId = d.DepartureId, @StartDate = d.StartDate, @BasePrice = t.BasePrice, @Status = b.Status
        FROM dbo.Bookings b
        JOIN dbo.TourDepartures d WITH (UPDLOCK, HOLDLOCK) ON d.DepartureId = b.DepartureId
        JOIN dbo.Tours t ON t.TourId = d.TourId
        WHERE b.BookingId = @BookingId;

        IF @DepartureId IS NULL
            THROW 50404, N'Бронювання не знайдено.', 1;
        IF @Status <> N'Pending'
            THROW 50409, N'Змінювати можна лише неоплачене бронювання.', 1;

        SELECT @BirthDate = BirthDate FROM dbo.Tourists WHERE TouristId = @TouristId AND IsDeleted = 0;
        IF @BirthDate IS NULL
            THROW 50404, N'Туриста не знайдено.', 1;
        IF EXISTS (SELECT 1 FROM dbo.BookingTravelers WHERE BookingId = @BookingId AND TouristId = @TouristId)
            THROW 50409, N'Турист уже є в цьому бронюванні.', 1;
        IF dbo.fn_GetAvailableSeats(@DepartureId) < 1
            THROW 50409, N'Немає вільних місць.', 1;

        INSERT dbo.BookingTravelers (BookingId, TouristId, Price)
        VALUES (@BookingId, @TouristId, dbo.fn_CalcTravelerPrice(@BasePrice, @BirthDate, @StartDate));

        UPDATE dbo.Bookings SET UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE BookingId = @BookingId;

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Booking_AddTraveler;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_AddExcursion
    @BookingId int, @ExcursionId int, @Persons tinyint, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Status nvarchar(10) = (SELECT Status FROM dbo.Bookings WHERE BookingId = @BookingId);
    DECLARE @Price decimal(12,2) = (SELECT Price FROM dbo.Excursions WHERE ExcursionId = @ExcursionId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50404, N'Бронювання не знайдено.', 1;
    IF @Status <> N'Pending'
        THROW 50409, N'Змінювати можна лише неоплачене бронювання.', 1;
    IF @Price IS NULL
        THROW 50404, N'Екскурсію не знайдено.', 1;
    IF @Persons > (SELECT COUNT(*) FROM dbo.BookingTravelers WHERE BookingId = @BookingId)
        THROW 50400, N'Кількість осіб більша за кількість мандрівників.', 1;
    IF EXISTS (SELECT 1 FROM dbo.BookingExcursions WHERE BookingId = @BookingId AND ExcursionId = @ExcursionId)
        THROW 50409, N'Екскурсію вже додано.', 1;

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Booking_AddExcursion;

        INSERT dbo.BookingExcursions (BookingId, ExcursionId, Persons, UnitPrice)
        VALUES (@BookingId, @ExcursionId, @Persons, @Price);

        UPDATE dbo.Bookings SET UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE BookingId = @BookingId;

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Booking_AddExcursion;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_Cancel
    @BookingId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Status nvarchar(10), @StartDate date, @Refund decimal(12,2);
    DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Booking_Cancel;

        SELECT @Status = b.Status, @StartDate = d.StartDate
        FROM dbo.Bookings b WITH (UPDLOCK)
        JOIN dbo.TourDepartures d ON d.DepartureId = b.DepartureId
        WHERE b.BookingId = @BookingId;

        IF @Status IS NULL
            THROW 50404, N'Бронювання не знайдено.', 1;
        IF @Status = N'Cancelled'
            THROW 50409, N'Бронювання вже скасовано.', 1;
        IF @StartDate <= @Today
            THROW 50409, N'Тур уже розпочався.', 1;

        SET @Refund = dbo.fn_CalcRefund(
            (SELECT SUM(Amount) FROM dbo.Payments WHERE BookingId = @BookingId AND PaidAt IS NOT NULL),
            @StartDate, @Today);

        UPDATE dbo.Bookings
        SET Status = N'Cancelled', CancelledAt = SYSUTCDATETIME(), RefundAmount = @Refund,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
        WHERE BookingId = @BookingId;

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Booking_Cancel;
        THROW;
    END CATCH

    SELECT @Refund AS RefundAmount;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_Register
    @BookingId int, @Amount decimal(12,2), @DueDate date, @PaidAt datetime2 = NULL,
    @Method nvarchar(10), @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingId = @BookingId AND Status <> N'Cancelled')
        THROW 50409, N'Бронювання не знайдено або скасоване.', 1;
    IF @PaidAt IS NOT NULL AND @Amount > dbo.fn_GetBookingDebt(@BookingId)
        THROW 50400, N'Сума платежу перевищує борг за бронюванням.', 1;

    INSERT dbo.Payments (BookingId, Amount, DueDate, PaidAt, Method, CreatedBy)
    VALUES (@BookingId, @Amount, @DueDate, @PaidAt, @Method, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS PaymentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_MarkPaid
    @PaymentId int, @PaidAt datetime2 = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @BookingId int, @Amount decimal(12,2);
    SELECT @BookingId = p.BookingId, @Amount = p.Amount
    FROM dbo.Payments p
    JOIN dbo.Bookings b ON b.BookingId = p.BookingId
    WHERE p.PaymentId = @PaymentId AND p.PaidAt IS NULL AND b.Status <> N'Cancelled';

    IF @BookingId IS NULL
        THROW 50404, N'Неоплачений платіж не знайдено.', 1;
    IF @Amount > dbo.fn_GetBookingDebt(@BookingId)
        THROW 50400, N'Сума платежу перевищує борг за бронюванням.', 1;

    UPDATE dbo.Payments
    SET PaidAt = ISNULL(@PaidAt, SYSUTCDATETIME()), UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE PaymentId = @PaymentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Review_Add
    @DepartureId int, @TouristId int, @Rating tinyint, @Comment nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.TourDepartures
                   WHERE DepartureId = @DepartureId AND EndDate < CAST(SYSUTCDATETIME() AS date))
        THROW 50409, N'Відгук можна залишити лише після завершення туру.', 1;
    IF NOT EXISTS (SELECT 1
                   FROM dbo.BookingTravelers bt
                   JOIN dbo.Bookings b ON b.BookingId = bt.BookingId
                   WHERE b.DepartureId = @DepartureId AND bt.TouristId = @TouristId AND b.Status <> N'Cancelled')
        THROW 50409, N'Турист не брав участі в цьому турі.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Reviews WHERE DepartureId = @DepartureId AND TouristId = @TouristId)
        THROW 50409, N'Відгук уже залишено.', 1;

    INSERT dbo.Reviews (DepartureId, TouristId, Rating, Comment)
    VALUES (@DepartureId, @TouristId, @Rating, @Comment);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS ReviewId;
END
GO
