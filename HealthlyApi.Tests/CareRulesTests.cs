using HealthlyApi.Models;
using HealthlyApi.Services.Care;

namespace HealthlyApi.Tests;

// Связь пациент ↔ врач / лаборатория: кто и когда может менять статус, и порядок предложений
public class CareRulesTests
{
    private const string Requested = CareConnectionStatuses.Requested;
    private const string Accepted = CareConnectionStatuses.Accepted;
    private const string Active = CareConnectionStatuses.Active;
    private const string Declined = CareConnectionStatuses.Declined;
    private const string Cancelled = CareConnectionStatuses.Cancelled;
    private const string Ended = CareConnectionStatuses.Ended;

    [Fact]
    public void Happy_path_request_accept_confirm()
    {
        var accepted = CareRules.NextStatus(Requested, CareAction.Accept, CareActor.Provider);
        Assert.Equal(Accepted, accepted);
        Assert.Equal(Active, CareRules.NextStatus(accepted!, CareAction.Confirm, CareActor.Patient));
    }

    [Fact]
    public void Doctor_gets_no_access_until_the_patient_confirms()
    {
        // Принятие врачом ещё не Active — доступ к медкарте открывает только подтверждение пациента
        Assert.NotEqual(Active, CareRules.NextStatus(Requested, CareAction.Accept, CareActor.Provider));
        Assert.Null(CareRules.NextStatus(Requested, CareAction.Confirm, CareActor.Patient));
    }

    [Fact]
    public void Only_the_provider_answers_and_only_the_patient_confirms()
    {
        Assert.Null(CareRules.NextStatus(Requested, CareAction.Accept, CareActor.Patient));
        Assert.Null(CareRules.NextStatus(Requested, CareAction.Decline, CareActor.Patient));
        Assert.Null(CareRules.NextStatus(Accepted, CareAction.Confirm, CareActor.Provider));
    }

    [Fact]
    public void Provider_can_decline_only_a_waiting_request()
    {
        Assert.Equal(Declined, CareRules.NextStatus(Requested, CareAction.Decline, CareActor.Provider));
        Assert.Null(CareRules.NextStatus(Active, CareAction.Decline, CareActor.Provider));
        Assert.Null(CareRules.NextStatus(Accepted, CareAction.Accept, CareActor.Provider)); // повторное принятие
    }

    [Theory]
    [InlineData(Requested, Cancelled)] // отозвать запрос
    [InlineData(Accepted, Cancelled)]  // отказаться делиться после принятия
    [InlineData(Active, Ended)]        // прекратить доступ
    public void Patient_can_always_step_back(string current, string expected)
    {
        Assert.Equal(expected, CareRules.NextStatus(current, CareAction.End, CareActor.Patient));
    }

    [Theory]
    [InlineData(Declined)]
    [InlineData(Cancelled)]
    [InlineData(Ended)]
    public void Closed_connections_cannot_change(string closed)
    {
        foreach (var action in Enum.GetValues<CareAction>())
        {
            Assert.Null(CareRules.NextStatus(closed, action, CareActor.Patient));
            Assert.Null(CareRules.NextStatus(closed, action, CareActor.Provider));
        }
        Assert.False(CareRules.IsOpen(closed));
    }

    [Fact]
    public void Provider_cannot_end_a_request_it_has_not_answered()
    {
        // Неотвеченный запрос врач отклоняет (Decline), а не «завершает»
        Assert.Null(CareRules.NextStatus(Requested, CareAction.End, CareActor.Provider));
        Assert.Equal(Ended, CareRules.NextStatus(Active, CareAction.End, CareActor.Provider));
    }

    [Fact]
    public void Suggestions_start_with_the_least_busy_then_by_name()
    {
        var doctors = new[] { ("Dr. Can", 12), ("Dr. Ayse", 3), ("dr. Zeynep", 3), ("Dr. Emre", 0) };

        var ranked = CareRules.RankByLoad(doctors, d => d.Item2, d => d.Item1).Select(d => d.Item1).ToList();

        Assert.Equal(["Dr. Emre", "Dr. Ayse", "dr. Zeynep", "Dr. Can"], ranked);
    }
}
