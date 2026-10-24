namespace HealthlyApi.Models;

// Статусы обработки медицинского документа (хранятся в MedicalDocument.Status строкой)
public static class DocumentStatuses
{
    public const string Pending = "Pending";       // загружен, показателей ещё нет
    public const string Processing = "Processing"; // врач взял в работу
    public const string Completed = "Completed";   // показатели внесены
    public const string Error = "Error";           // документ нечитаем / ошибка
}
