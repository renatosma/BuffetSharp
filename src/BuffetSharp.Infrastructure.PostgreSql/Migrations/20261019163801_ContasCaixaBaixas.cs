using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ContasCaixaBaixas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "formaspagamento",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Outros");

            // Tipo das formas já cadastradas, deduzido do nome (o usuário pode corrigir no cadastro).
            foreach (var (tipo, condicao) in new[]
                     {
                         ("CartaoCredito", "UPPER(Nome) LIKE '%CR_DITO%'"),
                         ("CartaoDebito", "UPPER(Nome) LIKE '%D_BITO%'"),
                         ("Pix", "UPPER(Nome) LIKE '%PIX%'"),
                         ("Dinheiro", "UPPER(Nome) LIKE '%DINHEIRO%' OR UPPER(Nome) LIKE '%ESP_CIE%'"),
                         ("Boleto", "UPPER(Nome) LIKE '%BOLETO%'"),
                         ("Transferencia", "UPPER(Nome) LIKE '%TRANSF%' OR UPPER(Nome) LIKE '%TED%' OR UPPER(Nome) LIKE '%DEP_SITO%'"),
                         ("Cheque", "UPPER(Nome) LIKE '%CHEQUE%'")
                     })
                migrationBuilder.Sql($"UPDATE FormasPagamento SET Tipo = '{tipo}' WHERE Tipo = 'Outros' AND ({condicao})");

            migrationBuilder.AddColumn<int>(
                name: "origembaixaformaid",
                table: "contaspagar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "baixas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baixas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cartoescredito",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    final = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    diafechamento = table.Column<int>(type: "integer", nullable: false),
                    diavencimento = table.Column<int>(type: "integer", nullable: false),
                    fornecedorid = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cartoescredito", x => x.id);
                    table.ForeignKey(
                        name: "fk_cartoescredito_fornecedores_fornecedorid",
                        column: x => x.fornecedorid,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contasfinanceiras",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    banco = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    agencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    saldoinicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    datasaldoinicial = table.Column<DateOnly>(type: "date", nullable: false),
                    fechadaate = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contasfinanceiras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "baixatitulos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    baixaid = table.Column<int>(type: "integer", nullable: false),
                    parcelaid = table.Column<int>(type: "integer", nullable: true),
                    contapagarid = table.Column<int>(type: "integer", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    juros = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    desconto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldoparcelaid = table.Column<int>(type: "integer", nullable: true),
                    saldocontapagarid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baixatitulos", x => x.id);
                    table.ForeignKey(
                        name: "fk_baixatitulos_baixas_baixaid",
                        column: x => x.baixaid,
                        principalTable: "baixas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_baixatitulos_contaspagar_contapagarid",
                        column: x => x.contapagarid,
                        principalTable: "contaspagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baixatitulos_contaspagar_saldocontapagarid",
                        column: x => x.saldocontapagarid,
                        principalTable: "contaspagar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_baixatitulos_parcelas_parcelaid",
                        column: x => x.parcelaid,
                        principalTable: "parcelas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baixatitulos_parcelas_saldoparcelaid",
                        column: x => x.saldoparcelaid,
                        principalTable: "parcelas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "baixaformas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    baixaid = table.Column<int>(type: "integer", nullable: false),
                    formapagamentoid = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    contafinanceiraid = table.Column<int>(type: "integer", nullable: true),
                    cartaocreditoid = table.Column<int>(type: "integer", nullable: true),
                    parcelas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baixaformas", x => x.id);
                    table.ForeignKey(
                        name: "fk_baixaformas_baixas_baixaid",
                        column: x => x.baixaid,
                        principalTable: "baixas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_baixaformas_cartoescredito_cartaocreditoid",
                        column: x => x.cartaocreditoid,
                        principalTable: "cartoescredito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baixaformas_contasfinanceiras_contafinanceiraid",
                        column: x => x.contafinanceiraid,
                        principalTable: "contasfinanceiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baixaformas_formaspagamento_formapagamentoid",
                        column: x => x.formapagamentoid,
                        principalTable: "formaspagamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentosconta",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contafinanceiraid = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    baixaformaid = table.Column<int>(type: "integer", nullable: true),
                    contrapartidaid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentosconta", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentosconta_baixaformas_baixaformaid",
                        column: x => x.baixaformaid,
                        principalTable: "baixaformas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_movimentosconta_contasfinanceiras_contafinanceiraid",
                        column: x => x.contafinanceiraid,
                        principalTable: "contasfinanceiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentosconta_movimentosconta_contrapartidaid",
                        column: x => x.contrapartidaid,
                        principalTable: "movimentosconta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fechamentosconta",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contafinanceiraid = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    saldosistema = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldoinformado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ajusteid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fechamentosconta", x => x.id);
                    table.ForeignKey(
                        name: "fk_fechamentosconta_contasfinanceiras_contafinanceiraid",
                        column: x => x.contafinanceiraid,
                        principalTable: "contasfinanceiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fechamentosconta_movimentosconta_ajusteid",
                        column: x => x.ajusteid,
                        principalTable: "movimentosconta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contaspagar_origembaixaformaid",
                table: "contaspagar",
                column: "origembaixaformaid");

            migrationBuilder.CreateIndex(
                name: "ix_baixaformas_baixaid",
                table: "baixaformas",
                column: "baixaid");

            migrationBuilder.CreateIndex(
                name: "ix_baixaformas_cartaocreditoid",
                table: "baixaformas",
                column: "cartaocreditoid");

            migrationBuilder.CreateIndex(
                name: "ix_baixaformas_contafinanceiraid",
                table: "baixaformas",
                column: "contafinanceiraid");

            migrationBuilder.CreateIndex(
                name: "ix_baixaformas_formapagamentoid",
                table: "baixaformas",
                column: "formapagamentoid");

            migrationBuilder.CreateIndex(
                name: "ix_baixas_data",
                table: "baixas",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "ix_baixatitulos_baixaid",
                table: "baixatitulos",
                column: "baixaid");

            migrationBuilder.CreateIndex(
                name: "ix_baixatitulos_contapagarid",
                table: "baixatitulos",
                column: "contapagarid");

            migrationBuilder.CreateIndex(
                name: "ix_baixatitulos_parcelaid",
                table: "baixatitulos",
                column: "parcelaid");

            migrationBuilder.CreateIndex(
                name: "ix_baixatitulos_saldocontapagarid",
                table: "baixatitulos",
                column: "saldocontapagarid");

            migrationBuilder.CreateIndex(
                name: "ix_baixatitulos_saldoparcelaid",
                table: "baixatitulos",
                column: "saldoparcelaid");

            migrationBuilder.CreateIndex(
                name: "ix_cartoescredito_fornecedorid",
                table: "cartoescredito",
                column: "fornecedorid");

            migrationBuilder.CreateIndex(
                name: "ix_cartoescredito_nome",
                table: "cartoescredito",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contasfinanceiras_nome",
                table: "contasfinanceiras",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fechamentosconta_ajusteid",
                table: "fechamentosconta",
                column: "ajusteid");

            migrationBuilder.CreateIndex(
                name: "ix_fechamentosconta_contafinanceiraid_data",
                table: "fechamentosconta",
                columns: new[] { "contafinanceiraid", "data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentosconta_baixaformaid",
                table: "movimentosconta",
                column: "baixaformaid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentosconta_contafinanceiraid_data",
                table: "movimentosconta",
                columns: new[] { "contafinanceiraid", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_movimentosconta_contrapartidaid",
                table: "movimentosconta",
                column: "contrapartidaid");

            migrationBuilder.AddForeignKey(
                name: "fk_contaspagar_baixaformas_origembaixaformaid",
                table: "contaspagar",
                column: "origembaixaformaid",
                principalTable: "baixaformas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_contaspagar_baixaformas_origembaixaformaid",
                table: "contaspagar");

            migrationBuilder.DropTable(
                name: "baixatitulos");

            migrationBuilder.DropTable(
                name: "fechamentosconta");

            migrationBuilder.DropTable(
                name: "movimentosconta");

            migrationBuilder.DropTable(
                name: "baixaformas");

            migrationBuilder.DropTable(
                name: "baixas");

            migrationBuilder.DropTable(
                name: "cartoescredito");

            migrationBuilder.DropTable(
                name: "contasfinanceiras");

            migrationBuilder.DropIndex(
                name: "ix_contaspagar_origembaixaformaid",
                table: "contaspagar");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "formaspagamento");

            migrationBuilder.DropColumn(
                name: "origembaixaformaid",
                table: "contaspagar");
        }
    }
}
