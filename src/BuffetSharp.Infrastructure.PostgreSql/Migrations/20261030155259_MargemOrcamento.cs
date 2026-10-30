using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MargemOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "custoequipefixo",
                table: "tiposevento",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custoequipeporpessoa",
                table: "tiposevento",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custo",
                table: "servicosadicionais",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custocomissao",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custoequipe",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custoespaco",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custoimpostos",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custoinsumos",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custoservicos",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "custotaxa",
                table: "orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "margemaprovada",
                table: "orcamentos",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "custoporevento",
                table: "espacos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custoequipefixo",
                table: "tiposevento");

            migrationBuilder.DropColumn(
                name: "custoequipeporpessoa",
                table: "tiposevento");

            migrationBuilder.DropColumn(
                name: "custo",
                table: "servicosadicionais");

            migrationBuilder.DropColumn(
                name: "custocomissao",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custoequipe",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custoespaco",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custoimpostos",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custoinsumos",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custoservicos",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custotaxa",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "margemaprovada",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "custoporevento",
                table: "espacos");
        }
    }
}
