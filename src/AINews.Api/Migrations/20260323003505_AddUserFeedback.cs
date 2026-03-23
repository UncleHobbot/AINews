using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AINews.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackAt",
                table: "NewsItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserFeedback",
                table: "NewsItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackAt",
                table: "NewsItems");

            migrationBuilder.DropColumn(
                name: "UserFeedback",
                table: "NewsItems");
        }
    }
}
