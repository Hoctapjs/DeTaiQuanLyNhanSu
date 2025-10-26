IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Provider] nvarchar(max) NULL,
        [Hours] int NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [PayrollRuns] (
        [Id] uniqueidentifier NOT NULL,
        [Period] nvarchar(7) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Positions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Level] nvarchar(max) NULL,
        CONSTRAINT [PK_Positions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [RewardPenaltyTypes] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [DefaultAmount] decimal(18,2) NULL,
        [Level] nvarchar(max) NOT NULL,
        [Form] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_RewardPenaltyTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Attendances] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [CheckIn] time NULL,
        [CheckOut] time NULL,
        [Status] nvarchar(max) NOT NULL,
        [Note] nvarchar(max) NULL,
        CONSTRAINT [PK_Attendances] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [TableName] nvarchar(max) NULL,
        [RecordId] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [IPAddress] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Contracts] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [ContractNumber] nvarchar(100) NOT NULL,
        [Title] nvarchar(max) NULL,
        [Type] nvarchar(max) NOT NULL,
        [SignedDate] date NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [WorkType] nvarchar(max) NOT NULL,
        [BasicSalary] decimal(18,2) NOT NULL,
        [InsuranceSalary] decimal(18,2) NULL,
        [RepresentativeId] uniqueidentifier NULL,
        [Status] nvarchar(max) NOT NULL,
        [AttachmentUrl] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ManagerId] uniqueidentifier NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [Gender] int NULL,
        [Dob] date NULL,
        [Cccd] nvarchar(450) NULL,
        [Email] nvarchar(100) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Address] nvarchar(max) NULL,
        [HireDate] date NOT NULL,
        [DepartmentId] uniqueidentifier NULL,
        [PositionId] uniqueidentifier NULL,
        [Status] nvarchar(max) NOT NULL,
        [AvatarUrl] nvarchar(max) NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Positions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [Positions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [InsuranceProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Bhxh] decimal(18,2) NULL,
        [Bhyt] decimal(18,2) NULL,
        [Bhtn] decimal(18,2) NULL,
        CONSTRAINT [PK_InsuranceProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InsuranceProfiles_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Overtimes] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Hours] decimal(5,2) NOT NULL,
        [Rate] decimal(5,2) NOT NULL,
        [Reason] nvarchar(max) NULL,
        CONSTRAINT [PK_Overtimes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Overtimes_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Salaries] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [PayrollRunId] uniqueidentifier NOT NULL,
        [Gross] decimal(18,2) NOT NULL,
        [Net] decimal(18,2) NOT NULL,
        [Details] nvarchar(max) NULL,
        CONSTRAINT [PK_Salaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Salaries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Salaries_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [UserName] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [LastLoginAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Shift] nvarchar(max) NULL,
        [StartTime] time NULL,
        [EndTime] time NULL,
        [Note] nvarchar(max) NULL,
        CONSTRAINT [PK_WorkSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkSchedules_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [SalaryItems] (
        [Id] uniqueidentifier NOT NULL,
        [SalaryId] uniqueidentifier NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Note] nvarchar(max) NULL,
        CONSTRAINT [PK_SalaryItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryItems_Salaries_SalaryId] FOREIGN KEY ([SalaryId]) REFERENCES [Salaries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ReadAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Policies] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Content] nvarchar(max) NULL,
        [AttachmentUrl] nvarchar(max) NULL,
        [EffectiveDate] date NULL,
        [ExpiredDate] date NULL,
        [CreatedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Version] nvarchar(max) NULL,
        CONSTRAINT [PK_Policies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Policies_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [Requests] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(max) NULL,
        [FromDate] date NULL,
        [ToDate] date NULL,
        [AttachmentUrl] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Requests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Requests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Requests_Users_ApprovedBy] FOREIGN KEY ([ApprovedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [RewardPenalties] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [TypeId] uniqueidentifier NOT NULL,
        [AmountOverride] decimal(18,2) NULL,
        [CustomReason] nvarchar(max) NULL,
        [DecidedAt] date NOT NULL,
        [DecidedBy] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RewardPenalties] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RewardPenalties_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RewardPenalties_RewardPenaltyTypes_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [RewardPenaltyTypes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RewardPenalties_Users_DecidedBy] FOREIGN KEY ([DecidedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE TABLE [TrainingRecords] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [StartDate] date NULL,
        [EndDate] datetime2 NULL,
        [Score] decimal(18,2) NULL,
        [Status] nvarchar(max) NOT NULL,
        [EvaluatedBy] uniqueidentifier NULL,
        [EvaluationNote] nvarchar(max) NULL,
        CONSTRAINT [PK_TrainingRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainingRecords_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TrainingRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TrainingRecords_Users_EvaluatedBy] FOREIGN KEY ([EvaluatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Attendances_EmployeeId_Date] ON [Attendances] ([EmployeeId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Contracts_ContractNumber] ON [Contracts] ([ContractNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contracts_EmployeeId] ON [Contracts] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contracts_RepresentativeId] ON [Contracts] ([RepresentativeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Departments_ManagerId] ON [Departments] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Employees_Cccd] ON [Employees] ([Cccd]) WHERE [Cccd] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_Code] ON [Employees] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_Email] ON [Employees] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_PositionId] ON [Employees] ([PositionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InsuranceProfiles_EmployeeId] ON [InsuranceProfiles] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Overtimes_EmployeeId] ON [Overtimes] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollRuns_Period] ON [PayrollRuns] ([Period]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Policies_CreatedBy] ON [Policies] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Requests_ApprovedBy] ON [Requests] ([ApprovedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Requests_EmployeeId] ON [Requests] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RewardPenalties_DecidedBy] ON [RewardPenalties] ([DecidedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RewardPenalties_EmployeeId] ON [RewardPenalties] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RewardPenalties_TypeId] ON [RewardPenalties] ([TypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Salaries_EmployeeId_PayrollRunId] ON [Salaries] ([EmployeeId], [PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Salaries_PayrollRunId] ON [Salaries] ([PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SalaryItems_SalaryId] ON [SalaryItems] ([SalaryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TrainingRecords_CourseId] ON [TrainingRecords] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TrainingRecords_EmployeeId_CourseId] ON [TrainingRecords] ([EmployeeId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TrainingRecords_EvaluatedBy] ON [TrainingRecords] ([EvaluatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_EmployeeId] ON [Users] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkSchedules_Employee_Date] ON [WorkSchedules] ([EmployeeId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    ALTER TABLE [Attendances] ADD CONSTRAINT [FK_Attendances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    ALTER TABLE [Contracts] ADD CONSTRAINT [FK_Contracts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    ALTER TABLE [Contracts] ADD CONSTRAINT [FK_Contracts_Users_RepresentativeId] FOREIGN KEY ([RepresentativeId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Employees_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929160910_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250929160910_InitialCreate', N'9.0.9');
END;

COMMIT;
GO

