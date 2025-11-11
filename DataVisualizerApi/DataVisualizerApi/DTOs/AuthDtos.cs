namespace DataVisualizerApi.DTOs;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? DisplayName);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
public record RefreshRequest(string RefreshToken);
public record ResetPasswordRequest(string Email, string NewPassword);