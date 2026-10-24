using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Services;

namespace HealthlyApi.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Get a user's health report</summary>
    /// <remarks>
    /// Counts of documents, results and out-of-range results, plus the latest value of each indicator
    /// (grouped by test name, case-insensitive; out-of-range indicators first). Allowed for the owner, Doctor and Admin.
    /// </remarks>
    /// <response code="200">The report.</response>
    /// <response code="403">A patient requested another user's report.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("/api/users/{userId:int}/report")]
    [ProducesResponseType<UserReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserReportResponse>> GetReport(int userId)
    {
        if (!User.CanAccessUser(userId))
        {
            return Forbid();
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        var documents = await _context.MedicalDocuments
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .Include(d => d.TestResults)
            .ToListAsync();

        var measurements = documents
            .SelectMany(d => d.TestResults.Select(r => (Result: r, Document: d)))
            .ToList();

        // Группируем по названию показателя без учёта регистра и берём самое свежее измерение
        var indicators = measurements
            .GroupBy(m => m.Result.TestName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latest = group.OrderByDescending(m => m.Document.UploadDate).ThenByDescending(m => m.Result.Id).First();
                return new ReportIndicator(
                    latest.Result.TestName,
                    latest.Result.Value,
                    latest.Result.Unit,
                    latest.Result.ReferenceRange,
                    latest.Result.IsAbnormal,
                    latest.Document.UploadDate,
                    group.Count(),
                    latest.Document.Id);
            })
            .OrderByDescending(i => i.IsAbnormal)
            .ThenBy(i => i.TestName)
            .ToList();

        return new UserReportResponse(
            user.ToResponse(),
            documents.Count,
            measurements.Count,
            measurements.Count(m => m.Result.IsAbnormal),
            documents.Count == 0 ? null : documents.Max(d => d.UploadDate),
            indicators);
    }

    /// <summary>Get indicator trends for charts</summary>
    /// <remarks>
    /// The history of each indicator, with points sorted by date (oldest first). Only numeric values are included.
    /// <c>referenceMin</c>/<c>referenceMax</c> are parsed from the reference range (<c>null</c> when that side has no bound).
    /// A chart needs at least two points. Allowed for the owner, Doctor and Admin.
    /// </remarks>
    /// <response code="200">One entry per indicator; the indicators with the most measurements come first.</response>
    /// <response code="403">A patient requested another user's trends.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("/api/users/{userId:int}/trends")]
    [ProducesResponseType<List<IndicatorTrend>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<IndicatorTrend>>> GetTrends(int userId)
    {
        if (!User.CanAccessUser(userId))
        {
            return Forbid();
        }

        if (!await _context.Users.AnyAsync(u => u.Id == userId))
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        var measurements = await _context.TestResults
            .AsNoTracking()
            .Where(r => r.MedicalDocument!.UserId == userId)
            .Select(r => new { r.TestName, r.Value, r.Unit, r.ReferenceRange, r.IsAbnormal, r.MedicalDocumentId, r.MedicalDocument!.UploadDate, r.Id })
            .ToListAsync();

        // Нечисловые значения ("positive", "not detected") на графике не показать — пропускаем их
        var trends = measurements
            .Select(m => (Measurement: m, IsNumber: ReferenceRangeEvaluator.TryParse(m.Value, out var number), Number: number))
            .Where(m => m.IsNumber)
            .GroupBy(m => m.Measurement.TestName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var points = group.OrderBy(m => m.Measurement.UploadDate).ThenBy(m => m.Measurement.Id).ToList();
                // Единицы и диапазон берём из самого свежего измерения
                var latest = points[^1].Measurement;
                var (min, max) = ReferenceRangeEvaluator.GetBounds(latest.ReferenceRange);
                return new IndicatorTrend(
                    latest.TestName,
                    latest.Unit,
                    latest.ReferenceRange,
                    min,
                    max,
                    points.Select(p => new TrendPoint(p.Measurement.MedicalDocumentId, p.Measurement.UploadDate, p.Number, p.Measurement.IsAbnormal)).ToList());
            })
            .OrderByDescending(t => t.Points.Count)
            .ThenBy(t => t.TestName)
            .ToList();

        return trends;
    }
}
