using System.Data;
using Dapper;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Data.Repositories;

public interface IPaymentRepository
{
    Task<IEnumerable<PaymentView>> GetByBookingAsync(int bookingId);
    Task<IEnumerable<OverduePaymentView>> GetOverdueAsync();
    Task<int> RegisterAsync(int bookingId, PaymentRequest request, int userId);
    Task MarkPaidAsync(int paymentId, DateTime? paidAt, int userId);
}

internal sealed class PaymentRepository(DbSession db) : IPaymentRepository
{
    public Task<IEnumerable<PaymentView>> GetByBookingAsync(int bookingId) =>
        db.Connection.QueryAsync<PaymentView>(
            "SELECT * FROM dbo.vw_PaymentsByBooking WHERE BookingId = @bookingId ORDER BY DueDate",
            new { bookingId }, db.Transaction);

    public Task<IEnumerable<OverduePaymentView>> GetOverdueAsync() =>
        db.Connection.QueryAsync<OverduePaymentView>(
            "SELECT * FROM dbo.vw_OverduePayments ORDER BY DaysOverdue DESC",
            transaction: db.Transaction);

    public Task<int> RegisterAsync(int bookingId, PaymentRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Payment_Register",
            new { BookingId = bookingId, r.Amount, DueDate = r.DueDate.Date, r.PaidAt, r.Method, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task MarkPaidAsync(int paymentId, DateTime? paidAt, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Payment_MarkPaid",
            new { PaymentId = paymentId, PaidAt = paidAt, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
