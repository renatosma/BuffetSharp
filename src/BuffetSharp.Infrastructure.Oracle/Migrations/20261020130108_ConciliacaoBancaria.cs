using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ConciliacaoBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CONCILIADOEM",
                table: "MOVIMENTOSCONTA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IDEXTRATO",
                table: "MOVIMENTOSCONTA",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTOSCONTA_CONTAFINANCEIRAID_IDEXTRATO",
                table: "MOVIMENTOSCONTA",
                columns: new[] { "CONTAFINANCEIRAID", "IDEXTRATO" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MOVIMENTOSCONTA_CONTAFINANCEIRAID_IDEXTRATO",
                table: "MOVIMENTOSCONTA");

            migrationBuilder.DropColumn(
                name: "CONCILIADOEM",
                table: "MOVIMENTOSCONTA");

            migrationBuilder.DropColumn(
                name: "IDEXTRATO",
                table: "MOVIMENTOSCONTA");
        }
    }
}
