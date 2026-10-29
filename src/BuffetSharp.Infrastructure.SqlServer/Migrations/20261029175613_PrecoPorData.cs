using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PrecoPorData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AjusteDescricao",
                table: "Orcamentos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AjusteLocacaoPercentual",
                table: "Orcamentos",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AjustePessoaPercentual",
                table: "Orcamentos",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MinimoConvidadosData",
                table: "Orcamentos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegrasPreco",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DiasSemana = table.Column<int>(type: "int", nullable: false),
                    DiaInicio = table.Column<int>(type: "int", nullable: true),
                    MesInicio = table.Column<int>(type: "int", nullable: true),
                    DiaFim = table.Column<int>(type: "int", nullable: true),
                    MesFim = table.Column<int>(type: "int", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: true),
                    Anual = table.Column<bool>(type: "bit", nullable: false),
                    Exclusiva = table.Column<bool>(type: "bit", nullable: false),
                    Percentual = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    AjustaPessoa = table.Column<bool>(type: "bit", nullable: false),
                    AjustaLocacao = table.Column<bool>(type: "bit", nullable: false),
                    PacoteId = table.Column<int>(type: "int", nullable: true),
                    EspacoId = table.Column<int>(type: "int", nullable: true),
                    MinimoConvidados = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasPreco", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegrasPreco_Espacos_EspacoId",
                        column: x => x.EspacoId,
                        principalTable: "Espacos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegrasPreco_Pacotes_PacoteId",
                        column: x => x.PacoteId,
                        principalTable: "Pacotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegrasPreco_EspacoId",
                table: "RegrasPreco",
                column: "EspacoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasPreco_PacoteId",
                table: "RegrasPreco",
                column: "PacoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegrasPreco");

            migrationBuilder.DropColumn(
                name: "AjusteDescricao",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "AjusteLocacaoPercentual",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "AjustePessoaPercentual",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "MinimoConvidadosData",
                table: "Orcamentos");
        }
    }
}
