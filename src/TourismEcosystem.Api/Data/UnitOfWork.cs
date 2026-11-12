using Microsoft.Data.SqlClient;
using TourismEcosystem.Api.Data.Repositories;

namespace TourismEcosystem.Api.Data;

public interface IUnitOfWork : IDisposable
{
    ITouristRepository Tourists { get; }
    ITourRepository Tours { get; }
    IDepartureRepository Departures { get; }
    IBookingRepository Bookings { get; }
    IPaymentRepository Payments { get; }

    void BeginTransaction();
    void Commit();
    void Rollback();
}

internal sealed class DbSession(SqlConnection connection)
{
    public SqlConnection Connection { get; } = connection;
    public SqlTransaction? Transaction { get; set; }
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _db;

    public UnitOfWork(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        _db = new DbSession(connection);

        Tourists = new TouristRepository(_db);
        Tours = new TourRepository(_db);
        Departures = new DepartureRepository(_db);
        Bookings = new BookingRepository(_db);
        Payments = new PaymentRepository(_db);
    }

    public ITouristRepository Tourists { get; }
    public ITourRepository Tours { get; }
    public IDepartureRepository Departures { get; }
    public IBookingRepository Bookings { get; }
    public IPaymentRepository Payments { get; }

    public void BeginTransaction()
    {
        if (_db.Transaction is not null)
            throw new InvalidOperationException("Транзакцію вже розпочато.");
        _db.Transaction = _db.Connection.BeginTransaction();
    }

    public void Commit()
    {
        var tx = _db.Transaction ?? throw new InvalidOperationException("Транзакцію не розпочато.");
        tx.Commit();
        EndTransaction();
    }

    public void Rollback()
    {
        if (_db.Transaction?.Connection is not null)
            _db.Transaction.Rollback();
        EndTransaction();
    }

    private void EndTransaction()
    {
        _db.Transaction?.Dispose();
        _db.Transaction = null;
    }

    public void Dispose()
    {
        EndTransaction();
        _db.Connection.Dispose();
    }
}
