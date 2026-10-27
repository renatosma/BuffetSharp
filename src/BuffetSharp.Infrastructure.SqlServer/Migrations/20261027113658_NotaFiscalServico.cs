using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class NotaFiscalServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesFiscais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ambiente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    InscricaoMunicipal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodigoMunicipio = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Regime = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ItemListaServico = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CodigoTributarioMunicipio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodigoCnae = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AliquotaIss = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IssRetido = table.Column<bool>(type: "bit", nullable: false),
                    Discriminacao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesFiscais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotasFiscaisServico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Referencia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Ambiente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValorServicos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AliquotaIss = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IssRetido = table.Column<bool>(type: "bit", nullable: false),
                    Discriminacao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TomadorDocumento = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    TomadorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TomadorEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodigoVerificacao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CaminhoXml = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Mensagem = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SolicitadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CanceladaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscaisServico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasFiscaisServico_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisServico_EventoId_Status",
                table: "NotasFiscaisServico",
                columns: new[] { "EventoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisServico_Referencia",
                table: "NotasFiscaisServico",
                column: "Referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisServico_SolicitadaEm",
                table: "NotasFiscaisServico",
                column: "SolicitadaEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesFiscais");

            migrationBuilder.DropTable(
                name: "NotasFiscaisServico");
        }
    }
}
