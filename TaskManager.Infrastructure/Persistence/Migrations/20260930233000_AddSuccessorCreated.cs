using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuccessorCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SuccessorCreated",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuccessorCreated",
                table: "Tasks");
        }
    }
}
