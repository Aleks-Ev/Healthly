using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;

namespace HealthlyApi.Controllers;

// Лаборатории-партнёры. Список нужен админу (привязать сотрудника) — создаёт, правит и удаляет тоже Admin.
// Описание, часы работы и телефон видят пациенты, выбирающие лабораторию в своём городе (CareController).
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
            .Select(l => new LaboratoryResponse(
                l.Id, l.Name, l.City, l.Address,
                _context.Users.Count(u => u.LaboratoryId == l.Id),
                l.Description, l.OpeningHours, l.Phone, l.AcceptingPatients,
                _context.CareConnections.Count(c => c.LaboratoryId == l.Id && c.Status == CareConnectionStatuses.Active)))
            .ToListAsync();

    /// <summary>Create a laboratory (Admin)</summary>
    /// <response code="201">Laboratory created.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    [HttpPost]
    [ProducesResponseType<LaboratoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LaboratoryResponse>> CreateLaboratory(LaboratoryRequest request)
    {
        var laboratory = new Laboratory();
        Apply(laboratory, request);
        _context.Laboratories.Add(laboratory);
        await _context.SaveChangesAsync();

        return Created($"/api/laboratories/{laboratory.Id}", ToResponse(laboratory, staffCount: 0, patientCount: 0));
    }

    /// <summary>Update a laboratory (Admin)</summary>
    /// <remarks>
    /// Full update. <c>description</c>, <c>openingHours</c> and <c>phone</c> are shown to patients choosing a laboratory
    /// in their city; <c>acceptingPatients: false</c> hides it from the suggestions (existing connections stay).
    /// </remarks>
    /// <response code="200">The updated laboratory.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    /// <response code="404">Laboratory not found.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<LaboratoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LaboratoryResponse>> UpdateLaboratory(int id, LaboratoryRequest request)
    {
        var laboratory = await _context.Laboratories.FindAsync(id);
        if (laboratory == null)
        {
            return Problem(detail: "Laboratory not found", statusCode: StatusCodes.Status404NotFound);
        }

        Apply(laboratory, request);
        await _context.SaveChangesAsync();

        var staffCount = await _context.Users.CountAsync(u => u.LaboratoryId == id);
        var patientCount = await _context.CareConnections.CountAsync(c => c.LaboratoryId == id && c.Status == CareConnectionStatuses.Active);
        return ToResponse(laboratory, staffCount, patientCount);
    }

    private static void Apply(Laboratory laboratory, LaboratoryRequest request)
    {
        laboratory.Name = request.Name.Trim();
        laboratory.City = request.City;
        laboratory.Address = request.Address?.Trim() ?? string.Empty;
        laboratory.Description = request.Description?.Trim() ?? string.Empty;
        laboratory.OpeningHours = request.OpeningHours?.Trim() ?? string.Empty;
        laboratory.Phone = string.IsNullOrWhiteSpace(request.Phone) ? string.Empty : UserMappings.NormalizePhone(request.Phone);
        laboratory.AcceptingPatients = request.AcceptingPatients;
    }

    private static LaboratoryResponse ToResponse(Laboratory l, int staffCount, int patientCount) =>
        new(l.Id, l.Name, l.City, l.Address, staffCount, l.Description, l.OpeningHours, l.Phone, l.AcceptingPatients, patientCount);

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
