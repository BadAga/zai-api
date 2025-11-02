using DataVisualizerApi.Data;
using DataVisualizerApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataVisualizerApi.Models;

namespace DataVisualizerApi.Controllers;


[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthCrypto _crypto;

    public AuthController(AppDbContext db, IAuthCrypto crypto)
    {
        _db = db;
        _crypto = crypto;
    }

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);

    // POST /auth/register
    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email and password required.");

        var emailHash = _crypto.ComputeEmailHash(req.Email);

        bool exists = await _db.Users.AnyAsync(u => u.EmailHash == emailHash, ct);
        if (exists)
            return Conflict("User already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            EmailHash = emailHash,
            EmailHashVersion = 1,
            PasswordHash = _crypto.HashPassword(req.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok("User created successfully.");
    }

    // POST /auth/login
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized();

        var emailHash = _crypto.ComputeEmailHash(req.Email);
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailHash == emailHash, ct);

        if (user == null)
            return Unauthorized("Invalid credentials.");

        if (!_crypto.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        return Ok("Login successful.");
    }
}
