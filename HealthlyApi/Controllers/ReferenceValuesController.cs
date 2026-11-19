using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;

namespace HealthlyApi.Controllers;

// Справочник норм — только чтение, для врачей и админа.
// Нормы правятся не через API, а в Data/reference-values.csv (медсоветники, см. Data/REFERENCE_VALUES.md):
// DbInitializer синхронизирует файл в таблицу при каждом запуске
[ApiController]
[Route("api/reference-values")]
[Tags("Reference values")]
[Authorize(Roles = Roles.Staff)]
[Produces("application/json")]
public class ReferenceValuesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReferenceValuesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>List reference values (Doctor, Admin)</summary>
    /// <remarks>
    /// Read-only view of the norms used to evaluate lab results. Rows with <c>isReviewed: false</c> are drafts
    /// that a medical advisor has not checked yet. The values are edited in <c>Data/reference-values.csv</c>, not via the API.
    /// </remarks>
    /// <response code="200">All rows, ordered by category, metric, sex and age.</response>
    [HttpGet]
    [ProducesResponseType<List<ReferenceValueResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReferenceValueResponse>>> GetReferenceValues()
    {
        var values = await _context.ReferenceMedicalValues
            .AsNoTracking()
            .OrderBy(v => v.Category).ThenBy(v => v.MetricName).ThenBy(v => v.Sex).ThenBy(v => v.AgeFrom)
            .ToListAsync();

        return values.Select(ReferenceValueResponse.From).ToList();
    }
}
