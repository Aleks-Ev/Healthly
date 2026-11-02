namespace HealthlyApi.Models;

// Роли пользователей (хранятся в User.Role строкой)
public static class Roles
{
    public const string Patient = "Patient";
    public const string Doctor = "Doctor";
    public const string Admin = "Admin";

    // Сотрудник лаборатории-партнёра: находит пациента по Healthly ID, загружает его бланки и подтверждает анализ.
    // Видит только документы, загруженные своей лабораторией (User.LaboratoryId), а не всю медкарту пациента
    public const string LabStaff = "LabStaff";

    // Для [Authorize(Roles = ...)]
    public const string Staff = Doctor + "," + Admin;
}
