using System.Security.Cryptography;
using HealthlyApi.Data;
using HealthlyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthlyApi.Services;

// Healthly ID пациента: "HL-" + 6 символов без похожих (нет 0/O, 1/I/L) — удобно продиктовать и ввести вручную.
// 31^6 ≈ 887 млн вариантов: случайно угадать чужой код практически невозможно.
// Код — пропуск сотрудника лаборатории к пациенту: пациент сам называет его или показывает QR-код.
public static class PatientCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Prefix = "HL-";

    public static string Generate() =>
        Prefix + new string(Enumerable.Range(0, 6).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());

    // Ввод сотрудника: "hl 7k3p9q", "HL7K3P9Q", " HL-7K3P9Q " → "HL-7K3P9Q"
    public static string Normalize(string input)
    {
        var chars = new string(input.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (chars.StartsWith("HL", StringComparison.Ordinal))
        {
            chars = chars[2..];
        }
        return Prefix + chars;
    }

    // Уникальный код (проверка по БД — на случай редкого совпадения)
    public static async Task<string> GenerateUniqueAsync(AppDbContext context)
    {
        while (true)
        {
            var code = Generate();
            if (!await context.Users.AnyAsync(u => u.PatientCode == code))
            {
                return code;
            }
        }
    }

    // Пациентам без кода (зарегистрированы до появления Healthly ID) — выдать при запуске API
    public static async Task AssignMissingAsync(AppDbContext context)
    {
        var patients = await context.Users.Where(u => u.Role == Roles.Patient && u.PatientCode == null).ToListAsync();
        foreach (var patient in patients)
        {
            patient.PatientCode = await GenerateUniqueAsync(context);
        }
        if (patients.Count > 0)
        {
            await context.SaveChangesAsync();
        }
    }
}
