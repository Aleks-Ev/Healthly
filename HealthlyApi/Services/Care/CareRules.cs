using HealthlyApi.Models;

namespace HealthlyApi.Services.Care;

public enum CareAction
{
    Accept,  // врач/лаборатория принимает запрос
    Decline, // врач/лаборатория отклоняет запрос
    Confirm, // пациент разрешает доступ (делится данными)
    End,     // отменить запрос / не делиться / завершить связь
}

public enum CareActor
{
    Patient,
    Provider, // врач или сотрудник лаборатории
}

// Правила переходов статуса связи — без базы и HTTP, чтобы их было легко проверить тестами (CareRulesTests).
public static class CareRules
{
    // Сколько запросов без ответа (Requested) может висеть у пациента одновременно — защита от рассылки «всем подряд»
    public const int MaxPendingRequests = 5;

    // Новый статус или null, если такое действие сейчас невозможно
    public static string? NextStatus(string current, CareAction action, CareActor actor) => (action, actor, current) switch
    {
        (CareAction.Accept, CareActor.Provider, CareConnectionStatuses.Requested) => CareConnectionStatuses.Accepted,
        (CareAction.Decline, CareActor.Provider, CareConnectionStatuses.Requested) => CareConnectionStatuses.Declined,
        (CareAction.Confirm, CareActor.Patient, CareConnectionStatuses.Accepted) => CareConnectionStatuses.Active,

        // Пациент: до подтверждения — отмена запроса / отказ делиться; после — прекращение доступа
        (CareAction.End, CareActor.Patient, CareConnectionStatuses.Requested) => CareConnectionStatuses.Cancelled,
        (CareAction.End, CareActor.Patient, CareConnectionStatuses.Accepted) => CareConnectionStatuses.Cancelled,
        (CareAction.End, CareActor.Patient, CareConnectionStatuses.Active) => CareConnectionStatuses.Ended,

        // Врач/лаборатория: могут передумать после принятия или завершить наблюдение
        (CareAction.End, CareActor.Provider, CareConnectionStatuses.Accepted) => CareConnectionStatuses.Ended,
        (CareAction.End, CareActor.Provider, CareConnectionStatuses.Active) => CareConnectionStatuses.Ended,

        _ => null,
    };

    public static bool IsOpen(string status) => CareConnectionStatuses.Open.Contains(status);

    // Порядок предложений: сначала те, у кого меньше пациентов (равномерная нагрузка), при равенстве — по имени
    public static IEnumerable<T> RankByLoad<T>(IEnumerable<T> providers, Func<T, int> load, Func<T, string> name) =>
        providers.OrderBy(load).ThenBy(name, StringComparer.OrdinalIgnoreCase);
}
