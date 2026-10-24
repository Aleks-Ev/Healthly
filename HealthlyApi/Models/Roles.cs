namespace HealthlyApi.Models;

// Роли пользователей (хранятся в User.Role строкой)
public static class Roles
{
    public const string Patient = "Patient";
    public const string Doctor = "Doctor";
    public const string Admin = "Admin";

    // Для [Authorize(Roles = ...)]
    public const string Staff = Doctor + "," + Admin;
}
