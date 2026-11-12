USE TourismEcosystemDb;
GO

CREATE TABLE Roles (
    RoleId int IDENTITY PRIMARY KEY,
    Name   nvarchar(50) NOT NULL CONSTRAINT UQ_Roles_Name UNIQUE
);

CREATE TABLE Users (
    UserId    int IDENTITY PRIMARY KEY,
    RoleId    int NOT NULL REFERENCES Roles(RoleId),
    Email     nvarchar(255) NOT NULL,
    FullName  nvarchar(150) NOT NULL,
    Phone     nvarchar(20) NULL,
    IsActive  bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Countries (
    CountryId int IDENTITY PRIMARY KEY,
    Name      nvarchar(100) NOT NULL CONSTRAINT UQ_Countries_Name UNIQUE,
    IsoCode   char(2) NOT NULL CONSTRAINT UQ_Countries_IsoCode UNIQUE
);

CREATE TABLE Cities (
    CityId    int IDENTITY PRIMARY KEY,
    CountryId int NOT NULL REFERENCES Countries(CountryId),
    Name      nvarchar(100) NOT NULL,
    CONSTRAINT UQ_Cities_Country_Name UNIQUE (CountryId, Name)
);

CREATE TABLE Tourists (
    TouristId  int IDENTITY PRIMARY KEY,
    FullName   nvarchar(150) NOT NULL,
    BirthDate  date NOT NULL CONSTRAINT CK_Tourists_BirthDate CHECK (BirthDate >= '1900-01-01'),
    PassportNo nvarchar(20) NULL,
    Phone      nvarchar(20) NULL,
    Email      nvarchar(255) NULL,
    CreatedAt  datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  int NOT NULL REFERENCES Users(UserId),
    UpdatedAt  datetime2 NULL,
    UpdatedBy  int NULL REFERENCES Users(UserId),
    IsDeleted  bit NOT NULL DEFAULT 0,
    DeletedAt  datetime2 NULL,
    DeletedBy  int NULL REFERENCES Users(UserId)
);

CREATE TABLE Guides (
    GuideId         int IDENTITY PRIMARY KEY,
    FullName        nvarchar(150) NOT NULL,
    Phone           nvarchar(20) NOT NULL,
    Languages       nvarchar(100) NOT NULL,
    ExperienceYears tinyint NOT NULL DEFAULT 0,
    IsDeleted       bit NOT NULL DEFAULT 0,
    DeletedAt       datetime2 NULL,
    DeletedBy       int NULL REFERENCES Users(UserId)
);

CREATE TABLE Hotels (
    HotelId   int IDENTITY PRIMARY KEY,
    CityId    int NOT NULL REFERENCES Cities(CityId),
    Name      nvarchar(150) NOT NULL,
    Stars     tinyint NOT NULL CONSTRAINT CK_Hotels_Stars CHECK (Stars BETWEEN 1 AND 5),
    Address   nvarchar(250) NOT NULL,
    Phone     nvarchar(20) NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy int NOT NULL REFERENCES Users(UserId),
    UpdatedAt datetime2 NULL,
    UpdatedBy int NULL REFERENCES Users(UserId),
    IsDeleted bit NOT NULL DEFAULT 0,
    DeletedAt datetime2 NULL,
    DeletedBy int NULL REFERENCES Users(UserId)
);

CREATE TABLE RoomTypes (
    RoomTypeId int IDENTITY PRIMARY KEY,
    Name       nvarchar(50) NOT NULL CONSTRAINT UQ_RoomTypes_Name UNIQUE,
    Capacity   tinyint NOT NULL CONSTRAINT CK_RoomTypes_Capacity CHECK (Capacity BETWEEN 1 AND 10)
);

CREATE TABLE HotelRooms (
    HotelRoomId   int IDENTITY PRIMARY KEY,
    HotelId       int NOT NULL REFERENCES Hotels(HotelId),
    RoomTypeId    int NOT NULL REFERENCES RoomTypes(RoomTypeId),
    PricePerNight decimal(12,2) NOT NULL CONSTRAINT CK_HotelRooms_Price CHECK (PricePerNight > 0),
    RoomsCount    smallint NOT NULL CONSTRAINT CK_HotelRooms_Count CHECK (RoomsCount > 0),
    CONSTRAINT UQ_HotelRooms_Hotel_Type UNIQUE (HotelId, RoomTypeId)
);

CREATE TABLE Tours (
    TourId       int IDENTITY PRIMARY KEY,
    CountryId    int NOT NULL REFERENCES Countries(CountryId),
    Title        nvarchar(200) NOT NULL,
    Description  nvarchar(max) NULL,
    DurationDays tinyint NOT NULL CONSTRAINT CK_Tours_Duration CHECK (DurationDays BETWEEN 1 AND 60),
    BasePrice    decimal(12,2) NOT NULL CONSTRAINT CK_Tours_Price CHECK (BasePrice > 0),
    Currency     char(3) NOT NULL DEFAULT 'UAH',
    RowVersion   rowversion,
    CreatedAt    datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy    int NOT NULL REFERENCES Users(UserId),
    UpdatedAt    datetime2 NULL,
    UpdatedBy    int NULL REFERENCES Users(UserId),
    IsDeleted    bit NOT NULL DEFAULT 0,
    DeletedAt    datetime2 NULL,
    DeletedBy    int NULL REFERENCES Users(UserId)
);

CREATE TABLE TourItinerary (
    ItineraryId int IDENTITY PRIMARY KEY,
    TourId      int NOT NULL REFERENCES Tours(TourId),
    DayNo       tinyint NOT NULL CONSTRAINT CK_TourItinerary_Day CHECK (DayNo >= 1),
    CityId      int NOT NULL REFERENCES Cities(CityId),
    HotelId     int NULL REFERENCES Hotels(HotelId),
    Description nvarchar(500) NOT NULL,
    CONSTRAINT UQ_TourItinerary_Tour_Day UNIQUE (TourId, DayNo)
);

CREATE TABLE TourDepartures (
    DepartureId int IDENTITY PRIMARY KEY,
    TourId      int NOT NULL REFERENCES Tours(TourId),
    GuideId     int NULL REFERENCES Guides(GuideId),
    StartDate   date NOT NULL,
    EndDate     date NOT NULL,
    Capacity    smallint NOT NULL CONSTRAINT CK_TourDepartures_Capacity CHECK (Capacity > 0),
    Status      nvarchar(10) NOT NULL DEFAULT N'Open'
                CONSTRAINT CK_TourDepartures_Status CHECK (Status IN (N'Open', N'Closed', N'Cancelled')),
    RowVersion  rowversion,
    CreatedAt   datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   int NOT NULL REFERENCES Users(UserId),
    UpdatedAt   datetime2 NULL,
    UpdatedBy   int NULL REFERENCES Users(UserId),
    CONSTRAINT CK_TourDepartures_Dates CHECK (EndDate >= StartDate)
);

CREATE TABLE TourPriceHistory (
    PriceHistoryId int IDENTITY PRIMARY KEY,
    TourId         int NOT NULL REFERENCES Tours(TourId),
    OldPrice       decimal(12,2) NOT NULL,
    NewPrice       decimal(12,2) NOT NULL,
    ChangedAt      datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangedBy      int NULL REFERENCES Users(UserId)
);

CREATE TABLE Transfers (
    TransferId    int IDENTITY PRIMARY KEY,
    DepartureId   int NOT NULL REFERENCES TourDepartures(DepartureId),
    TransportType nvarchar(10) NOT NULL
                  CONSTRAINT CK_Transfers_Type CHECK (TransportType IN (N'Flight', N'Bus', N'Train', N'Ferry')),
    Carrier       nvarchar(100) NOT NULL,
    FromCityId    int NOT NULL REFERENCES Cities(CityId),
    ToCityId      int NOT NULL REFERENCES Cities(CityId),
    DepartAt      datetime2 NOT NULL,
    ArriveAt      datetime2 NOT NULL,
    CONSTRAINT CK_Transfers_Cities CHECK (FromCityId <> ToCityId),
    CONSTRAINT CK_Transfers_Times CHECK (ArriveAt > DepartAt)
);

CREATE TABLE Bookings (
    BookingId     int IDENTITY PRIMARY KEY,
    DepartureId   int NOT NULL REFERENCES TourDepartures(DepartureId),
    LeadTouristId int NOT NULL REFERENCES Tourists(TouristId),
    TotalAmount   decimal(12,2) NOT NULL DEFAULT 0 CONSTRAINT CK_Bookings_Total CHECK (TotalAmount >= 0),
    Status        nvarchar(10) NOT NULL DEFAULT N'Pending'
                  CONSTRAINT CK_Bookings_Status CHECK (Status IN (N'Pending', N'Confirmed', N'Cancelled')),
    BookedAt      datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CancelledAt   datetime2 NULL,
    RefundAmount  decimal(12,2) NULL CONSTRAINT CK_Bookings_Refund CHECK (RefundAmount >= 0),
    CreatedAt     datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     int NOT NULL REFERENCES Users(UserId),
    UpdatedAt     datetime2 NULL,
    UpdatedBy     int NULL REFERENCES Users(UserId)
);

CREATE TABLE BookingTravelers (
    BookingId int NOT NULL REFERENCES Bookings(BookingId),
    TouristId int NOT NULL REFERENCES Tourists(TouristId),
    Price     decimal(12,2) NOT NULL CONSTRAINT CK_BookingTravelers_Price CHECK (Price >= 0),
    CONSTRAINT PK_BookingTravelers PRIMARY KEY (BookingId, TouristId)
);

CREATE TABLE Payments (
    PaymentId int IDENTITY PRIMARY KEY,
    BookingId int NOT NULL REFERENCES Bookings(BookingId),
    Amount    decimal(12,2) NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    DueDate   date NOT NULL,
    PaidAt    datetime2 NULL,
    Method    nvarchar(10) NOT NULL CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Cash', N'Card', N'Transfer')),
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy int NOT NULL REFERENCES Users(UserId),
    UpdatedAt datetime2 NULL,
    UpdatedBy int NULL REFERENCES Users(UserId)
);

CREATE TABLE Excursions (
    ExcursionId   int IDENTITY PRIMARY KEY,
    CityId        int NOT NULL REFERENCES Cities(CityId),
    GuideId       int NULL REFERENCES Guides(GuideId),
    Name          nvarchar(150) NOT NULL,
    Price         decimal(12,2) NOT NULL CONSTRAINT CK_Excursions_Price CHECK (Price > 0),
    DurationHours decimal(4,1) NOT NULL CONSTRAINT CK_Excursions_Duration CHECK (DurationHours > 0),
    IsDeleted     bit NOT NULL DEFAULT 0,
    DeletedAt     datetime2 NULL,
    DeletedBy     int NULL REFERENCES Users(UserId)
);

CREATE TABLE BookingExcursions (
    BookingId   int NOT NULL REFERENCES Bookings(BookingId),
    ExcursionId int NOT NULL REFERENCES Excursions(ExcursionId),
    Persons     tinyint NOT NULL CONSTRAINT CK_BookingExcursions_Persons CHECK (Persons > 0),
    UnitPrice   decimal(12,2) NOT NULL CONSTRAINT CK_BookingExcursions_Price CHECK (UnitPrice > 0),
    CONSTRAINT PK_BookingExcursions PRIMARY KEY (BookingId, ExcursionId)
);

CREATE TABLE Reviews (
    ReviewId    int IDENTITY PRIMARY KEY,
    DepartureId int NOT NULL REFERENCES TourDepartures(DepartureId),
    TouristId   int NOT NULL REFERENCES Tourists(TouristId),
    Rating      tinyint NOT NULL CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    Comment     nvarchar(1000) NULL,
    CreatedAt   datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Reviews_Departure_Tourist UNIQUE (DepartureId, TouristId)
);
GO
