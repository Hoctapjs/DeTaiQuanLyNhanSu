using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeTaiNhanSu.Migrations
{
    /// <inheritdoc />
    public partial class AddDocSeqAndUniqueContractNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Tạo SEQUENCE (chỉ tạo nếu chưa có)
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = 'DocSeq' AND SCHEMA_NAME(schema_id) = 'dbo')
    EXEC('CREATE SEQUENCE dbo.DocSeq AS int START WITH 1 INCREMENT BY 1;')
");

            // 2) (Nếu chưa có) Unique index cho ContractNumber
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes i
    JOIN sys.objects o ON i.object_id = o.object_id
    WHERE i.name = 'UX_Contracts_ContractNumber' AND o.name = 'Contracts'
)
    CREATE UNIQUE INDEX UX_Contracts_ContractNumber ON dbo.Contracts(ContractNumber);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xoá SEQUENCE khi rollback (tuỳ chọn)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.sequences WHERE name = 'DocSeq' AND SCHEMA_NAME(schema_id) = 'dbo')
    DROP SEQUENCE dbo.DocSeq;
");

            // Xoá index nếu đã tạo ở Up bằng SQL (tuỳ chọn)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Contracts_ContractNumber')
    DROP INDEX UX_Contracts_ContractNumber ON dbo.Contracts;
");
        }
    }
}
