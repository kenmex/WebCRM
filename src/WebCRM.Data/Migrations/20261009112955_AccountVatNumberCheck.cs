using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebCRM.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountVatNumberCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_VatNumber",
                table: "Accounts",
                sql: "[VatNumber] IS NULL OR (LEN([VatNumber]) BETWEEN 4 AND 20 AND [VatNumber] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z0-9]%' AND ([VatNumber] COLLATE Latin1_General_100_BIN2 NOT LIKE 'EL%' OR [VatNumber] COLLATE Latin1_General_100_BIN2 LIKE 'EL[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_VatNumber",
                table: "Accounts");
        }
    }
}
