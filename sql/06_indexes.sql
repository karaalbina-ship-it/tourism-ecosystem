USE TourismEcosystemDb;
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_Users_Email ON dbo.Users(Email);

CREATE UNIQUE NONCLUSTERED INDEX UX_Tourists_Passport_Active
    ON dbo.Tourists(PassportNo) WHERE IsDeleted = 0 AND PassportNo IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_Tours_Country_Active
    ON dbo.Tours(CountryId) INCLUDE (Title, BasePrice, DurationDays) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_Hotels_City_Active
    ON dbo.Hotels(CityId) INCLUDE (Name, Stars) WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_TourDepartures_Status_StartDate
    ON dbo.TourDepartures(Status, StartDate) INCLUDE (TourId, GuideId, Capacity);

CREATE NONCLUSTERED INDEX IX_Cities_Country             ON dbo.Cities(CountryId);
CREATE NONCLUSTERED INDEX IX_TourDepartures_Tour        ON dbo.TourDepartures(TourId);
CREATE NONCLUSTERED INDEX IX_TourDepartures_Guide       ON dbo.TourDepartures(GuideId, StartDate) INCLUDE (EndDate, Status);
CREATE NONCLUSTERED INDEX IX_TourPriceHistory_Tour      ON dbo.TourPriceHistory(TourId);
CREATE NONCLUSTERED INDEX IX_Transfers_Departure        ON dbo.Transfers(DepartureId);
CREATE NONCLUSTERED INDEX IX_Bookings_Departure         ON dbo.Bookings(DepartureId) INCLUDE (Status);
CREATE NONCLUSTERED INDEX IX_Bookings_LeadTourist       ON dbo.Bookings(LeadTouristId);
CREATE NONCLUSTERED INDEX IX_BookingTravelers_Tourist   ON dbo.BookingTravelers(TouristId);
CREATE NONCLUSTERED INDEX IX_BookingExcursions_Excursion ON dbo.BookingExcursions(ExcursionId);
CREATE NONCLUSTERED INDEX IX_Payments_Booking           ON dbo.Payments(BookingId) INCLUDE (Amount, PaidAt);
CREATE NONCLUSTERED INDEX IX_Excursions_City            ON dbo.Excursions(CityId);
CREATE NONCLUSTERED INDEX IX_Reviews_Tourist            ON dbo.Reviews(TouristId);

CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Payments
    ON dbo.Payments(BookingId, Amount, DueDate, PaidAt, Method);
GO
