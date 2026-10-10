using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebCRM.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeadConvertedConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Leads_ConvertedConsistent",
                table: "Leads",
                sql: "([ConvertedAt] IS NULL AND [ConvertedAccountId] IS NULL AND [ConvertedContactId] IS NULL AND [ConvertedOpportunityId] IS NULL) OR ([ConvertedAt] IS NOT NULL AND [ConvertedAccountId] IS NOT NULL AND [ConvertedContactId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Leads_ConvertedConsistent",
                table: "Leads");
        }
    }
}
