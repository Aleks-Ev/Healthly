using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Models;

namespace HealthlyApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    // Внедряем AppDbContext через конструктор (Dependency Injection)
    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/users — получить список всех пользователей
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }

    // GET: api/users/5 — получить пользователя по Id
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "Пользователь не найден" });
        }

        return user;
    }

    // POST: api/users — создать нового пользователя
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }
}