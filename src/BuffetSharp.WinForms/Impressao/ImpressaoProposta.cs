using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>Proposta comercial impressa a partir do orçamento.</summary>
internal static class ImpressaoProposta
{
    public static async Task VisualizarAsync(IWin32Window dono, OrcamentoInput o, IConsultasLookup lookups,
        IConsultasEstoqueCardapio cardapio, BuffetOptions empresa, CondicaoPagamento? condicao = null) =>
        (await MontarAsync(o, lookups, cardapio, empresa, condicao)).Visualizar(dono);

    /// <summary>Monta o documento (para visualizar ou gerar o PDF anexado ao e-mail).</summary>
    /// <param name="condicao">Condição de pagamento do orçamento (impressa com a simulação das parcelas).</param>
    public static async Task<DocumentoImpresso> MontarAsync(OrcamentoInput o, IConsultasLookup lookups,
        IConsultasEstoqueCardapio cardapio, BuffetOptions empresa, CondicaoPagamento? condicao = null)
    {
        string Nome(IReadOnlyList<ItemLookup> itens, int? id) => itens.FirstOrDefault(i => i.Id == id)?.Nome ?? "-";

        var clientes = await lookups.ListarAsync(TipoLookup.Clientes, somenteAtivos: false);
        var tipos = await lookups.ListarAsync(TipoLookup.TiposEvento, somenteAtivos: false);
        var pacotes = await lookups.ListarAsync(TipoLookup.Pacotes, somenteAtivos: false);
        var espacos = await lookups.ListarAsync(TipoLookup.Espacos, somenteAtivos: false);
        var composicao = o.PacoteId is { } pacoteId ? await cardapio.ComposicaoPacoteAsync(pacoteId) : [];

        var valorPessoa = o.ValorPorPessoa ?? 0;
        var valorEspaco = o.ValorEspaco ?? 0;
        var resumo = CalculadoraValores.Calcular(valorPessoa, o.Convidados, valorEspaco, o.Servicos.Sum(s => s.Total), o.PercentualDesconto);

        var doc = new DocumentoImpresso($"Proposta comercial {o.Codigo}", empresa.NomeEmpresa);
        doc.Subtitulo(string.Join("  •  ", new[] { empresa.Documento, empresa.Telefone, empresa.Email, empresa.Endereco }
            .Where(s => !string.IsNullOrWhiteSpace(s))));
        doc.Espaco(6);
        doc.Secao("Dados do evento");
        doc.Campos(3,
            ("Cliente", Nome(clientes, o.ClienteId)),
            ("Tipo de evento", Nome(tipos, o.TipoEventoId)),
            ("Convidados", $"{o.Convidados} pessoas"),
            ("Data", o.Data.ToString("dddd, dd/MM/yyyy", Textos.PtBr)),
            ("Horário", $"{o.HoraInicio:HH\\:mm} às {o.HoraFim:HH\\:mm}"),
            ("Espaço", Nome(espacos, o.EspacoId)));

        if (composicao.Count > 0)
        {
            doc.Secao($"Cardápio - pacote {Nome(pacotes, o.PacoteId)}");
            foreach (var grupo in composicao.GroupBy(c => c.Categoria))
            {
                doc.Paragrafo(grupo.Key, negrito: true);
                doc.Paragrafo(string.Join("  •  ", grupo.Select(i => i.Item)));
            }
        }

        if (o.Servicos.Count > 0)
        {
            doc.Secao("Serviços adicionais");
            doc.Tabela(["Serviço", "Quantidade", "Valor unitário", "Total"],
                o.Servicos.Select(s => new[] { s.Descricao, s.Quantidade.ToString("N0"), Textos.Moeda(s.ValorUnitario), Textos.Moeda(s.Total) }).ToList(),
                [4, 1.2f, 1.6f, 1.6f], [false, true, true, true]);
        }

        doc.Secao("Investimento");
        var linhas = new List<string[]>
        {
            new[] { $"Alimentação ({o.Convidados} × {Textos.Moeda(valorPessoa)})", Textos.Moeda(resumo.Alimentacao) },
            new[] { "Locação do espaço", Textos.Moeda(resumo.Espaco) },
            new[] { "Serviços adicionais", Textos.Moeda(resumo.Servicos) }
        };
        if (resumo.Desconto > 0)
            linhas.Add([$"Desconto ({o.PercentualDesconto:0.##}%)", "- " + Textos.Moeda(resumo.Desconto)]);
        doc.Tabela(["Descrição", "Valor"], linhas, [4, 1.6f], [false, true]);
        doc.Total("Valor total do evento:", Textos.Moeda(resumo.Total));
        doc.Paragrafo($"Equivale a {Textos.Moeda(o.Convidados == 0 ? 0 : resumo.Total / o.Convidados)} por convidado.");

        doc.Secao("Condições");
        if (condicao is not null)
        {
            doc.Paragrafo($"Condição de pagamento: {condicao.Texto}", negrito: true);
            var simulacao = TextosPagamento.Simulacao(condicao, resumo.Total, DateOnly.FromDateTime(DateTime.Today), o.Data);
            if (simulacao.Length > 0)
                doc.Paragrafo(simulacao + ".");
        }
        doc.Paragrafo($"Proposta válida até {o.ValidoAte:dd/MM/yyyy}. A reserva da data é confirmada mediante assinatura do contrato " +
                      "e pagamento da entrada. Quantidade final de convidados deve ser confirmada com 10 dias de antecedência.");
        if (!string.IsNullOrWhiteSpace(o.Observacoes))
            doc.Paragrafo("Observações: " + o.Observacoes);
        doc.Espaco(30);
        doc.Assinaturas(empresa.NomeEmpresa, Nome(clientes, o.ClienteId));
        return doc;
    }
}
