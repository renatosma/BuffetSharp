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
        var compra = new EntradaEstoqueInput { ProdutoId = produto.Id, FornecedorId = produto.FornecedorId };
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

                ProdutoEmbalagem? Selecionada() =>
                    embalagem.SelectedItem is ItemLookup { Id: > 0 } item ? embalagens.FirstOrDefault(x => x.Id == item.Id) : null;
                void Atualizar() => conversao.Text = Conversao(Selecionada(), campoQuantidade.Value, campoCusto.Value);

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
        editor.Texto("Observação", () => observacao, v => observacao = v, maximo: 300, span: 2,
            dica: tipo == TipoMovimentacao.Entrada ? "Ex.: NF 1234 - Atacadão" : "Motivo da movimentação");

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
                TipoMovimentacao.Entrada => await servico.RegistrarCompraAsync(new EntradaEstoqueInput
                {
                    ProdutoId = produto.Id,
                    FornecedorId = compra.FornecedorId,
                    ProdutoEmbalagemId = compra.ProdutoEmbalagemId,
                    Quantidade = quantidade,
                    CustoUnitario = custo,
                    Observacao = observacao
                }),
                TipoMovimentacao.Saida => await servico.RegistrarSaidaAsync(produto.Id, quantidade, observacao),
                _ => await servico.AjustarSaldoAsync(produto.Id, saldoContado, observacao)
            };
        }, 560);

        if (dialogo.ShowDialog(contexto.Dono) == DialogResult.OK)
            await contexto.Recarregar();

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
