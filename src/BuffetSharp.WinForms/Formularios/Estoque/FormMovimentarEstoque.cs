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

        switch (tipo)
        {
            case TipoMovimentacao.Entrada:
                editor.Decimal($"Quantidade ({unidade})", () => quantidade, v => quantidade = v, casas: 3);
                editor.Decimal("Custo unitário (R$)", () => custo, v => custo = v, casas: 4);
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
                TipoMovimentacao.Entrada => await servico.RegistrarEntradaAsync(produto.Id, quantidade, custo, observacao),
                TipoMovimentacao.Saida => await servico.RegistrarSaidaAsync(produto.Id, quantidade, observacao),
                _ => await servico.AjustarSaldoAsync(produto.Id, saldoContado, observacao)
            };
        }, 560);

        if (dialogo.ShowDialog(contexto.Dono) == DialogResult.OK)
            await contexto.Recarregar();
    }
}
