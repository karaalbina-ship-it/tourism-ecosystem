USE TourismEcosystemDb;
GO

ALTER TABLE dbo.Tours ADD TourType varchar(10) NOT NULL
    CONSTRAINT DF_Tours_TourType DEFAULT 'Excursion'
    CONSTRAINT CK_Tours_TourType CHECK (TourType IN ('Excursion', 'Beach', 'Ski', 'Cruise'));
GO

CREATE TABLE TourAttributes (
    TourId    int NOT NULL REFERENCES Tours(TourId),
    AttrKey   varchar(40) NOT NULL,
    NumValue  decimal(18,2) NULL,
    TextValue nvarchar(100) NULL,
    CONSTRAINT PK_TourAttributes PRIMARY KEY (TourId, AttrKey),
    CONSTRAINT CK_TourAttributes_Value CHECK (NumValue IS NOT NULL OR TextValue IS NOT NULL)
);
GO

CREATE NONCLUSTERED INDEX IX_TourAttributes_Num  ON dbo.TourAttributes(AttrKey, NumValue);
CREATE NONCLUSTERED INDEX IX_TourAttributes_Text ON dbo.TourAttributes(AttrKey, TextValue);
CREATE NONCLUSTERED INDEX IX_Tours_Type_Price
    ON dbo.Tours(TourType, BasePrice) INCLUDE (CountryId, Title) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_Tours_Country_Price
    ON dbo.Tours(CountryId, BasePrice) INCLUDE (TourType, Title) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_TourDepartures_Tour_Search
    ON dbo.TourDepartures(TourId, Status, StartDate) INCLUDE (Capacity, GuideId);
CREATE NONCLUSTERED INDEX IX_TourItinerary_City
    ON dbo.TourItinerary(CityId, TourId) INCLUDE (HotelId);
CREATE NONCLUSTERED INDEX IX_Reviews_Departure_Created
    ON dbo.Reviews(DepartureId, CreatedAt DESC) INCLUDE (TouristId, Rating);
GO

CREATE OR ALTER VIEW dbo.vw_OfferCards
AS
SELECT d.DepartureId, d.TourId, t.Title, t.Description, t.TourType,
       t.CountryId, c.Name AS Country,
       d.StartDate, d.EndDate, t.DurationDays, t.BasePrice AS Price, t.Currency, d.Status, d.Capacity,
       d.Capacity - ISNULL(bs.BookedSeats, 0) AS AvailableSeats,
       d.GuideId, g.FullName AS GuideName, g.Languages AS GuideLanguages,
       ISNULL(rv.ReviewsCount, 0) AS ReviewsCount, rv.AvgRating
FROM dbo.TourDepartures d
JOIN dbo.Tours t     ON t.TourId = d.TourId
JOIN dbo.Countries c ON c.CountryId = t.CountryId
LEFT JOIN dbo.Guides g ON g.GuideId = d.GuideId
OUTER APPLY (SELECT COUNT(*) AS BookedSeats
             FROM dbo.Bookings b
             JOIN dbo.BookingTravelers bt ON bt.BookingId = b.BookingId
             WHERE b.DepartureId = d.DepartureId AND b.Status <> N'Cancelled') bs
OUTER APPLY (SELECT COUNT(*) AS ReviewsCount, CAST(AVG(CAST(r.Rating AS decimal(5,2))) AS decimal(5,2)) AS AvgRating
             FROM dbo.Reviews r
             WHERE r.DepartureId = d.DepartureId) rv
WHERE t.IsDeleted = 0;
GO

CREATE OR ALTER PROCEDURE dbo.sp_OfferCard_Create
    @CountryId int, @Title nvarchar(200), @Description nvarchar(max) = NULL, @TourType varchar(10),
    @DurationDays tinyint, @BasePrice decimal(12,2), @Currency char(3),
    @GuideId int = NULL, @StartDate date, @Capacity smallint,
    @Itinerary nvarchar(max),
    @Attributes nvarchar(max),
    @PriceHistory nvarchar(max),
    @Transfers nvarchar(max),
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TourId int, @DepartureId int;

    BEGIN TRAN;
    INSERT dbo.Tours (CountryId, Title, Description, TourType, DurationDays, BasePrice, Currency, CreatedBy)
    VALUES (@CountryId, @Title, @Description, @TourType, @DurationDays, @BasePrice, @Currency, @UserId);
    SET @TourId = SCOPE_IDENTITY();

    INSERT dbo.TourItinerary (TourId, DayNo, CityId, HotelId, Description)
    SELECT @TourId, DayNo, CityId, HotelId, Description
    FROM OPENJSON(@Itinerary) WITH (DayNo tinyint, CityId int, HotelId int, Description nvarchar(500));

    INSERT dbo.TourAttributes (TourId, AttrKey, NumValue, TextValue)
    SELECT @TourId, [key],
           CASE type WHEN 2 THEN CAST(value AS decimal(18,2)) WHEN 3 THEN IIF(value = N'true', 1.0, 0.0) END,
           CASE WHEN type = 1 THEN value END
    FROM OPENJSON(@Attributes);

    INSERT dbo.TourPriceHistory (TourId, OldPrice, NewPrice, ChangedAt, ChangedBy)
    SELECT @TourId, OldPrice, NewPrice, ChangedAt, @UserId
    FROM OPENJSON(@PriceHistory) WITH (OldPrice decimal(12,2), NewPrice decimal(12,2), ChangedAt datetime2);

    INSERT dbo.TourDepartures (TourId, GuideId, StartDate, EndDate, Capacity, CreatedBy)
    VALUES (@TourId, @GuideId, @StartDate, DATEADD(day, @DurationDays - 1, @StartDate), @Capacity, @UserId);
    SET @DepartureId = SCOPE_IDENTITY();

    INSERT dbo.Transfers (DepartureId, TransportType, Carrier, FromCityId, ToCityId, DepartAt, ArriveAt)
    SELECT @DepartureId, TransportType, Carrier, FromCityId, ToCityId, DepartAt, ArriveAt
    FROM OPENJSON(@Transfers) WITH (TransportType nvarchar(10), Carrier nvarchar(100), FromCityId int, ToCityId int,
                                    DepartAt datetime2, ArriveAt datetime2);
    COMMIT;

    SELECT @DepartureId AS DepartureId;
END
GO
