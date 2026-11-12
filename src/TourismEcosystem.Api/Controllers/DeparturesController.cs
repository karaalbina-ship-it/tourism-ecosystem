using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismEcosystem.Api.Data;
using TourismEcosystem.Api.Models;

namespace TourismEcosystem.Api.Controllers;

[ApiController]
[Route("api/departures")]
public sealed class DeparturesController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DepartureView>> Get(int id) =>
        await uow.Departures.GetByIdAsync(id) is { } d ? d : NotFound();

    [HttpGet]
    public Task<IEnumerable<DepartureView>> List([FromQuery] int? countryId, [FromQuery] int? guideId) =>
        guideId is { } g ? uow.Departures.GetByGuideAsync(g) : uow.Departures.GetUpcomingAsync(countryId);

    [HttpPost("~/api/tours/{tourId:int}/departures")]
    public async Task<ActionResult<DepartureView>> Create(int tourId, DepartureRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var id = await uow.Departures.CreateAsync(tourId, request, userId);
        return CreatedAtAction(nameof(Get), new { id }, await uow.Departures.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DepartureUpdateRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Departures.UpdateAsync(id, request, userId);
        return NoContent();
    }
}
