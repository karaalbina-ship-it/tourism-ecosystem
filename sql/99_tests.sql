USE TourismEcosystemDb;
GO
SET NOCOUNT ON;
BEGIN TRAN;

EXEC sp_set_session_context @key = N'UserId', @value = 1;
DELETE FROM dbo.Tours WHERE TourId = 6;
DELETE FROM dbo.Hotels WHERE HotelId = 8;
DELETE FROM dbo.Tourists WHERE TouristId = 8;
IF NOT EXISTS (SELECT 1 FROM dbo.Tours WHERE TourId = 6 AND IsDeleted = 1 AND DeletedBy = 1)
    THROW 51000, N'FAIL 1: Tours видалено фізично або без DeletedBy', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Hotels WHERE HotelId = 8 AND IsDeleted = 1 AND DeletedBy = 1)
    THROW 51000, N'FAIL 1: Hotels видалено фізично або без DeletedBy', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Tourists WHERE TouristId = 8 AND IsDeleted = 1 AND DeletedBy = 1)
    THROW 51000, N'FAIL 1: Tourists видалено фізично або без DeletedBy', 1;
IF EXISTS (SELECT 1 FROM dbo.vw_ActiveTours WHERE TourId = 6) OR EXISTS (SELECT 1 FROM dbo.vw_ActiveTourists WHERE TouristId = 8)
    THROW 51000, N'FAIL 1: видалені рядки видно у представленнях', 1;
PRINT N'PASS 1: soft delete через INSTEAD OF DELETE (Tours, Hotels, Tourists)';

IF (SELECT COUNT(*) FROM dbo.TourPriceHistory WHERE TourId = 2 AND ChangedBy = 2) <> 2
    THROW 51000, N'FAIL 2: TourPriceHistory не заповнена тригером', 1;
PRINT N'PASS 2: trg_Tours_PriceHistory';

EXEC dbo.sp_Tourist_Update @TouristId = 1, @FullName = N'Оксана Ткаченко-Литвин', @BirthDate = '1988-04-12',
     @PassportNo = N'FX100001', @Phone = N'+380932220001', @UserId = 4;
IF NOT EXISTS (SELECT 1 FROM dbo.Tourists WHERE TouristId = 1 AND UpdatedBy = 4 AND UpdatedAt IS NOT NULL)
    THROW 51000, N'FAIL 3: UpdatedAt/UpdatedBy не встановлено', 1;
PRINT N'PASS 3: аудит UpdatedAt/UpdatedBy';

IF dbo.fn_CalcTravelerPrice(1000.00, '2014-06-15', '2026-06-14') <> 500.00
   OR dbo.fn_CalcTravelerPrice(1000.00, '2014-06-15', '2026-06-15') <> 1000.00
    THROW 51000, N'FAIL 4: fn_CalcTravelerPrice', 1;
IF (SELECT Price FROM dbo.BookingTravelers WHERE BookingId = 2 AND TouristId = 3) <> 6000.00
    THROW 51000, N'FAIL 4: дитяча знижка не застосована', 1;
IF (SELECT TotalAmount FROM dbo.Bookings WHERE BookingId = 2) <> 31800.00
    THROW 51000, N'FAIL 4: TotalAmount не перераховано тригерами', 1;
PRINT N'PASS 4: fn_CalcTravelerPrice + trg_BookingTravelers_Sync + trg_BookingExcursions_RecalcTotal';

IF (SELECT COUNT(*) FROM dbo.Bookings WHERE BookingId IN (1, 4, 5) AND Status = N'Confirmed') <> 3
   OR NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingId = 2 AND Status = N'Pending')
    THROW 51000, N'FAIL 5: trg_Payments_ConfirmBooking', 1;
IF dbo.fn_GetBookingDebt(2) <> 16800.00
    THROW 51000, N'FAIL 5: fn_GetBookingDebt', 1;
IF (SELECT COUNT(*) FROM dbo.vw_OverduePayments) <> 1
    THROW 51000, N'FAIL 5: vw_OverduePayments', 1;
PRINT N'PASS 5: trg_Payments_ConfirmBooking + fn_GetBookingDebt + vw_OverduePayments';

BEGIN TRY
    EXEC dbo.sp_Payment_Register @BookingId = 3, @Amount = 100000.00, @DueDate = '2026-01-01',
         @PaidAt = '2026-01-01', @Method = N'Cash', @UserId = 2;
    THROW 51000, N'FAIL 6: переплату прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50400 THROW;
END CATCH
PRINT N'PASS 6: sp_Payment_Register відхиляє переплату';

DECLARE @Rv binary(8) = (SELECT RowVersion FROM dbo.Tours WHERE TourId = 3);
EXEC dbo.sp_Tour_UpdatePrice @TourId = 3, @NewPrice = 17500.00, @RowVersion = @Rv, @UserId = 2;
BEGIN TRY
    EXEC dbo.sp_Tour_UpdatePrice @TourId = 3, @NewPrice = 17000.00, @RowVersion = @Rv, @UserId = 2;
    THROW 51000, N'FAIL 7: застарілий RowVersion прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH
BEGIN TRY
    EXEC dbo.sp_Departure_Update @DepartureId = 3, @GuideId = 2, @Capacity = 1, @UserId = 2;
    THROW 51000, N'FAIL 7: місткість менша за бронювання прийнята', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH
PRINT N'PASS 7: rowversion у sp_Tour_UpdatePrice + перевірка місткості у sp_Departure_Update';

EXEC dbo.sp_Booking_Create @DepartureId = 6, @LeadTouristId = 6, @UserId = 3;
DECLARE @NewBooking int = (SELECT MAX(BookingId) FROM dbo.Bookings);
EXEC dbo.sp_Booking_AddTraveler @BookingId = @NewBooking, @TouristId = 7, @UserId = 3;
IF dbo.fn_GetAvailableSeats(6) <> 0
    THROW 51000, N'FAIL 8: fn_GetAvailableSeats', 1;
BEGIN TRY
    EXEC dbo.sp_Booking_AddTraveler @BookingId = @NewBooking, @TouristId = 1, @UserId = 3;
    THROW 51000, N'FAIL 8: перебронювання прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH
PRINT N'PASS 8: місткість виїзду (fn_GetAvailableSeats, sp_Booking_AddTraveler)';

EXEC dbo.sp_Booking_Cancel @BookingId = 2, @UserId = 3;
EXEC dbo.sp_Booking_Cancel @BookingId = 5, @UserId = 4;
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingId = 2 AND Status = N'Cancelled' AND RefundAmount = 15000.00)
    THROW 51000, N'FAIL 9: повернення 100% за 40 днів', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingId = 5 AND Status = N'Cancelled' AND RefundAmount = 0)
    THROW 51000, N'FAIL 9: повернення 0% за 5 днів', 1;
IF dbo.fn_GetAvailableSeats(2) <> 15 OR dbo.fn_GetAvailableSeats(6) <> 1
    THROW 51000, N'FAIL 9: скасування не звільнило місця', 1;
IF dbo.fn_CalcRefund(1000.00, '2026-01-20', '2026-01-01') <> 500.00
   OR dbo.fn_CalcRefund(1000.00, '2026-01-10', '2026-01-01') <> 250.00
    THROW 51000, N'FAIL 9: fn_CalcRefund', 1;
PRINT N'PASS 9: sp_Booking_Cancel + fn_CalcRefund';

BEGIN TRY
    EXEC dbo.sp_Review_Add @DepartureId = 3, @TouristId = 6, @Rating = 5;
    THROW 51000, N'FAIL 10: відгук на незавершений тур прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH
BEGIN TRY
    EXEC dbo.sp_Review_Add @DepartureId = 1, @TouristId = 1, @Rating = 5;
    THROW 51000, N'FAIL 10: відгук від неучасника прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH
PRINT N'PASS 10: sp_Review_Add';

IF (SELECT COUNT(*) FROM dbo.fn_DeparturesByCountry(1)) <> 2
    THROW 51000, N'FAIL 11: fn_DeparturesByCountry', 1;
IF (SELECT COUNT(*) FROM dbo.fn_DeparturesByGuide(1)) <> 3
    THROW 51000, N'FAIL 11: fn_DeparturesByGuide', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.vw_GuidePerformance WHERE GuideId = 1 AND AvgRating = 4.50 AND TouristsServed = 2)
    THROW 51000, N'FAIL 11: vw_GuidePerformance', 1;
PRINT N'PASS 11: fn_DeparturesByCountry + fn_DeparturesByGuide + vw_GuidePerformance';

BEGIN TRY
    EXEC dbo.sp_Booking_Cancel @BookingId = 999, @UserId = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50404 THROW;
END CATCH
IF @@TRANCOUNT <> 1 OR XACT_STATE() <> 1
    THROW 51000, N'FAIL 12: зовнішня транзакція зламана', 1;
PRINT N'PASS 12: savepoint у sp_Booking_Cancel';

ROLLBACK;
GO

SET NOCOUNT ON;
BEGIN TRAN;
BEGIN TRY
    INSERT dbo.BookingTravelers (BookingId, TouristId, Price) VALUES (5, 1, 7500.00), (5, 2, 7500.00), (5, 3, 3750.00);
    THROW 51000, N'FAIL 13: тригер пропустив перебронювання', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
    IF @@TRANCOUNT > 0 ROLLBACK;
END CATCH
IF @@TRANCOUNT > 0 ROLLBACK;
PRINT N'PASS 13: trg_BookingTravelers_Sync блокує пряме перебронювання';
PRINT N'Усі перевірки пройдено.';
GO

SET STATISTICS IO ON;
SELECT DepartureId, TourId, Capacity FROM dbo.TourDepartures
WHERE Status = N'Open' AND StartDate BETWEEN '2026-01-01' AND '2027-12-31';
SELECT TourId, Title, BasePrice FROM dbo.Tours WHERE CountryId = 1 AND IsDeleted = 0;
SELECT BookingId, SUM(Amount) AS Paid FROM dbo.Payments
WHERE PaidAt IS NOT NULL GROUP BY BookingId;
SET STATISTICS IO OFF;
GO
