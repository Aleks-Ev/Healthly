namespace HealthlyApi.Models;

// Допустимые значения полей формы регистрации (совпадают с вариантами на фронтенде)
public static class Genders
{
    public const string Female = "Female";
    public const string Male = "Male";
    public const string Other = "Other";
}

// Города Турции, в которых можно сдать анализы
public static class TestingCities
{
    public const string Istanbul = "Istanbul";
    public const string Ankara = "Ankara";
    public const string Izmir = "Izmir";
    public const string Antalya = "Antalya";
    public const string Bursa = "Bursa";
    public const string Adana = "Adana";
    public const string Gaziantep = "Gaziantep";
    public const string Konya = "Konya";
}
