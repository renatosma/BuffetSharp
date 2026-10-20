using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ConcorrenciaBloqueioLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BloqueadoAte",
                table: "Usuarios",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativasFalhas",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "UnidadesMedida",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "TiposEvento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ServicosAdicionais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Produtos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ProdutoEmbalagens",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Parcelas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ParcelamentosPagar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Pacotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Orcamentos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "MovimentosConta",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "MovimentacoesEstoque",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ModelosContrato",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Materiais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ItensCardapio",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Funcionarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Fornecedores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "FormasPagamento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "FechamentosConta",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "FechamentosComissao",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Eventos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Espacos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "EnderecosAdicionais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Degustacoes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Contratos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ContatosAdicionais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ContasPagar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "ContasFinanceiras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "CondicoesPagamento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "CategoriasProduto",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "CategoriasCardapio",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "CartoesCredito",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Cargos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Baixas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BloqueadoAte",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TentativasFalhas",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "UnidadesMedida");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "TiposEvento");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ServicosAdicionais");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ProdutoEmbalagens");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Parcelas");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ParcelamentosPagar");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Pacotes");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "MovimentosConta");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ModelosContrato");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Materiais");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ItensCardapio");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Fornecedores");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "FormasPagamento");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "FechamentosConta");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "FechamentosComissao");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "EnderecosAdicionais");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Degustacoes");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ContatosAdicionais");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "ContasFinanceiras");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "CondicoesPagamento");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "CategoriasProduto");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "CategoriasCardapio");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "CartoesCredito");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Cargos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Baixas");
        }
    }
}
