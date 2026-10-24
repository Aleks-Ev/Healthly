using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;

namespace HealthlyApi.Controllers;

// Показатели анализов внутри документа. Доступ: владелец документа, Doctor, Admin.
[ApiController]
[Route("api/results")]
[Tags("Test Results")]
[Authorize]
[Produces("application/json")]
public class TestResultsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TestResultsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Add a test result to a document</summary>
    /// <remarks>
    /// Send <c>isAbnormal: null</c> to detect it automatically from <c>referenceRange</c>
    /// (supported: <c>120-160</c>, <c>&lt;5</c>, <c>&lt;=5</c>, <c>&gt;1.2</c>, <c>&gt;=1.2</c>). Allowed for the owner, Doctor and Admin.
    /// </remarks>
    /// <response code="201">Result added.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    /// <response code="403">A patient tried to edit another user's document.</response>
    /// <response code="404">Document not found.</response>
    [HttpPost("/api/documents/{documentId:int}/results")]
    [ProducesResponseType<TestResultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestResultResponse>> AddResult(int documentId, TestResultRequest request)
    {
        var document = await _context.MedicalDocuments.FindAsync(documentId);

        if (document == null)
        {
            return Problem(detail: "Document not found", statusCode: StatusCodes.Status404NotFound);
        }

        if (!User.CanAccessUser(document.UserId))
        {
            return Forbid();
        }

        var result = new TestResult { MedicalDocumentId = documentId };
        Apply(result, request);
        _context.TestResults.Add(result);

        // Первые показатели внесены — документ считается обработанным
        if (document.Status is DocumentStatuses.Pending or DocumentStatuses.Processing)
        {
            document.Status = DocumentStatuses.Completed;
        }

        await _context.SaveChangesAsync();

        return Created($"/api/results/{result.Id}", result.ToResponse());
    }

    /// <summary>Update a test result</summary>
    /// <remarks>Full update. <c>isAbnormal: null</c> means: detect from <c>referenceRange</c>.</remarks>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    /// <response code="403">A patient tried to edit another user's result.</response>
    /// <response code="404">Test result not found.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<TestResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestResultResponse>> UpdateResult(int id, TestResultRequest request)
    {
        var result = await FindWithDocumentAsync(id);

        if (result == null)
        {
            return ResultNotFound();
        }

        if (!User.CanAccessUser(result.MedicalDocument!.UserId))
        {
            return Forbid();
        }

        Apply(result, request);
        await _context.SaveChangesAsync();

        return result.ToResponse();
    }

    /// <summary>Delete a test result</summary>
    /// <response code="204">Result deleted.</response>
    /// <response code="403">A patient tried to delete another user's result.</response>
    /// <response code="404">Test result not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResult(int id)
    {
        var result = await FindWithDocumentAsync(id);

        if (result == null)
        {
            return ResultNotFound();
        }

        if (!User.CanAccessUser(result.MedicalDocument!.UserId))
        {
            return Forbid();
        }

        _context.TestResults.Remove(result);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private Task<TestResult?> FindWithDocumentAsync(int id) =>
        _context.TestResults.Include(r => r.MedicalDocument).FirstOrDefaultAsync(r => r.Id == id);

    private static void Apply(TestResult result, TestResultRequest request)
    {
        result.TestName = request.TestName.Trim();
        result.Value = request.Value.Trim();
        result.Unit = request.Unit?.Trim() ?? string.Empty;
        result.ReferenceRange = request.ReferenceRange?.Trim() ?? string.Empty;
        result.IsAbnormal = request.IsAbnormal
            ?? ReferenceRangeEvaluator.IsAbnormal(result.Value, result.ReferenceRange)
            ?? false;
    }

    private ObjectResult ResultNotFound() => Problem(detail: "Test result not found", statusCode: StatusCodes.Status404NotFound);
}
