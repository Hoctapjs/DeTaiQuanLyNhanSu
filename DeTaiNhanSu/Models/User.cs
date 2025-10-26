using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public Guid EmployeeId {get; set;}
        public string UserName { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public Guid RoleId { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpire { get; set; }

        // chức năng quên mật khẩu

        public bool MustChangePassword { get; set; } = false;

        public DateTime? TempPasswordExpireAt { get; set; }

        public DateTime? LastPasswordChangedAt { get; set; }

        // đăng nhập lần đầu
        public bool is_first_login { get; set; }


        // các bảng tham chiếu
        public Employee Employee { get; set; } = default!;
        public Role Role { get; set; } = default!;
        public ICollection<Notification> Notifications { get; set; } = [];

    }
}
