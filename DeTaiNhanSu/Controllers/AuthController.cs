using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Infrastructure.Auditing;
using DeTaiNhanSu.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        //private readonly IAuthService _authService;

        //public AuthController(IAuthService authService)
        //{
        //    _authService = authService;
        //}

        //[HttpPost("register")]
        //[AllowAnonymous]
        //public async Task<ActionResult<TokenResponse>> Register(RegisterRequest req, CancellationToken ct)
        //{
        //    return Ok(await _authService.RegisterAsync(req, ct));
        //}

        //[HttpPost("login")]
        //[AllowAnonymous]
        //public async Task<ActionResult<TokenResponse>> Login(LoginRequest req, CancellationToken ct)
        //{
        //    return Ok(await _authService.LoginAsync(req, ct));
        //}

        //[HttpPost("refresh")]
        //[AllowAnonymous]
        //public async Task<ActionResult<TokenResponse>> Refresh([FromBody] string refreshToken, CancellationToken ct)
        //{
        //    return Ok(await _authService.RefreshAsync(refreshToken, ct));
        //}

        //[HttpGet("me")]
        //[Authorize]
        //public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
        //{
        //    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        //    return Ok(await _authService.MeAsync(userId, ct));
        //}

        //// đổi mật khẩu (người dùng tự đổi khi login)
        //[HttpPost("change-password")]
        //[Authorize]
        //public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
        //{
        //    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        //    await _authService.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ct);
        //    return NoContent();
        //}

        //// hr reset mật khẩu cho nhân viên
        //// cần quyền riêng của hr
        //[HttpPost("hr/reset-password/{id:guid}")]
        //[Authorize(Roles = "HR, Admin")]
        //[HasPermission("Users.Manage")]
        //public async Task<IActionResult> ResetPasswordByHRAsync([FromRoute] Guid id, CancellationToken ct)
        //{
        //    await _authService.ResetPasswordByHRAsync(id, ct);
        //    return NoContent();
        //}

        private readonly IAuthService _authService;
        private readonly AppDbContext _db;
        public AuthController(IAuthService authService, AppDbContext db)
        {
            _authService = authService;
            _db = db;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
        {
            try
            {
                var token = await _authService.RegisterAsync(req, ct);

                // Trả 201 với token theo schema
                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Đăng ký thành công.",
                    data = new[] { token },
                    success = true
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return this.FAIL(StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (Exception)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi đăng ký.");
            }
        }

        //[HttpPost("login")]
        //[AllowAnonymous]
        //public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        var token = await _authService.LoginAsync(req, ct);
        //        return this.OKSingle(token, "Đăng nhập thành công.");
        //    }
        //    catch (UnauthorizedAccessException ex)
        //    {
        //        return this.FAIL(StatusCodes.Status401Unauthorized, ex.Message);
        //    }
        //    catch (Exception)
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi đăng nhập.");
        //    }
        //}

        [SkipAudit]
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
        {
            try
            {
                // 1) Đăng nhập lấy token
                var token = await _authService.LoginAsync(req, ct);

                // 2) Lấy userId từ access_token
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
                var uid = jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier
                                             || c.Type == JwtRegisteredClaimNames.Sub).Value;
                var userId = Guid.Parse(uid);

                // 3) Lấy đầy đủ user + employee + role
                var u = await _db.Users
                    .Include(x => x.Employee)
                    .Include(x => x.Role)
                    .FirstAsync(x => x.Id == userId, ct);

                // 4) Quyền từ role
                var perms = await _db.RolePermissions
                    .Where(rp => rp.RoleId == u.RoleId)
                    .Select(rp => rp.Permission.Code)
                    .OrderBy(x => x)
                    .ToListAsync(ct);

                // 5) Trả đúng schema & snake_case
                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Login successful.",
                    data = new
                    {
                        access_token = token.AccessToken,
                        refresh_token = token.RefreshToken,
                        expires_at = token.ExpiresAt,

                        user = new
                        {
                            id = u.Id,
                            employee_id = u.EmployeeId,
                            username = u.UserName,
                            role_id = u.RoleId,
                            status = u.Status.ToString().ToLower(),
                            is_first_login = token.IsFirstLogin,
                            last_login_at = u.LastLoginAt,

                            employee = new
                            {
                                id = u.Employee.Id,
                                code = u.Employee.Code,
                                full_name = u.Employee.FullName,
                                gender = u.Employee.Gender?.ToString().ToLower(),
                                dob = u.Employee.Dob,
                                email = u.Employee.Email,
                                phone = u.Employee.Phone,
                                address = u.Employee.Address,
                                hire_date = u.Employee.HireDate,
                                department_id = u.Employee.DepartmentId,
                                position_id = u.Employee.PositionId,
                                status = u.Employee.Status.ToString().ToLower(),
                                avatar_url = u.Employee.AvatarUrl
                            },

                            role = new
                            {
                                id = u.Role.Id,
                                name = u.Role.Name,
                                description = u.Role.Description
                            }
                        },

                        //permissions = perms
                    },
                    success = true
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return this.FAIL(StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi đăng nhập.");
            }
        }

        [SkipAudit]
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] string refreshToken, CancellationToken ct)
        {
            try
            {
                var token = await _authService.RefreshAsync(refreshToken, ct);
                return this.OKSingle(token, "Làm mới token thành công.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return this.FAIL(StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (Exception)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi làm mới token.");
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var me = await _authService.MeAsync(userId, ct);
                return this.OKSingle(me, "Lấy thông tin người dùng hiện tại thành công.");
            }
            catch (Exception)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin người dùng.");
            }
        }

        // đổi mật khẩu (người dùng tự đổi khi login)
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ct);
                return this.OK(message: "Đổi mật khẩu thành công.");
            }
            catch (UnauthorizedAccessException ex)
            {
                // Ví dụ: current password sai
                return this.FAIL(StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (Exception)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi đổi mật khẩu.");
            }
        }

        // HR reset mật khẩu cho nhân viên (cần quyền)
        [HttpPost("hr/reset-password/{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        [HasPermission("Users.Manage")]
        public async Task<IActionResult> ResetPasswordByHRAsync([FromRoute] Guid id, CancellationToken ct)
        {
            try
            {
                await _authService.ResetPasswordByHRAsync(id, ct);
                return this.OK(message: "Đặt lại mật khẩu thành công.");
            }
            catch (KeyNotFoundException)
            {
                return this.FAIL(StatusCodes.Status404NotFound, "User không tồn tại.");
            }
            catch (Exception)
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi đặt lại mật khẩu.");
            }
        }
    }
}
