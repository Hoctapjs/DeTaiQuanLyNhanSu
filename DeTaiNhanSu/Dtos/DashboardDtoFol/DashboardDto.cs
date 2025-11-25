namespace DeTaiNhanSu.Dtos.DashboardDtoFol
{
        public record AttendanceTodayDto(int OnLeaveCount, int LateCount, int OnTimeCount, int CompletedCount, int AbsentCount, int CheckedInCount);
        public record LeaveStatsDto(int PendingApproval, int ApprovedThisMonth);
        public record DisciplineStatsDto(int PenaltiesThisMonth, int PenaltiesToday);
        public record CourseStatsDto(int Total, int NewThisMonth);
        public record SalaryStatsDto(string? LastFinalizedPeriod, decimal TotalGross, decimal TotalNet);
        public record PerformanceStatsDto(object AttendanceThisMonth, object TrainingAllTime);
        public record HiresQuitsChartDto(List<string> Labels, List<int> Hires, List<int> Quits);
        public record DeptChartDto(object DepartmentId, string DepartmentName, int Count);
        public record ExpiringContractDto(Guid Id, Guid EmployeeId, string EmployeeName, string ContractNumber, DateOnly? EndDate, string Status);
}
