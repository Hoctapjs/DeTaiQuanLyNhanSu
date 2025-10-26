namespace DeTaiNhanSu.Dtos;

public record RegisterRequest(string FullName, string Email, string Username, string Password);
public record LoginRequest(string Username, string Password);
public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, bool IsFirstLogin = false);
public record MeResponse(Guid UserId, string Username, string FullName, string Role, IEnumerable<string> Permissions);
