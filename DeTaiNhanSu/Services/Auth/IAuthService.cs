using DeTaiNhanSu.Dtos;

namespace DeTaiNhanSu.Services.Auth
{
    public interface IAuthService
    {
        // note: định nghĩa interface cho service
        Task<TokenResponse> RegisterAsync(RegisterRequest req, CancellationToken ct);
        Task<TokenResponse> LoginAsync(LoginRequest req, CancellationToken ct);
        Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);
        Task<MeResponse> MeAsync(Guid userId, CancellationToken ct);
        Task ResetPasswordByHRAsync(Guid id, CancellationToken ct);

        Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    }
}
