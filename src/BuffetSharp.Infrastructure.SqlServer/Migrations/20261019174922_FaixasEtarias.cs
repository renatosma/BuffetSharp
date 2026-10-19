using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class FaixasEtarias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaIsento",
                table: "Pacotes",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaMeia",
                table: "Pacotes",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualMeia",
                table: "Pacotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "ConvidadosIsentos",
                table: "Orcamentos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConvidadosMeia",
                table: "Orcamentos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaIsento",
                table: "Orcamentos",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaMeia",
                table: "Orcamentos",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualMeia",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "ConvidadosIsentos",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConvidadosMeia",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaIsento",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaMeia",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualMeia",
                table: "Eventos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 50m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdadeMaximaIsento",
                table: "Pacotes");

            migrationBuilder.DropColumn(
                name: "IdadeMaximaMeia",
                table: "Pacotes");

            migrationBuilder.DropColumn(
                name: "PercentualMeia",
                table: "Pacotes");

            migrationBuilder.DropColumn(
                name: "ConvidadosIsentos",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ConvidadosMeia",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "IdadeMaximaIsento",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "IdadeMaximaMeia",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "PercentualMeia",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ConvidadosIsentos",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "ConvidadosMeia",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "IdadeMaximaIsento",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "IdadeMaximaMeia",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "PercentualMeia",
                table: "Eventos");
        }
    }
}
