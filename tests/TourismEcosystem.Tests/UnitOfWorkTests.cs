using Microsoft.Data.SqlClient;
using TourismEcosystem.Api.Data;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Tests;

public sealed class UnitOfWorkTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TOURISM_DB")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=TourismEcosystemDb;Trusted_Connection=True;TrustServerCertificate=True";

    private const int UserId = 3;
    private const int CountryId = 1;
    private const int ExcursionId = 1;

    private static async Task<int> NewDepartureAsync(IUnitOfWork uow, short capacity, int daysAhead = 30)
    {
        var tourId = await uow.Tours.CreateAsync(
            new TourRequest(CountryId, $"Тест {Guid.NewGuid():N}", null, 5, 10_000m), UserId);
        return await uow.Departures.CreateAsync(tourId,
            new DepartureRequest(null, DateTime.Today.AddDays(daysAhead), capacity), UserId);
    }

    private static TouristRequest Adult() => new($"Турист {Guid.NewGuid():N}", new DateTime(1990, 5, 1));
    private static TouristRequest Child() => new($"Дитина {Guid.NewGuid():N}", DateTime.Today.AddYears(-8));

    [Fact]
    public async Task Booking_Commit_SavesAllEntities()
    {
        int bookingId, adultId, childId;
        using (var uow = new UnitOfWork(ConnectionString))
        {
            uow.BeginTransaction();
            var departureId = await NewDepartureAsync(uow, 10);
            adultId = await uow.Tourists.CreateAsync(Adult(), UserId);
            childId = await uow.Tourists.CreateAsync(Child(), UserId);
            bookingId = await uow.Bookings.CreateAsync(departureId, adultId, UserId);
            await uow.Bookings.AddTravelerAsync(bookingId, childId, UserId);
            await uow.Bookings.AddExcursionAsync(bookingId, ExcursionId, 2, UserId);
            await uow.Payments.RegisterAsync(bookingId,
                new PaymentRequest(16_200m, DateTime.Today, DateTime.UtcNow, "Card"), UserId);
            uow.Commit();
        }

        using var check = new UnitOfWork(ConnectionString);
        Assert.NotNull(await check.Tourists.GetByIdAsync(adultId));
        Assert.NotNull(await check.Tourists.GetByIdAsync(childId));
        var booking = await check.Bookings.GetByIdAsync(bookingId);
        Assert.NotNull(booking);
        Assert.Equal(2, booking.TravelersCount);
        Assert.Equal(16_200m, booking.TotalAmount);
        Assert.Equal(0m, booking.Debt);
        Assert.Equal("Confirmed", booking.Status);
        Assert.Single(await check.Payments.GetByBookingAsync(bookingId));
    }

    [Fact]
    public async Task Booking_FailsOnPayment_RollsBackEverything()
    {
        int touristId, bookingId, departureId;
        using (var uow = new UnitOfWork(ConnectionString))
        {
            uow.BeginTransaction();
            departureId = await NewDepartureAsync(uow, 10);
            touristId = await uow.Tourists.CreateAsync(Adult(), UserId);
            bookingId = await uow.Bookings.CreateAsync(departureId, touristId, UserId);

            var ex = await Assert.ThrowsAsync<SqlException>(() => uow.Payments.RegisterAsync(bookingId,
                new PaymentRequest(50_000m, DateTime.Today, DateTime.UtcNow, "Cash"), UserId));
            Assert.Equal(50400, ex.Number);
            uow.Rollback();
        }

        using var check = new UnitOfWork(ConnectionString);
        Assert.Null(await check.Tourists.GetByIdAsync(touristId));
        Assert.Null(await check.Bookings.GetByIdAsync(bookingId));
        Assert.Null(await check.Departures.GetByIdAsync(departureId));
    }

    [Fact]
    public async Task AddTraveler_OverCapacity_Throws409_AndKeepsTransaction()
    {
        using var uow = new UnitOfWork(ConnectionString);
        uow.BeginTransaction();
        var departureId = await NewDepartureAsync(uow, 1);
        var first = await uow.Tourists.CreateAsync(Adult(), UserId);
        var second = await uow.Tourists.CreateAsync(Adult(), UserId);
        var bookingId = await uow.Bookings.CreateAsync(departureId, first, UserId);

        var ex = await Assert.ThrowsAsync<SqlException>(() => uow.Bookings.AddTravelerAsync(bookingId, second, UserId));

        Assert.Equal(50409, ex.Number);
        Assert.Equal(1, (await uow.Bookings.GetByIdAsync(bookingId))!.TravelersCount);
        Assert.Equal(0, (await uow.Departures.GetByIdAsync(departureId))!.AvailableSeats);
        uow.Rollback();
    }

    [Fact]
    public async Task Cancel_RefundsPaidAmount_AndFreesSeats()
    {
        using var uow = new UnitOfWork(ConnectionString);
        uow.BeginTransaction();
        var departureId = await NewDepartureAsync(uow, 5, daysAhead: 35);
        var touristId = await uow.Tourists.CreateAsync(Adult(), UserId);
        var bookingId = await uow.Bookings.CreateAsync(departureId, touristId, UserId);
        await uow.Payments.RegisterAsync(bookingId, new PaymentRequest(10_000m, DateTime.Today, DateTime.UtcNow, "Card"), UserId);

        var refund = await uow.Bookings.CancelAsync(bookingId, UserId);

        Assert.Equal(10_000m, refund);
        Assert.Equal("Cancelled", (await uow.Bookings.GetByIdAsync(bookingId))!.Status);
        Assert.Equal(5, (await uow.Departures.GetByIdAsync(departureId))!.AvailableSeats);
        uow.Rollback();
    }

    [Fact]
    public async Task UpdatePrice_WithStaleRowVersion_Throws409()
    {
        using var uow = new UnitOfWork(ConnectionString);
        uow.BeginTransaction();
        var tourId = await uow.Tours.CreateAsync(new TourRequest(CountryId, $"Тест {Guid.NewGuid():N}", null, 3, 9_000m), UserId);
        var stale = (await uow.Tours.GetByIdAsync(tourId))!.RowVersion;

        await uow.Tours.UpdatePriceAsync(tourId, 8_500m, stale, UserId);
        var ex = await Assert.ThrowsAsync<SqlException>(() => uow.Tours.UpdatePriceAsync(tourId, 8_000m, stale, UserId));

        Assert.Equal(50409, ex.Number);
        Assert.Equal(8_500m, (await uow.Tours.GetByIdAsync(tourId))!.BasePrice);
        uow.Rollback();
    }

    [Fact]
    public async Task SoftDelete_HidesTourist()
    {
        using var uow = new UnitOfWork(ConnectionString);
        uow.BeginTransaction();
        var touristId = await uow.Tourists.CreateAsync(Adult(), UserId);

        await uow.Tourists.SoftDeleteAsync(touristId, UserId);

        Assert.Null(await uow.Tourists.GetByIdAsync(touristId));
        uow.Rollback();
    }
}
