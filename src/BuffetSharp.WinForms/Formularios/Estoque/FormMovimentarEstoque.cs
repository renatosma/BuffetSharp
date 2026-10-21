using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.WinForms.Formularios.Cadastros;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Estoque;

/// <summary>Entrada, saída manual ou inventário (ajuste) de um produto.</summary>
internal static class FormMovimentarEstoque
{
    public static async Task AbrirAsync(ContextoCadastro contexto, Produto produto, TipoMovimentacao tipo)
    {
        var servico = contexto.Navegador.Servico<EstoqueService>();
        var quantidade = 0m;
        var custo = produto.CustoMedio;
        var saldoContado = produto.EstoqueAtual;
        string? observacao = null;
        var unidade = produto.UnidadeMedida?.Sigla ?? "";

        var editor = new EditorCampos();
        editor.Informacao("Produto", () => produto.Nome, span: 2);
        editor.Informacao("Saldo atual", () => $"{produto.EstoqueAtual:N3} {unidade}");
        editor.Informacao("Custo médio atual", () => produto.CustoMedio.ToString("C4"));

        // Entrada: compra na unidade do fornecedor (caixa, fardo...) convertida para a unidade do estoque.
        var compra = new EntradaEstoqueInput
        {
            ProdutoId = produto.Id,
            FornecedorId = produto.FornecedorId,
            GerarContaPagar = true,
            Vencimento = DateOnly.FromDateTime(DateTime.Today)
        };
        List<ProdutoEmbalagem> embalagens = [];
        switch (tipo)
        {
            case TipoMovimentacao.Entrada:
                embalagens = await servico.ListarEmbalagensAsync(produto.Id);
                var fornecedores = await contexto.Navegador.Servico<IConsultasLookup>().ListarAsync(TipoLookup.Fornecedores);
                var fornecedor = editor.Lista("Fornecedor", fornecedores, () => compra.FornecedorId, v => compra.FornecedorId = v,
                    permitirVazio: true);
                var embalagem = editor.Lista("Unidade de compra", Unidades(compra.FornecedorId), () => compra.ProdutoEmbalagemId,
                    v => compra.ProdutoEmbalagemId = v);
                var campoQuantidade = editor.Decimal("Quantidade (na unidade de compra)", () => quantidade, v => quantidade = v, casas: 3);
                var campoCusto = editor.Decimal("Custo por unidade de compra (R$)", () => custo, v => custo = v, casas: 4);
                var conversao = editor.Informacao("Entra no estoque", () => Conversao(null, quantidade, custo), span: 2);
                editor.Texto("Observação", () => observacao, v => observacao = v, maximo: 300, span: 2,
                    dica: "Ex.: reposição para os eventos da semana");

                // Conta a pagar da compra: gravada junto com a entrada (conta única ou compra parcelada).
                editor.Secao("Conta a pagar");
                var gerar = editor.Marcador("Lançar a compra em contas a pagar", () => compra.GerarContaPagar, v => compra.GerarContaPagar = v, span: 2);
                var valorCompra = editor.Informacao("Valor da compra", () => Textos.Moeda(Math.Round(quantidade * custo, 2)));
                var categoria = editor.Opcoes("Categoria", () => compra.Categoria, v => compra.Categoria = v);
                var documento = editor.Texto("Documento (NF / boleto)", () => compra.Documento, v => compra.Documento = v,
                    maximo: Domain.Financeiro.ContaPagar.TamanhoDocumento, dica: "Ex.: NF 1234");
                var vencimento = editor.Data("Vencimento (1ª parcela)", () => compra.Vencimento, v => compra.Vencimento = v);
                var parcelas = editor.Inteiro("Parcelas", () => compra.Parcelas, v => compra.Parcelas = v, minimo: 1, maximo: 24);
                var intervalo = editor.Inteiro("Dias entre parcelas (0 = mensal)", () => compra.IntervaloDias ?? 0,
                    v => compra.IntervaloDias = v > 0 ? v : null, maximo: 365);
                Control[] camposConta = [categoria, documento, vencimento, parcelas];
                void HabilitarConta()
                {
                    foreach (var campo in camposConta)
                        campo.Enabled = gerar.Checked;
                    intervalo.Enabled = gerar.Checked && parcelas.Value > 1;
                }
                gerar.CheckedChanged += (_, _) => HabilitarConta();
                parcelas.ValueChanged += (_, _) => HabilitarConta();
                editor.AoCarregar(HabilitarConta);

                ProdutoEmbalagem? Selecionada() =>
                    embalagem.SelectedItem is ItemLookup { Id: > 0 } item ? embalagens.FirstOrDefault(x => x.Id == item.Id) : null;
                void Atualizar()
                {
                    conversao.Text = Conversao(Selecionada(), campoQuantidade.Value, campoCusto.Value);
                    valorCompra.Text = Textos.Moeda(Math.Round(campoQuantidade.Value * campoCusto.Value, 2));
                }

                fornecedor.SelectionChangeCommitted += (_, _) =>
                {
                    var atual = (embalagem.SelectedItem as ItemLookup)?.Id ?? 0;
                    var fornecedorId = (fornecedor.SelectedItem as ItemLookup)?.Id;
                    var opcoes = Unidades(fornecedorId > 0 ? fornecedorId : null);
                    embalagem.DataSource = opcoes;
                    embalagem.SelectedIndex = Math.Max(0, opcoes.FindIndex(o => o.Id == atual));
                    campoCusto.Value = Math.Clamp(Math.Round(produto.CustoMedio * (Selecionada()?.Fator ?? 1), 4), campoCusto.Minimum, campoCusto.Maximum);
                    Atualizar();
                };
                embalagem.SelectionChangeCommitted += (_, _) =>
                {
                    campoCusto.Value = Math.Clamp(Math.Round(produto.CustoMedio * (Selecionada()?.Fator ?? 1), 4), campoCusto.Minimum, campoCusto.Maximum);
                    Atualizar();
                };
                campoQuantidade.ValueChanged += (_, _) => Atualizar();
                campoCusto.ValueChanged += (_, _) => Atualizar();
                break;
            case TipoMovimentacao.Saida:
                editor.Decimal($"Quantidade ({unidade})", () => quantidade, v => quantidade = v, casas: 3);
                break;
            default:
                editor.Decimal($"Saldo contado no inventário ({unidade})", () => saldoContado, v => saldoContado = v, casas: 3, span: 2);
                break;
        }
        if (tipo != TipoMovimentacao.Entrada)
            editor.Texto("Observação", () => observacao, v => observacao = v, maximo: 300, span: 2, dica: "Motivo da movimentação");

        var titulo = tipo switch
        {
            TipoMovimentacao.Entrada => "Entrada de estoque",
            TipoMovimentacao.Saida => "Saída de estoque",
            _ => "Inventário (ajuste de saldo)"
        };

        using var dialogo = new FormEdicao(titulo, "A movimentação fica registrada no histórico do produto.", editor, async () =>
        {
            _ = tipo switch
            {
                TipoMovimentacao.Entrada => await servico.RegistrarCompraAsync(Compra()),
                TipoMovimentacao.Saida => await servico.RegistrarSaidaAsync(produto.Id, quantidade, observacao),
                _ => await servico.AjustarSaldoAsync(produto.Id, saldoContado, observacao)
            };
        }, 560);

        if (dialogo.ShowDialog(contexto.Dono) == DialogResult.OK)
            await contexto.Recarregar();

        EntradaEstoqueInput Compra()
        {
            compra.Quantidade = quantidade;
            compra.CustoUnitario = custo;
            compra.Observacao = observacao;
            return compra;
        }

        // Unidade do estoque + embalagens do fornecedor escolhido (e as que valem para qualquer fornecedor).
        List<ItemLookup> Unidades(int? fornecedorId) =>
        [
            new ItemLookup { Id = 0, Nome = $"{unidade} (unidade do estoque)" },
            .. embalagens.Where(x => x.FornecedorId is null || x.FornecedorId == fornecedorId).Select(x => new ItemLookup
            {
                Id = x.Id,
                Nome = $"{x.UnidadeMedida?.Sigla} com {x.Fator:0.####} {unidade}" +
                       (string.IsNullOrWhiteSpace(x.Descricao) ? "" : $" - {x.Descricao}") +
                       (x.FornecedorId is null ? " (qualquer fornecedor)" : "")
            })
        ];

        string Conversao(ProdutoEmbalagem? embalagemEscolhida, decimal quantidadeCompra, decimal custoCompra)
        {
            var fator = embalagemEscolhida?.Fator ?? 1;
            return $"{quantidadeCompra * fator:N3} {unidade} a {(fator == 0 ? 0 : custoCompra / fator):C4} por {unidade}";
        }
    }
}
