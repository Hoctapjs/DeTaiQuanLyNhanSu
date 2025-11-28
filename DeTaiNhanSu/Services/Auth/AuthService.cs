using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DeTaiNhanSu.Services.Auth
{
    public class AuthService : IAuthService
    {
        // note: cung cấp toàn bộ nghiệp vụ xác thực

        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _hasher;
        private readonly IConfiguration _cfg;
        private readonly IEmailSender _email;

        public AuthService(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration cfg, IEmailSender email)
        {
            _db = db;
            _hasher = hasher;
            _cfg = cfg;
            _email = email;
        }

        // đăng ký
        public async Task<TokenResponse> RegisterAsync(RegisterRequest req, CancellationToken ct)
        {
            // tạo nhân viên (tạm thời)
            var emp = new Employee
            {
                Id = Guid.NewGuid(),
                Code = "NV-" + Random.Shared.Next(100000, 999999),
                FullName = req.FullName,
                Email = req.Email,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = EmployeeStatus.active
            };

            // lấy role mặc định
            var userRole = await _db.Roles.FirstOrDefaultAsync(x => x.Name == "User", ct) ?? throw new InvalidOperationException("Role 'User' not found. Seed roles first.");

            // tạo user
            var user = new User
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                UserName = req.Username,
                PasswordHash = "",
                RoleId = userRole.Id,
                Status = UserStatus.active
            };

            user.PasswordHash = _hasher.HashPassword(user, req.Password);

            _db.Employees.Add(emp);
            _db.Users.Add(user);

            await _db.SaveChangesAsync(ct);

            // return về token
            return await CreateTokensAsync(user, ct);
        }

        // đăng nhập
        public async Task<TokenResponse> LoginAsync(LoginRequest req, CancellationToken ct)
        {
            // truy vấn user theo username
            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserName == req.Username, ct);
            
            // xử lý ngoại lệ
            if (user is null)
            {
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            // kiểm tra trạng thái
            if (user.Status == UserStatus.locked)
            {
                throw new UnauthorizedAccessException("Account locked.");
            }

            // kiểm tra mật khẩu
            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            // kiểm tra bắt buộc đổi mật khẩu
            if (user.MustChangePassword)
            {
                if (user.TempPasswordExpireAt is not null && user.TempPasswordExpireAt < DateTime.UtcNow)
                {
                    throw new UnauthorizedAccessException("Temporary password expired. Please contact HR to reset again.");
                }
            }

            // is first login
            var isFirst = user.LastLoginAt == null;

            // cập nhật LastLoginAt và sinh token mới
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            //return await CreateTokensAsync(user, ct);
            var token = await CreateTokensAsync(user, ct);
            return token with { IsFirstLogin = isFirst };
        }

        // refresh token
        public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            // truy vấn user từ refresh token
            var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.RefreshTokenExpire > DateTime.UtcNow, ct);

            // ngoại lệ ko thấy
            if (user is null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            // sinh token mới
            return await CreateTokensAsync(user, ct);
        }


        // lấy thông tin user
        public async Task<MeResponse> MeAsync(Guid userId, CancellationToken ct)
        {
            // truy vấn user
            var u = await _db.Users.Include(x => x.Employee).Include(x => x.Role).ThenInclude(rp => rp.RolePermissions).ThenInclude(p => p.Permission).FirstAsync(x => x.Id == userId, ct);

            // chuyển đổi quyền lấy được thành array
            var perms = u.Role.RolePermissions.Select(rp => rp.Permission.Code).ToArray();

            // trả về thông tin cơ bản của user
            return new MeResponse(u.Id, u.UserName, u.Employee.FullName, u.Role.Name, perms);
        }

        // sinh ra access token và refresh token
        private async Task<TokenResponse> CreateTokensAsync(User user, CancellationToken ct)
        {
            // đọc config an toàn
            var issuer = _cfg["Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer");
            var audience = _cfg["Jwt:Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience");
            var keyStr = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");

            var accessMinutes = _cfg.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 60;
            var refreshDays = _cfg.GetValue<int?>("Jwt:RefreshTokenDays") ?? 7;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // lấy role name + permissions từ bảng RolePermission
            var rp = await _db.Roles
                .Where(r => r.Id == user.RoleId)
                .Select(r => new
                {
                    RoleName = r.Name,
                    Perms = r.RolePermissions.Select(x => x.Permission.Code).ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (rp is null)
                throw new InvalidOperationException("User has no valid role.");

            // 3) tạo danh sách Claimss sub, name, role, permission, iat, jti
            var now = DateTime.UtcNow;
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName),           // đảm bảo dùng đúng property bạn chuẩn hóa
                new(ClaimTypes.Role, rp.RoleName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                // tùy chọn: đánh dấu loại token
                new("typ", "access"),
                new("pwd_must_change", user.MustChangePassword ? "true" : "false")
            };

            // chỉ cần 1 loại claim cho tất cả permissions
            foreach (var p in rp.Perms)
                claims.Add(new Claim("permission", p));

            var expires = now.AddMinutes(accessMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: creds
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            // sinh refresh token ngẫu nhiên
            user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            // cập nhật refresh token và refresh token expire
            user.RefreshTokenExpire = now.AddDays(refreshDays);
            await _db.SaveChangesAsync(ct);

            return new TokenResponse(accessToken, user.RefreshToken, expires);
        }

        // sinh mật khẩu tạm
        private static string GenerateTempPassword(int length = 14)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@$%";

            var bytes = RandomNumberGenerator.GetBytes(length);

            var chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            return new string(chars);
        }

        //// api cho hr reset (truyền user id)
        //public async Task ResetPasswordByHRAsync(Guid id, CancellationToken ct)
        //{
        //    var user = await _db.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new KeyNotFoundException("User not found");

        //    if (user.Status == UserStatus.locked)
        //    {
        //        throw new InvalidOperationException("Account is locked");
        //    }

        //    // sinh pass tạm
        //    //var tempPassword = GenerateTempPassword();

        //    string tempPassword;
        //    do
        //    {
        //        tempPassword = GenerateTempPassword();
        //    }
        //    while (_hasher.VerifyHashedPassword(user, user.PasswordHash, tempPassword) != PasswordVerificationResult.Failed);

        //    // ghi hash + cờ must change pass
        //    user.PasswordHash = _hasher.HashPassword(user, tempPassword);
        //    user.MustChangePassword = true;
        //    user.TempPasswordExpireAt = DateTime.UtcNow.AddHours(24);
        //    user.LastPasswordChangedAt = DateTime.UtcNow;

        //    // vô hiệu hóa refresh token cũ (đảm bảo đăng nhập lại)
        //    user.RefreshToken = null;
        //    user.RefreshTokenExpire = null;

        //    await _db.SaveChangesAsync(ct);

        //    var rawTo = user.Employee?.Email;
        //    if (string.IsNullOrWhiteSpace(rawTo))
        //        throw new InvalidOperationException("User has no employee email.");
        //    var to = rawTo.Trim();

        //    // gửi mail cho nhân viên
        //    //var to = user.Employee?.Email ?? throw new InvalidOperationException("User has no employee email");
        //    var subject = "[HRM] Mật khẩu tạm cho tài khoản của bạn";

        //    //var body = $@"
        //    //    <p>Chào {user.Employee!.FullName},</p>
        //    //    <p>HR đã đặt lại mật khẩu cho tài khoản <b>{user.UserName}</b>.</p>
        //    //    <p><b>Mật khẩu tạm:</b> <code>{tempPassword}</code></p>
        //    //    <ul>
        //    //      <li>Mật khẩu tạm {(user.TempPasswordExpireAt is null ? "có hiệu lực ngay" : $"hết hạn lúc {user.TempPasswordExpireAt:yyyy-MM-dd HH:mm} UTC")}.</li>
        //    //      <li>Vui lòng đăng nhập và <b>đổi mật khẩu ngay</b>.</li>
        //    //    </ul>
        //    //    <p>Nếu bạn không yêu cầu, hãy liên hệ HR ngay.</p>";

        //    var body = $@"
        //        <!DOCTYPE html>
        //        <html lang='vi'>
        //        <head>
        //        <meta charset='UTF-8'>
        //        <title>Đặt lại mật khẩu tài khoản HRM</title>
        //        <style>
        //            body {{
        //                font-family: 'Segoe UI', Arial, sans-serif;
        //                background-color: #F5F1DC;
        //                margin: 0;
        //                padding: 0;
        //            }}
        //            .container {{
        //                max-width: 600px;
        //                margin: 40px auto;
        //                background: #ffffff;
        //                border-radius: 10px;
        //                box-shadow: 0 4px 10px rgba(0,0,0,0.05);
        //                overflow: hidden;
        //            }}
        //            .header {{
        //                background-color: #0046FF;
        //                color: white;
        //                text-align: center;
        //                padding: 20px 10px;
        //            }}
        //            .header h1 {{
        //                margin: 0;
        //                font-size: 22px;
        //                letter-spacing: 0.5px;
        //            }}
        //            .content {{
        //                padding: 25px 30px;
        //                color: #333;
        //                line-height: 1.6;
        //            }}
        //            .content h2 {{
        //                color: #0046FF;
        //                font-size: 18px;
        //                margin-top: 0;
        //            }}
        //            .info-box {{
        //                background-color: #73C8D2;
        //                color: #fff;
        //                border-radius: 8px;
        //                padding: 15px 20px;
        //                margin: 15px 0;
        //                font-size: 15px;
        //            }}
        //            .info-box code {{
        //                background-color: #fff;
        //                color: #0046FF;
        //                padding: 3px 6px;
        //                border-radius: 4px;
        //                font-weight: bold;
        //            }}
        //            .warning {{
        //                background-color: #FF9013;
        //                color: #fff;
        //                padding: 12px 16px;
        //                border-radius: 6px;
        //                margin-top: 20px;
        //                font-weight: 500;
        //            }}
        //            .footer {{
        //                background-color: #F5F1DC;
        //                color: #666;
        //                text-align: center;
        //                padding: 10px;
        //                font-size: 13px;
        //            }}
        //        </style>
        //        </head>
        //        <body>
        //        <div class='container'>
        //            <div class='header'>
        //                <h1>Đặt lại mật khẩu HRM</h1>
        //            </div>
        //            <div class='content'>
        //                <p>Chào <strong>{user.Employee!.FullName}</strong>,</p>
        //                <p>Phòng Nhân sự đã <strong>đặt lại mật khẩu</strong> cho tài khoản <b>{user.UserName}</b>.</p>

        //                <div class='info-box'>
        //                    <p><b>Mật khẩu tạm:</b> <code>{tempPassword}</code></p>
        //                    <ul>
        //                        <li>Mật khẩu tạm {(user.TempPasswordExpireAt is null ? "có hiệu lực ngay" : $"hết hạn lúc {user.TempPasswordExpireAt:yyyy-MM-dd HH:mm} UTC")}.</li>
        //                        <li>Vui lòng đăng nhập và <strong>đổi mật khẩu ngay</strong> để bảo mật tài khoản.</li>
        //                    </ul>
        //                </div>

        //                <div class='warning'>
        //                    Nếu bạn không yêu cầu thao tác này, vui lòng liên hệ Phòng Nhân sự ngay.
        //                </div>

        //                <p style='margin-top:25px;'>Trân trọng,<br><strong>Phòng Nhân sự</strong></p>
        //            </div>
        //            <div class='footer'>
        //                <p>© {DateTime.Now.Year} HRM System | Công ty Huỳnh Thanh Sơn</p>
        //            </div>
        //        </div>
        //        </body>
        //        </html>";


        //    await _email.SendAsync(to, subject, body, ct);
        //}

        public async Task ResetPasswordByHRAsync(Guid id, CancellationToken ct)
        {
            // 1. Tìm User
            var user = await _db.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == id, ct)
                       ?? throw new KeyNotFoundException("User not found");

            if (user.Status == UserStatus.locked)
            {
                throw new InvalidOperationException("Account is locked");
            }

            // 2. Sinh mật khẩu tạm (Giữ nguyên logic cũ)
            string tempPassword;
            do
            {
                tempPassword = GenerateTempPassword();
            }
            while (_hasher.VerifyHashedPassword(user, user.PasswordHash, tempPassword) != PasswordVerificationResult.Failed);

            // 3. Lưu thông tin (Giữ nguyên logic cũ)
            user.PasswordHash = _hasher.HashPassword(user, tempPassword);
            user.MustChangePassword = true;
            user.TempPasswordExpireAt = DateTime.UtcNow.AddHours(24); // HR reset cho phép 24h
            user.LastPasswordChangedAt = DateTime.UtcNow;

            // Vô hiệu hóa session cũ
            user.RefreshToken = null;
            user.RefreshTokenExpire = null;

            await _db.SaveChangesAsync(ct);

            // 4. Gửi Email (Sử dụng Giao diện Premium)
            var rawTo = user.Employee?.Email;
            if (string.IsNullOrWhiteSpace(rawTo))
                throw new InvalidOperationException("User has no employee email.");
            var to = rawTo.Trim();

            var subject = "[HRM] Cấp lại mật khẩu đăng nhập";

            // Các biến cấu hình giao diện
            string helpEmail = "support@huynhthanhson.io.vn";
            string companyName = "Công Ty TNHH NPS";
            string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";
            string loginUrl = "https://hrm.frontend.ifanit.io.vn";

            // Helper encode
            string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var body = $@"
    <!doctype html>
    <html lang='vi'>
    <head>
      <meta charset='utf-8'>
      <meta name='viewport' content='width=device-width, initial-scale=1'>
      <title>Cấp lại mật khẩu</title>
    </head>
    <body style='margin:0;padding:0;background:#f5f7fa;'>
      
      <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
        <tr>
          <td align='center' style='padding:24px 12px;'>
            <table role='presentation' width='600' cellspacing='0' cellpadding='0'
                   style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
              
              <tr>
                <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                  <h1 style='margin:0;font-size:20px;line-height:1.4;'>Cấp lại mật khẩu</h1>
                  <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Thông báo từ phòng Nhân sự</p>
                </td>
              </tr>

              <tr>
                <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                  <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{Enc(user.Employee!.FullName)}</b>,</p>
                  <p style='margin:0 0 16px;font-size:15px;'>Phòng Nhân sự đã thực hiện đặt lại mật khẩu cho tài khoản <b>{user.UserName}</b>.</p>
                  
                  <p style='margin:0 0 16px;font-size:15px;'>Dưới đây là mật khẩu tạm mới của bạn:</p>

                  <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
                         style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                    <tr>
                      <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>
                        Thông tin bảo mật
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:16px;font-size:15px;line-height:1.8;text-align:center;background:#fff;'>
                        <div style='margin-bottom:5px;color:#64748b;font-size:13px;'>Mật khẩu tạm thời</div>
                        <div style='font-family:Monospace;font-size:24px;font-weight:bold;color:#2563eb;letter-spacing:2px;background:#eff6ff;padding:10px;border-radius:6px;border:1px dashed #bfdbfe;display:inline-block;'>
                            {tempPassword}
                        </div>
                        <div style='margin-top:10px;font-size:13px;color:#ef4444;'>
                            (Hết hạn sau 24 giờ)
                        </div>
                      </td>
                    </tr>
                  </table>

                  <div style='text-align:center;margin:24px 0;'>
                      <a href='{loginUrl}' style='background:#0f172a;color:#fff;text-decoration:none;padding:12px 24px;border-radius:6px;font-weight:600;font-size:14px;display:inline-block;'>Đăng nhập ngay</a>
                  </div>

                  <p style='margin:16px 0 0;font-size:13px;color:#64748b;border-top:1px solid #e6e9ef;padding-top:16px;'>
                    <b>Lưu ý:</b> Vui lòng đăng nhập và đổi mật khẩu ngay để đảm bảo an toàn cho tài khoản của bạn.
                  </p>
                </td>
              </tr>

              <tr>
                <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;text-align:center;'>
                  <div style='margin-bottom:4px;'>{companyName} • {companyAddress}</div>
                  <div>Hệ thống Quản trị Nhân sự (HRM)</div>
                </td>
              </tr>
            </table>
            
            <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#94a3b8;margin-top:12px;max-width:600px;'>
                Email này được gửi tự động, vui lòng không trả lời.
            </div>
          </td>
        </tr>
      </table>
    </body>
    </html>
            ";

            await _email.SendAsync(to, subject, body, ct);
        }

        public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
        {
            var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);

            // xác thực current
            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);

            if (verify == PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Current password is invalid");
            }

            // độ mạnh mật khẩu
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                throw new InvalidOperationException("Password must be at least 8 characters");
            }

            // không cho đặt lại trùng mật khẩu cũ
            if (_hasher.VerifyHashedPassword(user, user.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException("New password must be different from the current password.");
            }
            
            // cập nhật mật khẩu
            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            user.MustChangePassword = false;
            user.TempPasswordExpireAt = null;
            user.LastPasswordChangedAt = DateTime.UtcNow;
            
            // vô hiệu hóa refresh cũ
            user.RefreshToken = null;
            user.RefreshTokenExpire = null;

            await _db.SaveChangesAsync(ct);
        }

        public async Task ForgotPasswordAsync(string email, CancellationToken ct)
        {
            // 1. Tìm user (Giữ nguyên logic)
            var user = await _db.Users
                .Include(u => u.Employee)
                .Where(u => u.Employee.Email == email && u.Status == UserStatus.active)
                .FirstOrDefaultAsync(ct);

            if (user == null) return;

            // 2. Sinh mật khẩu tạm (Giữ nguyên logic)
            string tempPassword;
            do { tempPassword = GenerateTempPassword(); }
            while (_hasher.VerifyHashedPassword(user, user.PasswordHash, tempPassword) != PasswordVerificationResult.Failed);

            // 3. Cập nhật DB (Giữ nguyên logic)
            user.PasswordHash = _hasher.HashPassword(user, tempPassword);
            user.MustChangePassword = true;
            user.TempPasswordExpireAt = DateTime.UtcNow.AddHours(1);
            user.LastPasswordChangedAt = DateTime.UtcNow;
            user.RefreshToken = null; // Force logout
            await _db.SaveChangesAsync(ct);

            // 4. GỬI EMAIL VỚI GIAO DIỆN MỚI (Copy style từ Update Employee)
            var subject = "[HRM] Khôi phục mật khẩu đăng nhập";

            // Cấu hình (Có thể đưa vào appsettings.json)
            string helpEmail = "support@huynhthanhson.io.vn";
            string companyName = "Công Ty TNHH NPS";
            string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";
            string loginUrl = "https://hrm.frontend.ifanit.io.vn"; // Link đăng nhập của bạn

            // Helper encode
            string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var body = $@"
    <!doctype html>
    <html lang='vi'>
    <head>
      <meta charset='utf-8'>
      <meta name='viewport' content='width=device-width, initial-scale=1'>
      <title>Khôi phục mật khẩu</title>
    </head>
    <body style='margin:0;padding:0;background:#f5f7fa;'>
      
      <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
        <tr>
          <td align='center' style='padding:24px 12px;'>
            <table role='presentation' width='600' cellspacing='0' cellpadding='0'
                   style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
              
              <tr>
                <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                  <h1 style='margin:0;font-size:20px;line-height:1.4;'>Khôi phục mật khẩu</h1>
                  <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Yêu cầu từ người dùng</p>
                </td>
              </tr>

              <tr>
                <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                  <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{Enc(user.Employee!.FullName)}</b>,</p>
                  <p style='margin:0 0 16px;font-size:15px;'>Hệ thống đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản <b>{user.UserName}</b>.</p>
                  
                  <p style='margin:0 0 16px;font-size:15px;'>Dưới đây là mật khẩu tạm mới của bạn:</p>

                  <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
                         style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                    <tr>
                      <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>
                        Thông tin bảo mật
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:16px;font-size:15px;line-height:1.8;text-align:center;background:#fff;'>
                        <div style='margin-bottom:5px;color:#64748b;font-size:13px;'>Mật khẩu tạm thời</div>
                        <div style='font-family:Monospace;font-size:24px;font-weight:bold;color:#2563eb;letter-spacing:2px;background:#eff6ff;padding:10px;border-radius:6px;border:1px dashed #bfdbfe;display:inline-block;'>
                            {tempPassword}
                        </div>
                        <div style='margin-top:10px;font-size:13px;color:#ef4444;'>
                            (Hết hạn sau 01 giờ)
                        </div>
                      </td>
                    </tr>
                  </table>

                  <div style='text-align:center;margin:24px 0;'>
                      <a href='{loginUrl}' style='background:#0f172a;color:#fff;text-decoration:none;padding:12px 24px;border-radius:6px;font-weight:600;font-size:14px;display:inline-block;'>Đăng nhập ngay</a>
                  </div>

                  <p style='margin:16px 0 0;font-size:13px;color:#64748b;border-top:1px solid #e6e9ef;padding-top:16px;'>
                    <b>Lưu ý:</b> Nếu bạn không yêu cầu thay đổi mật khẩu, vui lòng liên hệ <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a> ngay lập tức vì tài khoản của bạn có thể đang gặp rủi ro.
                  </p>
                </td>
              </tr>

              <tr>
                <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;text-align:center;'>
                  <div style='margin-bottom:4px;'>{companyName} • {companyAddress}</div>
                  <div>Hệ thống Quản trị Nhân sự (HRM)</div>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
    </body>
    </html>
            ";

            await _email.SendAsync(email, subject, body, ct);
        }
    }
}
