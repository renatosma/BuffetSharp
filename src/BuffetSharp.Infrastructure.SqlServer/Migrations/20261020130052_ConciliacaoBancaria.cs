using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ConciliacaoBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConciliadoEm",
                table: "MovimentosConta",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdExtrato",
                table: "MovimentosConta",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosConta_ContaFinanceiraId_IdExtrato",
                table: "MovimentosConta",
                columns: new[] { "ContaFinanceiraId", "IdExtrato" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimentosConta_ContaFinanceiraId_IdExtrato",
                table: "MovimentosConta");

            migrationBuilder.DropColumn(
                name: "ConciliadoEm",
                table: "MovimentosConta");

            migrationBuilder.DropColumn(
                name: "IdExtrato",
                table: "MovimentosConta");
        }
    }
}
