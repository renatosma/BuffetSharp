using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ConcorrenciaBloqueioLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BLOQUEADOATE",
                table: "USUARIOS",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TENTATIVASFALHAS",
                table: "USUARIOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "USUARIOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "UNIDADESMEDIDA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "TIPOSEVENTO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "SERVICOSADICIONAIS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "PRODUTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "PRODUTOEMBALAGENS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "PARCELAS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "PARCELAMENTOSPAGAR",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "PACOTES",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "ORCAMENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "MOVIMENTOSCONTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "MOVIMENTACOESESTOQUE",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "MODELOSCONTRATO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "MATERIAIS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "ITENSCARDAPIO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "FUNCIONARIOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "FORNECEDORES",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "FORMASPAGAMENTO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "FECHAMENTOSCONTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "FECHAMENTOSCOMISSAO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "EVENTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "ESPACOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "ENDERECOSADICIONAIS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "DEGUSTACOES",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CONTRATOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CONTATOSADICIONAIS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CONTASPAGAR",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CONTASFINANCEIRAS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CONDICOESPAGAMENTO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CLIENTES",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CATEGORIASPRODUTO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CATEGORIASCARDAPIO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CARTOESCREDITO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "CARGOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSAO",
                table: "BAIXAS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BLOQUEADOATE",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "TENTATIVASFALHAS",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "USUARIOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "UNIDADESMEDIDA");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "TIPOSEVENTO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "SERVICOSADICIONAIS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "PRODUTOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "PRODUTOEMBALAGENS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "PARCELAS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "PARCELAMENTOSPAGAR");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "PACOTES");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "ORCAMENTOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "MOVIMENTOSCONTA");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "MOVIMENTACOESESTOQUE");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "MODELOSCONTRATO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "MATERIAIS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "ITENSCARDAPIO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "FUNCIONARIOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "FORNECEDORES");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "FORMASPAGAMENTO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "FECHAMENTOSCONTA");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "FECHAMENTOSCOMISSAO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "EVENTOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "ESPACOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "ENDERECOSADICIONAIS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "DEGUSTACOES");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CONTRATOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CONTATOSADICIONAIS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CONTASPAGAR");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CONTASFINANCEIRAS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CONDICOESPAGAMENTO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CLIENTES");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CATEGORIASPRODUTO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CATEGORIASCARDAPIO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CARTOESCREDITO");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "CARGOS");

            migrationBuilder.DropColumn(
                name: "VERSAO",
                table: "BAIXAS");
        }
    }
}
