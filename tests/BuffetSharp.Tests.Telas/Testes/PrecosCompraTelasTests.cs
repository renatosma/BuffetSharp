using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Estoque;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Preços de compra: o histórico com o gráfico por fornecedor e o aviso de preço acima do último pago no pedido.</summary>
public class PrecosCompraTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Historico_do_produto_mostra_o_grafico_por_fornecedor() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormPrecosCompra>(principal, "Preços de compra");
            var grade = Robo.Grade(tela);
            robo.Esperar(() => Robo.Linhas<PrecoProdutoResumo>(grade).Any(p => p.Produto == "Filé mignon"), "filé na lista");
            var file = Robo.Linhas<PrecoProdutoResumo>(grade).Single(p => p.Produto == "Filé mignon");
            Assert.True(file.Variacao > 0);
            robo.SelecionarLinha<PrecoProdutoResumo>(grade, p => p.ProdutoId == file.ProdutoId);

            robo.Clicar(tela, "Histórico do produto");
            robo.Esperar(() => Robo.AbaAtual(principal) is FormHistoricoPrecos h && Robo.Carregado(h), "histórico do produto");
            var historico = Robo.AbaAtual(principal)!;
            robo.Esperar(() => Robo.Linhas<PrecoCompra>(Robo.Grade(historico)).Count > 0, "compras do filé");
            Assert.All(Robo.Linhas<PrecoCompra>(Robo.Grade(historico)), c => Assert.Equal(file.ProdutoId, c.ProdutoId));
            var grafico = Robo.Interno<GraficoLinhas>(historico, "_grafico");
            Assert.Equal(["Atacadão Central", "Boi Nobre"], grafico.Series.Select(s => s.Nome));
            using var imagem = new Bitmap(Math.Max(grafico.Width, 400), Math.Max(grafico.Height, 200));
            grafico.DrawToBitmap(imagem, new Rectangle(Point.Empty, imagem.Size)); // desenha sem erro
        });

    [Fact]
    public void Pedido_com_preco_acima_do_ultimo_pago_pede_confirmacao() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var arroz = robo.Rodar(() => robo.Servico<IConsultasLookup>().ListarAsync(TipoLookup.Produtos)).Single(p => p.Nome == "Arroz agulhinha");
            var ultimo = robo.Rodar(() => robo.Servico<PrecoCompraService>().UltimoAsync(arroz.Id))!;
            var tela = robo.AbrirMenu<FormPedidosCompra>(principal, "Pedidos de compra");
            robo.Dialogo<FormEdicao>(() => robo.Clicar(tela, "Novo pedido"), dialogo =>
            {
                var itens = Robo.Descendentes(dialogo).OfType<DataGridView>().Single();
                robo.Clicar(dialogo, "Incluir produto");
                itens.CancelEdit();
                itens.EndEdit();
                itens.Rows[0].Cells[0].Value = arroz.Id; // sugere o custo e mostra o último pago
                Assert.Equal(ultimo.Custo, (decimal?)itens.Rows[0].Cells[5].Value);
                itens.Rows[0].Cells[2].Value = 10m;
                var custo = Math.Round(ultimo.Custo * 1.5m, 2);
                itens.Rows[0].Cells[3].Value = custo;
                itens.Refresh();
                Assert.Equal(PrecoCompraService.Variacao(ultimo.Custo, custo).ToString("+0.0'%';-0.0'%';0'%'", Textos.PtBr),
                    itens.Rows[0].Cells[6].FormattedValue);

                // Desiste na confirmação: o pedido não é gravado e o diálogo continua aberto.
                robo.RespostaConfirmacao = "Não";
                robo.Clicar(dialogo, "Salvar");
                robo.Esperar(() => robo.Mensagens.Any(m => m.Titulo == "Preço acima do último pago"), "aviso de preço");
                Assert.Contains("Arroz agulhinha", robo.Mensagens.Last().Texto);
                Assert.True(dialogo.Visible);

                robo.RespostaConfirmacao = "Sim";
                robo.Clicar(dialogo, "Salvar");
            }, "pedido com preço alto");
            Assert.Equal(2, robo.Mensagens.Count(m => m.Titulo == "Preço acima do último pago"));
        });
}
