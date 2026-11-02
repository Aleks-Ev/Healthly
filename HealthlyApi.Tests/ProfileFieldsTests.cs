using System.ComponentModel.DataAnnotations;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services.Ai;

namespace HealthlyApi.Tests;

// Телефон, дата рождения, город в создании/редактировании профиля — те же правила, что при регистрации;
// подсказка в анализе, если без них нормы по полу/возрасту не применить
public class ProfileFieldsTests
{
    [Theory]
    [InlineData("+90 532 123 45 67", true)]
    [InlineData("+905321234567", true)]
    [InlineData("", true)]               // пустая строка — очистить телефон
    [InlineData("+7 912 345 67 89", false)]
    [InlineData("0532 123 45 67", false)]
    public void Phone_must_be_turkish(string phone, bool valid)
    {
        Assert.Equal(valid, IsValid(new UpdateUserRequest { Email = "a@b.com", FullName = "A B", PhoneNumber = phone }));
    }

    [Fact]
    public void Age_must_be_between_18_and_120()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.True(IsValid(new UpdateUserRequest { Email = "a@b.com", FullName = "A B", DateOfBirth = today.AddYears(-18) }));
        Assert.False(IsValid(new UpdateUserRequest { Email = "a@b.com", FullName = "A B", DateOfBirth = today.AddYears(-17) }));
        Assert.False(IsValid(new UpdateUserRequest { Email = "a@b.com", FullName = "A B", DateOfBirth = today.AddYears(-121) }));
        Assert.True(IsValid(new UpdateUserRequest { Email = "a@b.com", FullName = "A B", DateOfBirth = null }));
    }

    [Theory]
    [InlineData("Istanbul", true)]
    [InlineData("", true)]
    [InlineData("Paris", false)]
    public void City_must_be_a_testing_city(string city, bool valid)
    {
        Assert.Equal(valid, IsValid(new CreateUserRequest { Email = "a@b.com", FullName = "A B", Password = "Password1!", City = city }));
    }

    [Theory]
    [InlineData("Female", true)]
    [InlineData("", true)]
    [InlineData("Unknown", false)]
    public void Gender_must_be_one_of_the_options(string gender, bool valid)
    {
        Assert.Equal(valid, IsValid(new UpdateUserRequest { Email = "a@b.com", FullName = "A B", Gender = gender }));
    }

    [Fact]
    public void Complete_profile_needs_no_note()
    {
        Assert.Null(DocumentAnalysisService.GetPatientDataNote(Patient(Genders.Female, new DateOnly(1990, 1, 1))));
    }

    [Theory]
    [InlineData("", true, "date of birth and sex")]
    [InlineData("Female", true, "date of birth is missing")]
    [InlineData("", false, "sex is missing")]
    [InlineData("Other", false, "\"Other\"")]
    public void Missing_profile_data_is_explained(string gender, bool withoutBirthDate, string expected)
    {
        var note = DocumentAnalysisService.GetPatientDataNote(Patient(gender, withoutBirthDate ? null : new DateOnly(1990, 1, 1)));

        Assert.NotNull(note);
        Assert.Contains(expected, note);
    }

    private static User Patient(string gender, DateOnly? dateOfBirth) => new() { Gender = gender, DateOfBirth = dateOfBirth };

    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);
}
