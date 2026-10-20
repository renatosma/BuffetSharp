using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ConciliacaoBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "conciliadoem",
                table: "movimentosconta",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idextrato",
                table: "movimentosconta",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentosconta_contafinanceiraid_idextrato",
                table: "movimentosconta",
                columns: new[] { "contafinanceiraid", "idextrato" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_movimentosconta_contafinanceiraid_idextrato",
                table: "movimentosconta");

            migrationBuilder.DropColumn(
                name: "conciliadoem",
                table: "movimentosconta");

            migrationBuilder.DropColumn(
                name: "idextrato",
                table: "movimentosconta");
        }
    }
}
