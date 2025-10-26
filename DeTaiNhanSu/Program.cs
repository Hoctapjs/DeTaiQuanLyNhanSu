using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos;
using DeTaiNhanSu.Infrastructure.Auditing;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Auth;
using DeTaiNhanSu.Services.ContractMaintenance;
using DeTaiNhanSu.Services.Email;
using DeTaiNhanSu.Services.Log;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

// đặt alias nếu muốn, hoặc bỏ alias và dùng trực tiếp AuditActionFilter
using AuditActionFilter = DeTaiNhanSu.Services.Log.AuditActionFilter;

var builder = WebApplication.CreateBuilder(args);

// ==== Audit DI ====
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditScope, AuditScope>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<AuditActionFilter>(); // để MVC có thể resolve filter từ DI

// ==== Controllers + Global Filters (chỉ ĐÚNG 1 lần) ====
builder.Services.AddControllers(o =>
{
    o.Filters.Add<AuditActionFilter>(); // global audit filter
});

// ==== DbContext + Interceptor ====
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
    opt.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// ==== Email ====
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();

// ==== Auth services ====
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ==== JWT Auth ====
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Chuẩn hóa 401/403 theo schema
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var isExpired = context.AuthenticateFailure is SecurityTokenExpiredException;
                var message = isExpired
                    ? "Token đã hết hạn."
                    : "Bạn chưa đăng nhập hoặc token không hợp lệ.";

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";

                var payload = JsonSerializer.Serialize(new
                {
                    statusCode = StatusCodes.Status401Unauthorized,
                    message,
                    data = Array.Empty<object>(),
                    success = false
                });

                await context.Response.WriteAsync(payload);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";

                var payload = JsonSerializer.Serialize(new
                {
                    statusCode = StatusCodes.Status403Forbidden,
                    message = "Bạn không có quyền truy cập tài nguyên này.",
                    data = Array.Empty<object>(),
                    success = false
                });

                await context.Response.WriteAsync(payload);
            }
        };
    });

// ==== Authorization (roles + permissions) ====
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

// ==== Swagger ====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HRM API", Version = "v1" });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Nhập JWT token (không cần 'Bearer ' phía trước)",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });
});

// ==== CORS ====
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ==== Background services / options ====
builder.Services.Configure<ContractMaintenanceOptions>(
    builder.Configuration.GetSection("ContractMaintenance"));
builder.Services.AddHostedService<ContractMaintenanceWorker>();

var app = builder.Build();

// ==== Pipeline ====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi(); // nếu bạn đang dùng OpenAPI v8 (tùy gói)
}

app.UseHttpsRedirection();
app.UseCors("DevCors");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();
app.MapOpenApi();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

app.Run();
