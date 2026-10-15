using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuffetSharp.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class NovasFuncionalidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PercentualComissao",
                table: "Usuarios",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VendedorId",
                table: "Orcamentos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualComissao",
                table: "Eventos",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VendedorId",
                table: "Eventos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Entidade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RegistroId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Alteracoes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contratos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    Conteudo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashConteudo = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EmitidoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssinadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NomeSignatario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DocumentoSignatario = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    AssinaturaCliente = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    NomeRepresentante = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AssinaturaEmpresa = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contratos_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Degustacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    PacoteId = table.Column<int>(type: "int", nullable: true),
                    OrcamentoId = table.Column<int>(type: "int", nullable: true),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Pessoas = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Avaliacao = table.Column<int>(type: "int", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Degustacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Degustacoes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Degustacoes_Orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalTable: "Orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Degustacoes_Pacotes_PacoteId",
                        column: x => x.PacoteId,
                        principalTable: "Pacotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnviosProposta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrcamentoId = table.Column<int>(type: "int", nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EnviadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosProposta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosProposta_Orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalTable: "Orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FechamentosComissao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendedorId = table.Column<int>(type: "int", nullable: false),
                    De = table.Column<DateOnly>(type: "date", nullable: false),
                    Ate = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseCalculo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContaPagarId = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosComissao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosComissao_ContasPagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "ContasPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosComissao_Usuarios_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Materiais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    QuantidadePropria = table.Column<int>(type: "int", nullable: false),
                    ValorReposicao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustoLocacao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FornecedorId = table.Column<int>(type: "int", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materiais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materiais_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventoMateriais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    QuantidadeAlugada = table.Column<int>(type: "int", nullable: false),
                    CustoLocacaoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorReposicaoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeDevolvida = table.Column<int>(type: "int", nullable: true),
                    QuantidadePerdida = table.Column<int>(type: "int", nullable: false),
                    DevolvidoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoMateriais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoMateriais_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventoMateriais_Materiais_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_VendedorId",
                table: "Orcamentos",
                column: "VendedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_VendedorId",
                table: "Eventos",
                column: "VendedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_DataHora",
                table: "Auditoria",
                column: "DataHora");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Entidade_RegistroId",
                table: "Auditoria",
                columns: new[] { "Entidade", "RegistroId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_EventoId",
                table: "Contratos",
                column: "EventoId",
                unique: true,
                filter: "[Status] <> 'Cancelado'");

            migrationBuilder.CreateIndex(
                name: "IX_Degustacoes_ClienteId",
                table: "Degustacoes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Degustacoes_DataHora",
                table: "Degustacoes",
                column: "DataHora");

            migrationBuilder.CreateIndex(
                name: "IX_Degustacoes_OrcamentoId",
                table: "Degustacoes",
                column: "OrcamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Degustacoes_PacoteId",
                table: "Degustacoes",
                column: "PacoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Degustacoes_Status_DataHora",
                table: "Degustacoes",
                columns: new[] { "Status", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosProposta_OrcamentoId_EnviadoEm",
                table: "EnviosProposta",
                columns: new[] { "OrcamentoId", "EnviadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_EventoMateriais_EventoId_MaterialId",
                table: "EventoMateriais",
                columns: new[] { "EventoId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventoMateriais_MaterialId",
                table: "EventoMateriais",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosComissao_ContaPagarId",
                table: "FechamentosComissao",
                column: "ContaPagarId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosComissao_VendedorId_De_Ate",
                table: "FechamentosComissao",
                columns: new[] { "VendedorId", "De", "Ate" });

            migrationBuilder.CreateIndex(
                name: "IX_Materiais_FornecedorId",
                table: "Materiais",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Materiais_Nome",
                table: "Materiais",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Eventos_Usuarios_VendedorId",
                table: "Eventos",
                column: "VendedorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orcamentos_Usuarios_VendedorId",
                table: "Orcamentos",
                column: "VendedorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Eventos_Usuarios_VendedorId",
                table: "Eventos");

            migrationBuilder.DropForeignKey(
                name: "FK_Orcamentos_Usuarios_VendedorId",
                table: "Orcamentos");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Contratos");

            migrationBuilder.DropTable(
                name: "Degustacoes");

            migrationBuilder.DropTable(
                name: "EnviosProposta");

            migrationBuilder.DropTable(
                name: "EventoMateriais");

            migrationBuilder.DropTable(
                name: "FechamentosComissao");

            migrationBuilder.DropTable(
                name: "Materiais");

            migrationBuilder.DropIndex(
                name: "IX_Orcamentos_VendedorId",
                table: "Orcamentos");

            migrationBuilder.DropIndex(
                name: "IX_Eventos_VendedorId",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "PercentualComissao",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VendedorId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "PercentualComissao",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "VendedorId",
                table: "Eventos");
        }
    }
}
