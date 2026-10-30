using BuffetSharp.Application.Modelos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Impressao;

/// <summary>Documentos da operação: ordem de produção da cozinha e pedido de compra por fornecedor.</summary>
internal static class ImpressaoOperacao
{
    public static void OrdensProducao(IWin32Window dono, IReadOnlyList<OrdemProducao> ordens, string empresa)
    {
        if (ordens.Count == 0)
        {
            Mensagens.Aviso(dono, "Não há evento confirmado para imprimir.");
            return;
        }
        var doc = new DocumentoImpresso(ordens.Count == 1 ? $"Ordem de produção - {ordens[0].Codigo}" : "Ordens de produção", empresa);
        foreach (var ordem in ordens)
            Adicionar(doc, ordem);
        doc.Visualizar(dono);
    }

    private static void Adicionar(DocumentoImpresso doc, OrdemProducao o)
    {
        doc.Secao($"{o.Codigo}  {o.Titulo}");
        doc.Campos(3,
            ("Data", o.Inicio.ToString("dddd, dd/MM/yyyy", Textos.PtBr)),
            ("Horário", $"{o.Inicio:HH:mm} às {o.Fim:HH:mm}"),
            ("Espaço", o.Espaco),
            ("Cliente", o.Cliente),
            ("Pessoas", o.Convidados),
            ("Pacote", o.Pacote ?? "(sem pacote)"));
        if (o.Pratos.Count == 0)
        {
            doc.Paragrafo("Evento sem pacote de cardápio: combine a produção diretamente com o cliente.");
        }
        if (o.Restricoes.Count > 0)
        {
            doc.Paragrafo($"Restrições alimentares ({o.PessoasComRestricao} pessoa(s))", negrito: true);
            doc.Tabela(["Restrição", "Pessoas", "Quem", "No lugar de"],
                o.Restricoes.Select(r => new[]
                {
                    $"{r.Restricao} ({r.Sigla})", r.Pessoas.ToString(), string.Join(", ", r.Quem),
                    r.Trocas.Count == 0 ? "todo o cardápio serve" : string.Join("; ", r.Trocas)
                }).ToList(),
                [1.8f, 0.8f, 3f, 3.4f], [false, true, false, false]);
        }
        if (o.ObservacoesRestricao.Count > 0)
            doc.Paragrafo("Atenção: " + string.Join("; ", o.ObservacoesRestricao));
        foreach (var secao in o.Pratos.GroupBy(p => (p.OrdemSecao, p.Secao)).OrderBy(g => g.Key.OrdemSecao))
        {
            doc.Paragrafo(secao.Key.Secao, negrito: true);
            foreach (var prato in secao)
            {
                doc.Paragrafo($"{prato.Prato} - {prato.Porcoes} porções" + (prato.Substituto ? " (substituto, para restrições)" : ""));
                if (prato.Ingredientes.Count > 0)
                    doc.Tabela(["Insumo", "Quantidade", "Un.", "Separado"],
                        prato.Ingredientes.Select(i => new[] { i.Produto, i.Quantidade.ToString("N3"), i.Unidade, "[   ]" }).ToList(),
                        [4, 1.4f, 0.8f, 1], [false, true, false, false]);
            }
        }
        if (!string.IsNullOrWhiteSpace(o.Observacoes))
        {
            doc.Paragrafo("Observações", negrito: true);
            doc.Paragrafo(o.Observacoes);
        }
        doc.Espaco(18);
    }

    public static void PedidoCompra(IWin32Window dono, ListaCompras lista, IEnumerable<ItemListaCompras> itens, string empresa)
    {
        var doc = new DocumentoImpresso("Lista de compras", empresa);
        doc.Subtitulo($"Eventos de {lista.De:dd/MM/yyyy} a {lista.Ate:dd/MM/yyyy}: {lista.Eventos.Count} evento(s)" +
                      (lista.ReporMinimo ? " • inclui reposição do estoque mínimo" : ""));
        foreach (var fornecedor in itens.GroupBy(i => i.Fornecedor).OrderBy(g => g.Key))
        {
            doc.Secao(fornecedor.Key + (fornecedor.First().TelefoneFornecedor is { } telefone ? $"  •  {telefone}" : ""));
            doc.Tabela(["Produto", "Comprar", "Un.", "Embalagem sugerida", "Custo estimado"],
                fornecedor.Select(i => new[]
                {
                    i.Produto, i.AComprar.ToString("N3"), i.Unidade, i.Embalagem ?? "", Textos.Moeda(i.CustoEstimado)
                }).ToList(), [4, 1.3f, 0.7f, 2.2f, 1.5f], [false, true, false, false, true]);
            doc.Total("Total do fornecedor", Textos.Moeda(fornecedor.Sum(i => i.CustoEstimado)));
        }
        doc.Total("Total estimado", Textos.Moeda(itens.Sum(i => i.CustoEstimado)));
        doc.Visualizar(dono);
    }

    /// <summary>Texto do pedido para o fornecedor (WhatsApp ou e-mail).</summary>
    public static string TextoPedido(IEnumerable<ItemListaCompras> itens, string empresa) =>
        $"Olá! Aqui é do *{empresa}*. Gostaria de fazer o seguinte pedido:\n\n" +
        string.Join("\n", itens.Select(i => $"• {i.Produto}: {i.Embalagem ?? $"{i.AComprar:0.###} {i.Unidade}"}")) +
        "\n\nPode me confirmar o valor e o prazo de entrega? Obrigado!";
}
