using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ConcorrenciaBloqueioLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "bloqueadoate",
                table: "usuarios",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tentativasfalhas",
                table: "usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "unidadesmedida",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "tiposevento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "servicosadicionais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "produtoembalagens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "parcelas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "parcelamentospagar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "pacotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "orcamentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "movimentosconta",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "movimentacoesestoque",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "modeloscontrato",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "materiais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "itenscardapio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "funcionarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "fornecedores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "formaspagamento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "fechamentosconta",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "fechamentoscomissao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "eventos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "espacos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "enderecosadicionais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "degustacoes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "contratos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "contatosadicionais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "contaspagar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "contasfinanceiras",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "condicoespagamento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "clientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "categoriasproduto",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "categoriascardapio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "cartoescredito",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "cargos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "versao",
                table: "baixas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bloqueadoate",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "tentativasfalhas",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "unidadesmedida");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "tiposevento");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "servicosadicionais");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "produtoembalagens");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "parcelas");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "parcelamentospagar");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "pacotes");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "movimentosconta");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "movimentacoesestoque");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "modeloscontrato");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "materiais");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "itenscardapio");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "fornecedores");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "formaspagamento");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "fechamentosconta");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "fechamentoscomissao");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "espacos");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "enderecosadicionais");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "degustacoes");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "contratos");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "contatosadicionais");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "contaspagar");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "contasfinanceiras");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "condicoespagamento");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "categoriasproduto");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "categoriascardapio");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "cartoescredito");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "cargos");

            migrationBuilder.DropColumn(
                name: "versao",
                table: "baixas");
        }
    }
}
