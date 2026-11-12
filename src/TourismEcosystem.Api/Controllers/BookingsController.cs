using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismEcosystem.Api.Data;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingView>> Get(int id) =>
        await uow.Bookings.GetByIdAsync(id) is { } b ? b : NotFound();

    [HttpPost]
    public async Task<ActionResult<BookingResult>> Create(BookingRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var touristIds = new List<int>(request.TouristIds ?? []);
        if (touristIds.Count == 0 && (request.NewTourists ?? []).Length == 0)
            return BadRequest(new { error = "Потрібен хоча б один мандрівник." });

        uow.BeginTransaction();
        try
        {
            foreach (var tourist in request.NewTourists ?? [])
                touristIds.Add(await uow.Tourists.CreateAsync(tourist, userId));

            var bookingId = await uow.Bookings.CreateAsync(request.DepartureId, touristIds[0], userId);
            foreach (var touristId in touristIds.Skip(1))
                await uow.Bookings.AddTravelerAsync(bookingId, touristId, userId);
            foreach (var excursion in request.Excursions ?? [])
                await uow.Bookings.AddExcursionAsync(bookingId, excursion.ExcursionId, excursion.Persons, userId);

            int? paymentId = request.Payment is { } payment
                ? await uow.Payments.RegisterAsync(bookingId, payment, userId)
                : null;
            uow.Commit();

            return CreatedAtAction(nameof(Get), new { id = bookingId },
                new BookingResult(bookingId, [.. touristIds], paymentId));
        }
        catch
        {
            uow.Rollback();
            throw;
        }
    }

    [HttpPost("{id:int}/travelers")]
    public async Task<IActionResult> AddTraveler(int id, TravelerRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Bookings.AddTravelerAsync(id, request.TouristId, userId);
        return NoContent();
    }

    [HttpPost("{id:int}/excursions")]
    public async Task<IActionResult> AddExcursion(int id, ExcursionRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Bookings.AddExcursionAsync(id, request.ExcursionId, request.Persons, userId);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<CancelResult>> Cancel(int id, [FromHeader(Name = "X-User-Id"), BindRequired] int userId) =>
        new CancelResult(await uow.Bookings.CancelAsync(id, userId));
}
