using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class CotacaoRecebimentoNotaEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDOCOMPRAITENS_MOVIMENTACOESESTOQUE_MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDOSCOMPRA_CONTASPAGAR_CONTAPAGARID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDOSCOMPRA_PARCELAMENTOSPAGAR_PARCELAMENTOPAGARID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDOSCOMPRA_CONTAPAGARID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDOSCOMPRA_NOTAFISCAL",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDOCOMPRAITENS_MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS");

            migrationBuilder.DropColumn(
                name: "CONTAPAGARID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "DATACOMPRA",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "NOTAFISCAL",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "RECEBIDOEM",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropColumn(
                name: "MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS");

            migrationBuilder.RenameColumn(
                name: "PARCELAMENTOPAGARID",
                table: "PEDIDOSCOMPRA",
                newName: "COTACAOCOMPRAID");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDOSCOMPRA_PARCELAMENTOPAGARID",
                table: "PEDIDOSCOMPRA",
                newName: "IX_PEDIDOSCOMPRA_COTACAOCOMPRAID");

            migrationBuilder.AddColumn<decimal>(
                name: "QUANTIDADERECEBIDA",
                table: "PEDIDOCOMPRAITENS",
                type: "DECIMAL(18,3)",
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
                name: "COTACOESCOMPRA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    PRAZORESPOSTA = table.Column<DateTime>(type: "DATE", nullable: true),
                    EVENTOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CONCLUIDAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COTACOESCOMPRA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COTACOESCOMPRA_EVENTOS_EVENTOID",
                        column: x => x.EVENTOID,
                        principalTable: "EVENTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NOTASFISCAISENTRADA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NUMERO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    SERIE = table.Column<string>(type: "NVARCHAR2(5)", maxLength: 5, nullable: true),
                    DATAEMISSAO = table.Column<DateTime>(type: "DATE", nullable: false),
                    CATEGORIA = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    VALORPRODUTOS = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    VALORTOTAL = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    CANCELADAEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOTIVOCANCELAMENTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CONTAPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PARCELAMENTOPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTASFISCAISENTRADA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NOTASFISCAISENTRADA_CONTASPAGAR_CONTAPAGARID",
                        column: x => x.CONTAPAGARID,
                        principalTable: "CONTASPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NOTASFISCAISENTRADA_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NOTASFISCAISENTRADA_PARCELAMENTOSPAGAR_PARCELAMENTOPAGARID",
                        column: x => x.PARCELAMENTOPAGARID,
                        principalTable: "PARCELAMENTOSPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COTACAOPARTICIPANTES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COTACAOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    CONTATO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PREVISAOENTREGA = table.Column<DateTime>(type: "DATE", nullable: true),
                    CONDICAOPAGAMENTO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COTACAOPARTICIPANTES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COTACAOPARTICIPANTES_COTACOESCOMPRA_COTACAOCOMPRAID",
                        column: x => x.COTACAOCOMPRAID,
                        principalTable: "COTACOESCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_COTACAOPARTICIPANTES_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECEBIMENTOSCOMPRA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PEDIDOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    OBSERVACOES = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    REGISTRADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    NOTAFISCALENTRADAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ESTORNADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOTIVOESTORNO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEBIMENTOSCOMPRA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RECEBIMENTOSCOMPRA_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECEBIMENTOSCOMPRA_NOTASFISCAISENTRADA_NOTAFISCALENTRADAID",
                        column: x => x.NOTAFISCALENTRADAID,
                        principalTable: "NOTASFISCAISENTRADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECEBIMENTOSCOMPRA_PEDIDOSCOMPRA_PEDIDOCOMPRAID",
                        column: x => x.PEDIDOCOMPRAID,
                        principalTable: "PEDIDOSCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COTACAOITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COTACAOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PRODUTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    ESPECIFICACAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    VENCEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ESCOLHAMANUAL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    MOTIVOESCOLHA = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COTACAOITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COTACAOITENS_COTACAOPARTICIPANTES_VENCEDORID",
                        column: x => x.VENCEDORID,
                        principalTable: "COTACAOPARTICIPANTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COTACAOITENS_COTACOESCOMPRA_COTACAOCOMPRAID",
                        column: x => x.COTACAOCOMPRAID,
                        principalTable: "COTACOESCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_COTACAOITENS_PRODUTOS_PRODUTOID",
                        column: x => x.PRODUTOID,
                        principalTable: "PRODUTOS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECEBIMENTOITENS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RECEBIMENTOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PEDIDOCOMPRAITEMID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTIDADE = table.Column<decimal>(type: "DECIMAL(18,3)", precision: 18, scale: 3, nullable: false),
                    CUSTOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    MOVIMENTACAOESTOQUEID = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEBIMENTOITENS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RECEBIMENTOITENS_MOVIMENTACOESESTOQUE_MOVIMENTACAOESTOQUEID",
                        column: x => x.MOVIMENTACAOESTOQUEID,
                        principalTable: "MOVIMENTACOESESTOQUE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECEBIMENTOITENS_PEDIDOCOMPRAITENS_PEDIDOCOMPRAITEMID",
                        column: x => x.PEDIDOCOMPRAITEMID,
                        principalTable: "PEDIDOCOMPRAITENS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECEBIMENTOITENS_RECEBIMENTOSCOMPRA_RECEBIMENTOCOMPRAID",
                        column: x => x.RECEBIMENTOCOMPRAID,
                        principalTable: "RECEBIMENTOSCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "COTACAOPRECOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    COTACAOCOMPRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    COTACAOITEMID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    COTACAOPARTICIPANTEID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PRECOUNITARIO = table.Column<decimal>(type: "DECIMAL(18,4)", precision: 18, scale: 4, nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COTACAOPRECOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COTACAOPRECOS_COTACAOITENS_COTACAOITEMID",
                        column: x => x.COTACAOITEMID,
                        principalTable: "COTACAOITENS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COTACAOPRECOS_COTACAOPARTICIPANTES_COTACAOPARTICIPANTEID",
                        column: x => x.COTACAOPARTICIPANTEID,
                        principalTable: "COTACAOPARTICIPANTES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COTACAOPRECOS_COTACOESCOMPRA_COTACAOCOMPRAID",
                        column: x => x.COTACAOCOMPRAID,
                        principalTable: "COTACOESCOMPRA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOITENS_COTACAOCOMPRAID",
                table: "COTACAOITENS",
                column: "COTACAOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOITENS_PRODUTOID",
                table: "COTACAOITENS",
                column: "PRODUTOID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOITENS_VENCEDORID",
                table: "COTACAOITENS",
                column: "VENCEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOPARTICIPANTES_COTACAOCOMPRAID",
                table: "COTACAOPARTICIPANTES",
                column: "COTACAOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOPARTICIPANTES_FORNECEDORID",
                table: "COTACAOPARTICIPANTES",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOPRECOS_COTACAOCOMPRAID",
                table: "COTACAOPRECOS",
                column: "COTACAOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOPRECOS_COTACAOITEMID_COTACAOPARTICIPANTEID",
                table: "COTACAOPRECOS",
                columns: new[] { "COTACAOITEMID", "COTACAOPARTICIPANTEID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COTACAOPRECOS_COTACAOPARTICIPANTEID",
                table: "COTACAOPRECOS",
                column: "COTACAOPARTICIPANTEID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACOESCOMPRA_EVENTOID",
                table: "COTACOESCOMPRA",
                column: "EVENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_COTACOESCOMPRA_STATUS_DATA",
                table: "COTACOESCOMPRA",
                columns: new[] { "STATUS", "DATA" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISENTRADA_CONTAPAGARID",
                table: "NOTASFISCAISENTRADA",
                column: "CONTAPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISENTRADA_DATAEMISSAO",
                table: "NOTASFISCAISENTRADA",
                column: "DATAEMISSAO");

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISENTRADA_FORNECEDORID_NUMERO",
                table: "NOTASFISCAISENTRADA",
                columns: new[] { "FORNECEDORID", "NUMERO" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTASFISCAISENTRADA_PARCELAMENTOPAGARID",
                table: "NOTASFISCAISENTRADA",
                column: "PARCELAMENTOPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOITENS_MOVIMENTACAOESTOQUEID",
                table: "RECEBIMENTOITENS",
                column: "MOVIMENTACAOESTOQUEID");

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOITENS_PEDIDOCOMPRAITEMID",
                table: "RECEBIMENTOITENS",
                column: "PEDIDOCOMPRAITEMID");

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOITENS_RECEBIMENTOCOMPRAID",
                table: "RECEBIMENTOITENS",
                column: "RECEBIMENTOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOSCOMPRA_FORNECEDORID_STATUS",
                table: "RECEBIMENTOSCOMPRA",
                columns: new[] { "FORNECEDORID", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOSCOMPRA_NOTAFISCALENTRADAID",
                table: "RECEBIMENTOSCOMPRA",
                column: "NOTAFISCALENTRADAID");

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOSCOMPRA_PEDIDOCOMPRAID",
                table: "RECEBIMENTOSCOMPRA",
                column: "PEDIDOCOMPRAID");

            migrationBuilder.CreateIndex(
                name: "IX_RECEBIMENTOSCOMPRA_STATUS_DATA",
                table: "RECEBIMENTOSCOMPRA",
                columns: new[] { "STATUS", "DATA" });

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDOSCOMPRA_COTACOESCOMPRA_COTACAOCOMPRAID",
                table: "PEDIDOSCOMPRA",
                column: "COTACAOCOMPRAID",
                principalTable: "COTACOESCOMPRA",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDOSCOMPRA_COTACOESCOMPRA_COTACAOCOMPRAID",
                table: "PEDIDOSCOMPRA");

            migrationBuilder.DropTable(
                name: "COTACAOPRECOS");

            migrationBuilder.DropTable(
                name: "RECEBIMENTOITENS");

            migrationBuilder.DropTable(
                name: "COTACAOITENS");

            migrationBuilder.DropTable(
                name: "RECEBIMENTOSCOMPRA");

            migrationBuilder.DropTable(
                name: "COTACAOPARTICIPANTES");

            migrationBuilder.DropTable(
                name: "NOTASFISCAISENTRADA");

            migrationBuilder.DropTable(
                name: "COTACOESCOMPRA");

            migrationBuilder.DropColumn(
                name: "QUANTIDADERECEBIDA",
                table: "PEDIDOCOMPRAITENS");

            migrationBuilder.RenameColumn(
                name: "COTACAOCOMPRAID",
                table: "PEDIDOSCOMPRA",
                newName: "PARCELAMENTOPAGARID");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDOSCOMPRA_COTACAOCOMPRAID",
                table: "PEDIDOSCOMPRA",
                newName: "IX_PEDIDOSCOMPRA_PARCELAMENTOPAGARID");

            migrationBuilder.AddColumn<int>(
                name: "CONTAPAGARID",
                table: "PEDIDOSCOMPRA",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATACOMPRA",
                table: "PEDIDOSCOMPRA",
                type: "DATE",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NOTAFISCAL",
                table: "PEDIDOSCOMPRA",
                type: "NVARCHAR2(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RECEBIDOEM",
                table: "PEDIDOSCOMPRA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_CONTAPAGARID",
                table: "PEDIDOSCOMPRA",
                column: "CONTAPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOSCOMPRA_NOTAFISCAL",
                table: "PEDIDOSCOMPRA",
                column: "NOTAFISCAL");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDOCOMPRAITENS_MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS",
                column: "MOVIMENTACAOESTOQUEID");

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDOCOMPRAITENS_MOVIMENTACOESESTOQUE_MOVIMENTACAOESTOQUEID",
                table: "PEDIDOCOMPRAITENS",
                column: "MOVIMENTACAOESTOQUEID",
                principalTable: "MOVIMENTACOESESTOQUE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDOSCOMPRA_CONTASPAGAR_CONTAPAGARID",
                table: "PEDIDOSCOMPRA",
                column: "CONTAPAGARID",
                principalTable: "CONTASPAGAR",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDOSCOMPRA_PARCELAMENTOSPAGAR_PARCELAMENTOPAGARID",
                table: "PEDIDOSCOMPRA",
                column: "PARCELAMENTOPAGARID",
                principalTable: "PARCELAMENTOSPAGAR",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
