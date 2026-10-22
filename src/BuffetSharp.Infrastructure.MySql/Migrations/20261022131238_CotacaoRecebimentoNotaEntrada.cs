using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class CotacaoRecebimentoNotaEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PedidoCompraItens_MovimentacoesEstoque_MovimentacaoEstoqueId",
                table: "PedidoCompraItens");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompra_ContasPagar_ContaPagarId",
                table: "PedidosCompra");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompra_ParcelamentosPagar_ParcelamentoPagarId",
                table: "PedidosCompra");

            migrationBuilder.DropIndex(
                name: "IX_PedidosCompra_ContaPagarId",
                table: "PedidosCompra");

            migrationBuilder.DropIndex(
                name: "IX_PedidosCompra_NotaFiscal",
                table: "PedidosCompra");

            migrationBuilder.DropIndex(
                name: "IX_PedidoCompraItens_MovimentacaoEstoqueId",
                table: "PedidoCompraItens");

            migrationBuilder.DropColumn(
                name: "ContaPagarId",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "DataCompra",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "NotaFiscal",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "RecebidoEm",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "MovimentacaoEstoqueId",
                table: "PedidoCompraItens");

            migrationBuilder.RenameColumn(
                name: "ParcelamentoPagarId",
                table: "PedidosCompra",
                newName: "CotacaoCompraId");

            migrationBuilder.RenameIndex(
                name: "IX_PedidosCompra_ParcelamentoPagarId",
                table: "PedidosCompra",
                newName: "IX_PedidosCompra_CotacaoCompraId");

            migrationBuilder.AddColumn<decimal>(
                name: "QuantidadeRecebida",
                table: "PedidoCompraItens",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // A coluna renomeada guardava o parcelamento da nota (que agora fica na nota fiscal de entrada).
            migrationBuilder.Sql("UPDATE PedidosCompra SET CotacaoCompraId = NULL");

            // Pedidos já recebidos pelo fluxo anterior (tudo de uma vez) ficam com o saldo zerado.
            migrationBuilder.Sql("UPDATE PedidoCompraItens SET QuantidadeRecebida = Quantidade " +
                                 "WHERE PedidoCompraId IN (SELECT Id FROM PedidosCompra WHERE Status = 'Recebido')");

            migrationBuilder.CreateTable(
                name: "CotacoesCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Descricao = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    PrazoResposta = table.Column<DateTime>(type: "date", nullable: true),
                    EventoId = table.Column<int>(type: "int", nullable: true),
                    Observacoes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ConcluidaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotacoesCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotacoesCompra_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotasFiscaisEntrada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FornecedorId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Serie = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "date", nullable: false),
                    Categoria = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ValorProdutos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Observacoes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CanceladaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ContaPagarId = table.Column<int>(type: "int", nullable: true),
                    ParcelamentoPagarId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscaisEntrada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasFiscaisEntrada_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasFiscaisEntrada_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasFiscaisEntrada_ParcelamentosPagar_ParcelamentoPagarId",
                        column: x => x.ParcelamentoPagarId,
                        principalTable: "ParcelamentosPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CotacaoParticipantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CotacaoCompraId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Contato = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Telefone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    PrevisaoEntrega = table.Column<DateTime>(type: "date", nullable: true),
                    CondicaoPagamento = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Observacoes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotacaoParticipantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotacaoParticipantes_CotacoesCompra_CotacaoCompraId",
                        column: x => x.CotacaoCompraId,
                        principalTable: "CotacoesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CotacaoParticipantes_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecebimentosCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PedidoCompraId = table.Column<int>(type: "int", nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    Observacoes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    NotaFiscalEntradaId = table.Column<int>(type: "int", nullable: true),
                    EstornadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    MotivoEstorno = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecebimentosCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecebimentosCompra_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecebimentosCompra_NotasFiscaisEntrada_NotaFiscalEntradaId",
                        column: x => x.NotaFiscalEntradaId,
                        principalTable: "NotasFiscaisEntrada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecebimentosCompra_PedidosCompra_PedidoCompraId",
                        column: x => x.PedidoCompraId,
                        principalTable: "PedidosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CotacaoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CotacaoCompraId = table.Column<int>(type: "int", nullable: false),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Especificacao = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    VencedorId = table.Column<int>(type: "int", nullable: true),
                    EscolhaManual = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MotivoEscolha = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotacaoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotacaoItens_CotacaoParticipantes_VencedorId",
                        column: x => x.VencedorId,
                        principalTable: "CotacaoParticipantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CotacaoItens_CotacoesCompra_CotacaoCompraId",
                        column: x => x.CotacaoCompraId,
                        principalTable: "CotacoesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CotacaoItens_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecebimentoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RecebimentoCompraId = table.Column<int>(type: "int", nullable: false),
                    PedidoCompraItemId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MovimentacaoEstoqueId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecebimentoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecebimentoItens_MovimentacoesEstoque_MovimentacaoEstoqueId",
                        column: x => x.MovimentacaoEstoqueId,
                        principalTable: "MovimentacoesEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecebimentoItens_PedidoCompraItens_PedidoCompraItemId",
                        column: x => x.PedidoCompraItemId,
                        principalTable: "PedidoCompraItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecebimentoItens_RecebimentosCompra_RecebimentoCompraId",
                        column: x => x.RecebimentoCompraId,
                        principalTable: "RecebimentosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CotacaoPrecos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CotacaoCompraId = table.Column<int>(type: "int", nullable: false),
                    CotacaoItemId = table.Column<int>(type: "int", nullable: false),
                    CotacaoParticipanteId = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Observacao = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotacaoPrecos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotacaoPrecos_CotacaoItens_CotacaoItemId",
                        column: x => x.CotacaoItemId,
                        principalTable: "CotacaoItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CotacaoPrecos_CotacaoParticipantes_CotacaoParticipanteId",
                        column: x => x.CotacaoParticipanteId,
                        principalTable: "CotacaoParticipantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CotacaoPrecos_CotacoesCompra_CotacaoCompraId",
                        column: x => x.CotacaoCompraId,
                        principalTable: "CotacoesCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoItens_CotacaoCompraId",
                table: "CotacaoItens",
                column: "CotacaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoItens_ProdutoId",
                table: "CotacaoItens",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoItens_VencedorId",
                table: "CotacaoItens",
                column: "VencedorId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoParticipantes_CotacaoCompraId",
                table: "CotacaoParticipantes",
                column: "CotacaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoParticipantes_FornecedorId",
                table: "CotacaoParticipantes",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoPrecos_CotacaoCompraId",
                table: "CotacaoPrecos",
                column: "CotacaoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoPrecos_CotacaoItemId_CotacaoParticipanteId",
                table: "CotacaoPrecos",
                columns: new[] { "CotacaoItemId", "CotacaoParticipanteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CotacaoPrecos_CotacaoParticipanteId",
                table: "CotacaoPrecos",
                column: "CotacaoParticipanteId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacoesCompra_EventoId",
                table: "CotacoesCompra",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_CotacoesCompra_Status_Data",
                table: "CotacoesCompra",
                columns: new[] { "Status", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisEntrada_ContaPagarId",
                table: "NotasFiscaisEntrada",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisEntrada_DataEmissao",
                table: "NotasFiscaisEntrada",
                column: "DataEmissao");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisEntrada_FornecedorId_Numero",
                table: "NotasFiscaisEntrada",
                columns: new[] { "FornecedorId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisEntrada_ParcelamentoPagarId",
                table: "NotasFiscaisEntrada",
                column: "ParcelamentoPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentoItens_MovimentacaoEstoqueId",
                table: "RecebimentoItens",
                column: "MovimentacaoEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentoItens_PedidoCompraItemId",
                table: "RecebimentoItens",
                column: "PedidoCompraItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentoItens_RecebimentoCompraId",
                table: "RecebimentoItens",
                column: "RecebimentoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosCompra_FornecedorId_Status",
                table: "RecebimentosCompra",
                columns: new[] { "FornecedorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosCompra_NotaFiscalEntradaId",
                table: "RecebimentosCompra",
                column: "NotaFiscalEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosCompra_PedidoCompraId",
                table: "RecebimentosCompra",
                column: "PedidoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosCompra_Status_Data",
                table: "RecebimentosCompra",
                columns: new[] { "Status", "Data" });

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompra_CotacoesCompra_CotacaoCompraId",
                table: "PedidosCompra",
                column: "CotacaoCompraId",
                principalTable: "CotacoesCompra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompra_CotacoesCompra_CotacaoCompraId",
                table: "PedidosCompra");

            migrationBuilder.DropTable(
                name: "CotacaoPrecos");

            migrationBuilder.DropTable(
                name: "RecebimentoItens");

            migrationBuilder.DropTable(
                name: "CotacaoItens");

            migrationBuilder.DropTable(
                name: "RecebimentosCompra");

            migrationBuilder.DropTable(
                name: "CotacaoParticipantes");

            migrationBuilder.DropTable(
                name: "NotasFiscaisEntrada");

            migrationBuilder.DropTable(
                name: "CotacoesCompra");

            migrationBuilder.DropColumn(
                name: "QuantidadeRecebida",
                table: "PedidoCompraItens");

            migrationBuilder.RenameColumn(
                name: "CotacaoCompraId",
                table: "PedidosCompra",
                newName: "ParcelamentoPagarId");

            migrationBuilder.RenameIndex(
                name: "IX_PedidosCompra_CotacaoCompraId",
                table: "PedidosCompra",
                newName: "IX_PedidosCompra_ParcelamentoPagarId");

            migrationBuilder.AddColumn<int>(
                name: "ContaPagarId",
                table: "PedidosCompra",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCompra",
                table: "PedidosCompra",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotaFiscal",
                table: "PedidosCompra",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecebidoEm",
                table: "PedidosCompra",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovimentacaoEstoqueId",
                table: "PedidoCompraItens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_ContaPagarId",
                table: "PedidosCompra",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_NotaFiscal",
                table: "PedidosCompra",
                column: "NotaFiscal");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoCompraItens_MovimentacaoEstoqueId",
                table: "PedidoCompraItens",
                column: "MovimentacaoEstoqueId");

            migrationBuilder.AddForeignKey(
                name: "FK_PedidoCompraItens_MovimentacoesEstoque_MovimentacaoEstoqueId",
                table: "PedidoCompraItens",
                column: "MovimentacaoEstoqueId",
                principalTable: "MovimentacoesEstoque",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompra_ContasPagar_ContaPagarId",
                table: "PedidosCompra",
                column: "ContaPagarId",
                principalTable: "ContasPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompra_ParcelamentosPagar_ParcelamentoPagarId",
                table: "PedidosCompra",
                column: "ParcelamentoPagarId",
                principalTable: "ParcelamentosPagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
