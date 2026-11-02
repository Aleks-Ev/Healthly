using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;

namespace HealthlyApi.Controllers;

// Лаборатории-партнёры. Список нужен админу (привязать сотрудника) — создаёт и удаляет тоже Admin.
// Удаление лаборатории не удаляет документы и сотрудников: связь обнуляется (ON DELETE SET NULL)
[ApiController]
[Route("api/laboratories")]
[Tags("Laboratories")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class LaboratoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public LaboratoriesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>List laboratories (Admin)</summary>
    /// <response code="200">The laboratories with the number of their staff.</response>
    [HttpGet]
    [ProducesResponseType<List<LaboratoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LaboratoryResponse>>> GetLaboratories() =>
        await _context.Laboratories
            .AsNoTracking()
            .OrderBy(l => l.City).ThenBy(l => l.Name)
            .Select(l => new LaboratoryResponse(l.Id, l.Name, l.City, l.Address, _context.Users.Count(u => u.LaboratoryId == l.Id)))
            .ToListAsync();

    /// <summary>Create a laboratory (Admin)</summary>
    /// <response code="201">Laboratory created.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    [HttpPost]
    [ProducesResponseType<LaboratoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LaboratoryResponse>> CreateLaboratory(LaboratoryRequest request)
    {
        var laboratory = new Laboratory
        {
            Name = request.Name.Trim(),
            City = request.City,
            Address = request.Address?.Trim() ?? string.Empty,
        };
        _context.Laboratories.Add(laboratory);
        await _context.SaveChangesAsync();

        return Created($"/api/laboratories/{laboratory.Id}",
            new LaboratoryResponse(laboratory.Id, laboratory.Name, laboratory.City, laboratory.Address, 0));
    }

    /// <summary>Delete a laboratory (Admin)</summary>
    /// <remarks>Its documents stay with the patients; its staff lose access until they are assigned to another laboratory.</remarks>
    /// <response code="204">Laboratory deleted.</response>
    /// <response code="404">Laboratory not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLaboratory(int id)
    {
        var deleted = await _context.Laboratories.Where(l => l.Id == id).ExecuteDeleteAsync();
        return deleted == 0
            ? Problem(detail: "Laboratory not found", statusCode: StatusCodes.Status404NotFound)
            : NoContent();
    }
}
