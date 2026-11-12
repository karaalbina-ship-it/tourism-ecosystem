using System.Data;
using Dapper;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Data.Repositories;

public interface IDepartureRepository
{
    Task<DepartureView?> GetByIdAsync(int departureId);
    Task<IEnumerable<DepartureView>> GetUpcomingAsync(int? countryId);
    Task<IEnumerable<DepartureView>> GetByGuideAsync(int guideId);
    Task<int> CreateAsync(int tourId, DepartureRequest request, int userId);
    Task UpdateAsync(int departureId, DepartureUpdateRequest request, int userId);
}

internal sealed class DepartureRepository(DbSession db) : IDepartureRepository
{
    public Task<DepartureView?> GetByIdAsync(int departureId) =>
        db.Connection.QuerySingleOrDefaultAsync<DepartureView>(
            "SELECT * FROM dbo.vw_DepartureDetails WHERE DepartureId = @departureId",
            new { departureId }, db.Transaction);

    public Task<IEnumerable<DepartureView>> GetUpcomingAsync(int? countryId) =>
        countryId is { } id
            ? db.Connection.QueryAsync<DepartureView>(
                "SELECT * FROM dbo.fn_DeparturesByCountry(@id) ORDER BY StartDate",
                new { id }, db.Transaction)
            : db.Connection.QueryAsync<DepartureView>(
                "SELECT * FROM dbo.vw_UpcomingDepartures ORDER BY StartDate",
                transaction: db.Transaction);

    public Task<IEnumerable<DepartureView>> GetByGuideAsync(int guideId) =>
        db.Connection.QueryAsync<DepartureView>(
            "SELECT * FROM dbo.fn_DeparturesByGuide(@guideId) ORDER BY StartDate",
            new { guideId }, db.Transaction);

    public Task<int> CreateAsync(int tourId, DepartureRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Departure_Create",
            new { TourId = tourId, r.GuideId, StartDate = r.StartDate.Date, r.Capacity, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task UpdateAsync(int departureId, DepartureUpdateRequest r, int userId)
    {
        var p = new DynamicParameters(new { DepartureId = departureId, r.GuideId, r.Capacity, UserId = userId });
        p.Add("RowVersion", r.RowVersion, DbType.Binary, size: 8);
        return db.Connection.ExecuteAsync("dbo.sp_Departure_Update", p,
            db.Transaction, commandType: CommandType.StoredProcedure);
    }
}
