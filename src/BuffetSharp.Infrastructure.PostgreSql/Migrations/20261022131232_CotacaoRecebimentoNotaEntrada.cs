using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CotacaoRecebimentoNotaEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pedidocompraitens_movimentacoesestoque_movimentacaoestoqueid",
                table: "pedidocompraitens");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidoscompra_contaspagar_contapagarid",
                table: "pedidoscompra");

            migrationBuilder.DropForeignKey(
                name: "fk_pedidoscompra_parcelamentospagar_parcelamentopagarid",
                table: "pedidoscompra");

            migrationBuilder.DropIndex(
                name: "ix_pedidoscompra_contapagarid",
                table: "pedidoscompra");

            migrationBuilder.DropIndex(
                name: "ix_pedidoscompra_notafiscal",
                table: "pedidoscompra");

            migrationBuilder.DropIndex(
                name: "ix_pedidocompraitens_movimentacaoestoqueid",
                table: "pedidocompraitens");

            migrationBuilder.DropColumn(
                name: "contapagarid",
                table: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "datacompra",
                table: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "notafiscal",
                table: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "recebidoem",
                table: "pedidoscompra");

            migrationBuilder.DropColumn(
                name: "movimentacaoestoqueid",
                table: "pedidocompraitens");

            migrationBuilder.RenameColumn(
                name: "parcelamentopagarid",
                table: "pedidoscompra",
                newName: "cotacaocompraid");

            migrationBuilder.RenameIndex(
                name: "ix_pedidoscompra_parcelamentopagarid",
                table: "pedidoscompra",
                newName: "ix_pedidoscompra_cotacaocompraid");

            migrationBuilder.AddColumn<decimal>(
                name: "quantidaderecebida",
                table: "pedidocompraitens",
                type: "numeric(18,3)",
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
                name: "cotacoescompra",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    prazoresposta = table.Column<DateOnly>(type: "date", nullable: true),
                    eventoid = table.Column<int>(type: "integer", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    motivocancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    concluidaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacoescompra", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotacoescompra_eventos_eventoid",
                        column: x => x.eventoid,
                        principalTable: "eventos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notasfiscaisentrada",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fornecedorid = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    serie = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    dataemissao = table.Column<DateOnly>(type: "date", nullable: false),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valorprodutos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valortotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    canceladaem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivocancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contapagarid = table.Column<int>(type: "integer", nullable: true),
                    parcelamentopagarid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notasfiscaisentrada", x => x.id);
                    table.ForeignKey(
                        name: "fk_notasfiscaisentrada_contaspagar_contapagarid",
                        column: x => x.contapagarid,
                        principalTable: "contaspagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notasfiscaisentrada_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notasfiscaisentrada_parcelamentospagar_parcelamentopagarid",
                        column: x => x.parcelamentopagarid,
                        principalTable: "parcelamentospagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cotacaoparticipantes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cotacaocompraid = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    previsaoentrega = table.Column<DateOnly>(type: "date", nullable: true),
                    condicaopagamento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacaoparticipantes", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotacaoparticipantes_cotacoescompra_cotacaocompraid",
                        column: x => x.cotacaocompraid,
                        principalTable: "cotacoescompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cotacaoparticipantes_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recebimentoscompra",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pedidocompraid = table.Column<int>(type: "integer", nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    registradoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notafiscalentradaid = table.Column<int>(type: "integer", nullable: true),
                    estornadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivoestorno = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recebimentoscompra", x => x.id);
                    table.ForeignKey(
                        name: "fk_recebimentoscompra_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recebimentoscompra_notasfiscaisentrada_notafiscalentradaid",
                        column: x => x.notafiscalentradaid,
                        principalTable: "notasfiscaisentrada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recebimentoscompra_pedidoscompra_pedidocompraid",
                        column: x => x.pedidocompraid,
                        principalTable: "pedidoscompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cotacaoitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cotacaocompraid = table.Column<int>(type: "integer", nullable: false),
                    produtoid = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    especificacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    vencedorid = table.Column<int>(type: "integer", nullable: true),
                    escolhamanual = table.Column<bool>(type: "boolean", nullable: false),
                    motivoescolha = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacaoitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotacaoitens_cotacaoparticipantes_vencedorid",
                        column: x => x.vencedorid,
                        principalTable: "cotacaoparticipantes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotacaoitens_cotacoescompra_cotacaocompraid",
                        column: x => x.cotacaocompraid,
                        principalTable: "cotacoescompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cotacaoitens_produtos_produtoid",
                        column: x => x.produtoid,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recebimentoitens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recebimentocompraid = table.Column<int>(type: "integer", nullable: false),
                    pedidocompraitemid = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    custounitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    movimentacaoestoqueid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recebimentoitens", x => x.id);
                    table.ForeignKey(
                        name: "fk_recebimentoitens_movimentacoesestoque_movimentacaoestoqueid",
                        column: x => x.movimentacaoestoqueid,
                        principalTable: "movimentacoesestoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recebimentoitens_pedidocompraitens_pedidocompraitemid",
                        column: x => x.pedidocompraitemid,
                        principalTable: "pedidocompraitens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recebimentoitens_recebimentoscompra_recebimentocompraid",
                        column: x => x.recebimentocompraid,
                        principalTable: "recebimentoscompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cotacaoprecos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cotacaocompraid = table.Column<int>(type: "integer", nullable: false),
                    cotacaoitemid = table.Column<int>(type: "integer", nullable: false),
                    cotacaoparticipanteid = table.Column<int>(type: "integer", nullable: false),
                    precounitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    observacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacaoprecos", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotacaoprecos_cotacaoitens_cotacaoitemid",
                        column: x => x.cotacaoitemid,
                        principalTable: "cotacaoitens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotacaoprecos_cotacaoparticipantes_cotacaoparticipanteid",
                        column: x => x.cotacaoparticipanteid,
                        principalTable: "cotacaoparticipantes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotacaoprecos_cotacoescompra_cotacaocompraid",
                        column: x => x.cotacaocompraid,
                        principalTable: "cotacoescompra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoitens_cotacaocompraid",
                table: "cotacaoitens",
                column: "cotacaocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoitens_produtoid",
                table: "cotacaoitens",
                column: "produtoid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoitens_vencedorid",
                table: "cotacaoitens",
                column: "vencedorid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoparticipantes_cotacaocompraid",
                table: "cotacaoparticipantes",
                column: "cotacaocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoparticipantes_fornecedorid",
                table: "cotacaoparticipantes",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoprecos_cotacaocompraid",
                table: "cotacaoprecos",
                column: "cotacaocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoprecos_cotacaoitemid_cotacaoparticipanteid",
                table: "cotacaoprecos",
                columns: new[] { "cotacaoitemid", "cotacaoparticipanteid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cotacaoprecos_cotacaoparticipanteid",
                table: "cotacaoprecos",
                column: "cotacaoparticipanteid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacoescompra_eventoid",
                table: "cotacoescompra",
                column: "eventoid");

            migrationBuilder.CreateIndex(
                name: "ix_cotacoescompra_status_data",
                table: "cotacoescompra",
                columns: new[] { "status", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisentrada_contapagarid",
                table: "notasfiscaisentrada",
                column: "contapagarid");

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisentrada_dataemissao",
                table: "notasfiscaisentrada",
                column: "dataemissao");

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisentrada_fornecedorid_numero",
                table: "notasfiscaisentrada",
                columns: new[] { "fornecedorid", "numero" });

            migrationBuilder.CreateIndex(
                name: "ix_notasfiscaisentrada_parcelamentopagarid",
                table: "notasfiscaisentrada",
                column: "parcelamentopagarid");

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoitens_movimentacaoestoqueid",
                table: "recebimentoitens",
                column: "movimentacaoestoqueid");

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoitens_pedidocompraitemid",
                table: "recebimentoitens",
                column: "pedidocompraitemid");

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoitens_recebimentocompraid",
                table: "recebimentoitens",
                column: "recebimentocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoscompra_fornecedorid_status",
                table: "recebimentoscompra",
                columns: new[] { "fornecedorid", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoscompra_notafiscalentradaid",
                table: "recebimentoscompra",
                column: "notafiscalentradaid");

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoscompra_pedidocompraid",
                table: "recebimentoscompra",
                column: "pedidocompraid");

            migrationBuilder.CreateIndex(
                name: "ix_recebimentoscompra_status_data",
                table: "recebimentoscompra",
                columns: new[] { "status", "data" });

            migrationBuilder.AddForeignKey(
                name: "fk_pedidoscompra_cotacoescompra_cotacaocompraid",
                table: "pedidoscompra",
                column: "cotacaocompraid",
                principalTable: "cotacoescompra",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pedidoscompra_cotacoescompra_cotacaocompraid",
                table: "pedidoscompra");

            migrationBuilder.DropTable(
                name: "cotacaoprecos");

            migrationBuilder.DropTable(
                name: "recebimentoitens");

            migrationBuilder.DropTable(
                name: "cotacaoitens");

            migrationBuilder.DropTable(
                name: "recebimentoscompra");

            migrationBuilder.DropTable(
                name: "cotacaoparticipantes");

            migrationBuilder.DropTable(
                name: "notasfiscaisentrada");

            migrationBuilder.DropTable(
                name: "cotacoescompra");

            migrationBuilder.DropColumn(
                name: "quantidaderecebida",
                table: "pedidocompraitens");

            migrationBuilder.RenameColumn(
                name: "cotacaocompraid",
                table: "pedidoscompra",
                newName: "parcelamentopagarid");

            migrationBuilder.RenameIndex(
                name: "ix_pedidoscompra_cotacaocompraid",
                table: "pedidoscompra",
                newName: "ix_pedidoscompra_parcelamentopagarid");

            migrationBuilder.AddColumn<int>(
                name: "contapagarid",
                table: "pedidoscompra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "datacompra",
                table: "pedidoscompra",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notafiscal",
                table: "pedidoscompra",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "recebidoem",
                table: "pedidoscompra",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "movimentacaoestoqueid",
                table: "pedidocompraitens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_contapagarid",
                table: "pedidoscompra",
                column: "contapagarid");

            migrationBuilder.CreateIndex(
                name: "ix_pedidoscompra_notafiscal",
                table: "pedidoscompra",
                column: "notafiscal");

            migrationBuilder.CreateIndex(
                name: "ix_pedidocompraitens_movimentacaoestoqueid",
                table: "pedidocompraitens",
                column: "movimentacaoestoqueid");

            migrationBuilder.AddForeignKey(
                name: "fk_pedidocompraitens_movimentacoesestoque_movimentacaoestoqueid",
                table: "pedidocompraitens",
                column: "movimentacaoestoqueid",
                principalTable: "movimentacoesestoque",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidoscompra_contaspagar_contapagarid",
                table: "pedidoscompra",
                column: "contapagarid",
                principalTable: "contaspagar",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidoscompra_parcelamentospagar_parcelamentopagarid",
                table: "pedidoscompra",
                column: "parcelamentopagarid",
                principalTable: "parcelamentospagar",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
