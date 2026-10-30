using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class MargemOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOEQUIPEFIXO",
                table: "TIPOSEVENTO",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOEQUIPEPORPESSOA",
                table: "TIPOSEVENTO",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTO",
                table: "SERVICOSADICIONAIS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOCOMISSAO",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOEQUIPE",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOESPACO",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOIMPOSTOS",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOINSUMOS",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOSERVICOS",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOTAXA",
                table: "ORCAMENTOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MARGEMAPROVADA",
                table: "ORCAMENTOS",
                type: "DECIMAL(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTOPOREVENTO",
                table: "ESPACOS",
                type: "DECIMAL(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CUSTOEQUIPEFIXO",
                table: "TIPOSEVENTO");

            migrationBuilder.DropColumn(
                name: "CUSTOEQUIPEPORPESSOA",
                table: "TIPOSEVENTO");

            migrationBuilder.DropColumn(
                name: "CUSTO",
                table: "SERVICOSADICIONAIS");

            migrationBuilder.DropColumn(
                name: "CUSTOCOMISSAO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOEQUIPE",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOESPACO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOIMPOSTOS",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOINSUMOS",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOSERVICOS",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOTAXA",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "MARGEMAPROVADA",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "CUSTOPOREVENTO",
                table: "ESPACOS");
        }
    }
}
