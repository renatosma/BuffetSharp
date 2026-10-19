using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FaixasEtarias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "idademaximaisento",
                table: "pacotes",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "idademaximameia",
                table: "pacotes",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "percentualmeia",
                table: "pacotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "convidadosisentos",
                table: "orcamentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "convidadosmeia",
                table: "orcamentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "idademaximaisento",
                table: "orcamentos",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "idademaximameia",
                table: "orcamentos",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "percentualmeia",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "convidadosisentos",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "convidadosmeia",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "idademaximaisento",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "idademaximameia",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "percentualmeia",
                table: "eventos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "idademaximaisento",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "idademaximameia",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "percentualmeia",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "convidadosisentos",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "convidadosmeia",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "idademaximaisento",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "idademaximameia",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "percentualmeia",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "convidadosisentos",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "convidadosmeia",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "idademaximaisento",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "idademaximameia",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "percentualmeia",
                table: "eventos");
        }
    }
}
