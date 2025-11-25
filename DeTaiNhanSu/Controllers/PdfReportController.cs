using System.Globalization;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Bảo mật
    public class PdfReportController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;

        public PdfReportController(AppDbContext db, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _env = env;
        }

        // GET: /api/PdfReport/payslip/{salaryId}
        [HttpGet("payslip-report-pdf/{salaryId}")]
        public async Task<IActionResult> ExportPayslip(Guid salaryId)
        {
            // 1. LẤY DỮ LIỆU (Giống SalaryController)
            var salary = await _db.Salaries
                .AsNoTracking()
                .Include(s => s.Employee)
                .ThenInclude(e => e.Department)
                .Include(s => s.Employee)
                .ThenInclude(e => e.Position)
                .Include(s => s.PayrollRun)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == salaryId);

            if (salary == null) return NotFound("Không tìm thấy dữ liệu lương.");

            // Phân loại các khoản lương
            var earnings = salary.Items.Where(i => i.Amount >= 0).ToList();
            var deductions = salary.Items.Where(i => i.Amount < 0).ToList();

            // Tính toán tổng cộng cho các header
            decimal totalEarningsSum = earnings.Sum(i => i.Amount);
            decimal totalDeductionsSum = deductions.Sum(i => i.Amount); // Dữ liệu gốc đang là số âm

            // 2. VẼ PDF BẰNG QUESTPDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2.5f, Unit.Centimetre); // Tăng margin cho thoáng hơn
                    page.PageColor(Colors.White);
                    // Cố gắng dùng font không chân (Sans-serif) để hiện đại hơn. 
                    // "Segoe UI" hoặc "Arial" là tốt trên Windows. Nếu trên Linux, cân nhắc "DejaVu Sans".
                    page.DefaultTextStyle(x => x.FontSize(13).FontFamily("Segoe UI", "Arial"));

                    // --- HEADER ---
                    page.Header().Row(row =>
                    {
                        row.ConstantColumn(200).Column(col => // Cố định chiều rộng cho phần logo/tên cty
                        {
                            // Thêm logo (nếu có file ảnh, thay thế bằng Image().FromFile())
                            col.Item().Text("CÔNG TY TNHH MTV CÔNG NGHỆ 3IT").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                            col.Item().Text("Địa chỉ: 140 Đường Lê Trọng Tấn, Tây Thạnh, Tân Phú, TP.HCM").FontSize(11).FontColor(Colors.Grey.Darken1);
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PHIẾU LƯƠNG").FontSize(28).Bold().AlignRight().FontColor(Colors.Grey.Darken4); // Lớn hơn, màu đậm hơn
                            col.Item().PaddingTop(5).AlignRight().Text($"Kỳ lương: {salary.PayrollRun.Period}").FontSize(13).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    // --- CONTENT ---
                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        // 1. Thông tin nhân viên (Nền xám nhẹ và viền bo tròn)
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten3).Background(Colors.Grey.Lighten5).Padding(15).Row(row =>
                        {
                            row.RelativeItem().Column(c => {
                                c.Item().PaddingBottom(5).Text(t => { t.Span("Mã NV: ").SemiBold(); t.Span(salary.Employee.Code).FontColor(Colors.Grey.Darken2); });
                                c.Item().Text(t => { t.Span("Họ tên: ").SemiBold(); t.Span(salary.Employee.FullName).FontColor(Colors.Grey.Darken2); });
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().PaddingBottom(5).Text(t => { t.Span("Phòng ban: ").SemiBold(); t.Span(salary.Employee.Department?.Name ?? "-").FontColor(Colors.Grey.Darken2); });
                                c.Item().Text(t => { t.Span("Chức vụ: ").SemiBold(); t.Span(salary.Employee.Position?.Name ?? "-").FontColor(Colors.Grey.Darken2); });
                            });
                        });

                        col.Item().Height(30); // Tăng khoảng cách

                        // 2. Bảng chi tiết lương
                        col.Item().Table(table =>
                        {
                            // Định nghĩa cột
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40); // STT
                                columns.RelativeColumn(3); // Khoản mục (chiếm 3 phần)
                                columns.RelativeColumn(2); // Số tiền (chiếm 2 phần)
                            });

                            // Header bảng
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("STT");
                                header.Cell().Element(HeaderCellStyle).Text("Khoản mục");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Số tiền (VNĐ)");

                                // Style cho header cell của bảng chi tiết
                                static IContainer HeaderCellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold().FontSize(13))
                                                    .BorderBottom(2).BorderColor(Colors.Grey.Darken1) // Đường kẻ đậm hơn
                                                    .PaddingVertical(8).Background(Colors.Grey.Lighten4); // Nền xám nhạt
                                }
                            });

                            // Body bảng: Thu nhập
                            table.Cell().ColumnSpan(3).PaddingTop(15).Text("I. THU NHẬP").FontSize(14).Bold().FontColor(Colors.Blue.Medium);

                            int stt = 1;
                            foreach (var item in earnings)
                            {
                                table.Cell().Element(BlockStyle).Text($"{stt++}");
                                table.Cell().Element(BlockStyle).Text(item.Note ?? item.Type.ToString());
                                table.Cell().Element(BlockStyle).AlignRight().Text(FormatCurrency(item.Amount));
                            }

                            // Tổng thu nhập
                            table.Cell().ColumnSpan(2).Element(TotalBlockStyle).Text("Tổng Thu Nhập (Gross):");
                            table.Cell().Element(TotalBlockStyle).AlignRight().Text(FormatCurrency(salary.Gross));


                            // Body bảng: Khấu trừ
                            table.Cell().ColumnSpan(3).PaddingTop(15).Text("II. KHẤU TRỪ").FontSize(14).Bold().FontColor(Colors.Red.Medium); // Màu đỏ cho khấu trừ

                            stt = 1;
                            foreach (var item in deductions)
                            {
                                table.Cell().Element(BlockStyle).Text($"{stt++}");
                                table.Cell().Element(BlockStyle).Text(item.Note ?? item.Type.ToString());
                                // Hiển thị giá trị tuyệt đối cho khấu trừ để dễ đọc, nhưng vẫn có thể giữ số âm tùy ý
                                table.Cell().Element(BlockStyle).AlignRight().Text(FormatCurrency(Math.Abs(item.Amount)));
                            }

                            // Tổng khấu trừ
                            table.Cell().ColumnSpan(2).Element(TotalBlockStyle).Text("Tổng Khấu Trừ:");
                            table.Cell().Element(TotalBlockStyle).AlignRight().Text(FormatCurrency(Math.Abs(totalDeductionsSum)));


                            // 3. THỰC LĨNH (NET) - Quan trọng nhất
                            table.Cell().ColumnSpan(3).PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Darken1); // Đường gạch ngang đậm hơn

                            table.Cell().ColumnSpan(2).PaddingTop(10).Text("THỰC LĨNH (NET):").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                            table.Cell().PaddingTop(10).AlignRight().Text(FormatCurrency(salary.Net) + " VNĐ").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                            // Helper styles
                            static IContainer BlockStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(8); // padding nhiều hơn
                            static IContainer TotalBlockStyle(IContainer container) => container.PaddingVertical(8).DefaultTextStyle(x => x.SemiBold().FontSize(13)).Background(Colors.Grey.Lighten5);
                        });

                        col.Item().Height(50); // Khoảng cách lớn hơn

                        // 4. Chữ ký
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("Người lập biểu").FontSize(13).SemiBold();
                                c.Item().PaddingTop(5).AlignCenter().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(11).FontColor(Colors.Grey.Darken1);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("Người nhận").FontSize(13).SemiBold();
                                c.Item().PaddingTop(5).AlignCenter().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(11).FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    //// --- FOOTER ---
                    //page.Footer().PaddingVertical(10).AlignCenter().Text(x =>
                    //{
                    //    x.Span("Trang ");
                    //    x.CurrentPageNumber();
                    //    x.Span(" / ");
                    //    x.TotalPages();
                    //    x.Span(" - Tài liệu nội bộ, vui lòng bảo mật.").FontSize(13).FontColor(Colors.Grey.Darken1);
                    //});
                });
            });

            // Hàm hỗ trợ định dạng tiền tệ
            string FormatCurrency(decimal amount)
            {
                // Sử dụng CultureInfo để đảm bảo dấu phân cách hàng nghìn và hàng thập phân chuẩn Việt Nam
                CultureInfo viVnCulture = new CultureInfo("vi-VN");
                return amount.ToString("N0", viVnCulture); // "N0" là định dạng số nguyên có dấu phân cách
            }

            // 3. TRẢ VỀ FILE STREAM
            var stream = new MemoryStream();
            document.GeneratePdf(stream);
            stream.Position = 0;

            var fileName = $"PhieuLuong_{salary.Employee.Code}_{salary.PayrollRun.Period}.pdf";
            return File(stream, "application/pdf", fileName);
        }

        //[HttpGet("profile/{employeeId}")]
        //public async Task<IActionResult> ExportEmployeeProfile(Guid employeeId)
        //{
        //    // 1. LẤY DỮ LIỆU
        //    // Lấy thông tin cơ bản
        //    var emp = await _db.Employees
        //        .AsNoTracking()
        //        .Include(e => e.Department)
        //        .Include(e => e.Position)
        //        .FirstOrDefaultAsync(e => e.Id == employeeId);

        //    if (emp == null) return NotFound("Nhân viên không tồn tại.");

        //    // Lấy lịch sử hợp đồng
        //    var contracts = await _db.Contracts
        //        .AsNoTracking()
        //        .Where(c => c.EmployeeId == employeeId)
        //        .OrderByDescending(c => c.StartDate)
        //        .ToListAsync();

        //    // Lấy lịch sử đào tạo
        //    var trainings = await _db.TrainingRecords
        //        .AsNoTracking()
        //        .Include(t => t.Course)
        //        .Where(t => t.EmployeeId == employeeId)
        //        .OrderByDescending(t => t.Course.CreatedAt)
        //        .ToListAsync();

        //    // 2. VẼ PDF
        //    var document = Document.Create(container =>
        //    {
        //        container.Page(page =>
        //        {
        //            page.Size(PageSizes.A4);
        //            page.Margin(2, Unit.Centimetre);
        //            page.PageColor(Colors.White);
        //            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

        //            // --- HEADER (Dùng chung style với Phiếu lương) ---
        //            page.Header().PaddingBottom(10).Row(row =>
        //            {
        //                row.RelativeItem().Column(col =>
        //                {
        //                    col.Item().Text("CÔNG TY CÔNG NGHỆ IT").Bold().FontSize(14).FontColor(Colors.Blue.Medium);
        //                    col.Item().Text("123 Đường ABC, TP.HCM").FontSize(9).FontColor(Colors.Grey.Darken1);
        //                    col.Item().Text("Hotline: (028) 3838 3838").FontSize(9).FontColor(Colors.Grey.Darken1);
        //                });

        //                // Avatar giả lập (Khung hình vuông bên phải)
        //                row.ConstantColumn(80).Border(1).BorderColor(Colors.Grey.Lighten2).Height(100).AlignCenter().AlignMiddle().Text("ẢNH 3x4").FontColor(Colors.Grey.Lighten1);
        //            });

        //            // --- CONTENT ---
        //            page.Content().Column(col =>
        //            {
        //                // Tiêu đề lớn
        //                col.Item().PaddingVertical(10).AlignCenter().Text("SƠ YẾU LÝ LỊCH NHÂN VIÊN").FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
        //                col.Item().PaddingBottom(20).AlignCenter().Text($"(Cập nhật ngày: {DateTime.Now:dd/MM/yyyy})").Italic().FontSize(9);

        //                // --- I. THÔNG TIN CÁ NHÂN ---
        //                col.Item().Element(SectionHeader).Text("I. THÔNG TIN CÁ NHÂN");

        //                col.Item().PaddingBottom(15).Table(table =>
        //                {
        //                    table.ColumnsDefinition(c =>
        //                    {
        //                        c.ConstantColumn(100); // Label
        //                        c.RelativeColumn();    // Value
        //                        c.ConstantColumn(100); // Label
        //                        c.RelativeColumn();    // Value
        //                    });

        //                    // Dòng 1
        //                    table.Cell().Element(LabelStyle).Text("Mã nhân viên:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Code).Bold();
        //                    table.Cell().Element(LabelStyle).Text("Họ và tên:");
        //                    table.Cell().Element(ValueStyle).Text(emp.FullName.ToUpper()).Bold();

        //                    // Dòng 2
        //                    table.Cell().Element(LabelStyle).Text("Ngày sinh:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Dob.HasValue ? emp.Dob.Value.ToString("dd/MM/yyyy") : "-");
        //                    table.Cell().Element(LabelStyle).Text("Giới tính:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Gender);

        //                    // Dòng 3
        //                    table.Cell().Element(LabelStyle).Text("Điện thoại:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Phone ?? "-");
        //                    table.Cell().Element(LabelStyle).Text("Email:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Email ?? "-");

        //                    // Dòng 4 (Address merge cột)
        //                    table.Cell().Element(LabelStyle).Text("Địa chỉ:");
        //                    table.Cell().ColumnSpan(3).Element(ValueStyle).Text(emp.Address ?? "-");
        //                });

        //                // --- II. THÔNG TIN CÔNG VIỆC ---
        //                col.Item().Element(SectionHeader).Text("II. THÔNG TIN CÔNG VIỆC");

        //                col.Item().PaddingBottom(15).Table(table =>
        //                {
        //                    table.ColumnsDefinition(c =>
        //                    {
        //                        c.ConstantColumn(100);
        //                        c.RelativeColumn();
        //                        c.ConstantColumn(100);
        //                        c.RelativeColumn();
        //                    });

        //                    table.Cell().Element(LabelStyle).Text("Phòng ban:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Department?.Name ?? "-");
        //                    table.Cell().Element(LabelStyle).Text("Chức vụ:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Position?.Name ?? "-");

        //                    table.Cell().Element(LabelStyle).Text("Ngày vào làm:");
        //                    table.Cell().Element(ValueStyle).Text(emp.HireDate.ToString("dd/MM/yyyy") ?? "-");
        //                    table.Cell().Element(LabelStyle).Text("Trạng thái:");
        //                    table.Cell().Element(ValueStyle).Text(emp.Status.ToString());
        //                });

        //                // --- III. LỊCH SỬ HỢP ĐỒNG ---
        //                col.Item().Element(SectionHeader).Text("III. QUÁ TRÌNH HỢP ĐỒNG");

        //                if (contracts.Any())
        //                {
        //                    col.Item().Table(table =>
        //                    {
        //                        table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); c.ConstantColumn(80); });

        //                        // Header bảng
        //                        table.Header(h => {
        //                            h.Cell().Element(TableHeaderStyle).Text("STT");
        //                            h.Cell().Element(TableHeaderStyle).Text("Số HĐ / Loại HĐ");
        //                            h.Cell().Element(TableHeaderStyle).Text("Ngày ký");
        //                            h.Cell().Element(TableHeaderStyle).Text("Hiệu lực");
        //                            h.Cell().Element(TableHeaderStyle).Text("Hết hạn");
        //                        });

        //                        int stt = 1;
        //                        foreach (var c in contracts)
        //                        {
        //                            table.Cell().Element(TableBodyStyle).Text($"{stt++}");
        //                            table.Cell().Element(TableBodyStyle).Text(t => {
        //                                t.Span(c.ContractNumber).Bold();
        //                                t.Span($"\n({c.Type})").FontSize(8).Italic();
        //                            });
        //                            table.Cell().Element(TableBodyStyle).Text(c.SignedDate?.ToString("dd/MM/yyyy") ?? "-");
        //                            table.Cell().Element(TableBodyStyle).Text(c.StartDate.ToString("dd/MM/yyyy"));
        //                            table.Cell().Element(TableBodyStyle).Text(c.EndDate.HasValue ? c.EndDate.Value.ToString("dd/MM/yyyy") : "Vô thời hạn");
        //                        }
        //                    });
        //                }
        //                else
        //                {
        //                    col.Item().Text("(Chưa có dữ liệu hợp đồng)").Italic().FontColor(Colors.Grey.Darken1);
        //                }

        //                col.Item().Height(15);

        //                // --- IV. LỊCH SỬ ĐÀO TẠO ---
        //                col.Item().Element(SectionHeader).Text("IV. LỊCH SỬ ĐÀO TẠO");

        //                if (trainings.Any())
        //                {
        //                    col.Item().Table(table =>
        //                    {
        //                        table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(); c.ConstantColumn(60); c.ConstantColumn(80); });

        //                        table.Header(h => {
        //                            h.Cell().Element(TableHeaderStyle).Text("STT");
        //                            h.Cell().Element(TableHeaderStyle).Text("Khóa học");
        //                            h.Cell().Element(TableHeaderStyle).Text("Điểm");
        //                            h.Cell().Element(TableHeaderStyle).Text("Kết quả");
        //                        });

        //                        int stt = 1;
        //                        foreach (var tr in trainings)
        //                        {
        //                            table.Cell().Element(TableBodyStyle).Text($"{stt++}");
        //                            table.Cell().Element(TableBodyStyle).Text(tr.Course.Name);
        //                            table.Cell().Element(TableBodyStyle).AlignCenter().Text(tr.Score.ToString());

        //                            // Tô màu trạng thái
        //                            string statusText = tr.Status.ToString();
        //                            var color = Colors.Black;
        //                            if (statusText == "completed") { statusText = "ĐẠT"; color = Colors.Green.Medium; }
        //                            else if (statusText == "failed") { statusText = "TRƯỢT"; color = Colors.Red.Medium; }

        //                            table.Cell().Element(TableBodyStyle).AlignRight().Text(statusText).SemiBold().FontColor(color);
        //                        }
        //                    });
        //                }
        //                else
        //                {
        //                    col.Item().Text("(Chưa tham gia khóa đào tạo nào)").Italic().FontColor(Colors.Grey.Darken1);
        //                }
        //            });

        //            // --- FOOTER ---
        //            page.Footer().PaddingTop(10).Row(row => {
        //                row.RelativeItem().Text(x => {
        //                    x.Span("In ngày: ");
        //                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        //                });
        //                row.RelativeItem().AlignRight().Text(x => {
        //                    x.Span("Trang ");
        //                    x.CurrentPageNumber();
        //                });
        //            });
        //        });
        //    });

        //    // --- STYLES (Helper Functions) ---
        //    static IContainer SectionHeader(IContainer container) =>
        //        container.BorderBottom(1).BorderColor(Colors.Blue.Medium).PaddingBottom(5).PaddingTop(10).PaddingBottom(10).DefaultTextStyle(x => x.Bold().FontSize(12).FontColor(Colors.Blue.Medium));

        //    static IContainer LabelStyle(IContainer container) => container.PaddingVertical(2).DefaultTextStyle(x => x.FontColor(Colors.Grey.Darken2));
        //    static IContainer ValueStyle(IContainer container) => container.PaddingVertical(2).PaddingLeft(5);

        //    static IContainer TableHeaderStyle(IContainer container) =>
        //        container.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Darken1).Padding(5).DefaultTextStyle(x => x.SemiBold());

        //    static IContainer TableBodyStyle(IContainer container) =>
        //        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5);


        //    // 3. TRẢ VỀ FILE
        //    var stream = new MemoryStream();
        //    document.GeneratePdf(stream);
        //    stream.Position = 0;
        //    return File(stream, "application/pdf", $"HoSo_{emp.Code}.pdf");
        //}

        [HttpGet("profile-report-pdf/{employeeId}")]
        public async Task<IActionResult> ExportEmployeeProfile(Guid employeeId)
        {
            // 1. LẤY DỮ LIỆU
            var emp = await _db.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (emp == null) return NotFound("Nhân viên không tồn tại.");

            var contracts = await _db.Contracts
                .AsNoTracking()
                .Where(c => c.EmployeeId == employeeId)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var trainings = await _db.TrainingRecords
                .AsNoTracking()
                .Include(t => t.Course)
                .Where(t => t.EmployeeId == employeeId)
                .OrderByDescending(t => t.Course.CreatedAt)
                .ToListAsync();

            // --- B. TẢI ẢNH TỪ URL (LOGIC MỚI) ---
            byte[]? avatarImageBytes = null;

            if (!string.IsNullOrEmpty(emp.AvatarUrl))
            {
                try
                {
                    // Tạo Client từ Factory
                    var client = _httpClientFactory.CreateClient();
                    // Đặt timeout ngắn (ví dụ 5s) để tránh treo báo cáo nếu link ảnh chết
                    client.Timeout = TimeSpan.FromSeconds(5);

                    // Tải ảnh về dạng mảng byte
                    avatarImageBytes = await client.GetByteArrayAsync(emp.AvatarUrl);
                }
                catch
                {
                    // Nếu lỗi (404, timeout, url sai...) -> avatarImageBytes vẫn là null
                    // Ta lờ đi để code chạy tiếp và hiển thị khung "ẢNH 3x4"
                }
            }

            // 2. VẼ PDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // --- HEADER ---
                    page.Header().PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("CÔNG TY CÔNG NGHỆ IT").Bold().FontSize(14).FontColor(Colors.Blue.Medium);
                            col.Item().Text("Địa Chỉ: 140 Đường Lê Trọng Tấn, Tây Thạnh, Tân Phú, TP.HCM").FontSize(9).FontColor(Colors.Grey.Darken1);
                            col.Item().Text("Hotline: (028) 3838 3838").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        // [YÊU CẦU 1]: Thêm hình ảnh nhân viên
                        // Lưu ý: Bạn cần xử lý đường dẫn ảnh thực tế (VD: wwwroot/images/...)
                        row.ConstantColumn(80).Height(100).Element(container =>
                        {
                            // Kiểm tra nếu có AvatarUrl (Giả sử là đường dẫn file cục bộ trên server)
                            if (avatarImageBytes != null)
                            {
                                // Load ảnh thật
                                //byte[] imageBytes = System.IO.File.ReadAllBytes(emp.AvatarUrl);
                                //container.Image(imageBytes, ImageScaling.FitArea);
                                container.Image(avatarImageBytes, ImageScaling.FitArea);
                            }
                            else
                            {
                                // Placeholder nếu không có ảnh
                                container.Border(1).BorderColor(Colors.Grey.Lighten2).AlignCenter().AlignMiddle().Text("ẢNH 3x4").FontColor(Colors.Grey.Lighten1);
                            }
                        });
                    });

                    // --- CONTENT ---
                    page.Content().Column(col =>
                    {
                        col.Item().PaddingVertical(10).AlignCenter().Text("SƠ YẾU LÝ LỊCH NHÂN VIÊN").FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingBottom(20).AlignCenter().Text($"(Cập nhật ngày: {DateTime.Now:dd/MM/yyyy})").Italic().FontSize(9);

                        // --- I. THÔNG TIN CÁ NHÂN ---
                        col.Item().Element(SectionHeader).Text("I. THÔNG TIN CÁ NHÂN");

                        // [YÊU CẦU 4]: Thêm PaddingTop(10) để chữ không sát đường kẻ trên
                        col.Item().PaddingTop(10).PaddingBottom(15).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                // [YÊU CẦU 2]: Giảm độ rộng cột Label từ 100 -> 85 để sát thông tin hơn
                                c.ConstantColumn(85); // Label 1
                                c.RelativeColumn();   // Value 1
                                c.ConstantColumn(85); // Label 2
                                c.RelativeColumn();   // Value 2
                            });

                            // Dòng 1
                            table.Cell().Element(LabelStyle).Text("Mã nhân viên:");
                            table.Cell().Element(ValueStyle).Text(emp.Code).Bold();
                            table.Cell().Element(LabelStyle).Text("Họ và tên:");
                            table.Cell().Element(ValueStyle).Text(emp.FullName.ToUpper()).Bold();

                            // Dòng 2
                            table.Cell().Element(LabelStyle).Text("Ngày sinh:");
                            table.Cell().Element(ValueStyle).Text(emp.Dob.HasValue ? emp.Dob.Value.ToString("dd/MM/yyyy") : "-");
                            table.Cell().Element(LabelStyle).Text("Giới tính:");
                            string genderVietnamese = emp.Gender switch
                            {
                                Gender.female => "Nữ",
                                Gender.male => "Nam",
                                Gender.other => "Khác"
                            };
                            //table.Cell().Element(ValueStyle).Text(emp.Gender);
                            table.Cell().Element(ValueStyle).Text(genderVietnamese);

                            // Dòng 3
                            table.Cell().Element(LabelStyle).Text("Điện thoại:");
                            table.Cell().Element(ValueStyle).Text(emp.Phone ?? "-");
                            table.Cell().Element(LabelStyle).Text("Email:");
                            table.Cell().Element(ValueStyle).Text(emp.Email ?? "-");

                            // Dòng 4 - [YÊU CẦU 3]: Địa chỉ chiếm hết chiều dài còn lại
                            // Bảng có 4 cột. Địa chỉ bắt đầu ở cột 2 (Value 1). 
                            // Để chiếm hết bên phải, nó phải gộp cột 2, 3, và 4 => ColumnSpan(3)
                            table.Cell().Element(LabelStyle).Text("Địa chỉ:");
                            table.Cell().ColumnSpan(3).Element(ValueStyle).Text(emp.Address ?? "-");
                        });

                        // --- II. THÔNG TIN CÔNG VIỆC ---
                        col.Item().Element(SectionHeader).Text("II. THÔNG TIN CÔNG VIỆC");

                        // [YÊU CẦU 4]: Thêm PaddingTop(10)
                        col.Item().PaddingTop(10).PaddingBottom(15).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                // [YÊU CẦU 2]: Giảm độ rộng cột Label
                                c.ConstantColumn(85);
                                c.RelativeColumn();
                                c.ConstantColumn(85);
                                c.RelativeColumn();
                            });

                            table.Cell().Element(LabelStyle).Text("Phòng ban:");
                            table.Cell().Element(ValueStyle).Text(emp.Department?.Name ?? "-");
                            table.Cell().Element(LabelStyle).Text("Chức vụ:");
                            table.Cell().Element(ValueStyle).Text(emp.Position?.Name ?? "-");

                            table.Cell().Element(LabelStyle).Text("Ngày vào làm:");
                            table.Cell().Element(ValueStyle).Text(emp.HireDate.ToString("dd/MM/yyyy") ?? "-");
                            table.Cell().Element(LabelStyle).Text("Trạng thái:");
                            string statusVietnamese = emp.Status switch
                            {
                                EmployeeStatus.active => "Đang hoạt động",
                                EmployeeStatus.inactive => "Không hoạt động",
                            };
                            //table.Cell().Element(ValueStyle).Text(emp.Gender);
                            table.Cell().Element(ValueStyle).Text(statusVietnamese);
                        });

                        // --- III. LỊCH SỬ HỢP ĐỒNG ---
                        col.Item().Element(SectionHeader).Text("III. QUÁ TRÌNH HỢP ĐỒNG");

                        if (contracts.Any())
                        {
                            // Thêm PaddingTop cho bảng này luôn cho đồng bộ
                            col.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); c.ConstantColumn(80); });

                                table.Header(h => {
                                    h.Cell().Element(TableHeaderStyle).Text("STT");
                                    h.Cell().Element(TableHeaderStyle).Text("Số HĐ / Loại HĐ");
                                    h.Cell().Element(TableHeaderStyle).Text("Ngày ký");
                                    h.Cell().Element(TableHeaderStyle).Text("Hiệu lực");
                                    h.Cell().Element(TableHeaderStyle).Text("Hết hạn");
                                });

                                int stt = 1;
                                foreach (var c in contracts)
                                {
                                    table.Cell().Element(TableBodyStyle).Text($"{stt++}");
                                    table.Cell().Element(TableBodyStyle).Text(t => {
                                        t.Span(c.ContractNumber).Bold();
                                        t.Span($"\n({c.Type})").FontSize(8).Italic();
                                    });
                                    table.Cell().Element(TableBodyStyle).Text(c.SignedDate?.ToString("dd/MM/yyyy"));
                                    table.Cell().Element(TableBodyStyle).Text(c.StartDate.ToString("dd/MM/yyyy"));
                                    table.Cell().Element(TableBodyStyle).Text(c.EndDate.HasValue ? c.EndDate.Value.ToString("dd/MM/yyyy") : "Vô thời hạn");
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(5).Text("(Chưa có dữ liệu hợp đồng)").Italic().FontColor(Colors.Grey.Darken1);
                        }

                        col.Item().Height(15);

                        // --- IV. LỊCH SỬ ĐÀO TẠO ---
                        col.Item().Element(SectionHeader).Text("IV. LỊCH SỬ ĐÀO TẠO");

                        if (trainings.Any())
                        {
                            // Thêm PaddingTop cho bảng này luôn cho đồng bộ
                            col.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(); c.ConstantColumn(60); c.ConstantColumn(80); });

                                table.Header(h => {
                                    h.Cell().Element(TableHeaderStyle).Text("STT");
                                    h.Cell().Element(TableHeaderStyle).Text("Khóa học");
                                    h.Cell().Element(TableHeaderStyle).Text("Điểm");
                                    h.Cell().Element(TableHeaderStyle).Text("Kết quả");
                                });

                                int stt = 1;
                                foreach (var tr in trainings)
                                {
                                    table.Cell().Element(TableBodyStyle).Text($"{stt++}");
                                    table.Cell().Element(TableBodyStyle).Text(tr.Course.Name);
                                    table.Cell().Element(TableBodyStyle).AlignCenter().Text(tr.Score.ToString());

                                    string statusText = tr.Status.ToString();
                                    var color = Colors.Black;
                                    if (statusText == "completed") { statusText = "ĐẠT"; color = Colors.Green.Medium; }
                                    else if (statusText == "failed") { statusText = "TRƯỢT"; color = Colors.Red.Medium; }

                                    table.Cell().Element(TableBodyStyle).AlignRight().Text(statusText).SemiBold().FontColor(color);
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(5).Text("(Chưa tham gia khóa đào tạo nào)").Italic().FontColor(Colors.Grey.Darken1);
                        }
                    });

                    // --- FOOTER ---
                    page.Footer().PaddingTop(10).Row(row => {
                        row.RelativeItem().Text(x => {
                            x.Span("In ngày: ");
                            x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                        });
                        row.RelativeItem().AlignRight().Text(x => {
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                        });
                    });
                });
            });

            // Helper Styles (Giữ nguyên)
            static IContainer SectionHeader(IContainer container) =>
                container.BorderBottom(1).BorderColor(Colors.Blue.Medium).PaddingBottom(5).PaddingTop(10).PaddingBottom(0).DefaultTextStyle(x => x.Bold().FontSize(12).FontColor(Colors.Blue.Medium)); // MarginBottom 0 để ta dùng PaddingTop của Table kiểm soát

            static IContainer LabelStyle(IContainer container) => container.PaddingVertical(2).DefaultTextStyle(x => x.FontColor(Colors.Grey.Darken2));
            static IContainer ValueStyle(IContainer container) => container.PaddingVertical(2).PaddingLeft(5);

            static IContainer TableHeaderStyle(IContainer container) =>
                container.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Darken1).Padding(5).DefaultTextStyle(x => x.SemiBold());

            static IContainer TableBodyStyle(IContainer container) =>
                container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5);

            var stream = new MemoryStream();
            document.GeneratePdf(stream);
            stream.Position = 0;
            return File(stream, "application/pdf", $"HoSo_{emp.Code}.pdf");
        }

        [HttpGet("profile-report-pdf-mobile/{employeeId}")]
        public async Task<IActionResult> ExportEmployeeProfileMobile(Guid employeeId, [FromQuery] string returnType = "url")
        {
            // 1. LẤY DỮ LIỆU
            var emp = await _db.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (emp == null) return NotFound("Nhân viên không tồn tại.");

            var contracts = await _db.Contracts
                .AsNoTracking()
                .Where(c => c.EmployeeId == employeeId)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var trainings = await _db.TrainingRecords
                .AsNoTracking()
                .Include(t => t.Course)
                .Where(t => t.EmployeeId == employeeId)
                .OrderByDescending(t => t.Course.CreatedAt)
                .ToListAsync();

            // --- B. TẢI ẢNH TỪ URL (LOGIC MỚI) ---
            byte[]? avatarImageBytes = null;

            if (!string.IsNullOrEmpty(emp.AvatarUrl))
            {
                try
                {
                    // Tạo Client từ Factory
                    var client = _httpClientFactory.CreateClient();
                    // Đặt timeout ngắn (ví dụ 5s) để tránh treo báo cáo nếu link ảnh chết
                    client.Timeout = TimeSpan.FromSeconds(5);

                    // Tải ảnh về dạng mảng byte
                    avatarImageBytes = await client.GetByteArrayAsync(emp.AvatarUrl);
                }
                catch
                {
                    // Nếu lỗi (404, timeout, url sai...) -> avatarImageBytes vẫn là null
                    // Ta lờ đi để code chạy tiếp và hiển thị khung "ẢNH 3x4"
                }
            }

            // 2. VẼ PDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // --- HEADER ---
                    page.Header().PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(8);
                            col.Item().Text("CÔNG TY TNHH MTV CÔNG NGHỆ 3IT").Bold().FontSize(20).FontColor(Colors.Blue.Medium);
                            col.Item().Text("Address: 140 Đường Lê Trọng Tấn, Tây Thạnh, Tân Phú, TP.HCM").FontSize(12).FontColor(Colors.Grey.Darken1);
                            col.Item().Text("Hotline: (028) 3838 3838").FontSize(12).FontColor(Colors.Grey.Darken1);
                            col.Item().Text("Contact: contact@huynhthanhson.io.vn").FontSize(12).FontColor(Colors.Grey.Darken1);
                        });

                        // [YÊU CẦU 1]: Thêm hình ảnh nhân viên
                        // Lưu ý: Bạn cần xử lý đường dẫn ảnh thực tế (VD: wwwroot/images/...)
                        row.ConstantColumn(80).Height(100).Element(container =>
                        {
                            // Kiểm tra nếu có AvatarUrl (Giả sử là đường dẫn file cục bộ trên server)
                            if (avatarImageBytes != null)
                            {
                                // Load ảnh thật
                                //byte[] imageBytes = System.IO.File.ReadAllBytes(emp.AvatarUrl);
                                //container.Image(imageBytes, ImageScaling.FitArea);
                                container.Image(avatarImageBytes, ImageScaling.FitArea);
                            }
                            else
                            {
                                // Placeholder nếu không có ảnh
                                container.Border(1).BorderColor(Colors.Grey.Lighten2).AlignCenter().AlignMiddle().Text("ẢNH 3x4").FontColor(Colors.Grey.Lighten1);
                            }
                        });
                    });

                    // --- CONTENT ---
                    page.Content().Column(col =>
                    {
                        col.Item().PaddingVertical(10).AlignCenter().Text("SƠ YẾU LÝ LỊCH NHÂN VIÊN").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingBottom(20).AlignCenter().Text($"(Cập nhật ngày: {DateTime.Now:dd/MM/yyyy})").Italic().FontSize(13);

                        // --- I. THÔNG TIN CÁ NHÂN ---
                        col.Item().Element(SectionHeader).Text("I. THÔNG TIN CÁ NHÂN");

                        // [YÊU CẦU 4]: Thêm PaddingTop(10) để chữ không sát đường kẻ trên
                        col.Item().PaddingTop(10).PaddingBottom(15).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                // [YÊU CẦU 2]: Giảm độ rộng cột Label từ 100 -> 85 để sát thông tin hơn
                                c.ConstantColumn(85); // Label 1
                                c.RelativeColumn();   // Value 1
                                c.ConstantColumn(85); // Label 2
                                c.RelativeColumn();   // Value 2
                            });

                            // Dòng 1
                            table.Cell().Element(LabelStyle).Text("Mã nhân viên:");
                            table.Cell().Element(ValueStyle).Text(emp.Code).Bold();
                            table.Cell().Element(LabelStyle).Text("Họ và tên:");
                            table.Cell().Element(ValueStyle).Text(emp.FullName.ToUpper()).Bold();

                            // Dòng 2
                            table.Cell().Element(LabelStyle).Text("Ngày sinh:");
                            table.Cell().Element(ValueStyle).Text(emp.Dob.HasValue ? emp.Dob.Value.ToString("dd/MM/yyyy") : "-");
                            table.Cell().Element(LabelStyle).Text("Giới tính:");
                            string genderVietnamese = emp.Gender switch
                            {
                                Gender.female => "Nữ",
                                Gender.male => "Nam",
                                Gender.other => "Khác"
                            };
                            //table.Cell().Element(ValueStyle).Text(emp.Gender);
                            table.Cell().Element(ValueStyle).Text(genderVietnamese);

                            // Dòng 3
                            table.Cell().Element(LabelStyle).Text("Điện thoại:");
                            table.Cell().Element(ValueStyle).Text(emp.Phone ?? "-");
                            table.Cell().Element(LabelStyle).Text("Email:");
                            table.Cell().Element(ValueStyle).Text(emp.Email ?? "-");

                            // Dòng 4 - [YÊU CẦU 3]: Địa chỉ chiếm hết chiều dài còn lại
                            // Bảng có 4 cột. Địa chỉ bắt đầu ở cột 2 (Value 1). 
                            // Để chiếm hết bên phải, nó phải gộp cột 2, 3, và 4 => ColumnSpan(3)
                            table.Cell().Element(LabelStyle).Text("Địa chỉ:");
                            table.Cell().ColumnSpan(3).Element(ValueStyle).Text(emp.Address ?? "-");
                        });

                        // --- II. THÔNG TIN CÔNG VIỆC ---
                        col.Item().Element(SectionHeader).Text("II. THÔNG TIN CÔNG VIỆC");

                        // [YÊU CẦU 4]: Thêm PaddingTop(10)
                        col.Item().PaddingTop(10).PaddingBottom(15).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                // [YÊU CẦU 2]: Giảm độ rộng cột Label
                                c.ConstantColumn(85);
                                c.RelativeColumn();
                                c.ConstantColumn(85);
                                c.RelativeColumn();
                            });

                            table.Cell().Element(LabelStyle).Text("Phòng ban:");
                            table.Cell().Element(ValueStyle).Text(emp.Department?.Name ?? "-");
                            table.Cell().Element(LabelStyle).Text("Chức vụ:");
                            table.Cell().Element(ValueStyle).Text(emp.Position?.Name ?? "-");

                            table.Cell().Element(LabelStyle).Text("Ngày vào làm:");
                            table.Cell().Element(ValueStyle).Text(emp.HireDate.ToString("dd/MM/yyyy") ?? "-");
                            //table.Cell().Element(LabelStyle).Text("Trạng thái:");
                            //table.Cell().Element(ValueStyle).Text(emp.Status.ToString());
                            table.Cell().Element(LabelStyle).Text("Trạng thái:");
                            string statusVietnamese = emp.Status switch
                            {
                                EmployeeStatus.active => "Đang hoạt động",
                                EmployeeStatus.inactive => "Không hoạt động",
                            };
                            //table.Cell().Element(ValueStyle).Text(emp.Gender);
                            table.Cell().Element(ValueStyle).Text(statusVietnamese);
                        });

                        // --- III. LỊCH SỬ HỢP ĐỒNG ---
                        col.Item().Element(SectionHeader).Text("III. QUÁ TRÌNH HỢP ĐỒNG");

                        if (contracts.Any())
                        {
                            // Thêm PaddingTop cho bảng này luôn cho đồng bộ
                            col.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); c.ConstantColumn(80); });

                                table.Header(h => {
                                    h.Cell().Element(TableHeaderStyle).Text("STT");
                                    h.Cell().Element(TableHeaderStyle).Text("Số HĐ / Loại HĐ");
                                    h.Cell().Element(TableHeaderStyle).Text("Ngày ký");
                                    h.Cell().Element(TableHeaderStyle).Text("Hiệu lực");
                                    h.Cell().Element(TableHeaderStyle).Text("Hết hạn");
                                });

                                int stt = 1;
                                foreach (var c in contracts)
                                {
                                    table.Cell().Element(TableBodyStyle).Text($"{stt++}");
                                    table.Cell().Element(TableBodyStyle).Text(t => {
                                        t.Span(c.ContractNumber).Bold();
                                        t.Span($"\n({c.Type})").FontSize(8).Italic();
                                    });
                                    table.Cell().Element(TableBodyStyle).Text(c.SignedDate?.ToString("dd/MM/yyyy"));
                                    table.Cell().Element(TableBodyStyle).Text(c.StartDate.ToString("dd/MM/yyyy"));
                                    table.Cell().Element(TableBodyStyle).Text(c.EndDate.HasValue ? c.EndDate.Value.ToString("dd/MM/yyyy") : "Vô thời hạn");
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(5).Text("(Chưa có dữ liệu hợp đồng)").Italic().FontColor(Colors.Grey.Darken1);
                        }

                        col.Item().Height(15);

                        // --- IV. LỊCH SỬ ĐÀO TẠO ---
                        col.Item().Element(SectionHeader).Text("IV. LỊCH SỬ ĐÀO TẠO");

                        if (trainings.Any())
                        {
                            // Thêm PaddingTop cho bảng này luôn cho đồng bộ
                            col.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(); c.ConstantColumn(60); c.ConstantColumn(80); });

                                table.Header(h => {
                                    h.Cell().Element(TableHeaderStyle).Text("STT");
                                    h.Cell().Element(TableHeaderStyle).Text("Khóa học");
                                    h.Cell().Element(TableHeaderStyle).Text("Điểm");
                                    h.Cell().Element(TableHeaderStyle).Text("Kết quả");
                                });

                                int stt = 1;
                                foreach (var tr in trainings)
                                {
                                    table.Cell().Element(TableBodyStyle).Text($"{stt++}");
                                    table.Cell().Element(TableBodyStyle).Text(tr.Course.Name);
                                    table.Cell().Element(TableBodyStyle).AlignCenter().Text(tr.Score.ToString());

                                    string statusText = tr.Status.ToString();
                                    var color = Colors.Black;
                                    if (statusText == "completed") { statusText = "ĐẠT"; color = Colors.Green.Medium; }
                                    else if (statusText == "failed") { statusText = "TRƯỢT"; color = Colors.Red.Medium; }

                                    table.Cell().Element(TableBodyStyle).AlignRight().Text(statusText).SemiBold().FontColor(color);
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(5).Text("(Chưa tham gia khóa đào tạo nào)").Italic().FontColor(Colors.Grey.Darken1);
                        }
                    });

                    // --- FOOTER ---
                    page.Footer().PaddingTop(10).Row(row => {
                        row.RelativeItem().Text(x => {
                            x.Span("In ngày: ");
                            x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                        });
                        row.RelativeItem().AlignRight().Text(x => {
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                        });
                    });
                });
            });

            // --- 4. XỬ LÝ ĐẦU RA DỰA TRÊN returnType ---

            // Tạo tên file an toàn: HoSo_MãNV_TimeStamp.pdf
            string fileName = $"HoSo_{emp.Code}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            if (returnType.ToLower() == "url")
            {
                // === CASE A: LƯU SERVER & TRẢ VỀ URL (Cho Mobile App) ===
                try
                {
                    // 1. Tạo đường dẫn lưu trữ (Theo phong cách API UploadAvatar)
                    // Thay vì dùng _env.WebRootPath, ta dùng Directory.GetCurrentDirectory()
                    var reportsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");

                    // Tạo thư mục nếu chưa có
                    if (!Directory.Exists(reportsFolder))
                        Directory.CreateDirectory(reportsFolder);

                    // 2. Đường dẫn file vật lý đầy đủ
                    var fullPath = Path.Combine(reportsFolder, fileName);

                    // 3. Ghi file PDF ra ổ cứng
                    document.GeneratePdf(fullPath);

                    // 4. Tạo URL công khai (Theo phong cách API UploadAvatar)
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var relativePath = $"/reports/{fileName}";
                    var publicUrl = $"{baseUrl}{relativePath}";

                    return Ok(new
                    {
                        success = true,
                        message = "Xuất báo cáo thành công.",
                        downloadUrl = publicUrl,
                        fileName = fileName
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = $"Lỗi khi lưu file: {ex.Message}" });
                }
            }
            else
            {
                // === CASE B: TRẢ VỀ STREAM (Cho Trình duyệt / Download trực tiếp) ===
                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;
                return File(stream, "application/pdf", fileName);
            }

            // Helper Styles (Giữ nguyên)
            static IContainer SectionHeader(IContainer container) =>
                container.BorderBottom(1).BorderColor(Colors.Blue.Medium).PaddingBottom(5).PaddingTop(10).PaddingBottom(0).DefaultTextStyle(x => x.Bold().FontSize(12).FontColor(Colors.Blue.Medium)); // MarginBottom 0 để ta dùng PaddingTop của Table kiểm soát

            static IContainer LabelStyle(IContainer container) => container.PaddingVertical(2).DefaultTextStyle(x => x.FontColor(Colors.Grey.Darken2));
            static IContainer ValueStyle(IContainer container) => container.PaddingVertical(2).PaddingLeft(5);

            static IContainer TableHeaderStyle(IContainer container) =>
                container.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Darken1).Padding(5).DefaultTextStyle(x => x.SemiBold());

            static IContainer TableBodyStyle(IContainer container) =>
                container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5);
            //var stream = new MemoryStream();
            //document.GeneratePdf(stream);
            //stream.Position = 0;
            //return File(stream, "application/pdf", $"HoSo_{emp.Code}.pdf");
        }

        [HttpGet("payslip-report-pdf-mobile/{salaryId}")]
        public async Task<IActionResult> ExportPayslipMobile(Guid salaryId, [FromQuery] string returnType = "url")
        {
            // 1. LẤY DỮ LIỆU (Giữ nguyên)
            var salary = await _db.Salaries
                .AsNoTracking()
                .Include(s => s.Employee)
                .ThenInclude(e => e.Department)
                .Include(s => s.Employee)
                .ThenInclude(e => e.Position)
                .Include(s => s.PayrollRun)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == salaryId);

            if (salary == null) return NotFound("Không tìm thấy dữ liệu lương.");

            // Phân loại các khoản lương (Giữ nguyên)
            var earnings = salary.Items.Where(i => i.Amount >= 0).ToList();
            var deductions = salary.Items.Where(i => i.Amount < 0).ToList();

            decimal totalEarningsSum = earnings.Sum(i => i.Amount);
            decimal totalDeductionsSum = deductions.Sum(i => i.Amount);

            // 2. VẼ PDF BẰNG QUESTPDF (Giữ nguyên)
            var document = Document.Create(container =>
            {
                // ... (Code vẽ PDF giữ nguyên) ...
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(13).FontFamily("Segoe UI", "Arial"));

                    // --- HEADER ---
                    page.Header().Row(row =>
                    {
                        row.ConstantColumn(200).Column(col =>
                        {
                            col.Item().Text("CÔNG TY TNHH MTV CÔNG NGHỆ 3IT").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                            col.Item().Text("Địa chỉ: 140 Đường Lê Trọng Tấn, Tây Thạnh, Tân Phú, TP.HCM").FontSize(11).FontColor(Colors.Grey.Darken1);
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PHIẾU LƯƠNG").FontSize(28).Bold().AlignRight().FontColor(Colors.Grey.Darken4);
                            col.Item().PaddingTop(5).AlignRight().Text($"Kỳ lương: {salary.PayrollRun.Period}").FontSize(13).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    // --- CONTENT ---
                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        // 1. Thông tin nhân viên
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten3).Background(Colors.Grey.Lighten5).Padding(15).Row(row =>
                        {
                            row.RelativeItem().Column(c => {
                                c.Item().PaddingBottom(5).Text(t => { t.Span("Mã NV: ").SemiBold(); t.Span(salary.Employee.Code).FontColor(Colors.Grey.Darken2); });
                                c.Item().Text(t => { t.Span("Họ tên: ").SemiBold(); t.Span(salary.Employee.FullName).FontColor(Colors.Grey.Darken2); });
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().PaddingBottom(5).Text(t => { t.Span("Phòng ban: ").SemiBold(); t.Span(salary.Employee.Department?.Name ?? "-").FontColor(Colors.Grey.Darken2); });
                                c.Item().Text(t => { t.Span("Chức vụ: ").SemiBold(); t.Span(salary.Employee.Position?.Name ?? "-").FontColor(Colors.Grey.Darken2); });
                            });
                        });

                        col.Item().Height(30);

                        // 2. Bảng chi tiết lương
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            // Header bảng
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("STT");
                                header.Cell().Element(HeaderCellStyle).Text("Khoản mục");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Số tiền (VNĐ)");

                                static IContainer HeaderCellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold().FontSize(13))
                                                    .BorderBottom(2).BorderColor(Colors.Grey.Darken1)
                                                    .PaddingVertical(8).Background(Colors.Grey.Lighten4);
                                }
                            });

                            // Body bảng: Thu nhập
                            table.Cell().ColumnSpan(3).PaddingTop(15).Text("I. THU NHẬP").FontSize(14).Bold().FontColor(Colors.Blue.Medium);

                            int stt = 1;
                            foreach (var item in earnings)
                            {
                                table.Cell().Element(BlockStyle).Text($"{stt++}");
                                table.Cell().Element(BlockStyle).Text(item.Note ?? item.Type.ToString());
                                table.Cell().Element(BlockStyle).AlignRight().Text(FormatCurrency(item.Amount));
                            }

                            // Tổng thu nhập
                            table.Cell().ColumnSpan(2).Element(TotalBlockStyle).Text("Tổng Thu Nhập (Gross):");
                            table.Cell().Element(TotalBlockStyle).AlignRight().Text(FormatCurrency(salary.Gross));


                            // Body bảng: Khấu trừ
                            table.Cell().ColumnSpan(3).PaddingTop(15).Text("II. KHẤU TRỪ").FontSize(14).Bold().FontColor(Colors.Red.Medium);

                            stt = 1;
                            foreach (var item in deductions)
                            {
                                table.Cell().Element(BlockStyle).Text($"{stt++}");
                                table.Cell().Element(BlockStyle).Text(item.Note ?? item.Type.ToString());
                                table.Cell().Element(BlockStyle).AlignRight().Text(FormatCurrency(Math.Abs(item.Amount)));
                            }

                            // Tổng khấu trừ
                            table.Cell().ColumnSpan(2).Element(TotalBlockStyle).Text("Tổng Khấu Trừ:");
                            table.Cell().Element(TotalBlockStyle).AlignRight().Text(FormatCurrency(Math.Abs(totalDeductionsSum)));


                            // 3. THỰC LĨNH (NET) - Quan trọng nhất
                            table.Cell().ColumnSpan(3).PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Darken1);

                            table.Cell().ColumnSpan(2).PaddingTop(10).Text("THỰC LĨNH (NET):").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                            table.Cell().PaddingTop(10).AlignRight().Text(FormatCurrency(salary.Net) + " VNĐ").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                            // Helper styles
                            static IContainer BlockStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(8);
                            static IContainer TotalBlockStyle(IContainer container) => container.PaddingVertical(8).DefaultTextStyle(x => x.SemiBold().FontSize(13)).Background(Colors.Grey.Lighten5);
                        });

                        col.Item().Height(50);

                        // 4. Chữ ký
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("Người lập biểu").FontSize(13).SemiBold();
                                c.Item().PaddingTop(5).AlignCenter().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(11).FontColor(Colors.Grey.Darken1);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("Người nhận").FontSize(13).SemiBold();
                                c.Item().PaddingTop(5).AlignCenter().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(11).FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    // --- FOOTER ---
                    //page.Footer().PaddingVertical(10).AlignCenter().Text(x =>
                    //{
                    //    x.Span("Trang ");
                    //    x.CurrentPageNumber();
                    //    x.Span(" / ");
                    //    x.TotalPages();
                    //    x.Span(" - Tài liệu nội bộ, vui lòng bảo mật.").FontSize(11).FontColor(Colors.Grey.Darken1);
                    //});
                });
            });

            // Hàm hỗ trợ định dạng tiền tệ (cần đặt ngoài scope action hoặc làm private method của class)
            string FormatCurrency(decimal amount)
            {
                CultureInfo viVnCulture = new CultureInfo("vi-VN");
                return amount.ToString("N0", viVnCulture);
            }

            // Tạo tên file an toàn: PhieuLuong_MãNV_KỳLương.pdf
            string fileName = $"PhieuLuong_{salary.Employee.Code}_{salary.PayrollRun.Period}.pdf";

            if (returnType.ToLower() == "url")
            {
                // === CASE A: LƯU SERVER & TRẢ VỀ URL (Tham khảo UploadAvatar) ===
                try
                {
                    // 1. Lấy đường dẫn lưu trữ (Dùng phương pháp của UploadAvatar)
                    var reportsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");

                    if (!Directory.Exists(reportsFolder))
                        Directory.CreateDirectory(reportsFolder);

                    // 2. Đường dẫn file vật lý đầy đủ
                    string filePath = Path.Combine(reportsFolder, fileName);

                    // 3. Ghi file PDF ra ổ cứng
                    document.GeneratePdf(filePath);

                    // 4. Tạo URL công khai
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var relativePath = $"/reports/{fileName}";
                    var publicUrl = $"{baseUrl}{relativePath}";

                    return Ok(new
                    {
                        success = true,
                        message = "Xuất phiếu lương thành công.",
                        downloadUrl = publicUrl,
                        fileName = fileName
                    });
                }
                catch (Exception ex)
                {
                    // Trả lỗi 500 nếu quá trình lưu file bị lỗi
                    return StatusCode(500, new { message = $"Lỗi khi lưu file: {ex.Message}" });
                }
            }
            else
            {
                // === CASE B: TRẢ VỀ STREAM (Cho Trình duyệt) ===
                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;
                return File(stream, "application/pdf", fileName);
            }
        }

        [HttpGet("general-report-pdf")]
        public async Task<IActionResult> ExportGeneralReport(int month, int year, [FromQuery] string returnType = "stream")
        {
            // ======================================================================================
            // PHẦN 1: CHUẨN BỊ DỮ LIỆU (DATA FETCHING)
            // ======================================================================================

            var startDate = new DateOnly(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // --- A. Nhân sự Tổng quan ---
            int totalActive = await _db.Employees.CountAsync(e => e.Status == EmployeeStatus.active);
            int newHiresCount = await _db.Employees.CountAsync(e => e.HireDate >= startDate && e.HireDate <= endDate);
            int resignedCount = await _db.Contracts.CountAsync(c => c.Status == ContractStatus.terminated && c.EndDate >= startDate && c.EndDate <= endDate);

            // [DATA MỚI 1] Danh sách 5 nhân viên mới nhất
            var newHiresList = await _db.Employees
                .AsNoTracking()
                .Include(e => e.Department).Include(e => e.Position)
                .Where(e => e.HireDate >= startDate && e.HireDate <= endDate)
                .OrderByDescending(e => e.HireDate)
                .Take(5)
                .ToListAsync();

            // --- B. Tài chính ---
            string periodName = $"{year}-{month:00}";
            var payroll = await _db.PayrollRuns
                .Include(p => p.Salaries)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Period == periodName && (p.Status == PayrollRunStatus.processed || p.Status == PayrollRunStatus.locked));

            decimal totalGross = payroll?.Salaries.Sum(s => s.Gross) ?? 0;
            decimal totalNet = payroll?.Salaries.Sum(s => s.Net) ?? 0;

            // --- C. Vận hành ---
            var attendanceStats = await _db.Attendances
                .AsNoTracking()
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int totalLate = attendanceStats.Where(x => x.Status == AttendanceStatus.late).Sum(x => x.Count);
            int totalAbsent = attendanceStats.Where(x => x.Status == AttendanceStatus.absent).Sum(x => x.Count);

            // Đào tạo
            var dtStartDate = startDate.ToDateTime(TimeOnly.MinValue);
            var dtEndDate = endDate.ToDateTime(TimeOnly.MaxValue);
            int trainingCount = await _db.TrainingRecords
                .AsNoTracking()
                .Where(t => t.Status == TrainingStatus.completed)
                .Where(t => _db.CourseResults.Where(cr => cr.EmployeeId == t.EmployeeId && cr.CourseId == t.CourseId).Max(cr => (DateTime?)cr.AnsweredAt) >= dtStartDate && _db.CourseResults.Where(cr => cr.EmployeeId == t.EmployeeId && cr.CourseId == t.CourseId).Max(cr => (DateTime?)cr.AnsweredAt) <= dtEndDate)
                .CountAsync();

            int penaltyCount = await _db.RewardPenalties.AsNoTracking().CountAsync(p => p.Type.Type == RewardPenaltyKind.penalty && p.DecidedAt >= startDate && p.DecidedAt <= endDate);

            // [DATA MỚI 2] Danh sách 5 hợp đồng sắp hết hạn (trong vòng 30 ngày tới tính từ cuối tháng báo cáo)
            var expiryCheckDate = endDate.AddDays(30);
            var expiringList = await _db.Contracts
                .AsNoTracking()
                .Include(c => c.Employee)
                .Where(c => c.EndDate != null && c.EndDate >= startDate && c.EndDate <= expiryCheckDate && c.Status != ContractStatus.terminated)
                .OrderBy(c => c.EndDate)
                .Take(5)
                .ToListAsync();


            // ======================================================================================
            // PHẦN 2: VẼ PDF
            // ======================================================================================

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor(Colors.Grey.Darken3));

                    // HEADER (Giữ nguyên)
                    page.Header().PaddingBottom(20).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CÔNG TY CÔNG NGHỆ IT").Bold().FontSize(14).FontColor(Colors.Blue.Medium);
                                c.Item().PaddingTop(10).Text("BÁO CÁO QUẢN TRỊ NHÂN SỰ").FontSize(24).SemiBold().FontColor(Colors.Grey.Darken3);
                            });
                            row.ConstantColumn(120).AlignRight().Column(c => {
                                c.Item().Text($"Tháng {month}/{year}").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                c.Item().Text($"Ngày xuất: {DateTime.Now:dd/MM}").FontSize(9).Italic();
                            });
                        });
                        col.Item().PaddingTop(10).BorderBottom(2).BorderColor(Colors.Blue.Medium);
                    });

                    // CONTENT
                    page.Content().Column(col =>
                    {
                        // Helper Styles
                        static void SectionTitle(IContainer container, string title) => container.PaddingBottom(5).PaddingTop(15).Text(title.ToUpper()).FontSize(12).Bold().FontColor(Colors.Blue.Medium);
                        static IContainer HeaderStyle(IContainer container) => container.Background(Colors.Grey.Lighten4).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).DefaultTextStyle(x => x.Bold().FontSize(9));
                        static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(8).DefaultTextStyle(x => x.FontSize(9));

                        // --- I. BIẾN ĐỘNG NHÂN SỰ ---
                        col.Item().Element(c => SectionTitle(c, "I. Biến động nhân sự"));
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c => DrawStatCard(c, "Tổng nhân sự", totalActive.ToString(), "Người", Colors.Blue.Lighten5, Colors.Blue.Darken3));
                            row.Spacing(15);
                            row.RelativeItem().Element(c => DrawStatCard(c, "Tuyển mới", $"+ {newHiresCount}", "Nhân viên", Colors.Green.Lighten5, Colors.Green.Darken3));
                            row.Spacing(15);
                            row.RelativeItem().Element(c => DrawStatCard(c, "Nghỉ việc", $"- {resignedCount}", "Nhân viên", Colors.Red.Lighten5, Colors.Red.Darken3));
                        });

                        // --- II. TÀI CHÍNH ---
                        col.Item().Element(c => SectionTitle(c, "II. Quỹ lương tháng"));
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c => DrawStatCard(c, "Tổng lương (Gross)", FormatMoney(totalGross), "", Colors.Orange.Lighten5, Colors.Orange.Darken3));
                            row.Spacing(15);
                            row.RelativeItem().Element(c => DrawStatCard(c, "Thực chi (Net)", FormatMoney(totalNet), "", Colors.Teal.Lighten5, Colors.Teal.Darken3));
                        });

                        // --- III. VẬN HÀNH & HIỆU SUẤT (Layout 2 cột: Trái là Bảng Chỉ số, Phải là Chi tiết tuyển dụng) ---
                        col.Item().PaddingTop(15).Row(row =>
                        {
                            // Cột Trái: Chỉ số vận hành
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().PaddingBottom(5).Text("III. CHỈ SỐ VẬN HÀNH").FontSize(12).Bold().FontColor(Colors.Blue.Medium);
                                c.Item().Border(1).BorderColor(Colors.Grey.Lighten3).CornerRadius(5).Table(table =>
                                {
                                    table.ColumnsDefinition(def => { def.RelativeColumn(); def.ConstantColumn(60); });

                                    // Rows
                                    DrawRowSimple(table, "Số lượt đi trễ", totalLate.ToString(), false);
                                    DrawRowSimple(table, "Vắng / Không phép", totalAbsent.ToString(), true);
                                    DrawRowSimple(table, "Đào tạo hoàn thành", trainingCount.ToString(), false);
                                    DrawRowSimple(table, "Kỷ luật / Vi phạm", penaltyCount.ToString(), true);
                                });
                            });

                            row.Spacing(20);

                            // Cột Phải: Danh sách nhân viên mới (Mới thêm)
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().PaddingBottom(5).Text("IV. NHÂN VIÊN MỚI").FontSize(12).Bold().FontColor(Colors.Blue.Medium);
                                if (newHiresList.Any())
                                {
                                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten3).CornerRadius(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(def => { def.RelativeColumn(); def.RelativeColumn(); });
                                        table.Header(h => {
                                            h.Cell().Element(HeaderStyle).Text("Họ tên");
                                            h.Cell().Element(HeaderStyle).Text("Vị trí");
                                        });
                                        foreach (var nh in newHiresList)
                                        {
                                            table.Cell().Element(CellStyle).Text(nh.FullName).SemiBold();
                                            table.Cell().Element(CellStyle).Text(nh.Position?.Name ?? "-");
                                        }
                                    });
                                }
                                else { c.Item().Text("(Không có nhân viên mới)").Italic().FontSize(10); }
                            });
                        });

                        // --- V. CẢNH BÁO HỢP ĐỒNG (Full width) ---
                        col.Item().Element(c => SectionTitle(c, "V. CẢNH BÁO: HỢP ĐỒNG SẮP HẾT HẠN (30 NGÀY TỚI)"));
                        if (expiringList.Any())
                        {
                            col.Item().Border(1).BorderColor(Colors.Grey.Lighten3).CornerRadius(5).Table(table =>
                            {
                                table.ColumnsDefinition(def => { def.ConstantColumn(30); def.RelativeColumn(); def.RelativeColumn(); def.ConstantColumn(100); });
                                table.Header(h => {
                                    h.Cell().Element(HeaderStyle).Text("#");
                                    h.Cell().Element(HeaderStyle).Text("Nhân viên");
                                    h.Cell().Element(HeaderStyle).Text("Số Hợp Đồng");
                                    h.Cell().Element(HeaderStyle).Text("Ngày hết hạn");
                                });
                                int i = 1;
                                foreach (var ex in expiringList)
                                {
                                    table.Cell().Element(CellStyle).Text($"{i++}");
                                    table.Cell().Element(CellStyle).Text(ex.Employee.FullName).SemiBold();
                                    table.Cell().Element(CellStyle).Text(ex.ContractNumber);
                                    table.Cell().Element(CellStyle).Text(ex.EndDate?.ToString("dd/MM/yyyy")).FontColor(Colors.Red.Medium).Bold();
                                }
                            });
                        }
                        else { col.Item().Text("Không có hợp đồng nào sắp hết hạn.").Italic(); }

                        // Local helper for simple row
                        void DrawRowSimple(TableDescriptor table, string label, string value, bool isNegative)
                        {
                            table.Cell().Element(CellStyle).Text(label);
                            var txt = table.Cell().Element(CellStyle).AlignRight().Text(value).Bold();
                            if (isNegative && value != "0") txt.FontColor(Colors.Red.Medium);
                        }
                    });

                    // FOOTER
                    page.Footer().PaddingTop(10).AlignCenter().Text(x =>
                    {
                        x.Span("Báo cáo được xuất tự động từ hệ thống HRM - Trang ");
                        x.CurrentPageNumber();
                    });
                });
            });

            // ... (Helper DrawStatCard, FormatMoney, Return logic giữ nguyên) ...
            static void DrawStatCard(IContainer container, string title, string value, string unit, string bgColor, string textColor)
            {
                container.Background(bgColor).CornerRadius(8).Padding(15).Column(c => {
                    c.Item().Row(r => {
                        r.ConstantColumn(4).Height(25).Background(textColor).CornerRadius(2);
                        r.RelativeItem().PaddingLeft(10).Column(col => {
                            col.Item().Text(title).FontSize(10).FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(2).Text(value).FontSize(20).ExtraBold().FontColor(textColor);
                        });
                    });
                    if (!string.IsNullOrEmpty(unit)) c.Item().AlignRight().Text(unit).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
            static string FormatMoney(decimal amount) => string.Format(new CultureInfo("vi-VN"), "{0:N0} đ", amount);

            string fileName = $"BaoCaoTongHop_T{month}_{year}_{DateTime.UtcNow:HHmmss}.pdf";
            if (returnType.ToLower() == "url")
            {
                // Logic URL
                try
                {
                    var reportsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports");
                    if (!Directory.Exists(reportsFolder)) Directory.CreateDirectory(reportsFolder);
                    var fullPath = Path.Combine(reportsFolder, fileName);
                    document.GeneratePdf(fullPath);
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    return Ok(new { success = true, downloadUrl = $"{baseUrl}/reports/{fileName}", fileName });
                }
                catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
            }
            else
            {
                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;
                return File(stream, "application/pdf", fileName);
            }
        }
    }
}