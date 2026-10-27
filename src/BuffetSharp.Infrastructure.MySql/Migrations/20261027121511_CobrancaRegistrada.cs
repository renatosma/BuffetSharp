using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class CobrancaRegistrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CobrancasRegistradas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ParcelaId = table.Column<int>(type: "int", nullable: true),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Ambiente = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Vencimento = table.Column<DateTime>(type: "date", nullable: false),
                    Referencia = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    IdExterno = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    ClienteExterno = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    LinhaDigitavel = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    NossoNumero = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    UrlBoleto = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    UrlFatura = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    PixCopiaECola = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true),
                    DataPagamento = table.Column<DateTime>(type: "date", nullable: true),
                    ValorPago = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValorLiquido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PagaCom = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Baixada = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Mensagem = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    SolicitadaEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CanceladaEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrancasRegistradas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CobrancasRegistradas_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CobrancasRegistradas_Parcelas_ParcelaId",
                        column: x => x.ParcelaId,
                        principalTable: "Parcelas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConfiguracoesCobranca",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Ambiente = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    TipoPadrao = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ContaFinanceiraId = table.Column<int>(type: "int", nullable: true),
                    MultaPercentual = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    JurosMensalPercentual = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesCobranca", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracoesCobranca_ContasFinanceiras_ContaFinanceiraId",
                        column: x => x.ContaFinanceiraId,
                        principalTable: "ContasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasRegistradas_EventoId",
                table: "CobrancasRegistradas",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasRegistradas_ParcelaId",
                table: "CobrancasRegistradas",
                column: "ParcelaId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasRegistradas_Referencia",
                table: "CobrancasRegistradas",
                column: "Referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasRegistradas_SolicitadaEm",
                table: "CobrancasRegistradas",
                column: "SolicitadaEm");

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasRegistradas_Status_ParcelaId",
                table: "CobrancasRegistradas",
                columns: new[] { "Status", "ParcelaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesCobranca_ContaFinanceiraId",
                table: "ConfiguracoesCobranca",
                column: "ContaFinanceiraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CobrancasRegistradas");

            migrationBuilder.DropTable(
                name: "ConfiguracoesCobranca");
        }
    }
}
