using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MargemOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CustoEquipeFixo",
                table: "TiposEvento",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoEquipePorPessoa",
                table: "TiposEvento",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Custo",
                table: "ServicosAdicionais",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoComissao",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoEquipe",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoEspaco",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoImpostos",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoInsumos",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoServicos",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoTaxa",
                table: "Orcamentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MargemAprovada",
                table: "Orcamentos",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoPorEvento",
                table: "Espacos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustoEquipeFixo",
                table: "TiposEvento");

            migrationBuilder.DropColumn(
                name: "CustoEquipePorPessoa",
                table: "TiposEvento");

            migrationBuilder.DropColumn(
                name: "Custo",
                table: "ServicosAdicionais");

            migrationBuilder.DropColumn(
                name: "CustoComissao",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoEquipe",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoEspaco",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoImpostos",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoInsumos",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoServicos",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoTaxa",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "MargemAprovada",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoPorEvento",
                table: "Espacos");
        }
    }
}
