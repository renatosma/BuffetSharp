using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AlcadaDesconto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CELULAR",
                table: "USUARIOS",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DESCONTOMAXIMO",
                table: "USUARIOS",
                type: "DECIMAL(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EMAIL",
                table: "USUARIOS",
                type: "NVARCHAR2(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DESCONTOCONCEDIDOEM",
                table: "ORCAMENTOS",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DESCONTOCONCEDIDOPOR",
                table: "ORCAMENTOS",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DESCONTODECIDIDOEM",
                table: "ORCAMENTOS",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DESCONTODECIDIDOPOR",
                table: "ORCAMENTOS",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOTIVODESCONTO",
                table: "ORCAMENTOS",
                type: "NVARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENTUALDESCONTOAUTORIZADO",
                table: "ORCAMENTOS",
                type: "DECIMAL(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RESPOSTADESCONTO",
                table: "ORCAMENTOS",
                type: "NVARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SITUACAODESCONTO",
                table: "ORCAMENTOS",
                type: "NVARCHAR2(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DentroDaAlcada");

            migrationBuilder.AddColumn<decimal>(
                name: "VALORTABELA",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DESCONTOAPROVADOPOR",
                table: "BAIXAS",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOTIVODESCONTO",
                table: "BAIXAS",
                type: "NVARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORCAMENTOS_SITUACAODESCONTO",
                table: "ORCAMENTOS",
                column: "SITUACAODESCONTO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ORCAMENTOS_SITUACAODESCONTO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CELULAR",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "DESCONTOMAXIMO",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "EMAIL",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "DESCONTOCONCEDIDOEM",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "DESCONTOCONCEDIDOPOR",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "DESCONTODECIDIDOEM",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "DESCONTODECIDIDOPOR",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "MOTIVODESCONTO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "PERCENTUALDESCONTOAUTORIZADO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "RESPOSTADESCONTO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "SITUACAODESCONTO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "VALORTABELA",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "DESCONTOAPROVADOPOR",
                table: "BAIXAS");

            migrationBuilder.DropColumn(
                name: "MOTIVODESCONTO",
                table: "BAIXAS");
        }
    }
}
