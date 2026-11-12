using System.Data;
using Dapper;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Data.Repositories;

public interface ITouristRepository
{
    Task<TouristView?> GetByIdAsync(int touristId);
    Task<IEnumerable<TouristView>> GetAllAsync();
    Task<int> CreateAsync(TouristRequest request, int userId);
    Task UpdateAsync(int touristId, TouristRequest request, int userId);
    Task SoftDeleteAsync(int touristId, int userId);
}

internal sealed class TouristRepository(DbSession db) : ITouristRepository
{
    public Task<TouristView?> GetByIdAsync(int touristId) =>
        db.Connection.QuerySingleOrDefaultAsync<TouristView>(
            "SELECT * FROM dbo.vw_ActiveTourists WHERE TouristId = @touristId",
            new { touristId }, db.Transaction);

    public Task<IEnumerable<TouristView>> GetAllAsync() =>
        db.Connection.QueryAsync<TouristView>(
            "SELECT * FROM dbo.vw_ActiveTourists ORDER BY FullName",
            transaction: db.Transaction);

    public Task<int> CreateAsync(TouristRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Tourist_Create",
            new { r.FullName, BirthDate = r.BirthDate.Date, r.PassportNo, r.Phone, r.Email, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task UpdateAsync(int touristId, TouristRequest r, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Tourist_Update",
            new { TouristId = touristId, r.FullName, BirthDate = r.BirthDate.Date, r.PassportNo, r.Phone, r.Email, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task SoftDeleteAsync(int touristId, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Tourist_SoftDelete",
            new { TouristId = touristId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
