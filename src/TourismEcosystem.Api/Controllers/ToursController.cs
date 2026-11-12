using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismEcosystem.Api.Data;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Controllers;

[ApiController]
[Route("api/tours")]
public sealed class ToursController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TourView>> Get(int id) =>
        await uow.Tours.GetByIdAsync(id) is { } t ? t : NotFound();

    [HttpGet]
    public Task<IEnumerable<TourView>> List() => uow.Tours.GetActiveAsync();

    [HttpPost]
    public async Task<ActionResult<TourView>> Create(TourRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var id = await uow.Tours.CreateAsync(request, userId);
        return CreatedAtAction(nameof(Get), new { id }, await uow.Tours.GetByIdAsync(id));
    }

    [HttpPut("{id:int}/price")]
    public async Task<IActionResult> UpdatePrice(int id, UpdatePriceRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Tours.UpdatePriceAsync(id, request.NewPrice, request.RowVersion, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Tours.SoftDeleteAsync(id, userId);
        return NoContent();
    }
}
