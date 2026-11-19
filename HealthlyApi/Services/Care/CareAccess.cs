using System.Security.Claims;
using HealthlyApi.Data;
using HealthlyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthlyApi.Services.Care;

// Доступ к медкарте с учётом связей «пациент ↔ врач».
// Сам пациент и Admin — всегда; врач — только если у него с пациентом активная связь (пациент подтвердил,
// что делится данными); сотрудник лаборатории — только к документам, которые загрузила его лаборатория.
// Решение «можно / нельзя» — в ClaimsPrincipalExtensions.CanAccessUser/CanAccessDocument (чистые функции, есть тесты),
// здесь — только запрос к базе «лечит ли этот врач этого пациента».
public class CareAccess
{
    private readonly AppDbContext _context;

    public CareAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsTreatingDoctorAsync(ClaimsPrincipal user, int patientId, CancellationToken cancellationToken = default)
    {
        if (!user.IsInRole(Roles.Doctor) || user.GetUserId() is not { } doctorId)
        {
            return false;
        }

        return await _context.CareConnections.AnyAsync(
            c => c.DoctorId == doctorId && c.PatientId == patientId && c.Status == CareConnectionStatuses.Active,
            cancellationToken);
    }

    public async Task<bool> CanAccessUserAsync(ClaimsPrincipal user, int userId, CancellationToken cancellationToken = default)
    {
        // Без запроса к базе, если и так ясно
        if (user.CanAccessUser(userId, isTreatingDoctor: false))
        {
            return true;
        }
        return user.CanAccessUser(userId, await IsTreatingDoctorAsync(user, userId, cancellationToken));
    }

    public async Task<bool> CanAccessDocumentAsync(ClaimsPrincipal user, MedicalDocument document, CancellationToken cancellationToken = default)
    {
        if (user.CanAccessDocument(document, isTreatingDoctor: false))
        {
            return true;
        }
        return user.CanAccessDocument(document, await IsTreatingDoctorAsync(user, document.UserId, cancellationToken));
    }
}
