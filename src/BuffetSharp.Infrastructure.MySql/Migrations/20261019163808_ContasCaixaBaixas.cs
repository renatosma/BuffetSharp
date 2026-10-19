using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ContasCaixaBaixas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "FormasPagamento",
                type: "varchar(30)",
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
                name: "OrigemBaixaFormaId",
                table: "ContasPagar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Baixas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    Observacao = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baixas", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CartoesCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Final = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true),
                    DiaFechamento = table.Column<int>(type: "int", nullable: false),
                    DiaVencimento = table.Column<int>(type: "int", nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartoesCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartoesCredito_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ContasFinanceiras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Banco = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Agencia = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Numero = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    SaldoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DataSaldoInicial = table.Column<DateTime>(type: "date", nullable: false),
                    FechadaAte = table.Column<DateTime>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasFinanceiras", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BaixaTitulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BaixaId = table.Column<int>(type: "int", nullable: false),
                    ParcelaId = table.Column<int>(type: "int", nullable: true),
                    ContaPagarId = table.Column<int>(type: "int", nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Juros = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Desconto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoParcelaId = table.Column<int>(type: "int", nullable: true),
                    SaldoContaPagarId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaixaTitulos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaixaTitulos_Baixas_BaixaId",
                        column: x => x.BaixaId,
                        principalTable: "Baixas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaixaTitulos_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaixaTitulos_ContasPagar_SaldoContaPagarId",
                        column: x => x.SaldoContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BaixaTitulos_Parcelas_ParcelaId",
                        column: x => x.ParcelaId,
                        principalTable: "Parcelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaixaTitulos_Parcelas_SaldoParcelaId",
                        column: x => x.SaldoParcelaId,
                        principalTable: "Parcelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BaixaFormas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BaixaId = table.Column<int>(type: "int", nullable: false),
                    FormaPagamentoId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContaFinanceiraId = table.Column<int>(type: "int", nullable: true),
                    CartaoCreditoId = table.Column<int>(type: "int", nullable: true),
                    Parcelas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaixaFormas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaixaFormas_Baixas_BaixaId",
                        column: x => x.BaixaId,
                        principalTable: "Baixas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaixaFormas_CartoesCredito_CartaoCreditoId",
                        column: x => x.CartaoCreditoId,
                        principalTable: "CartoesCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaixaFormas_ContasFinanceiras_ContaFinanceiraId",
                        column: x => x.ContaFinanceiraId,
                        principalTable: "ContasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaixaFormas_FormasPagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "FormasPagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MovimentosConta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ContaFinanceiraId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descricao = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Origem = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    BaixaFormaId = table.Column<int>(type: "int", nullable: true),
                    ContrapartidaId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosConta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosConta_BaixaFormas_BaixaFormaId",
                        column: x => x.BaixaFormaId,
                        principalTable: "BaixaFormas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimentosConta_ContasFinanceiras_ContaFinanceiraId",
                        column: x => x.ContaFinanceiraId,
                        principalTable: "ContasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosConta_MovimentosConta_ContrapartidaId",
                        column: x => x.ContrapartidaId,
                        principalTable: "MovimentosConta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FechamentosConta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ContaFinanceiraId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    SaldoSistema = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoInformado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    AjusteId = table.Column<int>(type: "int", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosConta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosConta_ContasFinanceiras_ContaFinanceiraId",
                        column: x => x.ContaFinanceiraId,
                        principalTable: "ContasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosConta_MovimentosConta_AjusteId",
                        column: x => x.AjusteId,
                        principalTable: "MovimentosConta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_OrigemBaixaFormaId",
                table: "ContasPagar",
                column: "OrigemBaixaFormaId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaFormas_BaixaId",
                table: "BaixaFormas",
                column: "BaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaFormas_CartaoCreditoId",
                table: "BaixaFormas",
                column: "CartaoCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaFormas_ContaFinanceiraId",
                table: "BaixaFormas",
                column: "ContaFinanceiraId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaFormas_FormaPagamentoId",
                table: "BaixaFormas",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Baixas_Data",
                table: "Baixas",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaTitulos_BaixaId",
                table: "BaixaTitulos",
                column: "BaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaTitulos_ContaPagarId",
                table: "BaixaTitulos",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaTitulos_ParcelaId",
                table: "BaixaTitulos",
                column: "ParcelaId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaTitulos_SaldoContaPagarId",
                table: "BaixaTitulos",
                column: "SaldoContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_BaixaTitulos_SaldoParcelaId",
                table: "BaixaTitulos",
                column: "SaldoParcelaId");

            migrationBuilder.CreateIndex(
                name: "IX_CartoesCredito_FornecedorId",
                table: "CartoesCredito",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_CartoesCredito_Nome",
                table: "CartoesCredito",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContasFinanceiras_Nome",
                table: "ContasFinanceiras",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosConta_AjusteId",
                table: "FechamentosConta",
                column: "AjusteId");

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosConta_ContaFinanceiraId_Data",
                table: "FechamentosConta",
                columns: new[] { "ContaFinanceiraId", "Data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosConta_BaixaFormaId",
                table: "MovimentosConta",
                column: "BaixaFormaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosConta_ContaFinanceiraId_Data",
                table: "MovimentosConta",
                columns: new[] { "ContaFinanceiraId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosConta_ContrapartidaId",
                table: "MovimentosConta",
                column: "ContrapartidaId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContasPagar_BaixaFormas_OrigemBaixaFormaId",
                table: "ContasPagar",
                column: "OrigemBaixaFormaId",
                principalTable: "BaixaFormas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContasPagar_BaixaFormas_OrigemBaixaFormaId",
                table: "ContasPagar");

            migrationBuilder.DropTable(
                name: "BaixaTitulos");

            migrationBuilder.DropTable(
                name: "FechamentosConta");

            migrationBuilder.DropTable(
                name: "MovimentosConta");

            migrationBuilder.DropTable(
                name: "BaixaFormas");

            migrationBuilder.DropTable(
                name: "Baixas");

            migrationBuilder.DropTable(
                name: "CartoesCredito");

            migrationBuilder.DropTable(
                name: "ContasFinanceiras");

            migrationBuilder.DropIndex(
                name: "IX_ContasPagar_OrigemBaixaFormaId",
                table: "ContasPagar");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "FormasPagamento");

            migrationBuilder.DropColumn(
                name: "OrigemBaixaFormaId",
                table: "ContasPagar");
        }
    }
}
