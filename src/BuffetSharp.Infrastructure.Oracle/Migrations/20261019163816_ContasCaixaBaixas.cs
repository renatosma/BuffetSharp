using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class ContasCaixaBaixas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TIPO",
                table: "FORMASPAGAMENTO",
                type: "NVARCHAR2(30)",
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
                name: "ORIGEMBAIXAFORMAID",
                table: "CONTASPAGAR",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BAIXAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BAIXAS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CARTOESCREDITO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    FINAL = table.Column<string>(type: "NVARCHAR2(4)", maxLength: 4, nullable: true),
                    DIAFECHAMENTO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DIAVENCIMENTO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDORID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARTOESCREDITO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CARTOESCREDITO_FORNECEDORES_FORNECEDORID",
                        column: x => x.FORNECEDORID,
                        principalTable: "FORNECEDORES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTASFINANCEIRAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    TIPO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    BANCO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    AGENCIA = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    NUMERO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    SALDOINICIAL = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    DATASALDOINICIAL = table.Column<DateTime>(type: "DATE", nullable: false),
                    FECHADAATE = table.Column<DateTime>(type: "DATE", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTASFINANCEIRAS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BAIXATITULOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BAIXAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PARCELAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CONTAPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    JUROS = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    DESCONTO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    SALDOPARCELAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SALDOCONTAPAGARID = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BAIXATITULOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_BAIXATITULOS_BAIXAS_BAIXAID",
                        column: x => x.BAIXAID,
                        principalTable: "BAIXAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BAIXATITULOS_CONTASPAGAR_CONTAPAGARID",
                        column: x => x.CONTAPAGARID,
                        principalTable: "CONTASPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BAIXATITULOS_CONTASPAGAR_SALDOCONTAPAGARID",
                        column: x => x.SALDOCONTAPAGARID,
                        principalTable: "CONTASPAGAR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BAIXATITULOS_PARCELAS_PARCELAID",
                        column: x => x.PARCELAID,
                        principalTable: "PARCELAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BAIXATITULOS_PARCELAS_SALDOPARCELAID",
                        column: x => x.SALDOPARCELAID,
                        principalTable: "PARCELAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BAIXAFORMAS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BAIXAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORMAPAGAMENTOID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    CONTAFINANCEIRAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CARTAOCREDITOID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PARCELAS = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BAIXAFORMAS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_BAIXAFORMAS_BAIXAS_BAIXAID",
                        column: x => x.BAIXAID,
                        principalTable: "BAIXAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BAIXAFORMAS_CARTOESCREDITO_CARTAOCREDITOID",
                        column: x => x.CARTAOCREDITOID,
                        principalTable: "CARTOESCREDITO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BAIXAFORMAS_CONTASFINANCEIRAS_CONTAFINANCEIRAID",
                        column: x => x.CONTAFINANCEIRAID,
                        principalTable: "CONTASFINANCEIRAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BAIXAFORMAS_FORMASPAGAMENTO_FORMAPAGAMENTOID",
                        column: x => x.FORMAPAGAMENTOID,
                        principalTable: "FORMASPAGAMENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MOVIMENTOSCONTA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CONTAFINANCEIRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    VALOR = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ORIGEM = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    BAIXAFORMAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CONTRAPARTIDAID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIMENTOSCONTA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MOVIMENTOSCONTA_BAIXAFORMAS_BAIXAFORMAID",
                        column: x => x.BAIXAFORMAID,
                        principalTable: "BAIXAFORMAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MOVIMENTOSCONTA_CONTASFINANCEIRAS_CONTAFINANCEIRAID",
                        column: x => x.CONTAFINANCEIRAID,
                        principalTable: "CONTASFINANCEIRAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MOVIMENTOSCONTA_MOVIMENTOSCONTA_CONTRAPARTIDAID",
                        column: x => x.CONTRAPARTIDAID,
                        principalTable: "MOVIMENTOSCONTA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FECHAMENTOSCONTA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CONTAFINANCEIRAID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "DATE", nullable: false),
                    SALDOSISTEMA = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    SALDOINFORMADO = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    AJUSTEID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CRIADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADOEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FECHAMENTOSCONTA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FECHAMENTOSCONTA_CONTASFINANCEIRAS_CONTAFINANCEIRAID",
                        column: x => x.CONTAFINANCEIRAID,
                        principalTable: "CONTASFINANCEIRAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FECHAMENTOSCONTA_MOVIMENTOSCONTA_AJUSTEID",
                        column: x => x.AJUSTEID,
                        principalTable: "MOVIMENTOSCONTA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONTASPAGAR_ORIGEMBAIXAFORMAID",
                table: "CONTASPAGAR",
                column: "ORIGEMBAIXAFORMAID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXAFORMAS_BAIXAID",
                table: "BAIXAFORMAS",
                column: "BAIXAID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXAFORMAS_CARTAOCREDITOID",
                table: "BAIXAFORMAS",
                column: "CARTAOCREDITOID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXAFORMAS_CONTAFINANCEIRAID",
                table: "BAIXAFORMAS",
                column: "CONTAFINANCEIRAID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXAFORMAS_FORMAPAGAMENTOID",
                table: "BAIXAFORMAS",
                column: "FORMAPAGAMENTOID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXAS_DATA",
                table: "BAIXAS",
                column: "DATA");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXATITULOS_BAIXAID",
                table: "BAIXATITULOS",
                column: "BAIXAID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXATITULOS_CONTAPAGARID",
                table: "BAIXATITULOS",
                column: "CONTAPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXATITULOS_PARCELAID",
                table: "BAIXATITULOS",
                column: "PARCELAID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXATITULOS_SALDOCONTAPAGARID",
                table: "BAIXATITULOS",
                column: "SALDOCONTAPAGARID");

            migrationBuilder.CreateIndex(
                name: "IX_BAIXATITULOS_SALDOPARCELAID",
                table: "BAIXATITULOS",
                column: "SALDOPARCELAID");

            migrationBuilder.CreateIndex(
                name: "IX_CARTOESCREDITO_FORNECEDORID",
                table: "CARTOESCREDITO",
                column: "FORNECEDORID");

            migrationBuilder.CreateIndex(
                name: "IX_CARTOESCREDITO_NOME",
                table: "CARTOESCREDITO",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTASFINANCEIRAS_NOME",
                table: "CONTASFINANCEIRAS",
                column: "NOME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FECHAMENTOSCONTA_AJUSTEID",
                table: "FECHAMENTOSCONTA",
                column: "AJUSTEID");

            migrationBuilder.CreateIndex(
                name: "IX_FECHAMENTOSCONTA_CONTAFINANCEIRAID_DATA",
                table: "FECHAMENTOSCONTA",
                columns: new[] { "CONTAFINANCEIRAID", "DATA" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTOSCONTA_BAIXAFORMAID",
                table: "MOVIMENTOSCONTA",
                column: "BAIXAFORMAID");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTOSCONTA_CONTAFINANCEIRAID_DATA",
                table: "MOVIMENTOSCONTA",
                columns: new[] { "CONTAFINANCEIRAID", "DATA" });

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMENTOSCONTA_CONTRAPARTIDAID",
                table: "MOVIMENTOSCONTA",
                column: "CONTRAPARTIDAID");

            migrationBuilder.AddForeignKey(
                name: "FK_CONTASPAGAR_BAIXAFORMAS_ORIGEMBAIXAFORMAID",
                table: "CONTASPAGAR",
                column: "ORIGEMBAIXAFORMAID",
                principalTable: "BAIXAFORMAS",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONTASPAGAR_BAIXAFORMAS_ORIGEMBAIXAFORMAID",
                table: "CONTASPAGAR");

            migrationBuilder.DropTable(
                name: "BAIXATITULOS");

            migrationBuilder.DropTable(
                name: "FECHAMENTOSCONTA");

            migrationBuilder.DropTable(
                name: "MOVIMENTOSCONTA");

            migrationBuilder.DropTable(
                name: "BAIXAFORMAS");

            migrationBuilder.DropTable(
                name: "BAIXAS");

            migrationBuilder.DropTable(
                name: "CARTOESCREDITO");

            migrationBuilder.DropTable(
                name: "CONTASFINANCEIRAS");

            migrationBuilder.DropIndex(
                name: "IX_CONTASPAGAR_ORIGEMBAIXAFORMAID",
                table: "CONTASPAGAR");

            migrationBuilder.DropColumn(
                name: "TIPO",
                table: "FORMASPAGAMENTO");

            migrationBuilder.DropColumn(
                name: "ORIGEMBAIXAFORMAID",
                table: "CONTASPAGAR");
        }
    }
}
