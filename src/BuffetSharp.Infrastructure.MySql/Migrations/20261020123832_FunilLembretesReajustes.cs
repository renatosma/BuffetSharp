using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BuffetSharp.Infrastructure.MySql.Migrations
{
    /// <inheritdoc />
    public partial class FunilLembretesReajustes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Telefone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Origem = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    DetalheOrigem = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    TipoEventoId = table.Column<int>(type: "int", nullable: true),
                    DataPrevista = table.Column<DateTime>(type: "date", nullable: true),
                    Convidados = table.Column<int>(type: "int", nullable: true),
                    ResponsavelId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ProximoContato = table.Column<DateTime>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    MotivoPerda = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ConvertidoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_TiposEvento_TipoEventoId",
                        column: x => x.TipoEventoId,
                        principalTable: "TiposEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LembretesTratados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Chave = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Canal = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Usuario = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    TratadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LembretesTratados", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReajustesPreco",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Descricao = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Percentual = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    VigenciaEm = table.Column<DateTime>(type: "date", nullable: false),
                    Alcance = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Arredondamento = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Usuario = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    AplicadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DesfeitoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReajustesPreco", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeadInteracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Usuario = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Descricao = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadInteracoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadInteracoes_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReajusteItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ReajustePrecoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ValorAnterior = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorNovo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReajusteItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReajusteItens_ReajustesPreco_ReajustePrecoId",
                        column: x => x.ReajustePrecoId,
                        principalTable: "ReajustesPreco",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LeadInteracoes_LeadId_Data",
                table: "LeadInteracoes",
                columns: new[] { "LeadId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ClienteId",
                table: "Leads",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CriadoEm",
                table: "Leads",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ResponsavelId",
                table: "Leads",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status_ProximoContato",
                table: "Leads",
                columns: new[] { "Status", "ProximoContato" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TipoEventoId",
                table: "Leads",
                column: "TipoEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_LembretesTratados_Chave",
                table: "LembretesTratados",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LembretesTratados_TratadoEm",
                table: "LembretesTratados",
                column: "TratadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_ReajusteItens_ReajustePrecoId_Tipo_ItemId",
                table: "ReajusteItens",
                columns: new[] { "ReajustePrecoId", "Tipo", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReajustesPreco_Status_VigenciaEm",
                table: "ReajustesPreco",
                columns: new[] { "Status", "VigenciaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadInteracoes");

            migrationBuilder.DropTable(
                name: "LembretesTratados");

            migrationBuilder.DropTable(
                name: "ReajusteItens");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "ReajustesPreco");
        }
    }
}
