namespace HealthlyApi.Models;

// Лаборатория-партнёр: пользователи раз в год сдают там анализы, сотрудники (роль LabStaff) загружают бланки на сайт.
// Создаёт Admin; сотрудник привязан к одной лаборатории (User.LaboratoryId)
public class Laboratory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;    // TestingCities
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
