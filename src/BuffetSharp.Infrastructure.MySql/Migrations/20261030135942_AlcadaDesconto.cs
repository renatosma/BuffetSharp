using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AlcadaDesconto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Celular",
                table: "Usuarios",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescontoMaximo",
                table: "Usuarios",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DescontoConcedidoEm",
                table: "Orcamentos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescontoConcedidoPor",
                table: "Orcamentos",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DescontoDecididoEm",
                table: "Orcamentos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescontoDecididoPor",
                table: "Orcamentos",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDesconto",
                table: "Orcamentos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualDescontoAutorizado",
                table: "Orcamentos",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RespostaDesconto",
                table: "Orcamentos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SituacaoDesconto",
                table: "Orcamentos",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DentroDaAlcada");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorTabela",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DescontoAprovadoPor",
                table: "Baixas",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDesconto",
                table: "Baixas",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_SituacaoDesconto",
                table: "Orcamentos",
                column: "SituacaoDesconto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orcamentos_SituacaoDesconto",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "Celular",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DescontoMaximo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DescontoConcedidoEm",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "DescontoConcedidoPor",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "DescontoDecididoEm",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "DescontoDecididoPor",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "MotivoDesconto",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "PercentualDescontoAutorizado",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "RespostaDesconto",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "SituacaoDesconto",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ValorTabela",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "DescontoAprovadoPor",
                table: "Baixas");

            migrationBuilder.DropColumn(
                name: "MotivoDesconto",
                table: "Baixas");
        }
    }
}
