using DataVisualizerApi.Data;
using DataVisualizerApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public record ResetPasswordRequest(string Email, string NewPassword);


    // POST /auth/register
    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email and password required.");

        try
        {
            var addr = new System.Net.Mail.MailAddress(req.Email);
            if (addr.Address != req.Email)
                return BadRequest("Invalid email format.");
        }
        catch
        {
            return BadRequest("Invalid email format.");
        }

        if (req.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters long.");

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

        try
        {
            var addr = new System.Net.Mail.MailAddress(req.Email);
            if (addr.Address != req.Email)
                return BadRequest("Invalid email format.");
        }
        catch
        {
            return BadRequest("Invalid email format.");
        }

        var emailHash = _crypto.ComputeEmailHash(req.Email);
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailHash == emailHash, ct);

        if (user == null)
            return Unauthorized("Invalid credentials.");

        if (!_crypto.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        return Ok("Login successful.");
    }

    // POST /auth/reset-password
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Email and new password required.");

        try
        {
            var addr = new System.Net.Mail.MailAddress(req.Email);
            if (addr.Address != req.Email)
                return BadRequest("Invalid email format.");
        }
        catch
        {
            return BadRequest("Invalid email format.");
        }

        if (req.NewPassword.Length < 8)
            return BadRequest("Password must be at least 8 characters long.");

        var emailHash = _crypto.ComputeEmailHash(req.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash, ct);

        if (user == null)
            return NotFound("User not found.");

        user.PasswordHash = _crypto.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok("Password has been reset.");
    }
}
