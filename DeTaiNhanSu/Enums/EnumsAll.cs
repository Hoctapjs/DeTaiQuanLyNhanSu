namespace DeTaiNhanSu.Enums;

public enum Gender { female, male, other }
public enum EmployeeStatus { active, inactive }
public enum UserStatus { active, locked, pending_invite }
public enum AttendanceStatus { present, absent, late, leave, ot }
public enum RequestCategory { leave, resignation, business_trip, incident, proposal, other }
public enum RequestStatus { pending, approved, rejected, cancelled }
public enum ContractType { FT, PT, Intern, Probation, FixedTerm, Seasonal }
public enum WorkType { fulltime, parttime, remote, hybrid }
public enum ContractStatus { active, expired, terminated, draft }
public enum RewardPenaltyKind { reward, penalty }
public enum SeverityLevel { low, medium, high }
public enum ActionForm { verbal_warning, written_warning, fine, suspension, bonus, promotion }
public enum TrainingStatus { in_progress, completed, not_completed, failed }
public enum PayrollRunStatus { draft, processed, locked }
public enum SalaryItemType { basic, allowance, bonus, deduction, insurance, tax, ot }
