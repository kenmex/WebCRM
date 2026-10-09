using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebCRM.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountAndContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Contacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoNotContact",
                table: "Contacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DoNotContactSince",
                table: "Contacts",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalutationId",
                table: "Contacts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Accounts",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true,
                collation: "Greek_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "Accounts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Greek_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "TaxOffice",
                table: "Accounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Salutations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SystemCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salutations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_SalutationId",
                table: "Contacts",
                column: "SalutationId");

            migrationBuilder.CreateIndex(
                name: "IX_Salutations_Name",
                table: "Salutations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Salutations_SystemCode",
                table: "Salutations",
                column: "SystemCode",
                unique: true,
                filter: "[SystemCode] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_Salutations_SalutationId",
                table: "Contacts",
                column: "SalutationId",
                principalTable: "Salutations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_Salutations_SalutationId",
                table: "Contacts");

            migrationBuilder.DropTable(
                name: "Salutations");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_SalutationId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "DoNotContact",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "DoNotContactSince",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "SalutationId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TaxOffice",
                table: "Accounts");
        }
    }
}
