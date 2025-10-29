using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;


namespace DeTaiNhanSu.DbContextProject
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Position> Positions => Set<Position>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<Attendance> Attendances => Set<Attendance>();
        public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
        public DbSet<Request> Requests => Set<Request>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<Overtime> Overtimes => Set<Overtime>();
        public DbSet<RewardPenaltyType> RewardPenaltyTypes => Set<RewardPenaltyType>();
        public DbSet<RewardPenalty> RewardPenalties => Set<RewardPenalty>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<InsuranceProfile> InsuranceProfiles => Set<InsuranceProfile>();
        public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
        public DbSet<Salary> Salaries => Set<Salary>();
        public DbSet<SalaryItem> SalaryItems => Set<SalaryItem>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<TrainingRecord> TrainingRecords => Set<TrainingRecord>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === converter enum <-> string lowercase ===
            static ValueConverter<TEnum, string> LowerString<TEnum>()
            where TEnum : struct, Enum
            => new ValueConverter<TEnum, string>(
                v => v.ToString().ToLowerInvariant(),     // ghi xuống DB: "reward", "penalty", ...
                s => (TEnum)Enum.Parse(typeof(TEnum), s, true) // đọc lên, ignoreCase = true
            );

            modelBuilder.Entity<RewardPenaltyType>(b =>
            {
                b.Property(x => x.Type).HasConversion(LowerString<RewardPenaltyKind>())
                                       .HasMaxLength(32);
                b.Property(x => x.Level).HasConversion(LowerString<SeverityLevel>())
                                        .HasMaxLength(32);
                b.Property(x => x.Form).HasConversion(LowerString<ActionForm>())
                                       .HasMaxLength(32);
            });

            // common
            foreach (var prop in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType.IsEnum))
            {
                prop.SetProviderClrType(typeof(string));
            }

            // contract number
            modelBuilder.Entity<Contract>()
                .HasIndex(x => x.ContractNumber)
                .IsUnique();

            // employee
            modelBuilder.Entity<Employee>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Email).HasMaxLength(100).IsRequired();
                e.Property(x => x.Phone).HasMaxLength(20);
                e.HasIndex(x => x.Code).IsUnique();
                e.HasIndex(x => x.Email).IsUnique();
                e.HasIndex(x => x.Cccd).IsUnique().HasFilter("[Cccd] IS NOT NULL");
                e.HasOne(x => x.Department).WithMany(d => d.Employees).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Position).WithMany(p => p.Employees).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Department>(d =>
            {
                d.Property(x => x.Name).HasMaxLength(100).IsRequired();
                d.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Position>(p =>
            {
                p.Property(x => x.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<User>(u =>
            {
                u.Property(x => x.UserName).HasMaxLength(100).IsRequired();
                u.HasIndex(x => x.UserName).IsUnique();
                u.HasOne(x => x.Employee).WithOne(e => e.User).HasForeignKey<User>(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                u.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Role>(r =>
            {
                r.Property(x => x.Name).HasMaxLength(100).IsRequired();
                r.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<Permission>(p =>
            {
                p.Property(x => x.Code).HasMaxLength(100).IsRequired();
                p.HasIndex(x => x.Code).IsUnique();
            });

            modelBuilder.Entity<RolePermission>(rp =>
            {
                rp.HasKey(x => new { x.RoleId, x.PermissionId });
                rp.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId);
                rp.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId);
            });

            modelBuilder.Entity<Attendance>(a =>
            {
                a.HasOne(x => x.Employee).WithMany(e => e.Attendances).HasForeignKey(x => x.EmployeeId);
                a.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            });

            modelBuilder.Entity<WorkSchedule>(ws =>
            {
                ws.HasOne(x => x.Employee).WithMany()
                  .HasForeignKey(x => x.EmployeeId);
                ws.HasIndex(x => new { x.EmployeeId, x.Date }).HasDatabaseName("IX_WorkSchedules_Employee_Date");
            });

            modelBuilder.Entity<Request>(r =>
            {
                r.Property(x => x.Title).HasMaxLength(300).IsRequired();
                r.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
                r.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Contract>(c =>
            {
                c.Property(x => x.ContractNumber).HasMaxLength(100).IsRequired();
                c.Property(x => x.BasicSalary).HasColumnType("decimal(18,2)");
                c.Property(x => x.InsuranceSalary).HasColumnType("decimal(18,2)");
                c.HasIndex(x => x.ContractNumber).IsUnique();
                c.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
                c.HasOne(x => x.Representative).WithMany().HasForeignKey(x => x.RepresentativeId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Overtime>(o =>
            {
                o.Property(x => x.Hours).HasColumnType("decimal(5,2)");
                o.Property(x => x.Rate).HasColumnType("decimal(5,2)");
                o.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            });

            modelBuilder.Entity<RewardPenaltyType>(t =>
            {
                t.Property(x => x.DefaultAmount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<RewardPenalty>(rp =>
            {
                rp.Property(x => x.AmountOverride).HasColumnType("decimal(18,2)");
                rp.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
                rp.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
                rp.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Policy>(p =>
            {
                p.Property(x => x.Category).HasMaxLength(50).IsRequired();
                p.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<InsuranceProfile>(i =>
            {
                i.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            });

            modelBuilder.Entity<PayrollRun>(pr =>
            {
                pr.Property(x => x.Period).HasMaxLength(7).IsRequired(); // YYYY-MM
                pr.HasIndex(x => x.Period).IsUnique();
            });

            modelBuilder.Entity<Salary>(s =>
            {
                s.Property(x => x.Gross).HasColumnType("decimal(18,2)");
                s.Property(x => x.Net).HasColumnType("decimal(18,2)");
                s.Property(x => x.Details).HasColumnType("nvarchar(max)");
                s.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
                s.HasOne(x => x.PayrollRun).WithMany(p => p.Salaries).HasForeignKey(x => x.PayrollRunId);
                s.HasIndex(x => new { x.EmployeeId, x.PayrollRunId }).IsUnique();
            });

            modelBuilder.Entity<SalaryItem>(si =>
            {
                si.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                si.HasOne(x => x.Salary).WithMany(s => s.Items).HasForeignKey(x => x.SalaryId);
            });

            modelBuilder.Entity<TrainingRecord>(tr =>
            {
                tr.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
                tr.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId);
                tr.HasOne(x => x.EvaluatedByUser).WithMany().HasForeignKey(x => x.EvaluatedBy).OnDelete(DeleteBehavior.Restrict);
                tr.HasIndex(x => new { x.EmployeeId, x.CourseId }).IsUnique();
            });

            modelBuilder.Entity<Notification>(n =>
            {
                n.HasOne(x => x.User).WithMany(u => u.Notifications).HasForeignKey(x => x.UserId);
            });

            modelBuilder.Entity<AuditLog>(al =>
            {
                al.Property(x => x.Action).HasMaxLength(200).IsRequired();
                al.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
                al.HasIndex(x => x.CreatedAt);
            });

            // BỔ SUNG: Cấu hình GlobalSettings
            modelBuilder.Entity<GlobalSetting>(gs =>
            {
                // Key được cấu hình là UNIQUE để đảm bảo không có cài đặt trùng lặp
                gs.HasIndex(x => x.Key)
                  .IsUnique();

                // Cấu hình độ dài cho Key
                gs.Property(x => x.Key).HasMaxLength(100).IsRequired();
            });
        }
    }
}
