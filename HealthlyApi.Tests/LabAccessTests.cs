using System.Security.Claims;
using HealthlyApi.Models;
using HealthlyApi.Services;

namespace HealthlyApi.Tests;

// Доступ к документам: сотрудник лаборатории видит только документы своей лаборатории, а не всю медкарту пациента
public class LabAccessTests
{
    private const int PatientId = 10;
    private const int LabA = 1;
    private const int LabB = 2;

    [Fact]
    public void Lab_staff_can_open_documents_of_their_own_laboratory()
    {
        Assert.True(Staff(LabA).CanAccessDocument(Document(laboratoryId: LabA)));
    }

    [Fact]
    public void Lab_staff_cannot_open_documents_of_another_laboratory()
    {
        Assert.False(Staff(LabA).CanAccessDocument(Document(laboratoryId: LabB)));
    }

    [Fact]
    public void Lab_staff_cannot_open_documents_the_patient_uploaded_themselves()
    {
        Assert.False(Staff(LabA).CanAccessDocument(Document(laboratoryId: null)));
        Assert.False(Staff(LabA).CanAccessUser(PatientId)); // и медкарту пациента целиком
    }

    [Fact]
    public void Lab_staff_without_a_laboratory_has_no_access()
    {
        Assert.False(Principal(Roles.LabStaff, userId: 50).CanAccessDocument(Document(laboratoryId: LabA)));
    }

    [Theory]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Admin)]
    public void Doctors_and_admins_keep_access(string role)
    {
        Assert.True(Principal(role, userId: 99).CanAccessDocument(Document(laboratoryId: LabA)));
    }

    [Fact]
    public void Patient_sees_own_documents_including_those_uploaded_by_a_lab()
    {
        Assert.True(Principal(Roles.Patient, PatientId).CanAccessDocument(Document(laboratoryId: LabA)));
        Assert.False(Principal(Roles.Patient, userId: 11).CanAccessDocument(Document(laboratoryId: LabA)));
    }

    [Fact]
    public void Healthly_id_has_the_expected_format()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => PatientCodes.Generate()).ToList();

        Assert.All(codes, code => Assert.Matches("^HL-[2-9A-HJKMNP-Z]{6}$", code)); // без 0/O и 1/I/L
        Assert.True(codes.Distinct().Count() > 190);
    }

    [Theory]
    [InlineData("HL-7K3P9Q")]
    [InlineData("hl-7k3p9q")]
    [InlineData(" HL 7K3P9Q ")]
    [InlineData("HL7K3P9Q")]
    [InlineData("7K3P9Q")]
    public void Healthly_id_typed_or_scanned_in_any_form_is_normalized(string input)
    {
        Assert.Equal("HL-7K3P9Q", PatientCodes.Normalize(input));
    }

    private static MedicalDocument Document(int? laboratoryId) => new() { Id = 5, UserId = PatientId, LaboratoryId = laboratoryId };

    private static ClaimsPrincipal Staff(int laboratoryId) => Principal(Roles.LabStaff, userId: 50, laboratoryId);

    // Как после JWT: claim'ы "sub", "role" и (для LabStaff) "lab" — RoleClaimType = "role" (Program.cs)
    private static ClaimsPrincipal Principal(string role, int userId, int? laboratoryId = null)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()), new("role", role) };
        if (laboratoryId != null)
        {
            claims.Add(new Claim(TokenService.LaboratoryClaim, laboratoryId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
    }
}
