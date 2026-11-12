using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismEcosystem.Api.Data;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Controllers;

[ApiController]
[Route("api/tourists")]
public sealed class TouristsController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TouristView>> Get(int id) =>
        await uow.Tourists.GetByIdAsync(id) is { } t ? t : NotFound();

    [HttpGet]
    public Task<IEnumerable<TouristView>> List() => uow.Tourists.GetAllAsync();

    [HttpPost]
    public async Task<ActionResult<TouristView>> Create(TouristRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var id = await uow.Tourists.CreateAsync(request, userId);
        return CreatedAtAction(nameof(Get), new { id }, await uow.Tourists.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TouristRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Tourists.UpdateAsync(id, request, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Tourists.SoftDeleteAsync(id, userId);
        return NoContent();
    }
}
