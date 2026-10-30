using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AlcadaDesconto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "celular",
                table: "usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "descontomaximo",
                table: "usuarios",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "usuarios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "descontoconcedidoem",
                table: "orcamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descontoconcedidopor",
                table: "orcamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "descontodecididoem",
                table: "orcamentos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descontodecididopor",
                table: "orcamentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivodesconto",
                table: "orcamentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentualdescontoautorizado",
                table: "orcamentos",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "respostadesconto",
                table: "orcamentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "situacaodesconto",
                table: "orcamentos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DentroDaAlcada");

            migrationBuilder.AddColumn<decimal>(
                name: "valortabela",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "descontoaprovadopor",
                table: "baixas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivodesconto",
                table: "baixas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_situacaodesconto",
                table: "orcamentos",
                column: "situacaodesconto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orcamentos_situacaodesconto",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "celular",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "descontomaximo",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "email",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "descontoconcedidoem",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "descontoconcedidopor",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "descontodecididoem",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "descontodecididopor",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "motivodesconto",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "percentualdescontoautorizado",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "respostadesconto",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "situacaodesconto",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "valortabela",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "descontoaprovadopor",
                table: "baixas");

            migrationBuilder.DropColumn(
                name: "motivodesconto",
                table: "baixas");
        }
    }
}
