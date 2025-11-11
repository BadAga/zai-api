using DataVisualizerApi.Data;
using DataVisualizerApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataVisualizerApi.DTOs;
using DataVisualizerApi.Services;
using Microsoft.AspNetCore.Authorization;

namespace DataVisualizerApi.Controllers;


[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthCrypto _crypto;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;


    public AuthController(AppDbContext db, IAuthCrypto crypto, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _crypto = crypto;
        _tokenService = tokenService;
        _config = config;
    }    

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

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7")),
            Revoked = false
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(ct);


        var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15"));
        return Ok(new AuthResponse(accessToken, refreshToken, expiresAt));
    }

    // POST /auth/refresh
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest("Refresh token is required.");

        var oldToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == req.RefreshToken, ct);

        if (oldToken == null || oldToken.Revoked || oldToken.ExpiresAt < DateTime.UtcNow)
            return Unauthorized("Invalid or expired refresh token.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == oldToken.UserId, ct);

        if (user == null)
            return Unauthorized("User not found.");

        oldToken.Revoked = true;
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var newTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7")),
            Revoked = false
        };

        _db.RefreshTokens.Add(newTokenEntity);
        await _db.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15"));
        return Ok(new AuthResponse(accessToken, newRefreshToken, expiresAt));
    }

    // POST /auth/reset-password
    [HttpPost("reset-password")]
    [Authorize]
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
