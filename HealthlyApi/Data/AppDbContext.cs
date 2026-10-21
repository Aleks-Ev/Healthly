using Microsoft.EntityFrameworkCore;
using HealthlyApi.Models;

namespace HealthlyApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
}