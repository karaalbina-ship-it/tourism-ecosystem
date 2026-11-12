using System.Data;
using Dapper;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Data.Repositories;

public interface ITourRepository
{
    Task<TourView?> GetByIdAsync(int tourId);
    Task<IEnumerable<TourView>> GetActiveAsync();
    Task<int> CreateAsync(TourRequest request, int userId);
    Task UpdatePriceAsync(int tourId, decimal newPrice, byte[]? rowVersion, int userId);
    Task SoftDeleteAsync(int tourId, int userId);
}

internal sealed class TourRepository(DbSession db) : ITourRepository
{
    public Task<TourView?> GetByIdAsync(int tourId) =>
        db.Connection.QuerySingleOrDefaultAsync<TourView>(
            "SELECT * FROM dbo.vw_ActiveTours WHERE TourId = @tourId",
            new { tourId }, db.Transaction);

    public Task<IEnumerable<TourView>> GetActiveAsync() =>
        db.Connection.QueryAsync<TourView>(
            "SELECT * FROM dbo.vw_ActiveTours ORDER BY Title",
            transaction: db.Transaction);

    public Task<int> CreateAsync(TourRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Tour_Create",
            new { r.CountryId, r.Title, r.Description, r.DurationDays, r.BasePrice, r.Currency, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task UpdatePriceAsync(int tourId, decimal newPrice, byte[]? rowVersion, int userId)
    {
        var p = new DynamicParameters(new { TourId = tourId, NewPrice = newPrice, UserId = userId });
        p.Add("RowVersion", rowVersion, DbType.Binary, size: 8);
        return db.Connection.ExecuteAsync("dbo.sp_Tour_UpdatePrice", p,
            db.Transaction, commandType: CommandType.StoredProcedure);
    }

    public Task SoftDeleteAsync(int tourId, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Tour_SoftDelete",
            new { TourId = tourId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
