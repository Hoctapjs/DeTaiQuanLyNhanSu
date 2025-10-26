//using DeTaiNhanSu.DbContextProject;
//using DeTaiNhanSu.Dtos;
//using DeTaiNhanSu.Enums;
//using DeTaiNhanSu.Models;
//using DeTaiNhanSu.Services.Auth;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//using System.Globalization;
//using System.Text;
//using Microsoft.AspNetCore.Identity;
//using DeTaiNhanSu.Services.Email;
//using System.Security.Cryptography;

//namespace DeTaiNhanSu.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public sealed class EmployeeController : ControllerBase
//    {
//        private readonly AppDbContext _db;
//        private readonly IEmailSender _emailSender;
//        private readonly IPasswordHasher<User> _hasher;

//        public EmployeeController(AppDbContext db, IEmailSender emailSender, IPasswordHasher<User> hasher)
//        {
//            _db = db;
//            _emailSender = emailSender;
//            _hasher = hasher;
//        }


//        /// <summary>
//        /// function search nhân viên
//        /// </summary>
//        /// <param name="q">đại diện cho string filter, tìm trong fullname, code, email</param>
//        /// <param name="status">trạng thái nhân viên, active, inactive</param>
//        /// <param name="departmentId"></param>
//        /// <param name="positionId"></param>
//        /// <param name="page"> số trang hiện tại</param>
//        /// <param name="pageSize"> kích thước trang</param>
//        /// <param name="sort">kiểu mà dữ liệu sẽ được sắp xếp, sử dụng string và enum</param>
//        /// <param name="ct"></param>
//        /// <returns></returns>
//        //[HttpGet("Search")]
//        //[HasPermission("Employees.View")]
//        //[Authorize(Roles = "HR, Admin")]
//        //public async Task<IActionResult> Search(
//        //  [FromQuery] string? q,
//        //  [FromQuery] EmployeeStatus? status,
//        //  [FromQuery] Guid? departmentId,
//        //  [FromQuery] Guid? positionId,
//        //  [FromQuery] int page = 1,
//        //  [FromQuery] int pageSize = 20,
//        //  [FromQuery] string? sort = null,
//        //  CancellationToken ct = default)
//        //{
//        //    if (page < 1) page = 1;
//        //    if (pageSize is < 1 or > 200) pageSize = 20;

//        //    var query = _db.Employees
//        //        .AsNoTracking()
//        //        .Include(x => x.Department)
//        //        .Include(x => x.Position)
//        //        .AsQueryable();

//        //    // kiểm tra chuỗi search, làm sạch nếu hợp lệ
//        //    if (!string.IsNullOrWhiteSpace(q))
//        //    {
//        //        q = q.Trim();
//        //        query = query.Where(x =>
//        //            x.FullName.Contains(q) ||
//        //            x.Code.Contains(q) ||
//        //            (x.Email != null && x.Email.Contains(q)));
//        //    }

//        //    // kiểm tra các giá trị status, department, position và filter theo chúng
//        //    if (status is not null) query = query.Where(x => x.Status == status);
//        //    if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
//        //    if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

//        //    // Sort đơn giản: "Code", "-HireDate", "FullName"
//        //    query = sort?.Trim() switch
//        //    {
//        //        "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
//        //        "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
//        //        "-Code" => query.OrderByDescending(x => x.Code),
//        //        "Code" => query.OrderBy(x => x.Code),
//        //        "-FullName" => query.OrderByDescending(x => x.FullName),
//        //        "FullName" => query.OrderBy(x => x.FullName),
//        //        _ => query.OrderBy(x => x.FullName)
//        //    };

//        //    // số lượng bản ghi
//        //    var total = await query.CountAsync(ct);

//        //    // list các item bản ghi, đã skip và take cho phân trang, truyền cho Dto để bảo mật thông tin
//        //    var items = await query
//        //        .Skip((page - 1) * pageSize)
//        //        .Take(pageSize)
//        //        .Select(x => new EmployeeDto
//        //        {
//        //            Id = x.Id,
//        //            Code = x.Code,
//        //            FullName = x.FullName,
//        //            Gender = x.Gender,
//        //            Dob = x.Dob,
//        //            Cccd = x.Cccd,
//        //            Email = x.Email,
//        //            Phone = x.Phone,
//        //            Address = x.Address,
//        //            HireDate = x.HireDate,
//        //            DepartmentId = x.DepartmentId,
//        //            DepartmentName = x.Department!.Name,
//        //            PositionId = x.PositionId,
//        //            PositionName = x.Position!.Name,
//        //            Status = x.Status,
//        //            AvatarUrl = x.AvatarUrl
//        //        })
//        //        .ToListAsync(ct);

//        //    // trả vể json với total, trang hiện tại, kích thước trang, list nhân viên
//        //    return Ok(new { total, page, pageSize, items });
//        //}

//        [HttpGet("Search")]
//        [HasPermission("Employees.View")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Search(
//  [FromQuery] string? q,
//  [FromQuery] EmployeeStatus? status,
//  [FromQuery] Guid? departmentId,
//  [FromQuery] Guid? positionId,
//  [FromQuery] int page = 1,
//  [FromQuery] int pageSize = 20,
//  [FromQuery] string? sort = null,
//  CancellationToken ct = default)
//        {
//            try
//            {
//                if (page < 1) page = 1;
//                if (pageSize is < 1 or > 200) pageSize = 20;

//                var query = _db.Employees
//                    .AsNoTracking()
//                    .Include(x => x.Department)
//                    .Include(x => x.Position)
//                    .AsQueryable();

//                // search text
//                if (!string.IsNullOrWhiteSpace(q))
//                {
//                    q = q.Trim();
//                    query = query.Where(x =>
//                        x.FullName.Contains(q) ||
//                        x.Code.Contains(q) ||
//                        (x.Email != null && x.Email.Contains(q)));
//                }

//                // filters
//                if (status is not null) query = query.Where(x => x.Status == status);
//                if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
//                if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

//                // sort
//                query = sort?.Trim() switch
//                {
//                    "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
//                    "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
//                    "-Code" => query.OrderByDescending(x => x.Code),
//                    "Code" => query.OrderBy(x => x.Code),
//                    "-FullName" => query.OrderByDescending(x => x.FullName),
//                    "FullName" => query.OrderBy(x => x.FullName),
//                    _ => query.OrderBy(x => x.FullName)
//                };

//                var total = await query.CountAsync(ct);

//                var items = await query
//                    .Skip((page - 1) * pageSize)
//                    .Take(pageSize)
//                    .Select(x => new EmployeeDto
//                    {
//                        Id = x.Id,
//                        Code = x.Code,
//                        FullName = x.FullName,
//                        Gender = x.Gender,
//                        Dob = x.Dob,
//                        Cccd = x.Cccd,
//                        Email = x.Email,
//                        Phone = x.Phone,
//                        Address = x.Address,
//                        HireDate = x.HireDate,
//                        DepartmentId = x.DepartmentId,
//                        DepartmentName = x.Department!.Name,
//                        PositionId = x.PositionId,
//                        PositionName = x.Position!.Name,
//                        Status = x.Status,
//                        AvatarUrl = x.AvatarUrl
//                    })
//                    .ToListAsync(ct);

//                // Đặt payload phân trang vào trong mảng data theo đúng schema
//                var payload = new { total, page, pageSize, items };

//                return Ok(new
//                {
//                    statusCode = StatusCodes.Status200OK,
//                    message = total > 0 ? $"Tìm thấy {total} nhân viên." : "Không có kết quả.",
//                    data = new[] { payload },
//                    success = true
//                });
//            }
//            catch (Exception)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    statusCode = StatusCodes.Status500InternalServerError,
//                    message = "Đã xảy ra lỗi khi tìm kiếm nhân viên.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//        }


//        /// <summary>
//        /// function lấy nhân viên theo id của nhân viên
//        /// </summary>
//        /// <param name="id">id của nhân viên</param>
//        /// <param name="ct"></param>
//        /// <returns></returns>
//        //[HttpGet("GetEmployeeById")]
//        //[HasPermission("Employees.View")]
//        //public async Task<IActionResult> GetEmployeeById(Guid id, CancellationToken ct)
//        //{
//        //    var e = await _db.Employees.AsNoTracking().Include(x => x.Department).Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == id, ct);

//        //    if (e is null)
//        //    {
//        //        return NotFound();
//        //    }

//        //    var dto = new EmployeeDto
//        //    {
//        //        Id = e.Id,
//        //        Code = e.Code,
//        //        FullName = e.FullName,
//        //        Gender = e.Gender,
//        //        Dob = e.Dob,
//        //        Cccd = e.Cccd,
//        //        Email = e.Email,
//        //        Phone = e.Phone,
//        //        Address = e.Address,
//        //        HireDate = e.HireDate,
//        //        DepartmentId = e.DepartmentId,
//        //        DepartmentName = e.Department?.Name,
//        //        PositionId = e.PositionId,
//        //        PositionName = e.Position?.Name,
//        //        Status = e.Status,
//        //        AvatarUrl = e.AvatarUrl
//        //    };

//        //    return Ok(dto);
//        //}

//        [HttpGet("GetEmployeeById")]
//        [HasPermission("Employees.View")]
//        public async Task<IActionResult> GetEmployeeById(Guid id, CancellationToken ct)
//        {
//            try
//            {
//                var e = await _db.Employees
//                    .AsNoTracking()
//                    .Include(x => x.Department)
//                    .Include(x => x.Position)
//                    .FirstOrDefaultAsync(x => x.Id == id, ct);

//                if (e is null)
//                {
//                    return NotFound(new
//                    {
//                        statusCode = StatusCodes.Status404NotFound,
//                        message = "Không tìm thấy nhân viên.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                var dto = new EmployeeDto
//                {
//                    Id = e.Id,
//                    Code = e.Code,
//                    FullName = e.FullName,
//                    Gender = e.Gender,
//                    Dob = e.Dob,
//                    Cccd = e.Cccd,
//                    Email = e.Email,
//                    Phone = e.Phone,
//                    Address = e.Address,
//                    HireDate = e.HireDate,
//                    DepartmentId = e.DepartmentId,
//                    DepartmentName = e.Department?.Name,
//                    PositionId = e.PositionId,
//                    PositionName = e.Position?.Name,
//                    Status = e.Status,
//                    AvatarUrl = e.AvatarUrl
//                };

//                return Ok(new
//                {
//                    statusCode = StatusCodes.Status200OK,
//                    message = "Lấy thông tin nhân viên thành công.",
//                    data = new[] { dto }, // trả về mảng theo schema yêu cầu
//                    success = true
//                });
//            }
//            catch (Exception)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    statusCode = StatusCodes.Status500InternalServerError,
//                    message = "Đã xảy ra lỗi khi lấy thông tin nhân viên.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//        }


//        /// <summary>
//        /// function tạo thêm nhân viên
//        /// </summary>
//        /// <param name="req">request chứa các tham số cần truyền</param>
//        /// <param name="ct"></param>
//        /// <returns></returns>
//        //[HttpPost]
//        //[HasPermission("Employees.Manage")]
//        //[Authorize(Roles = "HR, Admin")]
//        //public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest req, CancellationToken ct)
//        //{
//        //    // code tự sinh tự động
//        //    string employeeCode = "NV-" + Random.Shared.Next(100000, 999999);

//        //    // tạo đối tượng và truyền giá trị
//        //    var e = new Employee
//        //    {
//        //        Id = Guid.NewGuid(),
//        //        Code = employeeCode,
//        //        FullName = req.FullName,
//        //        Gender = req.Gender,
//        //        Dob = req.Dob,
//        //        Cccd = req.Cccd,
//        //        Email = req.Email!,
//        //        Phone = req.Phone,
//        //        Address = req.Address,
//        //        HireDate = req.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
//        //        DepartmentId = req.DepartmentId,
//        //        PositionId = req.PositionId,
//        //        Status = req.Status ?? EmployeeStatus.active,
//        //        AvatarUrl = req.AvatarUrl
//        //    };

//        //    // thêm vào db đối tượng
//        //    _db.Employees.Add(e);

//        //    // thực hiện save
//        //    await _db.SaveChangesAsync(ct);

//        //    // helper method trả về http 201 (created)
//        //    // cho biết rằng một đối tượng đã tạo thành công
//        //    // tài nguyên có thể truy cập tại endpoint GetEmployeeById
//        //    // trả về http 201 cùng với id của bản ghi mới
//        //    return CreatedAtAction(nameof(GetEmployeeById), new { id = e.Id }, new { e.Id });
//        //}

//        //[HttpPost]
//        //[HasPermission("Employees.Manage")]
//        //[Authorize(Roles = "HR, Admin")]
//        //public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest req, CancellationToken ct)
//        //{
//        //    // code tự sinh tự động
//        //    string employeeCode = "NV-" + Random.Shared.Next(100000, 999999);

//        //    string username = await GenerateUniqueUsernameAsync(req.FullName, ct);

//        //    string companyDomain = "@huynhthanhson.io.vn";

//        //    string employeeEmail = $@"{username}{companyDomain}";

//        //    string tempPasswordEmail = "Temp@123";

//        //    // tạo đối tượng và truyền giá trị
//        //    var e = new Employee
//        //    {
//        //        Id = Guid.NewGuid(),
//        //        Code = employeeCode,
//        //        FullName = req.FullName,
//        //        Gender = req.Gender,
//        //        Dob = req.Dob,
//        //        Cccd = req.Cccd,
//        //        Email = req.Email!,
//        //        //Email = employeeEmail,
//        //        Phone = req.Phone,
//        //        Address = req.Address,
//        //        HireDate = req.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
//        //        DepartmentId = req.DepartmentId,
//        //        PositionId = req.PositionId,
//        //        Status = req.Status ?? EmployeeStatus.active,
//        //        AvatarUrl = req.AvatarUrl
//        //    };

//        //    // thêm vào db đối tượng
//        //    _db.Employees.Add(e);


//        //    string tempPassword = GenerateTempPassword();

//        //    var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User", ct);

//        //    if (role == null)
//        //    {
//        //        return BadRequest("Không tìm thấy role hợp lệ");
//        //    }

//        //    var user = new User
//        //    {
//        //        Id = Guid.NewGuid(),
//        //        EmployeeId = e.Id,
//        //        UserName = username,
//        //        RoleId = role.Id,
//        //        Status = UserStatus.active
//        //    };

//        //    user.PasswordHash = _hasher.HashPassword(user, tempPassword);
//        //    _db.Users.Add(user);

//        //    // thực hiện save
//        //    await _db.SaveChangesAsync(ct);

//        //    string To = req.Email!;
//        //    string Subject = "Tài khoản nhân sự của bạn đã được tạo";

//        //    //string Body = $@"
//        //    //<h1>Xin chào {req.FullName},</h1>

//        //    //<h2>Tài khoản của bạn trong hệ thống HRM đã được tạo thành công.</h2>

//        //    //🧾 Thông tin đăng nhập:
//        //    //- Username: {username}
//        //    //- Mật khẩu tạm: {tempPassword}
//        //    //- Email công ty: {employeeEmail}
//        //    //- Mật khẩu Email: {tempPasswordEmail}

//        //    //Vui lòng đăng nhập và đổi mật khẩu ngay tại:
//        //    //👉 link/reset-password

//        //    //Thân mến,
//        //    //Phòng Nhân sự";

//        //    string Body = $@"
//        //        <!DOCTYPE html>
//        //        <html lang='vi'>
//        //        <head>
//        //        <meta charset='UTF-8'>
//        //        <title>Thông báo tạo tài khoản HRM</title>
//        //        <style>
//        //            body {{
//        //                font-family: 'Segoe UI', Arial, sans-serif;
//        //                background-color: #F5F1DC;
//        //                margin: 0;
//        //                padding: 0;
//        //            }}
//        //            .container {{
//        //                max-width: 600px;
//        //                margin: 40px auto;
//        //                background: #ffffff;
//        //                border-radius: 10px;
//        //                box-shadow: 0 4px 10px rgba(0,0,0,0.05);
//        //                overflow: hidden;
//        //            }}
//        //            .header {{
//        //                background-color: #0046FF;
//        //                color: white;
//        //                text-align: center;
//        //                padding: 20px 10px;
//        //            }}
//        //            .header h1 {{
//        //                margin: 0;
//        //                font-size: 24px;
//        //                letter-spacing: 0.5px;
//        //            }}
//        //            .content {{
//        //                padding: 25px 30px;
//        //                color: #333;
//        //                line-height: 1.6;
//        //            }}
//        //            .content h2 {{
//        //                color: #0046FF;
//        //                font-size: 18px;
//        //                margin-top: 0;
//        //            }}
//        //            .info-box {{
//        //                background-color: #73C8D2;
//        //                color: #fff;
//        //                border-radius: 8px;
//        //                padding: 15px 20px;
//        //                margin: 15px 0;
//        //                font-size: 15px;
//        //            }}
//        //            .info-box strong {{
//        //                color: #F5F1DC;
//        //            }}
//        //            .button {{
//        //                display: inline-block;
//        //                background-color: #FF9013;
//        //                color: #fff !important;
//        //                text-decoration: none;
//        //                padding: 12px 24px;
//        //                border-radius: 5px;
//        //                font-weight: 600;
//        //                margin-top: 20px;
//        //            }}
//        //            .footer {{
//        //                background-color: #F5F1DC;
//        //                color: #666;
//        //                text-align: center;
//        //                padding: 10px;
//        //                font-size: 13px;
//        //            }}
//        //        </style>
//        //        </head>
//        //        <body>
//        //        <div class='container'>
//        //            <div class='header'>
//        //                <h1>Xin chào {req.FullName}</h1>
//        //            </div>
//        //            <div class='content'>
//        //                <h2>Tài khoản của bạn trong hệ thống HRM đã được tạo thành công 🎉</h2>
//        //                <p>Dưới đây là thông tin đăng nhập tạm thời của bạn:</p>
//        //                <div class='info-box'>
//        //                    <p><strong>Username:</strong> {username}</p>
//        //                    <p><strong>Mật khẩu tạm:</strong> {tempPassword}</p>
//        //                    <p><strong>Email công ty:</strong> {employeeEmail}</p>
//        //                    <p><strong>Mật khẩu Email:</strong> {tempPasswordEmail}</p>
//        //                </div>
//        //                <p>Vui lòng đăng nhập và <strong>đổi mật khẩu ngay</strong> tại đường dẫn sau:</p>
//        //                <p><a href='https://yourdomain.com/link/reset-password' class='button'>Đặt lại mật khẩu</a></p>
//        //                <p style='margin-top:25px;'>Thân mến,<br><strong>Phòng Nhân sự</strong></p>
//        //            </div>
//        //            <div class='footer'>
//        //                <p>© {DateTime.Now.Year} HRM System | Công ty Huỳnh Thanh Sơn</p>
//        //            </div>
//        //        </div>
//        //        </body>
//        //        </html>";


//        //    await _emailSender.SendAsync(To, Subject, Body, ct);



//        //    // helper method trả về http 201 (created)
//        //    // cho biết rằng một đối tượng đã tạo thành công
//        //    // tài nguyên có thể truy cập tại endpoint GetEmployeeById
//        //    // trả về http 201 cùng với id của bản ghi mới
//        //    return CreatedAtAction(nameof(GetEmployeeById), new { id = e.Id }, new { e.Id, username });
//        //}

//        [HttpPost]
//        [HasPermission("Employees.Manage")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest req, CancellationToken ct)
//        {
//            try
//            {
//                if (req is null || string.IsNullOrWhiteSpace(req.FullName))
//                {
//                    return BadRequest(new
//                    {
//                        statusCode = StatusCodes.Status400BadRequest,
//                        message = "Dữ liệu không hợp lệ.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                // code tự sinh tự động (tránh đụng độ hiếm bằng vòng lặp nhỏ)
//                string employeeCode;
//                do
//                {
//                    employeeCode = "NV-" + Random.Shared.Next(100000, 999999);
//                } while (await _db.Employees.AnyAsync(x => x.Code == employeeCode, ct));

//                string username = await GenerateUniqueUsernameAsync(req.FullName, ct);
//                const string companyDomain = "@huynhthanhson.io.vn";
//                string employeeEmail = $@"{username}{companyDomain}";
//                string tempPasswordEmail = "Temp@123";

//                var e = new Employee
//                {
//                    Id = Guid.NewGuid(),
//                    Code = employeeCode,
//                    FullName = req.FullName,
//                    Gender = req.Gender,
//                    Dob = req.Dob,
//                    Cccd = req.Cccd,
//                    Email = req.Email!, // hoặc dùng: employeeEmail nếu bạn muốn tạo email cty tự động
//                    Phone = req.Phone,
//                    Address = req.Address,
//                    HireDate = req.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
//                    DepartmentId = req.DepartmentId,
//                    PositionId = req.PositionId,
//                    Status = req.Status ?? EmployeeStatus.active,
//                    AvatarUrl = req.AvatarUrl
//                };

//                _db.Employees.Add(e);

//                string tempPassword = GenerateTempPassword();

//                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User", ct);
//                if (role is null)
//                {
//                    return BadRequest(new
//                    {
//                        statusCode = StatusCodes.Status400BadRequest,
//                        message = "Không tìm thấy role hợp lệ.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                var user = new User
//                {
//                    Id = Guid.NewGuid(),
//                    EmployeeId = e.Id,
//                    UserName = username,
//                    RoleId = role.Id,
//                    Status = UserStatus.active
//                };
//                user.PasswordHash = _hasher.HashPassword(user, tempPassword);
//                _db.Users.Add(user);

//                await _db.SaveChangesAsync(ct);

//                // Gửi mail (nếu fail vẫn có thể coi là tạo thành công, tuỳ policy của bạn)
//                var to = req.Email!;
//                var subject = "Tài khoản nhân sự của bạn đã được tạo";
//                var body = $@"
//            <!DOCTYPE html>
//            <html lang='vi'>
//            <head><meta charset='UTF-8'><title>Thông báo tạo tài khoản HRM</title></head>
//            <body>
//                <h2>Xin chào {req.FullName},</h2>
//                <p>Tài khoản HRM đã được tạo.</p>
//                <ul>
//                    <li><b>Username:</b> {username}</li>
//                    <li><b>Mật khẩu tạm:</b> {tempPassword}</li>
//                    <li><b>Email công ty:</b> {employeeEmail}</li>
//                    <li><b>Mật khẩu Email:</b> {tempPasswordEmail}</li>
//                </ul>
//                <p>Vui lòng đổi mật khẩu ngay sau khi đăng nhập.</p>
//            </body></html>";

//                try
//                {
//                    await _emailSender.SendAsync(to, subject, body, ct);
//                }
//                catch
//                {
//                    // Không chặn tạo; chỉ báo mail lỗi nếu cần
//                }

//                // 201 Created theo schema yêu cầu
//                return StatusCode(StatusCodes.Status201Created, new
//                {
//                    statusCode = StatusCodes.Status201Created,
//                    message = "Tạo nhân viên thành công.",
//                    data = Array.Empty<object>(),
//                    success = true
//                });
//            }
//            catch (DbUpdateException)
//            {
//                // Khả năng vi phạm unique (Email/CCCD/Code...) hoặc FK
//                return StatusCode(StatusCodes.Status409Conflict, new
//                {
//                    statusCode = StatusCodes.Status409Conflict,
//                    message = "Không thể tạo nhân viên do xung đột dữ liệu (trùng hoặc ràng buộc).",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//            catch (Exception)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    statusCode = StatusCodes.Status500InternalServerError,
//                    message = "Đã xảy ra lỗi không xác định khi tạo nhân viên.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//        }


//        //[HttpPut]
//        //[HasPermission("Employees.Manage")]
//        //[Authorize(Roles = "HR, Admin")]
//        //public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest req, CancellationToken ct)
//        //{
//        //    // chọn nhân viên update
//        //    var e = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

//        //    // kiểm tra null
//        //    if (e is null)
//        //    {
//        //        return NotFound();
//        //    }

//        //    // kiểm tra các giá trị đã tồn tại rồi thì thông báo mâu thuẫn
//        //    if (!string.Equals(e.Code, req.Code, StringComparison.OrdinalIgnoreCase) && await _db.Employees.AnyAsync(x => x.Code == req.Code, ct))
//        //    {
//        //        return Conflict(ProblemDetails("Duplicate Code", "Code đã tồn tại"));
//        //    }

//        //    if (!string.Equals(e.Email, req.Email, StringComparison.OrdinalIgnoreCase) && await _db.Employees.AnyAsync(x => x.Email == req.Email, ct))
//        //    {
//        //        return Conflict(ProblemDetails("Duplicate Email", "Email đã tồn tại."));
//        //    }

//        //    if (!string.Equals(e.Cccd, req.Cccd, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(req.Cccd) && await _db.Employees.AnyAsync(x => x.Cccd == req.Cccd, ct))
//        //    {
//        //        return Conflict(ProblemDetails("Duplicate CCCD", "CCCD đã tồn tại."));
//        //    }

//        //    // gán giá trị
//        //    string employeeCode = "NV-" + Random.Shared.Next(100000, 999999);

//        //    e.Code = req.Code!;
//        //    e.FullName = req.FullName!;
//        //    e.Gender = req.Gender;
//        //    e.Dob = req.Dob;
//        //    e.Cccd = req.Cccd;
//        //    e.Email = req.Email!;
//        //    e.Phone = req.Phone;
//        //    e.Address = req.Address;
//        //    e.HireDate = req.HireDate ?? e.HireDate;
//        //    e.DepartmentId = req.DepartmentId;
//        //    e.PositionId = req.PositionId;
//        //    e.Status = req.Status ?? e.Status;
//        //    e.AvatarUrl = req.AvatarUrl;

//        //    await _db.SaveChangesAsync(ct);

//        //    return NoContent();
//        //}

//        [HttpPut("{id:guid}")]
//        [HasPermission("Employees.Manage")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest req, CancellationToken ct)
//        {
//            try
//            {
//                // chọn nhân viên update
//                var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);

//                // kiểm tra null
//                if (e is null)
//                {
//                    return NotFound(new
//                    {
//                        statusCode = StatusCodes.Status404NotFound,
//                        message = "Không tìm thấy nhân viên.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                // kiểm tra trùng dữ liệu
//                if (!string.Equals(e.Code, req.Code, StringComparison.OrdinalIgnoreCase)
//                    && await _db.Employees.AnyAsync(x => x.Code == req.Code, ct))
//                {
//                    return StatusCode(StatusCodes.Status409Conflict, new
//                    {
//                        statusCode = StatusCodes.Status409Conflict,
//                        message = "Code đã tồn tại.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                if (!string.Equals(e.Email, req.Email, StringComparison.OrdinalIgnoreCase)
//                    && await _db.Employees.AnyAsync(x => x.Email == req.Email, ct))
//                {
//                    return StatusCode(StatusCodes.Status409Conflict, new
//                    {
//                        statusCode = StatusCodes.Status409Conflict,
//                        message = "Email đã tồn tại.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                if (!string.Equals(e.Cccd, req.Cccd, StringComparison.OrdinalIgnoreCase)
//                    && !string.IsNullOrWhiteSpace(req.Cccd)
//                    && await _db.Employees.AnyAsync(x => x.Cccd == req.Cccd, ct))
//                {
//                    return StatusCode(StatusCodes.Status409Conflict, new
//                    {
//                        statusCode = StatusCodes.Status409Conflict,
//                        message = "CCCD đã tồn tại.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                // gán giá trị
//                e.Code = req.Code!;
//                e.FullName = req.FullName!;
//                e.Gender = req.Gender;
//                e.Dob = req.Dob;
//                e.Cccd = req.Cccd;
//                e.Email = req.Email!;
//                e.Phone = req.Phone;
//                e.Address = req.Address;
//                e.HireDate = req.HireDate ?? e.HireDate;
//                e.DepartmentId = req.DepartmentId;
//                e.PositionId = req.PositionId;
//                e.Status = req.Status ?? e.Status;
//                e.AvatarUrl = req.AvatarUrl;

//                await _db.SaveChangesAsync(ct);

//                return Ok(new
//                {
//                    statusCode = StatusCodes.Status200OK,
//                    message = "Cập nhật nhân viên thành công.",
//                    data = Array.Empty<object>(),
//                    success = true
//                });
//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                return StatusCode(StatusCodes.Status409Conflict, new
//                {
//                    statusCode = StatusCodes.Status409Conflict,
//                    message = "Xung đột cập nhật: bản ghi đã thay đổi trước đó.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//            catch (Exception)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    statusCode = StatusCodes.Status500InternalServerError,
//                    message = "Đã xảy ra lỗi không xác định khi cập nhật nhân viên.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//        }


//        //[HttpDelete("{id:guid}")]
//        //[HasPermission("Employees.Manage")]
//        //[Authorize(Roles = "HR, Admin")]
//        //public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
//        //{
//        //    var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);

//        //    if (e is null)
//        //    {
//        //        return NotFound();
//        //    }

//        //    _db.Employees.Remove(e);

//        //    await _db.SaveChangesAsync(ct);

//        //    return NoContent();
//        //}

//        [HttpDelete("{id:guid}")]
//        [HasPermission("Employees.Manage")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
//        {
//            try
//            {
//                var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);

//                if (e is null)
//                {
//                    return NotFound(new
//                    {
//                        statusCode = StatusCodes.Status404NotFound,
//                        message = "Không tìm thấy nhân viên.",
//                        data = Array.Empty<object>(),
//                        success = false
//                    });
//                }

//                _db.Employees.Remove(e);
//                await _db.SaveChangesAsync(ct);

//                return Ok(new
//                {
//                    statusCode = StatusCodes.Status200OK,
//                    message = "Xoá nhân viên thành công.",
//                    data = Array.Empty<object>(),
//                    success = true
//                });
//            }
//            catch (DbUpdateException)
//            {
//                // Thường gặp khi có ràng buộc FK (vd: Users, Contracts...) chặn xoá.
//                return StatusCode(StatusCodes.Status409Conflict, new
//                {
//                    statusCode = StatusCodes.Status409Conflict,
//                    message = "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//            catch (Exception)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    statusCode = StatusCodes.Status500InternalServerError,
//                    message = "Đã xảy ra lỗi không xác định khi xoá nhân viên.",
//                    data = Array.Empty<object>(),
//                    success = false
//                });
//            }
//        }


//        // 12 10 2025
//        [HttpGet("NoCompanyEmail")]
//        [HasPermission("Employees.View")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> GetEmployeesWithoutCompanyEmail(
//            [FromQuery] string? q,
//            [FromQuery] EmployeeStatus? status,
//            [FromQuery] Guid? departmentId,
//            [FromQuery] Guid? positionId,
//            [FromQuery] int page = 1,
//            [FromQuery] int pageSize = 20,
//            CancellationToken ct = default)
//        {
//            try
//            {
//                if (page < 1) page = 1;
//                if (pageSize is < 1 or > 200) pageSize = 20;

//                const string companyDomain = "@huynhthanhson.io.vn";

//                // Base query nhân viên
//                var baseQuery = _db.Employees
//                    .AsNoTracking()
//                    .Include(x => x.Department)
//                    .Include(x => x.Position)
//                    .AsQueryable();

//                // Search text
//                if (!string.IsNullOrWhiteSpace(q))
//                {
//                    q = q.Trim();
//                    baseQuery = baseQuery.Where(x =>
//                        x.FullName.Contains(q) ||
//                        x.Code.Contains(q) ||
//                        (x.Email != null && x.Email.Contains(q)));
//                }

//                // Filters
//                if (status is not null) baseQuery = baseQuery.Where(x => x.Status == status);
//                if (departmentId is not null) baseQuery = baseQuery.Where(x => x.DepartmentId == departmentId);
//                if (positionId is not null) baseQuery = baseQuery.Where(x => x.PositionId == positionId);

//                // Điều kiện "chưa có email công ty"
//                baseQuery = baseQuery.Where(x => string.IsNullOrEmpty(x.Email) || !x.Email!.EndsWith(companyDomain));

//                // Sắp xếp để phân trang ổn định
//                baseQuery = baseQuery.OrderBy(x => x.FullName).ThenBy(x => x.Code);

//                // Join sang Users (LEFT JOIN) để lấy UserId và đề xuất CompanyEmail theo UserName (nếu có)
//                var query =
//                    from e in baseQuery
//                    join u in _db.Users.AsNoTracking() on e.Id equals u.EmployeeId into gj
//                    from u in gj.DefaultIfEmpty()
//                    select new
//                    {
//                        UserId = (Guid?)(u != null ? u.Id : null),
//                        EmployeeId = e.Id,
//                        CurrentEmail = e.Email,
//                        CompanyEmail = u != null ? (u.UserName + companyDomain) : null
//                    };

//                var total = await query.CountAsync(ct);

//                var data = await query
//                    .Skip((page - 1) * pageSize)
//                    .Take(pageSize)
//                    .ToListAsync(ct);

//                return Ok(new
//                {
//                    statusCode = StatusCodes.Status200OK,
//                    message = $"Tìm thấy {total} nhân viên chưa có email công ty.",
//                    data,
//                    success = true
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(StatusCodes.Status500InternalServerError, new
//                {
//                    statusCode = StatusCodes.Status500InternalServerError,
//                    message = "Đã xảy ra lỗi khi truy vấn danh sách.",
//                    data = Array.Empty<object>(),
//                    success = false,
//                    // Tip: có thể log ex.Message nội bộ, không nên trả full stack trace ra ngoài
//                });
//            }
//        }

//        private async Task<string> GenerateUniqueUsernameAsync(string fullName, CancellationToken ct)
//        {
//            var parts = fullName
//                .Trim()
//                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
//                .Select(p => p.Normalize(NormalizationForm.FormD)
//                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
//                    .Aggregate("", (acc, c) => acc + c)
//                    .ToLower())
//                .ToList();

//            var firstName = parts.Last();
//            var initials = string.Concat(parts.Take(parts.Count - 1).Select(p => p[0]));
//            for (int i = 1; i <= 99; i++)
//            {
//                string username = $"{firstName}{initials}{i:D2}";
//                bool exists = await _db.Users.AnyAsync(u => u.UserName == username, ct);
//                if (!exists)
//                    return username;
//            }
//            throw new InvalidOperationException("Không thể tạo username duy nhất.");
//        }

//        private static string GenerateTempPassword(int length = 14)
//        {
//            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@$%";

//            var bytes = RandomNumberGenerator.GetBytes(length);

//            var chars = new char[length];

//            for (int i = 0; i < length; i++)
//            {
//                chars[i] = alphabet[bytes[i] % alphabet.Length];
//            }

//            return new string(chars);
//        }


//        private static ProblemDetails ProblemDetails(string title, string detail)
//        {
//            return new() { Title = title, Detail = detail, Status = StatusCodes.Status409Conflict };
//        }
//    }
//}

// firstcode

using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using DeTaiNhanSu.Services.Email;
using System.Security.Cryptography;

// THÊM dòng này để dùng this.OK/this.FAIL/...
using DeTaiNhanSu.Common;
using System.Text.Json;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IPasswordHasher<User> _hasher;

        public EmployeeController(AppDbContext db, IEmailSender emailSender, IPasswordHasher<User> hasher)
        {
            _db = db;
            _emailSender = emailSender;
            _hasher = hasher;
        }

        //[HttpGet]
        //[HasPermission("Employees.View")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Search(
        //    [FromQuery] string? q,
        //    [FromQuery] EmployeeStatus? status,
        //    [FromQuery] Guid? departmentId,
        //    [FromQuery] Guid? positionId,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    [FromQuery] string? sort = null,
        //    CancellationToken ct = default)
        //{
        //    try
        //    {
        //        if (page < 1) page = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        var query = _db.Employees
        //            .AsNoTracking()
        //            .Include(x => x.Department)
        //            .Include(x => x.Position)
        //            .AsQueryable();

        //        if (!string.IsNullOrWhiteSpace(q))
        //        {
        //            q = q.Trim();
        //            query = query.Where(x =>
        //                x.FullName.Contains(q) ||
        //                x.Code.Contains(q) ||
        //                (x.Email != null && x.Email.Contains(q)));
        //        }

        //        if (status is not null) query = query.Where(x => x.Status == status);
        //        if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
        //        if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

        //        query = sort?.Trim() switch
        //        {
        //            "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
        //            "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
        //            "-Code" => query.OrderByDescending(x => x.Code),
        //            "Code" => query.OrderBy(x => x.Code),
        //            "-FullName" => query.OrderByDescending(x => x.FullName),
        //            "FullName" => query.OrderBy(x => x.FullName),
        //            _ => query.OrderBy(x => x.FullName)
        //        };

        //        var total = await query.CountAsync(ct);

        //        var items = await query
        //            .Skip((page - 1) * pageSize)
        //            .Take(pageSize)
        //            .Select(x => new EmployeeDto
        //            {
        //                Id = x.Id,
        //                Code = x.Code,
        //                FullName = x.FullName,
        //                Gender = x.Gender,
        //                Dob = x.Dob,
        //                Cccd = x.Cccd,
        //                Email = x.Email,
        //                Phone = x.Phone,
        //                Address = x.Address,
        //                HireDate = x.HireDate,
        //                DepartmentId = x.DepartmentId,
        //                DepartmentName = x.Department!.Name,
        //                PositionId = x.PositionId,
        //                PositionName = x.Position!.Name,
        //                Status = x.Status,
        //                AvatarUrl = x.AvatarUrl
        //            })
        //            .ToListAsync(ct);

        //        var payload = new { total, page, pageSize, items };
        //        return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} nhân viên." : "Không có kết quả.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm nhân viên.");
        //    }
        //}

        [HttpGet]
        [HasPermission("Employees.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
    [FromQuery] string? q,
    [FromQuery] EmployeeStatus? status,
    [FromQuery] Guid? departmentId,
    [FromQuery] Guid? positionId,
    [FromQuery] int current = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? sort = null,
    CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Employees
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Position)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(x =>
                        x.FullName.Contains(q) ||
                        x.Code.Contains(q) ||
                        (x.Email != null && x.Email.Contains(q)));
                }

                if (status is not null) query = query.Where(x => x.Status == status);
                if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
                if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

                query = sort?.Trim() switch
                {
                    "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
                    "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
                    "-Code" => query.OrderByDescending(x => x.Code),
                    "Code" => query.OrderBy(x => x.Code),
                    "-FullName" => query.OrderByDescending(x => x.FullName),
                    "FullName" => query.OrderBy(x => x.FullName),
                    _ => query.OrderBy(x => x.FullName)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new EmployeeDto
                    {
                        Id = x.Id,
                        Code = x.Code,
                        FullName = x.FullName,
                        Gender = x.Gender,
                        Dob = x.Dob,
                        Cccd = x.Cccd,
                        Email = x.Email,
                        Phone = x.Phone,
                        Address = x.Address,
                        HireDate = x.HireDate,
                        DepartmentId = x.DepartmentId,
                        DepartmentName = x.Department != null ? x.Department.Name : null,
                        PositionId = x.PositionId,
                        PositionName = x.Position != null ? x.Position.Name : null,
                        Status = x.Status,
                        AvatarUrl = x.AvatarUrl
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current = current,
                    pageSize = pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                var payload = new { meta, result };
                return this.OKSingle(payload, total > 0 ? $"Tìm thấy {total} nhân viên." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm nhân viên.");
            }
        }

        [HttpGet("all")]
        [HasPermission("Employees.View")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetAll(
    [FromQuery] string? q,
    [FromQuery] EmployeeStatus? status,
    [FromQuery] Guid? departmentId,
    [FromQuery] Guid? positionId,
    [FromQuery] string? sort = null,
    CancellationToken ct = default)
        {
            try
            {
                var query = _db.Employees
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Position)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var qn = q.Trim();
                    query = query.Where(x =>
                        x.FullName.Contains(qn) ||
                        x.Code.Contains(qn) ||
                        (x.Email != null && x.Email.Contains(qn)));
                }

                if (status is not null) query = query.Where(x => x.Status == status);
                if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
                if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

                query = sort?.Trim() switch
                {
                    "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
                    "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
                    "-Code" => query.OrderByDescending(x => x.Code),
                    "Code" => query.OrderBy(x => x.Code),
                    "-FullName" => query.OrderByDescending(x => x.FullName),
                    "FullName" => query.OrderBy(x => x.FullName),
                    _ => query.OrderBy(x => x.FullName)
                };

                var result = await query
                    .Select(x => new EmployeeDto
                    {
                        Id = x.Id,
                        Code = x.Code,
                        FullName = x.FullName,
                        Gender = x.Gender,
                        Dob = x.Dob,
                        Cccd = x.Cccd,
                        Email = x.Email,
                        Phone = x.Phone,
                        Address = x.Address,
                        HireDate = x.HireDate,
                        DepartmentId = x.DepartmentId,
                        DepartmentName = x.Department != null ? x.Department.Name : null,
                        PositionId = x.PositionId,
                        PositionName = x.Position != null ? x.Position.Name : null,
                        Status = x.Status,
                        AvatarUrl = x.AvatarUrl
                    })
                    .ToListAsync(ct);

                var total = result.Count;
                var meta = new
                {
                    current = 1,
                    pageSize = total, // không phân trang
                    pages = 1,
                    total
                };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Có {total} nhân viên." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy danh sách nhân viên.");
            }
        }


        //    [HttpGet("filter")]
        //    [Authorize(Roles = "HR, Admin")]
        //    public async Task<IActionResult> SelectiveSearch(
        //[FromQuery] string fields,
        //[FromQuery] string? q,

        //// ==== filter theo từng thuộc tính Employee ====
        //[FromQuery] Guid? id,
        //[FromQuery] string? code,                 // so khớp chính xác
        //[FromQuery] string? codeLike,             // contains
        //[FromQuery] string? fullName,             // exact
        //[FromQuery] string? fullNameLike,         // contains
        //[FromQuery] Gender? gender,
        //[FromQuery] DateOnly? dobFrom,
        //[FromQuery] DateOnly? dobTo,
        //[FromQuery] string? cccd,                 // exact
        //[FromQuery] string? email,                // exact
        //[FromQuery] string? emailLike,            // contains
        //[FromQuery] string? phone,                // exact
        //[FromQuery] string? phoneLike,            // contains
        //[FromQuery] string? addressLike,          // contains
        //[FromQuery] DateOnly? hireDateFrom,
        //[FromQuery] DateOnly? hireDateTo,
        //[FromQuery] EmployeeStatus? status,       // (đã có)
        //[FromQuery] Guid? departmentId,           // (đã có)
        //[FromQuery] Guid? positionId,             // (đã có)

        //[FromQuery] int current = 1,
        //[FromQuery] int pageSize = 20,
        //[FromQuery] string? sort = null,
        //CancellationToken ct = default)
        //    {
        //        try
        //        {
        //            if (string.IsNullOrWhiteSpace(fields))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu tham số 'fields'.");

        //            if (current < 1) current = 1;
        //            if (pageSize is < 1 or > 200) pageSize = 20;

        //            var reqFields = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //                                  .Select(f => f.ToLowerInvariant())
        //                                  .Distinct()
        //                                  .ToList();

        //            var allowed = new HashSet<string>(new[]
        //                    {
        //                "id","code","fullname","gender","dob","cccd","email","phone","address","hiredate",
        //                "departmentid","departmentname","positionid","positionname","avatarurl"
        //            });
        //            var invalid = reqFields.Where(f => !allowed.Contains(f)).ToList();
        //            if (invalid.Count > 0)
        //                return this.FAIL(StatusCodes.Status400BadRequest, $"Trường không hợp lệ: {string.Join(", ", invalid)}");

        //            var query = _db.Employees.AsNoTracking().AsQueryable();

        //            if (!string.IsNullOrWhiteSpace(q))
        //            {
        //                q = q.Trim();
        //                query = query.Where(x =>
        //                    x.FullName.Contains(q) ||
        //                    x.Code.Contains(q) ||
        //                    (x.Email != null && x.Email.Contains(q)));
        //            }

        //            // filter theo giá trị

        //            // ==== filter theo từng field ====
        //            if (id is not null) query = query.Where(x => x.Id == id);
        //            if (!string.IsNullOrWhiteSpace(code))
        //            {
        //                var v = code.Trim();
        //                query = query.Where(x => x.Code == v);
        //            }
        //            if (!string.IsNullOrWhiteSpace(codeLike))
        //            {
        //                var v = codeLike.Trim();
        //                query = query.Where(x => x.Code.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(fullName))
        //            {
        //                var v = fullName.Trim();
        //                query = query.Where(x => x.FullName == v);
        //            }
        //            if (!string.IsNullOrWhiteSpace(fullNameLike))
        //            {
        //                var v = fullNameLike.Trim();
        //                query = query.Where(x => x.FullName.Contains(v));
        //            }
        //            if (gender is not null) query = query.Where(x => x.Gender == gender);
        //            if (dobFrom is not null) query = query.Where(x => x.Dob >= dobFrom);
        //            if (dobTo is not null) query = query.Where(x => x.Dob <= dobTo);
        //            if (!string.IsNullOrWhiteSpace(cccd))
        //            {
        //                var v = cccd.Trim();
        //                query = query.Where(x => x.Cccd == v);
        //            }
        //            if (!string.IsNullOrWhiteSpace(email))
        //            {
        //                var v = email.Trim();
        //                query = query.Where(x => x.Email == v);
        //            }
        //            if (!string.IsNullOrWhiteSpace(emailLike))
        //            {
        //                var v = emailLike.Trim();
        //                query = query.Where(x => x.Email != null && x.Email.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(phone))
        //            {
        //                var v = phone.Trim();
        //                query = query.Where(x => x.Phone == v);
        //            }
        //            if (!string.IsNullOrWhiteSpace(phoneLike))
        //            {
        //                var v = phoneLike.Trim();
        //                query = query.Where(x => x.Phone != null && x.Phone.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(addressLike))
        //            {
        //                var v = addressLike.Trim();
        //                query = query.Where(x => x.Address != null && x.Address.Contains(v));
        //            }
        //            if (hireDateFrom is not null) query = query.Where(x => x.HireDate >= hireDateFrom);
        //            if (hireDateTo is not null) query = query.Where(x => x.HireDate <= hireDateTo);
        //            if (status is not null) query = query.Where(x => x.Status == status);
        //            if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
        //            if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

        //            query = sort?.Trim() switch
        //            {
        //                "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
        //                "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
        //                "-Code" => query.OrderByDescending(x => x.Code),
        //                "Code" => query.OrderBy(x => x.Code),
        //                "-FullName" => query.OrderByDescending(x => x.FullName),
        //                "FullName" => query.OrderBy(x => x.FullName),
        //                _ => query.OrderBy(x => x.FullName)
        //            };

        //            var total = await query.CountAsync(ct);

        //            // 1) EF chỉ select đúng cột cần thiết -> IQueryable<EmployeeFlat>
        //            var projected = ProjectEmployees(query, reqFields);

        //            // 2) Materialize
        //            var list = await projected
        //                .Skip((current - 1) * pageSize)
        //                .Take(pageSize)
        //                .ToListAsync(ct);

        //            // 3) Shape output theo fields (trên bộ nhớ) — ẩn hẳn field không được yêu cầu
        //            var result = list.Select(x =>
        //            {
        //                var o = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;

        //                void Add(string key, object? val)
        //                {
        //                    if (reqFields.Contains(key)) o[key switch
        //                    {
        //                        // mapping key output theo camelCase
        //                        "fullname" => "fullName",
        //                        "hiredate" => "hireDate",
        //                        "departmentid" => "departmentId",
        //                        "departmentname" => "departmentName",
        //                        "positionid" => "positionId",
        //                        "positionname" => "positionName",
        //                        "avatarurl" => "avatarUrl",
        //                        _ => key
        //                    }] = val;
        //                }

        //                Add("id", x.Id);
        //                Add("code", x.Code);
        //                Add("fullname", x.FullName);
        //                Add("gender", x.Gender);
        //                Add("dob", x.Dob);
        //                Add("cccd", x.Cccd);
        //                Add("email", x.Email);
        //                Add("phone", x.Phone);
        //                Add("address", x.Address);
        //                Add("hiredate", x.HireDate);
        //                Add("departmentid", x.DepartmentId);
        //                Add("departmentname", x.DepartmentName);
        //                Add("positionid", x.PositionId);
        //                Add("positionname", x.PositionName);
        //                Add("status", x.Status);
        //                Add("avatarurl", x.AvatarUrl);
        //                return (object)o;
        //            }).ToList();

        //            var meta = new
        //            {
        //                current,
        //                pageSize,
        //                pages = (int)Math.Ceiling(total / (double)pageSize),
        //                total
        //            };

        //            return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy {total} nhân viên." : "Không có kết quả.");
        //        }
        //        catch
        //        {
        //            return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi truy vấn danh sách.");
        //        }
        //    }

        //    [HttpGet("filter")]
        //    [Authorize(Roles = "HR, Admin")]
        //    public async Task<IActionResult> SelectiveSearch(
        //// chọn cột trả về (bắt buộc)
        //[FromQuery] string fields,

        //// full-text tổng quát
        //[FromQuery] string? q,

        //// ==== filter theo từng thuộc tính Employee (chỉ còn param "không like") ====
        //[FromQuery] Guid? id,
        //[FromQuery] string? code,           // dùng Contains
        //[FromQuery] string? fullName,       // dùng Contains
        //[FromQuery] Gender? gender,
        //[FromQuery] DateOnly? dobFrom,
        //[FromQuery] DateOnly? dobTo,
        //[FromQuery] string? cccd,           // dùng Contains
        //[FromQuery] string? email,          // dùng Contains
        //[FromQuery] string? phone,          // dùng Contains
        //[FromQuery] string? address,        // dùng Contains
        //[FromQuery] DateOnly? hireDateFrom,
        //[FromQuery] DateOnly? hireDateTo,
        //[FromQuery] EmployeeStatus? status,
        //[FromQuery] Guid? departmentId,
        //[FromQuery] Guid? positionId,

        //// paging + sort
        //[FromQuery] int current = 1,
        //[FromQuery] int pageSize = 20,
        //[FromQuery] string? sort = null,
        //CancellationToken ct = default)
        //    {
        //        try
        //        {
        //            if (string.IsNullOrWhiteSpace(fields))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Thiếu tham số 'fields'.");

        //            if (current < 1) current = 1;
        //            if (pageSize is < 1 or > 200) pageSize = 20;

        //            var reqFields = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //                                  .Select(f => f.ToLowerInvariant())
        //                                  .Distinct()
        //                                  .ToList();

        //            // BỔ SUNG 'status' vào allowed
        //            var allowed = new HashSet<string>(new[]
        //            {
        //        "id","code","fullname","gender","dob","cccd","email","phone","address","hiredate",
        //        "departmentid","departmentname","positionid","positionname","status","avatarurl"
        //    });
        //            var invalid = reqFields.Where(f => !allowed.Contains(f)).ToList();
        //            if (invalid.Count > 0)
        //                return this.FAIL(StatusCodes.Status400BadRequest, $"Trường không hợp lệ: {string.Join(", ", invalid)}");

        //            var query = _db.Employees.AsNoTracking().AsQueryable();

        //            // ==== full-text q (giữ Contains) ====
        //            if (!string.IsNullOrWhiteSpace(q))
        //            {
        //                var qn = q.Trim();
        //                query = query.Where(x =>
        //                    x.FullName.Contains(qn) ||
        //                    x.Code.Contains(qn) ||
        //                    (x.Email != null && x.Email.Contains(qn)));
        //            }

        //            // ==== filter theo từng field (chuỗi -> Contains) ====
        //            if (id is not null) query = query.Where(x => x.Id == id);

        //            if (!string.IsNullOrWhiteSpace(code))
        //            {
        //                var v = code.Trim();
        //                query = query.Where(x => x.Code.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(fullName))
        //            {
        //                var v = fullName.Trim();
        //                query = query.Where(x => x.FullName.Contains(v));
        //            }
        //            if (gender is not null) query = query.Where(x => x.Gender == gender);
        //            if (dobFrom is not null) query = query.Where(x => x.Dob >= dobFrom);
        //            if (dobTo is not null) query = query.Where(x => x.Dob <= dobTo);

        //            if (!string.IsNullOrWhiteSpace(cccd))
        //            {
        //                var v = cccd.Trim();
        //                query = query.Where(x => x.Cccd != null && x.Cccd.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(email))
        //            {
        //                var v = email.Trim();
        //                query = query.Where(x => x.Email != null && x.Email.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(phone))
        //            {
        //                var v = phone.Trim();
        //                query = query.Where(x => x.Phone != null && x.Phone.Contains(v));
        //            }
        //            if (!string.IsNullOrWhiteSpace(address))
        //            {
        //                var v = address.Trim();
        //                query = query.Where(x => x.Address != null && x.Address.Contains(v));
        //            }

        //            if (hireDateFrom is not null) query = query.Where(x => x.HireDate >= hireDateFrom);
        //            if (hireDateTo is not null) query = query.Where(x => x.HireDate <= hireDateTo);
        //            if (status is not null) query = query.Where(x => x.Status == status);
        //            if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
        //            if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

        //            // sort
        //            query = sort?.Trim() switch
        //            {
        //                "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
        //                "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
        //                "-Code" => query.OrderByDescending(x => x.Code),
        //                "Code" => query.OrderBy(x => x.Code),
        //                "-FullName" => query.OrderByDescending(x => x.FullName),
        //                "FullName" => query.OrderBy(x => x.FullName),
        //                _ => query.OrderBy(x => x.FullName)
        //            };

        //            var total = await query.CountAsync(ct);

        //            // 1) EF chỉ select cột cần thiết
        //            var projected = ProjectEmployees(query, reqFields);

        //            // 2) Materialize
        //            var list = await projected
        //                .Skip((current - 1) * pageSize)
        //                .Take(pageSize)
        //                .ToListAsync(ct);

        //            // 3) Shape output theo fields (ẩn field không chọn)
        //            var result = list.Select(x =>
        //            {
        //                var o = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;

        //                void Add(string key, object? val)
        //                {
        //                    if (reqFields.Contains(key)) o[key switch
        //                    {
        //                        "fullname" => "fullName",
        //                        "hiredate" => "hireDate",
        //                        "departmentid" => "departmentId",
        //                        "departmentname" => "departmentName",
        //                        "positionid" => "positionId",
        //                        "positionname" => "positionName",
        //                        "avatarurl" => "avatarUrl",
        //                        _ => key
        //                    }] = val;
        //                }

        //                Add("id", x.Id);
        //                Add("code", x.Code);
        //                Add("fullname", x.FullName);
        //                Add("gender", x.Gender);
        //                Add("dob", x.Dob);
        //                Add("cccd", x.Cccd);
        //                Add("email", x.Email);
        //                Add("phone", x.Phone);
        //                Add("address", x.Address);
        //                Add("hiredate", x.HireDate);
        //                Add("departmentid", x.DepartmentId);
        //                Add("departmentname", x.DepartmentName);
        //                Add("positionid", x.PositionId);
        //                Add("positionname", x.PositionName);
        //                Add("status", x.Status);
        //                Add("avatarurl", x.AvatarUrl);
        //                return (object)o;
        //            }).ToList();

        //            var meta = new
        //            {
        //                current,
        //                pageSize,
        //                pages = (int)Math.Ceiling(total / (double)pageSize),
        //                total
        //            };

        //            return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy {total} nhân viên." : "Không có kết quả.");
        //        }
        //        catch
        //        {
        //            return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi truy vấn danh sách.");
        //        }
        //    }

        [HttpGet("filter")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> SelectiveSearch(
    // chọn cột trả về (nếu bỏ trống -> trả full)
    [FromQuery] string? fields,
    // full-text tổng quát
    [FromQuery] string? q,

    // ==== filter theo từng thuộc tính Employee (dùng Contains cho chuỗi) ====
    [FromQuery] Guid? id,
    [FromQuery] string? code,
    [FromQuery] string? fullName,
    [FromQuery] Gender? gender,
    [FromQuery] DateOnly? dobFrom,
    [FromQuery] DateOnly? dobTo,
    [FromQuery] string? cccd,
    [FromQuery] string? email,
    [FromQuery] string? phone,
    [FromQuery] string? address,
    [FromQuery] DateOnly? hireDateFrom,
    [FromQuery] DateOnly? hireDateTo,
    [FromQuery] EmployeeStatus? status,
    [FromQuery] Guid? departmentId,
    [FromQuery] Guid? positionId,

    // paging + sort
    [FromQuery] int current = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? sort = null,
    CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                // Danh sách field hợp lệ (đầy đủ)
                var allowed = new HashSet<string>(new[]
                {
            "id","code","fullname","gender","dob","cccd","email","phone","address","hiredate",
            "departmentid","departmentname","positionid","positionname","avatarurl", "status"
        });

                // Nếu fields null/rỗng => mặc định lấy FULL
                List<string> reqFields;
                if (string.IsNullOrWhiteSpace(fields))
                {
                    reqFields = allowed.ToList();
                }
                else
                {
                    reqFields = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                      .Select(f => f.ToLowerInvariant())
                                      .Distinct()
                                      .ToList();

                    // Validate field yêu cầu ⊆ allowed
                    var invalid = reqFields.Where(f => !allowed.Contains(f)).ToList();
                    if (invalid.Count > 0)
                        return this.FAIL(StatusCodes.Status400BadRequest, $"Trường không hợp lệ: {string.Join(", ", invalid)}");
                }

                var query = _db.Employees.AsNoTracking().AsQueryable();

                // ==== full-text q (Contains) ====
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var qn = q.Trim();
                    query = query.Where(x =>
                        x.FullName.Contains(qn) ||
                        x.Code.Contains(qn) ||
                        (x.Email != null && x.Email.Contains(qn)));
                }

                // ==== filter từng field ====
                if (id is not null) query = query.Where(x => x.Id == id);

                if (!string.IsNullOrWhiteSpace(code))
                {
                    var v = code.Trim();
                    query = query.Where(x => x.Code.Contains(v));
                }
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    var v = fullName.Trim();
                    query = query.Where(x => x.FullName.Contains(v));
                }
                if (gender is not null) query = query.Where(x => x.Gender == gender);
                if (dobFrom is not null) query = query.Where(x => x.Dob >= dobFrom);
                if (dobTo is not null) query = query.Where(x => x.Dob <= dobTo);

                if (!string.IsNullOrWhiteSpace(cccd))
                {
                    var v = cccd.Trim();
                    query = query.Where(x => x.Cccd != null && x.Cccd.Contains(v));
                }
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var v = email.Trim();
                    query = query.Where(x => x.Email != null && x.Email.Contains(v));
                }
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var v = phone.Trim();
                    query = query.Where(x => x.Phone != null && x.Phone.Contains(v));
                }
                if (!string.IsNullOrWhiteSpace(address))
                {
                    var v = address.Trim();
                    query = query.Where(x => x.Address != null && x.Address.Contains(v));
                }

                if (hireDateFrom is not null) query = query.Where(x => x.HireDate >= hireDateFrom);
                if (hireDateTo is not null) query = query.Where(x => x.HireDate <= hireDateTo);
                if (status is not null) query = query.Where(x => x.Status == status);
                if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId);
                if (positionId is not null) query = query.Where(x => x.PositionId == positionId);

                // sort
                query = sort?.Trim() switch
                {
                    "-HireDate" => query.OrderByDescending(x => x.HireDate).ThenBy(x => x.FullName),
                    "HireDate" => query.OrderBy(x => x.HireDate).ThenBy(x => x.FullName),
                    "-Code" => query.OrderByDescending(x => x.Code),
                    "Code" => query.OrderBy(x => x.Code),
                    "-FullName" => query.OrderByDescending(x => x.FullName),
                    "FullName" => query.OrderBy(x => x.FullName),
                    _ => query.OrderBy(x => x.FullName)
                };

                var total = await query.CountAsync(ct);

                // 1) EF chỉ select cột cần thiết
                var projected = ProjectEmployees(query, reqFields);

                // 2) Materialize
                var list = await projected
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                // 3) Shape output theo fields (ẩn field không chọn)
                var result = list.Select(x =>
                {
                    var o = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;

                    void Add(string key, object? val)
                    {
                        if (reqFields.Contains(key)) o[key switch
                        {
                            "fullname" => "fullName",
                            "hiredate" => "hireDate",
                            "departmentid" => "departmentId",
                            "departmentname" => "departmentName",
                            "positionid" => "positionId",
                            "positionname" => "positionName",
                            "avatarurl" => "avatarUrl",
                            _ => key
                        }] = val;
                    }

                    Add("id", x.Id);
                    Add("code", x.Code);
                    Add("fullname", x.FullName);
                    Add("gender", x.Gender);
                    Add("dob", x.Dob);
                    Add("cccd", x.Cccd);
                    Add("email", x.Email);
                    Add("phone", x.Phone);
                    Add("address", x.Address);
                    Add("hiredate", x.HireDate);
                    Add("departmentid", x.DepartmentId);
                    Add("departmentname", x.DepartmentName);
                    Add("positionid", x.PositionId);
                    Add("positionname", x.PositionName);
                    Add("status", x.Status);
                    Add("avatarurl", x.AvatarUrl);
                    return (object)o;
                }).ToList();

                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy {total} nhân viên." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi truy vấn danh sách.");
            }
        }



        // ===== Quan trọng: trả về IQueryable<EmployeeFlat>, KHÔNG ép sang object trong query =====
        private static IQueryable<EmployeeFlat> ProjectEmployees(IQueryable<Employee> source, List<string> fields)
        {
            var e = System.Linq.Expressions.Expression.Parameter(typeof(Employee), "e");
            static System.Linq.Expressions.Expression Prop(System.Linq.Expressions.Expression obj, string name)
                => System.Linq.Expressions.Expression.Property(obj, name);

            var dtoType = typeof(EmployeeFlat);
            var bindings = new List<System.Linq.Expressions.MemberBinding>();
            void Bind(string field, System.Linq.Expressions.Expression valueExp)
            {
                var prop = dtoType.GetProperty(field);
                bindings.Add(System.Linq.Expressions.Expression.Bind(prop!, valueExp));
            }

            foreach (var f in fields)
            {
                switch (f)
                {
                    case "id": Bind(nameof(EmployeeFlat.Id), Prop(e, nameof(Employee.Id))); break;
                    case "code": Bind(nameof(EmployeeFlat.Code), Prop(e, nameof(Employee.Code))); break;
                    case "fullname": Bind(nameof(EmployeeFlat.FullName), Prop(e, nameof(Employee.FullName))); break;
                    case "gender": Bind(nameof(EmployeeFlat.Gender), Prop(e, nameof(Employee.Gender))); break;
                    case "dob": Bind(nameof(EmployeeFlat.Dob), Prop(e, nameof(Employee.Dob))); break;
                    case "cccd": Bind(nameof(EmployeeFlat.Cccd), Prop(e, nameof(Employee.Cccd))); break;
                    case "email": Bind(nameof(EmployeeFlat.Email), Prop(e, nameof(Employee.Email))); break;
                    case "phone": Bind(nameof(EmployeeFlat.Phone), Prop(e, nameof(Employee.Phone))); break;
                    case "address": Bind(nameof(EmployeeFlat.Address), Prop(e, nameof(Employee.Address))); break;
                    case "hiredate": Bind(nameof(EmployeeFlat.HireDate), Prop(e, nameof(Employee.HireDate))); break;
                    case "departmentid": Bind(nameof(EmployeeFlat.DepartmentId), Prop(e, nameof(Employee.DepartmentId))); break;
                    case "positionid": Bind(nameof(EmployeeFlat.PositionId), Prop(e, nameof(Employee.PositionId))); break;
                    case "status": Bind(nameof(EmployeeFlat.Status), Prop(e, nameof(Employee.Status))); break;
                    case "avatarurl": Bind(nameof(EmployeeFlat.AvatarUrl), Prop(e, nameof(Employee.AvatarUrl))); break;

                    case "departmentname":
                        {
                            var dep = Prop(e, nameof(Employee.Department));
                            var depName = Prop(dep, nameof(Department.Name));
                            // null-propagation: e.Department == null ? null : e.Department.Name
                            var cond = System.Linq.Expressions.Expression.Condition(
                                System.Linq.Expressions.Expression.Equal(dep, System.Linq.Expressions.Expression.Constant(null, typeof(Department))),
                                System.Linq.Expressions.Expression.Constant(null, typeof(string)),
                                depName);
                            Bind(nameof(EmployeeFlat.DepartmentName), cond);
                            break;
                        }
                    case "positionname":
                        {
                            var pos = Prop(e, nameof(Employee.Position));
                            var posName = Prop(pos, nameof(Position.Name));
                            var cond = System.Linq.Expressions.Expression.Condition(
                                System.Linq.Expressions.Expression.Equal(pos, System.Linq.Expressions.Expression.Constant(null, typeof(Position))),
                                System.Linq.Expressions.Expression.Constant(null, typeof(string)),
                                posName);
                            Bind(nameof(EmployeeFlat.PositionName), cond);
                            break;
                        }
                }
            }

            var init = System.Linq.Expressions.Expression.MemberInit(
                System.Linq.Expressions.Expression.New(dtoType), bindings);

            var selector = System.Linq.Expressions.Expression.Lambda<Func<Employee, EmployeeFlat>>(init, e);
            return source.Select(selector);
        }


        [HttpGet("GetEmployeeById")]
        [HasPermission("Employees.View")]
        public async Task<IActionResult> GetEmployeeById(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.Employees
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Position)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên.");

                var dto = new EmployeeDto
                {
                    Id = e.Id,
                    Code = e.Code,
                    FullName = e.FullName,
                    Gender = e.Gender,
                    Dob = e.Dob,
                    Cccd = e.Cccd,
                    Email = e.Email,
                    Phone = e.Phone,
                    Address = e.Address,
                    HireDate = e.HireDate,
                    DepartmentId = e.DepartmentId,
                    DepartmentName = e.Department?.Name,
                    PositionId = e.PositionId,
                    PositionName = e.Position?.Name,
                    Status = e.Status,
                    AvatarUrl = e.AvatarUrl
                };

                return this.OKSingle(dto, "Lấy thông tin nhân viên thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin nhân viên.");
            }
        }

        [HttpPost]
        [HasPermission("Employees.Manage")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest req, CancellationToken ct)
        {
            try
            {
                if (req is null || string.IsNullOrWhiteSpace(req.FullName))
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                // Sinh code tránh trùng
                string employeeCode;
                do
                {
                    employeeCode = "NV-" + Random.Shared.Next(100000, 999999);
                } while (await _db.Employees.AnyAsync(x => x.Code == employeeCode, ct));

                string username = await GenerateUniqueUsernameAsync(req.FullName, ct);
                const string companyDomain = "@huynhthanhson.io.vn";
                string employeeEmail = $@"{username}{companyDomain}";
                string tempPasswordEmail = "Temp@123";

                var e = new Employee
                {
                    Id = Guid.NewGuid(),
                    Code = employeeCode,
                    FullName = req.FullName,
                    Gender = req.Gender,
                    Dob = req.Dob,
                    Cccd = req.Cccd,
                    Email = req.Email!, // hoặc employeeEmail nếu muốn auto email công ty
                    Phone = req.Phone,
                    Address = req.Address,
                    HireDate = req.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    DepartmentId = req.DepartmentId,
                    PositionId = req.PositionId,
                    Status = req.Status ?? EmployeeStatus.active,
                    AvatarUrl = req.AvatarUrl
                };

                _db.Employees.Add(e);

                string tempPassword = GenerateTempPassword();

                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User", ct);
                if (role is null)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Không tìm thấy role hợp lệ.");

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = e.Id,
                    UserName = username,
                    RoleId = role.Id,
                    Status = UserStatus.active
                };
                user.PasswordHash = _hasher.HashPassword(user, tempPassword);
                _db.Users.Add(user);

                await _db.SaveChangesAsync(ct);

                // Gửi mail (không chặn tạo nếu lỗi gửi)
                try
                {
                    var to = req.Email!;
                    var subject = "Tài khoản nhân sự của bạn đã được tạo";
                    //var body = $@"
                    //    <!DOCTYPE html>
                    //    <html lang='vi'><head><meta charset='UTF-8'><title>Thông báo</title></head>
                    //    <body>
                    //        <h2>Xin chào {req.FullName},</h2>
                    //        <p>Tài khoản HRM đã được tạo.</p>
                    //        <ul>
                    //            <li><b>Username:</b> {username}</li>
                    //            <li><b>Mật khẩu tạm:</b> {tempPassword}</li>
                    //            <li><b>Email công ty:</b> {employeeEmail}</li>
                    //            <li><b>Mật khẩu Email:</b> {tempPasswordEmail}</li>
                    //            <li><a href='https://www.google.com/'>Đăng Nhập User</a></li>
                    //            <li><a href='https://webmail.huynhthanhson.io.vn/'>Đăng Nhập Web Mail Công Ty</a></li>
                    //        </ul>
                    //    </body></html>";

                    string hrmUrl = "https://google.com";
                    string webmailUrl = "https://webmail.huynhthanhson.io.vn";
                    string tempPasswordExpireAt = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm 'ICT'");
                    string helpEmail = "support@huynhthanhson.io.vn";
                    string companyName = "Công Ty TNHH NPS";
                    string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";

                    var body = $@"
                        <!doctype html>
                        <html lang='vi'>
                        <head>
                          <meta charset='utf-8'>
                          <meta name='viewport' content='width=device-width, initial-scale=1'>
                          <title>Chào mừng đến HRM</title>
                          <!-- Preheader: hiện trong preview của hộp thư -->
                          <style>
                            /* Một số client vẫn tôn trọng <style>, nhưng ta vẫn inline phần quan trọng */
                          </style>
                        </head>
                        <body style='margin:0;padding:0;background:#f5f7fa;'>
                          <!-- Preheader (ẩn) -->
                          <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
                            Tài khoản HRM & email công ty đã được tạo. Hãy đăng nhập và đổi mật khẩu trong ngày đầu tiên.
                          </div>

                          <table role='presentation' border='0' cellpadding='0' cellspacing='0' width='100%'>
                            <tr>
                              <td align='center' style='padding:24px 12px;'>
                                <table role='presentation' width='600' cellpadding='0' cellspacing='0' style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
                                  <!-- Header -->
                                  <tr>
                                    <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                                      <h1 style='margin:0;font-size:20px;line-height:1.4;'>Chào mừng đến hệ thống HRM</h1>
                                      <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Thông tin tài khoản cho nhân viên mới</p>
                                    </td>
                                  </tr>

                                  <!-- Nội dung chính -->
                                  <tr>
                                    <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                                      <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{req.FullName}</b>,</p>
                                      <p style='margin:0 0 16px;font-size:15px;'>Tài khoản làm việc của bạn đã được tạo. Vui lòng làm theo các bước bên dưới để bắt đầu.</p>

                                      <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                        <tr>
                                          <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Thông tin đăng nhập</td>
                                        </tr>
                                        <tr>
                                          <td style='padding:12px 16px;font-size:14px;'>
                                            <div style='margin:0 0 8px;'><b>HRM Username:</b> {username}</div>
                                            <div style='margin:0 0 8px;'><b>HRM Mật khẩu tạm:</b> <code style='background:#f1f5f9;padding:2px 6px;border-radius:4px;'>{tempPassword}</code></div>
                                            <div style='margin:0 0 8px;'><b>Email công ty:</b> {employeeEmail}</div>
                                            <div style='margin:0;'><b>Email Mật khẩu tạm:</b> <code style='background:#f1f5f9;padding:2px 6px;border-radius:4px;'>{tempPasswordEmail}</code></div>
                                          </td>
                                        </tr>
                                      </table>

                                      <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                        <tr>
                                          <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Bắt đầu trong 3 bước</td>
                                        </tr>
                                        <tr>
                                          <td style='padding:12px 16px;font-size:14px;line-height:1.6;'>
                                            <ol style='margin:0;padding-left:20px;'>
                                              <li><b>Đăng nhập HRM:</b> Nhấn nút bên dưới &rarr; dùng <i>HRM Username</i> và <i>Mật khẩu tạm</i> &rarr; hệ thống sẽ yêu cầu bạn <b>đổi mật khẩu</b>.
                                                <div style='margin:10px 0;'>
                                                  <a href='{hrmUrl}' style='background:#2563eb;text-decoration:none;color:#fff;padding:10px 16px;border-radius:8px;display:inline-block;font-weight:600;'>Đăng nhập HRM</a>
                                                </div>
                                              </li>
                                              <li><b>Thiết lập email công ty:</b> Mở Webmail &rarr; đăng nhập bằng <i>Email công ty</i> và <i>Mật khẩu tạm</i> &rarr; đổi mật khẩu &rarr; (khuyến nghị) thêm chữ ký.
                                                <div style='margin:10px 0;'>
                                                  <a href='{webmailUrl}' style='background:#16a34a;text-decoration:none;color:#fff;padding:10px 16px;border-radius:8px;display:inline-block;font-weight:600;'>Đăng nhập Webmail</a>
                                                </div>
                                              </li>
                                              <li><b>Bảo mật tài khoản:</b> Bật 2FA (nếu HRM cho phép), không chia sẻ mật khẩu, và không dùng chung mật khẩu giữa HRM & Email.</li>
                                            </ol>
                                          </td>
                                        </tr>
                                      </table>

                                      <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                        <tr>
                                          <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>Lưu ý quan trọng</td>
                                        </tr>
                                        <tr>
                                          <td style='padding:12px 16px;font-size:13px;line-height:1.6;color:#334155;'>
                                            <ul style='margin:0;padding-left:18px;'>
                                              <li>Mật khẩu tạm <b>hết hạn</b>: {tempPasswordExpireAt}.</li>
                                              <li>Mật khẩu mạnh &ge; 8 ký tự, gồm chữ hoa, chữ thường, số, ký tự đặc biệt.</li>
                                              <li>Nếu email rơi vào mục <i>Spam/Junk</i>, hãy <b>Mark as Not Spam</b> để nhận mail bình thường.</li>
                                              <li>Khi cần hỗ trợ, liên hệ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a>.</li>
                                            </ul>
                                          </td>
                                        </tr>
                                      </table>

                                      <p style='margin:12px 0 0;font-size:14px;color:#475569;'>
                                        Trân trọng,<br>
                                        Phòng Hành chính – Nhân sự
                                      </p>
                                    </td>
                                  </tr>

                                  <!-- Footer -->
                                  <tr>
                                    <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;'>
                                      <div>{companyName} &bull; {companyAddress}</div>
                                      <div style='margin-top:4px;'>Email hỗ trợ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a></div>
                                    </td>
                                  </tr>
                                </table>

                                <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#94a3b8;margin-top:12px;max-width:600px;'>
                                  Bạn nhận email này vì hồ sơ của bạn được tạo trong hệ thống HRM. Nếu bạn không mong đợi, vui lòng liên hệ {helpEmail}.
                                </div>
                              </td>
                            </tr>
                          </table>
                        </body>
                        </html>";

                    await _emailSender.SendAsync(to, subject, body, ct);
                }
                catch { /* swallow email error per policy */ }

                //return this.CREATED("Tạo nhân viên thành công.");
                //return StatusCode(StatusCodes.Status201Created, new
                //{
                //    statusCode = StatusCodes.Status201Created,
                //    message = "Tạo nhân viên thành công.",
                //    data = new[]
                //    {
                //        new
                //        {
                //            EmployeeId = e.Id,
                //            Username = username
                //        }
                //    },
                //    success = true
                //});

                // === Load lại đầy đủ để trả FULL object vừa tạo ===
                var full = await _db.Employees
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Position)
                    .FirstAsync(x => x.Id == e.Id, ct);

                var dto = new EmployeeDto
                {
                    Id = full.Id,
                    Code = full.Code,
                    FullName = full.FullName,
                    Gender = full.Gender,
                    Dob = full.Dob,
                    Cccd = full.Cccd,
                    Email = full.Email,
                    Phone = full.Phone,
                    Address = full.Address,
                    HireDate = full.HireDate,
                    DepartmentId = full.DepartmentId,
                    DepartmentName = full.Department?.Name,
                    PositionId = full.PositionId,
                    PositionName = full.Position?.Name,
                    Status = full.Status,
                    AvatarUrl = full.AvatarUrl
                };

                // Trả 201 với FULL employee + thông tin account tạo kèm
                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo nhân viên thành công.",
                    data = new
                    {
                        result = new
                        {
                            employee = dto,
                            account = new
                            {
                                id = user.Id,
                                username = username,
                                roleId = role.Id
                            }
                        }
                    },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo nhân viên do xung đột dữ liệu (trùng hoặc ràng buộc).");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi tạo nhân viên.");
            }
        }

        //[HttpPut("{id:guid}")]
        //[HasPermission("Employees.Manage")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        //        if (e is null)
        //            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên.");

        //        if (!string.Equals(e.Email, req.Email, StringComparison.OrdinalIgnoreCase)
        //            && await _db.Employees.AnyAsync(x => x.Email == req.Email, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Email đã tồn tại.");

        //        if (!string.Equals(e.Cccd, req.Cccd, StringComparison.OrdinalIgnoreCase)
        //            && !string.IsNullOrWhiteSpace(req.Cccd)
        //            && await _db.Employees.AnyAsync(x => x.Cccd == req.Cccd, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "CCCD đã tồn tại.");

        //        if (!string.Equals(e.Phone, req.Phone, StringComparison.OrdinalIgnoreCase)
        //            && !string.IsNullOrWhiteSpace(req.Phone)
        //            && await _db.Employees.AnyAsync(x => x.Phone == req.Phone, ct))
        //            return this.FAIL(StatusCodes.Status409Conflict, "Phone đã tồn tại.");

        //        if (!string.Equals(e.Code, req.Code, StringComparison.OrdinalIgnoreCase))
        //        {
        //            var duplicate = await _db.Employees
        //                .AnyAsync(x => x.Code == req.Code && x.Id != id, ct);
        //            if (duplicate)
        //            {
        //                return Conflict(new
        //                {
        //                    statusCode = 409,
        //                    message = $"Mã nhân viên '{req.Code}' đã tồn tại.",
        //                    data = Array.Empty<object>(),
        //                    success = false
        //                });
        //            }
        //            e.Code = req.Code!;
        //        }

        //        e.FullName = req.FullName!;
        //        e.Gender = req.Gender;
        //        e.Dob = req.Dob;
        //        e.Cccd = req.Cccd;
        //        e.Email = req.Email!;
        //        e.Phone = req.Phone;
        //        e.Address = req.Address;
        //        e.HireDate = req.HireDate ?? e.HireDate;
        //        e.DepartmentId = req.DepartmentId;
        //        e.PositionId = req.PositionId;
        //        e.Status = req.Status ?? e.Status;
        //        e.AvatarUrl = req.AvatarUrl;

        //        await _db.SaveChangesAsync(ct);

        //        return this.OK(message: "Cập nhật nhân viên thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật nhân viên.");
        //    }
        //}

        //[HttpPut("{id:guid}")]
        //[HasPermission("Employees.Manage")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        //{
        //    try
        //    {
        //        if (body.ValueKind != JsonValueKind.Object)
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

        //        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        //        if (e is null)
        //            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên.");

        //        // --- helpers ---
        //        static string? GetStringOrNull(JsonElement prop) =>
        //            prop.ValueKind switch
        //            {
        //                JsonValueKind.Null => null,
        //                JsonValueKind.String => string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString()!.Trim(),
        //                _ => null
        //            };

        //        static Guid? GetGuidOrNull(JsonElement prop)
        //        {
        //            if (prop.ValueKind == JsonValueKind.Null) return null;
        //            if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var g)) return g;
        //            return null;
        //        }

        //        static bool TryGetDateOnly(JsonElement prop, out DateOnly? value)
        //        {
        //            value = null;
        //            if (prop.ValueKind == JsonValueKind.Null) { value = null; return true; }
        //            if (prop.ValueKind == JsonValueKind.String &&
        //                DateOnly.TryParse(prop.GetString(), out var d)) { value = d; return true; }
        //            return false;
        //        }

        //        static bool TryGetInt(JsonElement prop, out int? value)
        //        {
        //            value = null;
        //            if (prop.ValueKind == JsonValueKind.Null) { value = null; return true; }
        //            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) { value = i; return true; }
        //            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var j)) { value = j; return true; }
        //            return false;
        //        }

        //        // --- Code (unique, non-empty) ---
        //        if (body.TryGetProperty("code", out var codeProp))
        //        {
        //            var newCode = GetStringOrNull(codeProp);
        //            if (string.IsNullOrWhiteSpace(newCode))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Mã nhân viên không được để trống.");

        //            var duplicate = await _db.Employees.AnyAsync(x => x.Code == newCode && x.Id != id, ct);
        //            if (duplicate)
        //                return this.FAIL(StatusCodes.Status409Conflict, $"Mã nhân viên '{newCode}' đã tồn tại.");

        //            e.Code = newCode!;
        //        }

        //        // --- FullName (non-empty) ---
        //        if (body.TryGetProperty("fullName", out var fullNameProp))
        //        {
        //            var newFullName = GetStringOrNull(fullNameProp);
        //            if (string.IsNullOrWhiteSpace(newFullName))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Họ tên không được để trống.");
        //            e.FullName = newFullName!;
        //        }

        //        // --- Email (unique, non-empty) ---
        //        if (body.TryGetProperty("email", out var emailProp))
        //        {
        //            var newEmail = GetStringOrNull(emailProp);
        //            if (string.IsNullOrWhiteSpace(newEmail))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Email không được để trống.");

        //            if (!string.Equals(e.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        //            {
        //                var dup = await _db.Employees.AnyAsync(x => x.Email == newEmail, ct);
        //                if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Email đã tồn tại.");
        //                e.Email = newEmail!;
        //            }
        //        }

        //        // --- CCCD (unique, allow null to clear) ---
        //        if (body.TryGetProperty("cccd", out var cccdProp))
        //        {
        //            var newCccd = GetStringOrNull(cccdProp); // null => xóa
        //            if (!string.Equals(e.Cccd ?? "", newCccd ?? "", StringComparison.OrdinalIgnoreCase))
        //            {
        //                if (!string.IsNullOrWhiteSpace(newCccd))
        //                {
        //                    var dup = await _db.Employees.AnyAsync(x => x.Cccd == newCccd, ct);
        //                    if (dup) return this.FAIL(StatusCodes.Status409Conflict, "CCCD đã tồn tại.");
        //                }
        //                e.Cccd = newCccd;
        //            }
        //        }

        //        // --- Phone (unique, allow null to clear) ---
        //        if (body.TryGetProperty("phone", out var phoneProp))
        //        {
        //            var newPhone = GetStringOrNull(phoneProp); // null => xóa
        //            if (!string.Equals(e.Phone ?? "", newPhone ?? "", StringComparison.OrdinalIgnoreCase))
        //            {
        //                if (!string.IsNullOrWhiteSpace(newPhone))
        //                {
        //                    var dup = await _db.Employees.AnyAsync(x => x.Phone == newPhone, ct);
        //                    if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Phone đã tồn tại.");
        //                }
        //                e.Phone = newPhone;
        //            }
        //        }

        //        // --- Address (allow null to clear) ---
        //        if (body.TryGetProperty("address", out var addrProp))
        //        {
        //            e.Address = GetStringOrNull(addrProp);
        //        }

        //        // --- AvatarUrl (allow null to clear) ---
        //        if (body.TryGetProperty("avatarUrl", out var avatarProp))
        //        {
        //            e.AvatarUrl = GetStringOrNull(avatarProp);
        //        }



        //        // --- Gender (int enum) ---
        //        if (body.TryGetProperty("gender", out var genderProp))
        //        {
        //            if (!TryGetInt(genderProp, out var newGender))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "gender phải là số hoặc null.");
        //            if (newGender.HasValue && (newGender < 0 || newGender > 2))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị gender không hợp lệ.");
        //            if (newGender.HasValue) e.Gender = (Gender)newGender.Value;
        //        }

        //        // --- Status (int enum, nullable) ---
        //        if (body.TryGetProperty("status", out var statusProp))
        //        {
        //            if (!TryGetInt(statusProp, out var newStatus))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "status phải là số hoặc null.");
        //            if (newStatus.HasValue && (newStatus < 0 || newStatus > 5)) // tùy enum của bạn
        //                return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị status không hợp lệ.");
        //            if (newStatus.HasValue) e.Status = (EmployeeStatus)newStatus.Value;
        //            else e.Status = e.Status; // null => giữ nguyên
        //        }

        //        // --- Dob (DateOnly "yyyy-MM-dd" hoặc null để xóa) ---
        //        if (body.TryGetProperty("dob", out var dobProp))
        //        {
        //            if (!TryGetDateOnly(dobProp, out var newDob))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "dob phải là 'yyyy-MM-dd' hoặc null.");
        //            e.Dob = newDob;
        //        }

        //        // --- HireDate (DateOnly, nếu null -> giữ nguyên) ---
        //        if (body.TryGetProperty("hireDate", out var hireProp))
        //        {
        //            if (!TryGetDateOnly(hireProp, out var newHire))
        //                return this.FAIL(StatusCodes.Status400BadRequest, "hireDate phải là 'yyyy-MM-dd' hoặc null.");
        //            if (newHire.HasValue) e.HireDate = newHire.Value;
        //        }

        //        // --- DepartmentId (Guid?; validate tồn tại nếu đặt giá trị) ---
        //        if (body.TryGetProperty("departmentId", out var depProp))
        //        {
        //            var newDepId = GetGuidOrNull(depProp); // null => xóa/cho phép?
        //            if (depProp.ValueKind != JsonValueKind.Null && newDepId is null)
        //                return this.FAIL(StatusCodes.Status400BadRequest, "departmentId phải là GUID hoặc null.");

        //            if (newDepId != e.DepartmentId)
        //            {
        //                if (newDepId.HasValue)
        //                {
        //                    var depExists = await _db.Departments.AnyAsync(d => d.Id == newDepId.Value, ct);
        //                    if (!depExists) return this.FAIL(StatusCodes.Status404NotFound, "Phòng ban không tồn tại.");
        //                }
        //                e.DepartmentId = newDepId;
        //            }
        //        }

        //        // --- PositionId (Guid?; validate tồn tại nếu đặt giá trị) ---
        //        if (body.TryGetProperty("positionId", out var posProp))
        //        {
        //            var newPosId = GetGuidOrNull(posProp); // null => xóa/cho phép?
        //            if (posProp.ValueKind != JsonValueKind.Null && newPosId is null)
        //                return this.FAIL(StatusCodes.Status400BadRequest, "positionId phải là GUID hoặc null.");

        //            if (newPosId != e.PositionId)
        //            {
        //                if (newPosId.HasValue)
        //                {
        //                    var posExists = await _db.Positions.AnyAsync(p => p.Id == newPosId.Value, ct);
        //                    if (!posExists) return this.FAIL(StatusCodes.Status404NotFound, "Vị trí không tồn tại.");
        //                }
        //                e.PositionId = newPosId;
        //            }
        //        }

        //        await _db.SaveChangesAsync(ct);
        //        return this.OK(message: "Cập nhật nhân viên thành công.");
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật nhân viên.");
        //    }
        //}

        [HttpPut("{id:guid}")]
        [HasPermission("Employees.Manage")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken ct)
        {
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Body phải là JSON object.");

                var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên.");

                // --- helpers ---
                static string? GetStringOrNull(JsonElement prop) =>
                    prop.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString()!.Trim(),
                        _ => null
                    };

                static Guid? GetGuidOrNull(JsonElement prop)
                {
                    if (prop.ValueKind == JsonValueKind.Null) return null;
                    if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var g)) return g;
                    return null;
                }

                static bool TryGetDateOnly(JsonElement prop, out DateOnly? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.String && DateOnly.TryParse(prop.GetString(), out var d)) { value = d; return true; }
                    return false;
                }

                static bool TryGetInt(JsonElement prop, out int? value)
                {
                    value = null;
                    if (prop.ValueKind == JsonValueKind.Null) return true;
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) { value = i; return true; }
                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var j)) { value = j; return true; }
                    return false;
                }

                // --- Code (unique, non-empty) ---
                if (body.TryGetProperty("code", out var codeProp))
                {
                    var newCode = GetStringOrNull(codeProp);
                    if (string.IsNullOrWhiteSpace(newCode))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Mã nhân viên không được để trống.");

                    var duplicate = await _db.Employees.AnyAsync(x => x.Code == newCode && x.Id != id, ct);
                    if (duplicate)
                        return this.FAIL(StatusCodes.Status409Conflict, $"Mã nhân viên '{newCode}' đã tồn tại.");

                    e.Code = newCode!;
                }

                // --- FullName (non-empty) ---
                if (body.TryGetProperty("fullName", out var fullNameProp))
                {
                    var newFullName = GetStringOrNull(fullNameProp);
                    if (string.IsNullOrWhiteSpace(newFullName))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Họ tên không được để trống.");
                    e.FullName = newFullName!;
                }

                // --- Email (unique, non-empty) ---
                if (body.TryGetProperty("email", out var emailProp))
                {
                    var newEmail = GetStringOrNull(emailProp);
                    if (string.IsNullOrWhiteSpace(newEmail))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Email không được để trống.");

                    if (!string.Equals(e.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        var dup = await _db.Employees.AnyAsync(x => x.Email == newEmail, ct);
                        if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Email đã tồn tại.");
                        e.Email = newEmail!;
                    }
                }

                // --- CCCD (unique, allow null to clear) ---
                if (body.TryGetProperty("cccd", out var cccdProp))
                {
                    var newCccd = GetStringOrNull(cccdProp);
                    if (!string.Equals(e.Cccd ?? "", newCccd ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(newCccd))
                        {
                            var dup = await _db.Employees.AnyAsync(x => x.Cccd == newCccd, ct);
                            if (dup) return this.FAIL(StatusCodes.Status409Conflict, "CCCD đã tồn tại.");
                        }
                        e.Cccd = newCccd;
                    }
                }

                // --- Phone (unique, allow null to clear) ---
                if (body.TryGetProperty("phone", out var phoneProp))
                {
                    var newPhone = GetStringOrNull(phoneProp);
                    if (!string.Equals(e.Phone ?? "", newPhone ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(newPhone))
                        {
                            var dup = await _db.Employees.AnyAsync(x => x.Phone == newPhone, ct);
                            if (dup) return this.FAIL(StatusCodes.Status409Conflict, "Phone đã tồn tại.");
                        }
                        e.Phone = newPhone;
                    }
                }

                // --- Address (allow null) ---
                if (body.TryGetProperty("address", out var addrProp))
                    e.Address = GetStringOrNull(addrProp);

                // --- AvatarUrl (allow null) ---
                if (body.TryGetProperty("avatarUrl", out var avatarProp))
                    e.AvatarUrl = GetStringOrNull(avatarProp);

                // --- Gender (enum) ---
                if (body.TryGetProperty("gender", out var genderProp))
                {
                    if (!TryGetInt(genderProp, out var newGender))
                        return this.FAIL(StatusCodes.Status400BadRequest, "gender phải là số hoặc null.");
                    if (newGender.HasValue && (newGender < 0 || newGender > 2))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị gender không hợp lệ.");
                    if (newGender.HasValue) e.Gender = (Gender)newGender.Value;
                }

                // --- Status (enum) ---
                if (body.TryGetProperty("status", out var statusProp))
                {
                    if (!TryGetInt(statusProp, out var newStatus))
                        return this.FAIL(StatusCodes.Status400BadRequest, "status phải là số hoặc null.");
                    if (newStatus.HasValue && (newStatus < 0 || newStatus > 5))
                        return this.FAIL(StatusCodes.Status400BadRequest, "Giá trị status không hợp lệ.");
                    if (newStatus.HasValue) e.Status = (EmployeeStatus)newStatus.Value;
                }

                // --- Dob (DateOnly) ---
                if (body.TryGetProperty("dob", out var dobProp))
                {
                    if (!TryGetDateOnly(dobProp, out var newDob))
                        return this.FAIL(StatusCodes.Status400BadRequest, "dob phải là 'yyyy-MM-dd' hoặc null.");
                    e.Dob = newDob;
                }

                // --- HireDate (DateOnly) ---
                if (body.TryGetProperty("hireDate", out var hireProp))
                {
                    if (!TryGetDateOnly(hireProp, out var newHire))
                        return this.FAIL(StatusCodes.Status400BadRequest, "hireDate phải là 'yyyy-MM-dd' hoặc null.");
                    if (newHire.HasValue) e.HireDate = newHire.Value;
                }

                // --- DepartmentId ---
                if (body.TryGetProperty("departmentId", out var depProp))
                {
                    var newDepId = GetGuidOrNull(depProp);
                    if (depProp.ValueKind != JsonValueKind.Null && newDepId is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "departmentId phải là GUID hoặc null.");

                    if (newDepId != e.DepartmentId)
                    {
                        if (newDepId.HasValue)
                        {
                            var depExists = await _db.Departments.AnyAsync(d => d.Id == newDepId.Value, ct);
                            if (!depExists) return this.FAIL(StatusCodes.Status404NotFound, "Phòng ban không tồn tại.");
                        }
                        e.DepartmentId = newDepId;
                    }
                }

                // --- PositionId ---
                if (body.TryGetProperty("positionId", out var posProp))
                {
                    var newPosId = GetGuidOrNull(posProp);
                    if (posProp.ValueKind != JsonValueKind.Null && newPosId is null)
                        return this.FAIL(StatusCodes.Status400BadRequest, "positionId phải là GUID hoặc null.");

                    if (newPosId != e.PositionId)
                    {
                        if (newPosId.HasValue)
                        {
                            var posExists = await _db.Positions.AnyAsync(p => p.Id == newPosId.Value, ct);
                            if (!posExists) return this.FAIL(StatusCodes.Status404NotFound, "Chức vụ không tồn tại.");
                        }
                        e.PositionId = newPosId;
                    }
                }

                await _db.SaveChangesAsync(ct);

                // === Load lại đầy đủ để trả về FULL object ===
                var full = await _db.Employees
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Position)
                    .FirstAsync(x => x.Id == e.Id, ct);

                var dto = new EmployeeDto
                {
                    Id = full.Id,
                    Code = full.Code,
                    FullName = full.FullName,
                    Gender = full.Gender,
                    Dob = full.Dob,
                    Cccd = full.Cccd,
                    Email = full.Email,
                    Phone = full.Phone,
                    Address = full.Address,
                    HireDate = full.HireDate,
                    DepartmentId = full.DepartmentId,
                    DepartmentName = full.Department?.Name,
                    PositionId = full.PositionId,
                    PositionName = full.Position?.Name,
                    Status = full.Status,
                    AvatarUrl = full.AvatarUrl
                };

                try
                {
                    if (!string.IsNullOrWhiteSpace(full.Email))
                    {
                        // (tuỳ bạn) có thể lấy từ cấu hình
                        string hrmUrlBase = "https://google.com"; // URL HRM của bạn
                        string helpEmail = "support@huynhthanhson.io.vn";
                        string companyName = "Công Ty TNHH NPS";
                        string companyAddress = "140 Lê Trọng Tấn, Tây Thạnh, Tân Phú";

                        // Link đến hồ sơ nhân viên
                        var profileUrl = $"{hrmUrlBase}/employees/{full.Id}";

                        var subject = $"[HRM] Cập nhật hồ sơ nhân viên: {full.FullName} ({full.Code})";

                        // Lưu ý: encode một số trường tự do để tránh phá HTML
                        string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "-");
                        string ToDate(DateOnly? d) => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : "-";

                        var bodymail = $@"
                            <!doctype html>
                            <html lang='vi'>
                            <head>
                              <meta charset='utf-8'>
                              <meta name='viewport' content='width=device-width, initial-scale=1'>
                              <title>Cập nhật hồ sơ</title>
                            </head>
                            <body style='margin:0;padding:0;background:#f5f7fa;'>
                              <!-- Preheader -->
                              <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
                                Hồ sơ của bạn trên HRM vừa được cập nhật.
                              </div>

                              <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                <tr>
                                  <td align='center' style='padding:24px 12px;'>
                                    <table role='presentation' width='600' cellspacing='0' cellpadding='0'
                                           style='width:600px;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6e9ef;'>
                                      <!-- Header -->
                                      <tr>
                                        <td style='background:#0f172a;padding:20px 24px;color:#fff;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;'>
                                          <h1 style='margin:0;font-size:20px;line-height:1.4;'>Cập nhật hồ sơ nhân viên</h1>
                                          <p style='margin:4px 0 0;font-size:13px;opacity:.85;'>Mã NV: {Enc(full.Code)}</p>
                                        </td>
                                      </tr>

                                      <!-- Content -->
                                      <tr>
                                        <td style='padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#0f172a;'>
                                          <p style='margin:0 0 12px;font-size:15px;'>Xin chào <b>{Enc(full.FullName)}</b>,</p>
                                          <p style='margin:0 0 16px;font-size:15px;'>Hồ sơ của bạn trên hệ thống HRM vừa được cập nhật. Thông tin hiện tại:</p>

                                          <table role='presentation' width='100%' cellspacing='0' cellpadding='0'
                                                 style='margin:8px 0 16px;border:1px solid #e6e9ef;border-radius:8px;'>
                                            <tr>
                                              <td style='padding:12px 16px;background:#f8fafc;border-bottom:1px solid #e6e9ef;font-weight:600;font-size:14px;'>
                                                Thông tin chi tiết
                                              </td>
                                            </tr>
                                            <tr>
                                              <td style='padding:12px 16px;font-size:14px;line-height:1.8;'>
                                                <div><b>Họ tên:</b> {Enc(full.FullName)}</div>
                                                <div><b>Email:</b> {Enc(full.Email)}</div>
                                                <div><b>Điện thoại:</b> {Enc(full.Phone)}</div>
                                                <div><b>Địa chỉ:</b> {Enc(full.Address)}</div>
                                                <div><b>Giới tính:</b> {full.Gender}</div>
                                                <div><b>Ngày sinh:</b> {ToDate(full.Dob)}</div>
                                                <div><b>Ngày vào làm:</b> {ToDate(full.HireDate)}</div>
                                                <div><b>Phòng ban:</b> {Enc(full.Department?.Name)}</div>
                                                <div><b>Chức vụ:</b> {Enc(full.Position?.Name)}</div>
                                                <div><b>Trạng thái:</b> {full.Status}</div>
                                              </td>
                                            </tr>
                                          </table>

                                          <p style='margin:16px 0 0;font-size:13px;color:#334155;'>
                                            Nếu có sai sót, vui lòng phản hồi về <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a>.
                                          </p>
                                        </td>
                                      </tr>

                                      <!-- Footer -->
                                      <tr>
                                        <td style='background:#f8fafc;padding:16px 24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:12px;color:#64748b;'>
                                          <div>{companyName} • {companyAddress}</div>
                                          <div style='margin-top:4px;'>Email hỗ trợ: <a href='mailto:{helpEmail}' style='color:#2563eb;text-decoration:none;'>{helpEmail}</a></div>
                                        </td>
                                      </tr>
                                    </table>

                                    <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#94a3b8;margin-top:12px;max-width:600px;'>
                                      Bạn nhận thư này vì hồ sơ của bạn đã được cập nhật trên hệ thống HRM.
                                    </div>
                                  </td>
                                </tr>
                              </table>
                            </body>
                            </html>";

                        await _emailSender.SendAsync(full.Email!, subject, bodymail, ct);
                    }
                }
                catch
                {
                    // Không chặn nghiệp vụ nếu gửi mail lỗi
                }

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật nhân viên thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi cập nhật nhân viên.");
            }
        }


        [HttpDelete("{id:guid}")]
        [HasPermission("Employees.Manage")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy nhân viên.");

                _db.Employees.Remove(e);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá nhân viên thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi không xác định khi xoá nhân viên.");
            }
        }

        // 12 10 2025
        //[HttpGet("NoCompanyEmail")]
        //[HasPermission("Employees.View")]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> GetEmployeesWithoutCompanyEmail(
        //    [FromQuery] string? q,
        //    [FromQuery] EmployeeStatus? status,
        //    [FromQuery] Guid? departmentId,
        //    [FromQuery] Guid? positionId,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    CancellationToken ct = default)
        //{
        //    try
        //    {
        //        if (page < 1) page = 1;
        //        if (pageSize is < 1 or > 200) pageSize = 20;

        //        const string companyDomain = "@huynhthanhson.io.vn";

        //        var baseQuery = _db.Employees
        //            .AsNoTracking()
        //            .Include(x => x.Department)
        //            .Include(x => x.Position)
        //            .AsQueryable();

        //        if (!string.IsNullOrWhiteSpace(q))
        //        {
        //            q = q.Trim();
        //            baseQuery = baseQuery.Where(x =>
        //                x.FullName.Contains(q) ||
        //                x.Code.Contains(q) ||
        //                (x.Email != null && x.Email.Contains(q)));
        //        }

        //        if (status is not null) baseQuery = baseQuery.Where(x => x.Status == status);
        //        if (departmentId is not null) baseQuery = baseQuery.Where(x => x.DepartmentId == departmentId);
        //        if (positionId is not null) baseQuery = baseQuery.Where(x => x.PositionId == positionId);

        //        baseQuery = baseQuery.Where(x => string.IsNullOrEmpty(x.Email) || !x.Email!.EndsWith(companyDomain));

        //        baseQuery = baseQuery.OrderBy(x => x.FullName).ThenBy(x => x.Code);

        //        var query =
        //            from e in baseQuery
        //            join u in _db.Users.AsNoTracking() on e.Id equals u.EmployeeId into gj
        //            from u in gj.DefaultIfEmpty()
        //            select new
        //            {
        //                UserId = (Guid?)(u != null ? u.Id : null),
        //                EmployeeId = e.Id,
        //                CurrentEmail = e.Email,
        //                CompanyEmail = u != null ? (u.UserName + companyDomain) : null
        //            };

        //        var total = await query.CountAsync(ct);

        //        var data = await query
        //            .Skip((page - 1) * pageSize)
        //            .Take(pageSize)
        //            .ToListAsync(ct);

        //        return this.OKList(data, $"Tìm thấy {total} nhân viên chưa có email công ty.");
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi truy vấn danh sách.");
        //    }
        //}

        [HttpGet("NoCompanyEmail")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetEmployeesWithoutCompanyEmail(
    [FromQuery] string? q,
    [FromQuery] EmployeeStatus? status,
    [FromQuery] Guid? departmentId,
    [FromQuery] Guid? positionId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                const string companyDomain = "@huynhthanhson.io.vn";

                var baseQuery = _db.Employees
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Position)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    baseQuery = baseQuery.Where(x =>
                        x.FullName.Contains(q) ||
                        x.Code.Contains(q) ||
                        (x.Email != null && x.Email.Contains(q)));
                }

                if (status is not null) baseQuery = baseQuery.Where(x => x.Status == status);
                if (departmentId is not null) baseQuery = baseQuery.Where(x => x.DepartmentId == departmentId);
                if (positionId is not null) baseQuery = baseQuery.Where(x => x.PositionId == positionId);

                // chỉ lấy những NV chưa có email công ty (hoặc email không đúng domain)
                baseQuery = baseQuery.Where(x => string.IsNullOrEmpty(x.Email) || !x.Email!.EndsWith(companyDomain));

                baseQuery = baseQuery.OrderBy(x => x.FullName).ThenBy(x => x.Code);

                var query =
                    from e in baseQuery
                    join u in _db.Users.AsNoTracking() on e.Id equals u.EmployeeId into gj
                    from u in gj.DefaultIfEmpty()
                    select new
                    {
                        UserId = (Guid?)(u != null ? u.Id : null),
                        EmployeeId = e.Id,
                        CurrentEmail = e.Email,
                        CompanyEmail = u != null ? (u.UserName + companyDomain) : null
                    };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var meta = new
                {
                    current = page,
                    pageSize = pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                var payload = new { meta, result };
                return this.OKSingle(payload, total > 0
                    ? $"Tìm thấy {total} nhân viên chưa có email công ty."
                    : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi truy vấn danh sách.");
            }
        }


        private async Task<string> GenerateUniqueUsernameAsync(string fullName, CancellationToken ct)
        {
            //var parts = fullName
            //    .Trim()
            //    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            //    .Select(p => p.Normalize(NormalizationForm.FormD)
            //        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            //        .Aggregate("", (acc, c) => acc + c)
            //        .ToLower())
            //    .ToList();

            var parts = fullName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p =>
            {
                // Loại dấu tiếng Việt (normalize)
                var normalized = p.Normalize(NormalizationForm.FormD);
                var withoutDiacritics = new string(
                    normalized
                        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                        .ToArray()
                );

                // Thay riêng ký tự 'đ' và 'Đ'
                withoutDiacritics = withoutDiacritics
                    .Replace('đ', 'd')
                    .Replace('Đ', 'D')
                    .ToLower();

                return withoutDiacritics;
            })
            .ToList();

            var firstName = parts.Last();
            var initials = string.Concat(parts.Take(parts.Count - 1).Select(p => p[0]));
            for (int i = 1; i <= 99; i++)
            {
                string username = $"{firstName}{initials}{i:D2}";
                bool exists = await _db.Users.AnyAsync(u => u.UserName == username, ct);
                if (!exists)
                    return username;
            }
            throw new InvalidOperationException("Không thể tạo username duy nhất.");
        }

        private static string GenerateTempPassword(int length = 14)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@$%";
            var bytes = RandomNumberGenerator.GetBytes(length);
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
            return new string(chars);
        }

        private static ProblemDetails ProblemDetails(string title, string detail)
            => new() { Title = title, Detail = detail, Status = StatusCodes.Status409Conflict };
    }
}

