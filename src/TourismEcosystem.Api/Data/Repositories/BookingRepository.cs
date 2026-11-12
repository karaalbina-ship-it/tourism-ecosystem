using System.Data;
using Dapper;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Data.Repositories;

public interface IBookingRepository
{
    Task<BookingView?> GetByIdAsync(int bookingId);
    Task<int> CreateAsync(int departureId, int leadTouristId, int userId);
    Task AddTravelerAsync(int bookingId, int touristId, int userId);
    Task AddExcursionAsync(int bookingId, int excursionId, byte persons, int userId);
    Task<decimal> CancelAsync(int bookingId, int userId);
}

internal sealed class BookingRepository(DbSession db) : IBookingRepository
{
    public Task<BookingView?> GetByIdAsync(int bookingId) =>
        db.Connection.QuerySingleOrDefaultAsync<BookingView>(
            "SELECT * FROM dbo.vw_BookingDetails WHERE BookingId = @bookingId",
            new { bookingId }, db.Transaction);

    public Task<int> CreateAsync(int departureId, int leadTouristId, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Booking_Create",
            new { DepartureId = departureId, LeadTouristId = leadTouristId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task AddTravelerAsync(int bookingId, int touristId, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Booking_AddTraveler",
            new { BookingId = bookingId, TouristId = touristId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task AddExcursionAsync(int bookingId, int excursionId, byte persons, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Booking_AddExcursion",
            new { BookingId = bookingId, ExcursionId = excursionId, Persons = persons, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task<decimal> CancelAsync(int bookingId, int userId) =>
        db.Connection.ExecuteScalarAsync<decimal>("dbo.sp_Booking_Cancel",
            new { BookingId = bookingId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
