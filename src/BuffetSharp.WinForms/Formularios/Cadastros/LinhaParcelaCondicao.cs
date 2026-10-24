using BuffetSharp.Domain.Cadastros;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>Linha editável da grade de parcelas da condição de pagamento (prazo e percentual), com o exemplo calculado.</summary>
internal sealed class LinhaParcelaCondicao
{
    private CondicaoPagamentoParcela? _origem;

    public int Dias { get; set; }
    public decimal Percentual { get; set; }

    /// <summary>Forma desta parcela (0 = a forma geral do pedido ou do evento).</summary>
    public int FormaPagamentoId { get; set; }

    // Somente exibição: ordem pelo prazo e o exemplo (vencimento e valor) recalculados a cada alteração.
    public int Numero { get; set; }
    public string? VenceEm { get; set; }
    public decimal ValorExemplo { get; set; }

    public static LinhaParcelaCondicao De(CondicaoPagamentoParcela parcela) => new()
    {
        _origem = parcela,
        Dias = parcela.Dias,
        Percentual = parcela.Percentual,
        FormaPagamentoId = parcela.FormaPagamentoId ?? 0
    };

    /// <summary>Devolve a parcela do cadastro (a mesma, quando já existia) com os valores da linha.</summary>
    public CondicaoPagamentoParcela ParaParcela()
    {
        var parcela = _origem ?? new CondicaoPagamentoParcela();
        parcela.Dias = Dias;
        parcela.Percentual = Percentual;
        parcela.FormaPagamentoId = FormaPagamentoId > 0 ? FormaPagamentoId : null;
        return parcela;
    }
}
