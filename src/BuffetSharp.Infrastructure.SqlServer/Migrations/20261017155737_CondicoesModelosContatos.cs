using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class CondicoesModelosContatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CondicaoPagamentoId",
                table: "Orcamentos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoUnitarioCompra",
                table: "MovimentacoesEstoque",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatorConversao",
                table: "MovimentacoesEstoque",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FornecedorId",
                table: "MovimentacoesEstoque",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantidadeCompra",
                table: "MovimentacoesEstoque",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadeCompra",
                table: "MovimentacoesEstoque",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Bairro",
                table: "Funcionarios",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Cep",
                table: "Funcionarios",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Cidade",
                table: "Funcionarios",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Complemento",
                table: "Funcionarios",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Logradouro",
                table: "Funcionarios",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Numero",
                table: "Funcionarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Uf",
                table: "Funcionarios",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CondicaoPagamentoId",
                table: "Eventos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Bairro",
                table: "Espacos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Cep",
                table: "Espacos",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Cidade",
                table: "Espacos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Complemento",
                table: "Espacos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Logradouro",
                table: "Espacos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Numero",
                table: "Espacos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco_Uf",
                table: "Espacos",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModeloContratoId",
                table: "Contratos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CondicoesPagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PercentualEntrada = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DiasEntrada = table.Column<int>(type: "int", nullable: false),
                    QuantidadeParcelas = table.Column<int>(type: "int", nullable: false),
                    DiasPrimeiraParcela = table.Column<int>(type: "int", nullable: false),
                    Mensal = table.Column<bool>(type: "bit", nullable: false),
                    IntervaloDias = table.Column<int>(type: "int", nullable: false),
                    QuitarDiasAntesEvento = table.Column<int>(type: "int", nullable: true),
                    FormaPagamentoId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicoesPagamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondicoesPagamento_FormasPagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "FormasPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContatosAdicionais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    FuncionarioId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContatosAdicionais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContatosAdicionais_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContatosAdicionais_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContatosAdicionais_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnderecosAdicionais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identificacao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    FuncionarioId = table.Column<int>(type: "int", nullable: true),
                    Endereco_Bairro = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Endereco_Cep = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Endereco_Cidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Endereco_Complemento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Endereco_Logradouro = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Endereco_Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Endereco_Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnderecosAdicionais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnderecosAdicionais_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnderecosAdicionais_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnderecosAdicionais_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelosContrato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoEventoId = table.Column<int>(type: "int", nullable: true),
                    Padrao = table.Column<bool>(type: "bit", nullable: false),
                    Conteudo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosContrato", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelosContrato_TiposEvento_TipoEventoId",
                        column: x => x.TipoEventoId,
                        principalTable: "TiposEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProdutoEmbalagens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    UnidadeMedidaId = table.Column<int>(type: "int", nullable: false),
                    Fator = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CodigoFornecedor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutoEmbalagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutoEmbalagens_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProdutoEmbalagens_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProdutoEmbalagens_UnidadesMedida_UnidadeMedidaId",
                        column: x => x.UnidadeMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_CondicaoPagamentoId",
                table: "Orcamentos",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_FornecedorId",
                table: "MovimentacoesEstoque",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_CondicaoPagamentoId",
                table: "Eventos",
                column: "CondicaoPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_ModeloContratoId",
                table: "Contratos",
                column: "ModeloContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_CondicoesPagamento_FormaPagamentoId",
                table: "CondicoesPagamento",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_CondicoesPagamento_Nome",
                table: "CondicoesPagamento",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContatosAdicionais_ClienteId",
                table: "ContatosAdicionais",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ContatosAdicionais_FornecedorId",
                table: "ContatosAdicionais",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContatosAdicionais_FuncionarioId",
                table: "ContatosAdicionais",
                column: "FuncionarioId");

            migrationBuilder.CreateIndex(
                name: "IX_EnderecosAdicionais_ClienteId",
                table: "EnderecosAdicionais",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_EnderecosAdicionais_FornecedorId",
                table: "EnderecosAdicionais",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_EnderecosAdicionais_FuncionarioId",
                table: "EnderecosAdicionais",
                column: "FuncionarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelosContrato_Nome",
                table: "ModelosContrato",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelosContrato_TipoEventoId",
                table: "ModelosContrato",
                column: "TipoEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutoEmbalagens_FornecedorId",
                table: "ProdutoEmbalagens",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutoEmbalagens_ProdutoId",
                table: "ProdutoEmbalagens",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutoEmbalagens_UnidadeMedidaId",
                table: "ProdutoEmbalagens",
                column: "UnidadeMedidaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contratos_ModelosContrato_ModeloContratoId",
                table: "Contratos",
                column: "ModeloContratoId",
                principalTable: "ModelosContrato",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Eventos_CondicoesPagamento_CondicaoPagamentoId",
                table: "Eventos",
                column: "CondicaoPagamentoId",
                principalTable: "CondicoesPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesEstoque_Fornecedores_FornecedorId",
                table: "MovimentacoesEstoque",
                column: "FornecedorId",
                principalTable: "Fornecedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orcamentos_CondicoesPagamento_CondicaoPagamentoId",
                table: "Orcamentos",
                column: "CondicaoPagamentoId",
                principalTable: "CondicoesPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contratos_ModelosContrato_ModeloContratoId",
                table: "Contratos");

            migrationBuilder.DropForeignKey(
                name: "FK_Eventos_CondicoesPagamento_CondicaoPagamentoId",
                table: "Eventos");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesEstoque_Fornecedores_FornecedorId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropForeignKey(
                name: "FK_Orcamentos_CondicoesPagamento_CondicaoPagamentoId",
                table: "Orcamentos");

            migrationBuilder.DropTable(
                name: "CondicoesPagamento");

            migrationBuilder.DropTable(
                name: "ContatosAdicionais");

            migrationBuilder.DropTable(
                name: "EnderecosAdicionais");

            migrationBuilder.DropTable(
                name: "ModelosContrato");

            migrationBuilder.DropTable(
                name: "ProdutoEmbalagens");

            migrationBuilder.DropIndex(
                name: "IX_Orcamentos_CondicaoPagamentoId",
                table: "Orcamentos");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesEstoque_FornecedorId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropIndex(
                name: "IX_Eventos_CondicaoPagamentoId",
                table: "Eventos");

            migrationBuilder.DropIndex(
                name: "IX_Contratos_ModeloContratoId",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "CondicaoPagamentoId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "CustoUnitarioCompra",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "FatorConversao",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "FornecedorId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "QuantidadeCompra",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "UnidadeCompra",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "Endereco_Bairro",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Endereco_Cep",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Endereco_Cidade",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Endereco_Complemento",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Endereco_Logradouro",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Endereco_Numero",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Endereco_Uf",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "CondicaoPagamentoId",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "Endereco_Bairro",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Endereco_Cep",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Endereco_Cidade",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Endereco_Complemento",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Endereco_Logradouro",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Endereco_Numero",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Endereco_Uf",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "ModeloContratoId",
                table: "Contratos");
        }
    }
}
