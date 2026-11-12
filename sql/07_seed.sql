USE TourismEcosystemDb;
GO
SET NOCOUNT ON;

DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);
DECLARE @Now datetime2 = SYSUTCDATETIME();

INSERT dbo.Roles (Name) VALUES (N'Admin'), (N'Manager'), (N'Agent');

INSERT dbo.Users (RoleId, Email, FullName, Phone) VALUES
 (1, N'admin@tourism.example',   N'Адміністратор Системи', N'+380500000001'),
 (2, N'manager@tourism.example', N'Олена Коваль',          N'+380500000002'),
 (3, N'agent1@tourism.example',  N'Андрій Мельник',        N'+380500000003'),
 (3, N'agent2@tourism.example',  N'Ірина Шевчук',          N'+380500000004'),
 (3, N'agent3@tourism.example',  N'Тарас Бондар',          N'+380500000005');

INSERT dbo.Countries (Name, IsoCode) VALUES
 (N'Україна', 'UA'), (N'Італія', 'IT'), (N'Туреччина', 'TR'), (N'Греція', 'GR'), (N'Польща', 'PL');

INSERT dbo.Cities (CountryId, Name) VALUES
 (1, N'Львів'), (1, N'Яремче'), (1, N'Кам''янець-Подільський'),
 (2, N'Рим'), (2, N'Флоренція'), (2, N'Венеція'),
 (3, N'Стамбул'), (4, N'Афіни'), (4, N'Санторіні'), (5, N'Краків'), (1, N'Київ');

INSERT dbo.Guides (FullName, Phone, Languages, ExperienceYears) VALUES
 (N'Олег Гнатюк',      N'+380671230001', N'uk, en',     9),
 (N'Марко Россі',      N'+390612340002', N'it, en, uk', 12),
 (N'Айше Йилмаз',      N'+905321230003', N'tr, en',     6),
 (N'Софія Панайоту',   N'+302101230004', N'el, en',     4);

INSERT dbo.Hotels (CityId, Name, Stars, Address, Phone, CreatedBy) VALUES
 (2,  N'Карпати Резорт',  4, N'вул. Свободи, 271',        N'+380343421001', 2),
 (1,  N'Старий Львів',    3, N'пл. Ринок, 12',            N'+380322001002', 2),
 (3,  N'Фортеця',         3, N'вул. Троїцька, 5',         N'+380384901003', 2),
 (4,  N'Roma Centro',     3, N'Via Nazionale, 18',        N'+390648001004', 2),
 (5,  N'Firenze Palace',  4, N'Via dei Calzaiuoli, 7',    N'+390552101005', 2),
 (7,  N'Bosphorus View',  4, N'Kennedy Cd. 40',           N'+902125101006', 2),
 (9,  N'Aegean Blue',     5, N'Fira, Main Street 3',      N'+302286101007', 2),
 (10, N'Wawel Inn',       3, N'ul. Grodzka 44',           N'+48124221008',  2);

INSERT dbo.RoomTypes (Name, Capacity) VALUES (N'Single', 1), (N'Double', 2), (N'Triple', 3), (N'Family', 4);

INSERT dbo.HotelRooms (HotelId, RoomTypeId, PricePerNight, RoomsCount) VALUES
 (1, 2, 2200.00, 40), (1, 4, 3600.00, 10),
 (2, 1, 1300.00, 12), (2, 2, 1900.00, 20),
 (3, 2, 1500.00, 15),
 (4, 1, 3100.00, 8),  (4, 2, 4200.00, 25),
 (5, 2, 5200.00, 30),
 (6, 2, 3900.00, 35), (6, 3, 4800.00, 6),
 (7, 2, 9800.00, 18),
 (8, 2, 2900.00, 22);

INSERT dbo.Tours (CountryId, Title, Description, DurationDays, BasePrice, CreatedBy) VALUES
 (1, N'Карпатська пригода',      N'Яремче, водоспади, Говерла',          5, 12000.00, 2),
 (2, N'Перлини Італії',          N'Рим, Флоренція, Венеція',             8, 45000.00, 2),
 (3, N'Стамбул вихідного дня',   N'Старе місто, Босфор, базари',         4, 18000.00, 2),
 (4, N'Грецькі острови',         N'Афіни та Санторіні',                  7, 38000.00, 2),
 (1, N'Замки Поділля',           N'Кам''янець-Подільський і Хотин',      3,  7500.00, 2),
 (5, N'Королівський Краків',     N'Вавель, Казімеж, соляна шахта',       4, 16000.00, 2);

EXEC dbo.sp_Tour_UpdatePrice @TourId = 2, @NewPrice = 43000.00, @UserId = 2;
EXEC dbo.sp_Tour_UpdatePrice @TourId = 2, @NewPrice = 42000.00, @UserId = 2;

INSERT dbo.TourItinerary (TourId, DayNo, CityId, HotelId, Description) VALUES
 (1, 1, 1, 2,    N'Прибуття до Львова, вечірня прогулянка'),
 (1, 2, 2, 1,    N'Переїзд до Яремче, водоспад Пробій'),
 (1, 3, 2, 1,    N'Сходження на Говерлу'),
 (1, 5, 2, NULL, N'Від''їзд'),
 (2, 1, 4, 4,    N'Рим: Колізей і Форум'),
 (2, 4, 5, 5,    N'Флоренція: Уффіці'),
 (2, 7, 6, NULL, N'Венеція: канали'),
 (3, 1, 7, 6,    N'Султанахмет'),
 (3, 2, 7, 6,    N'Круїз Босфором'),
 (4, 1, 8, NULL, N'Афіни: Акрополь'),
 (4, 3, 9, 7,    N'Пором на Санторіні'),
 (5, 1, 3, 3,    N'Кам''янецька фортеця'),
 (6, 1, 10, 8,   N'Вавель і Старе місто');

DECLARE @D1Start date = DATEADD(day, -20, @Today);
INSERT dbo.TourDepartures (TourId, GuideId, StartDate, EndDate, Capacity, Status, CreatedBy)
VALUES (1, 1, @D1Start, DATEADD(day, 4, @D1Start), 15, N'Closed', 2);

DECLARE @D2 date = DATEADD(day, 40, @Today), @D3 date = DATEADD(day, 60, @Today),
        @D4 date = DATEADD(day, 10, @Today), @D5 date = DATEADD(day, 90, @Today),
        @D6 date = DATEADD(day, 5, @Today);
EXEC dbo.sp_Departure_Create @TourId = 1, @GuideId = 1, @StartDate = @D2, @Capacity = 15, @UserId = 2;
EXEC dbo.sp_Departure_Create @TourId = 2, @GuideId = 2, @StartDate = @D3, @Capacity = 20, @UserId = 2;
EXEC dbo.sp_Departure_Create @TourId = 3, @GuideId = 3, @StartDate = @D4, @Capacity = 12, @UserId = 2;
EXEC dbo.sp_Departure_Create @TourId = 4, @GuideId = 4, @StartDate = @D5, @Capacity = 18, @UserId = 2;
EXEC dbo.sp_Departure_Create @TourId = 5, @GuideId = 1, @StartDate = @D6, @Capacity = 3,  @UserId = 2;

INSERT dbo.Transfers (DepartureId, TransportType, Carrier, FromCityId, ToCityId, DepartAt, ArriveAt) VALUES
 (1, N'Bus',    N'Карпати Тур', 11, 1,  DATEADD(hour,  -2, CAST(@D1Start AS datetime2)), DATEADD(hour, 7, CAST(@D1Start AS datetime2))),
 (2, N'Bus',    N'Карпати Тур', 11, 1,  DATEADD(hour,  -2, CAST(@D2 AS datetime2)),      DATEADD(hour, 7, CAST(@D2 AS datetime2))),
 (3, N'Flight', N'LOT',         10, 4,  DATEADD(hour,   8, CAST(@D3 AS datetime2)),      DATEADD(hour, 10, CAST(@D3 AS datetime2))),
 (3, N'Train',  N'Trenitalia',  4,  5,  DATEADD(day,    3, CAST(@D3 AS datetime2)),      DATEADD(hour, 74, CAST(@D3 AS datetime2))),
 (4, N'Flight', N'Pegasus',     10, 7,  DATEADD(hour,   6, CAST(@D4 AS datetime2)),      DATEADD(hour, 9, CAST(@D4 AS datetime2))),
 (5, N'Flight', N'Aegean',      10, 8,  DATEADD(hour,   7, CAST(@D5 AS datetime2)),      DATEADD(hour, 10, CAST(@D5 AS datetime2))),
 (5, N'Ferry',  N'Blue Star',   8,  9,  DATEADD(day,    2, CAST(@D5 AS datetime2)),      DATEADD(hour, 56, CAST(@D5 AS datetime2))),
 (6, N'Bus',    N'Поділля Тур', 11, 3,  DATEADD(hour,  -3, CAST(@D6 AS datetime2)),      DATEADD(hour, 3, CAST(@D6 AS datetime2)));

DECLARE @ChildBirth date = DATEADD(year, -8, @Today);
INSERT dbo.Tourists (FullName, BirthDate, PassportNo, Phone, Email, CreatedBy) VALUES
 (N'Оксана Ткаченко',  '1988-04-12', N'FX100001', N'+380932220001', N'tkachenko@mail.example', 3),
 (N'Дмитро Ткаченко',  '1986-09-30', N'FX100002', N'+380932220002', NULL,                     3),
 (N'Софія Ткаченко',   @ChildBirth,  N'FX100003', NULL,             NULL,                     3),
 (N'Наталія Кравець',  '1995-01-21', N'FX100004', N'+380932220004', N'kravets@mail.example',  4),
 (N'Віктор Поліщук',   '1979-07-07', N'FX100005', N'+380932220005', NULL,                     4),
 (N'Юлія Мороз',       '1999-11-02', N'FX100006', N'+380932220006', N'moroz@mail.example',    5),
 (N'Андрій Мороз',     '2001-03-15', N'FX100007', N'+380932220007', NULL,                     5),
 (N'Ігор Бондаренко',  '1970-12-25', N'FX100008', N'+380932220008', NULL,                     3);

INSERT dbo.Excursions (CityId, GuideId, Name, Price, DurationHours) VALUES
 (2, 1, N'Водоспад Пробій і ринок сувенірів', 600.00, 3.0),
 (4, 2, N'Колізей і Римський форум',         1500.00, 4.0),
 (5, 2, N'Галерея Уффіці',                   1800.00, 3.5),
 (7, 3, N'Круїз Босфором',                    900.00, 2.0),
 (8, 4, N'Акрополь',                         1200.00, 3.0),
 (3, 1, N'Кам''янецька фортеця',              400.00, 2.0);

INSERT dbo.Bookings (DepartureId, LeadTouristId, BookedAt, CreatedBy)
VALUES (1, 4, DATEADD(day, -60, @Now), 4);
INSERT dbo.BookingTravelers (BookingId, TouristId, Price) VALUES (1, 4, 12000.00), (1, 5, 12000.00);
DECLARE @PaidEarlier datetime2 = DATEADD(day, -55, @Now), @D1Due date = DATEADD(day, -55, @Today);
EXEC dbo.sp_Payment_Register @BookingId = 1, @Amount = 24000.00, @DueDate = @D1Due, @PaidAt = @PaidEarlier, @Method = N'Card', @UserId = 4;
EXEC dbo.sp_Review_Add @DepartureId = 1, @TouristId = 4, @Rating = 5, @Comment = N'Чудовий гід і маршрут';
EXEC dbo.sp_Review_Add @DepartureId = 1, @TouristId = 5, @Rating = 4, @Comment = NULL;

EXEC dbo.sp_Booking_Create @DepartureId = 2, @LeadTouristId = 1, @UserId = 3;
EXEC dbo.sp_Booking_AddTraveler @BookingId = 2, @TouristId = 2, @UserId = 3;
EXEC dbo.sp_Booking_AddTraveler @BookingId = 2, @TouristId = 3, @UserId = 3;
EXEC dbo.sp_Booking_AddExcursion @BookingId = 2, @ExcursionId = 1, @Persons = 3, @UserId = 3;
DECLARE @Due2 date = DATEADD(day, 20, @Today);
EXEC dbo.sp_Payment_Register @BookingId = 2, @Amount = 15000.00, @DueDate = @Today, @PaidAt = @Now, @Method = N'Card', @UserId = 3;
EXEC dbo.sp_Payment_Register @BookingId = 2, @Amount = 16800.00, @DueDate = @Due2, @Method = N'Card', @UserId = 3;

EXEC dbo.sp_Booking_Create @DepartureId = 3, @LeadTouristId = 6, @UserId = 5;
EXEC dbo.sp_Booking_AddTraveler @BookingId = 3, @TouristId = 7, @UserId = 5;
EXEC dbo.sp_Booking_AddExcursion @BookingId = 3, @ExcursionId = 2, @Persons = 2, @UserId = 5;
DECLARE @Due3 date = DATEADD(day, -3, @Today);
EXEC dbo.sp_Payment_Register @BookingId = 3, @Amount = 20000.00, @DueDate = @Today, @PaidAt = @Now, @Method = N'Transfer', @UserId = 5;
EXEC dbo.sp_Payment_Register @BookingId = 3, @Amount = 67000.00, @DueDate = @Due3, @Method = N'Transfer', @UserId = 5;

EXEC dbo.sp_Booking_Create @DepartureId = 4, @LeadTouristId = 8, @UserId = 3;
EXEC dbo.sp_Booking_AddExcursion @BookingId = 4, @ExcursionId = 4, @Persons = 1, @UserId = 3;
EXEC dbo.sp_Payment_Register @BookingId = 4, @Amount = 18900.00, @DueDate = @Today, @PaidAt = @Now, @Method = N'Cash', @UserId = 3;

EXEC dbo.sp_Booking_Create @DepartureId = 6, @LeadTouristId = 5, @UserId = 4;
EXEC dbo.sp_Payment_Register @BookingId = 5, @Amount = 7500.00, @DueDate = @Today, @PaidAt = @Now, @Method = N'Card', @UserId = 4;
GO
