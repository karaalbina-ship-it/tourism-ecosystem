using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismEcosystem.Api.Data;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Controllers;

[ApiController]
public sealed class PaymentsController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("api/bookings/{bookingId:int}/payments")]
    public Task<IEnumerable<PaymentView>> ByBooking(int bookingId) => uow.Payments.GetByBookingAsync(bookingId);

    [HttpGet("api/payments/overdue")]
    public Task<IEnumerable<OverduePaymentView>> Overdue() => uow.Payments.GetOverdueAsync();

    [HttpPost("api/bookings/{bookingId:int}/payments")]
    public async Task<ActionResult<int>> Register(int bookingId, PaymentRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId) =>
        Created($"/api/bookings/{bookingId}/payments", await uow.Payments.RegisterAsync(bookingId, request, userId));

    [HttpPut("api/payments/{id:int}/paid")]
    public async Task<IActionResult> MarkPaid(int id, MarkPaidRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Payments.MarkPaidAsync(id, request.PaidAt, userId);
        return NoContent();
    }
}
