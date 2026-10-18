using System.Globalization;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Monta o texto do contrato a partir de um modelo (texto com campos {{...}}) e dos dados do evento, do cliente, do
/// cardápio e do plano de pagamento. Requer o evento com cliente, tipo, espaço, pacote, serviços, parcelas (com a
/// forma de pagamento) e condição de pagamento carregados.
/// </summary>
public static class GeradorContrato
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Modelo usado quando nenhum foi cadastrado (é também o "Contrato padrão" criado na instalação).</summary>
    public const string ModeloPadrao = """
        CONTRATO DE PRESTAÇÃO DE SERVIÇOS DE BUFFET
        CONTRATADA: {{CONTRATADA}}.

        CONTRATANTE: {{CONTRATANTE}}.

        As partes acima identificadas têm entre si justo e contratado o presente instrumento, que se regerá pelas cláusulas e condições seguintes.

        CLÁUSULA {{N}}ª - DO OBJETO
        O presente contrato tem por objeto a prestação de serviços de buffet para o evento "{{EVENTO_TITULO}}" ({{TIPO_EVENTO}}), registrado sob o código {{EVENTO_CODIGO}}, conforme as especificações deste instrumento.

        CLÁUSULA {{N}}ª - DA DATA, DO HORÁRIO E DO LOCAL
        O evento será realizado em {{DATA_EVENTO_EXTENSO}}, das {{HORA_INICIO}} às {{HORA_FIM}}, no ambiente "{{ESPACO}}" ({{LOCAL_ENDERECO}}), cuja capacidade máxima é de {{CAPACIDADE}} pessoas. A prorrogação do horário dependerá da disponibilidade do espaço e será cobrada à parte.

        CLÁUSULA {{N}}ª - DOS CONVIDADOS
        Os serviços foram contratados para {{CONVIDADOS}} convidados. A CONTRATANTE deverá confirmar o número final de convidados com antecedência mínima de {{DIAS_CONFIRMACAO}} dias da data do evento. {{EXCEDENTES}}

        CLÁUSULA {{N}}ª - DO CARDÁPIO
        {{CARDAPIO}}

        CLÁUSULA {{N}}ª - DOS SERVIÇOS ADICIONAIS
        {{SERVICOS}}

        CLÁUSULA {{N}}ª - DO PREÇO E DA FORMA DE PAGAMENTO
        Pelos serviços contratados, a CONTRATANTE pagará à CONTRATADA o valor total de {{VALOR_TOTAL}} ({{VALOR_TOTAL_EXTENSO}}). Condição de pagamento: {{CONDICAO_PAGAMENTO}}.
        {{PARCELAS}}
        O atraso no pagamento sujeitará a CONTRATANTE à multa de 2% e juros de mora de 1% ao mês sobre o valor em atraso.

        CLÁUSULA {{N}}ª - DA DESISTÊNCIA E DA RESCISÃO
        Em caso de desistência pela CONTRATANTE, a título de compensação pela reserva da data, a CONTRATADA reterá: 20% do valor total, se a desistência ocorrer com mais de 90 dias de antecedência; 50%, entre 90 e 30 dias; e 100%, com menos de 30 dias. A comunicação deverá ser feita por escrito.

        CLÁUSULA {{N}}ª - DAS OBRIGAÇÕES DA CONTRATADA
        Fornecer os alimentos e bebidas do cardápio contratado observando as normas sanitárias vigentes; disponibilizar a equipe necessária ao bom atendimento dos convidados; entregar o espaço limpo, montado e decorado conforme contratado no horário do evento.

        CLÁUSULA {{N}}ª - DAS OBRIGAÇÕES DA CONTRATANTE
        Efetuar os pagamentos nas datas acordadas; respeitar o horário e a capacidade do espaço; zelar pelo espaço, mobiliário, louças, talheres, taças e enxoval, respondendo pelos danos e extravios causados por seus convidados, que serão cobrados pelo valor de reposição.

        CLÁUSULA {{N}}ª - DO FORO
        Fica eleito o foro da comarca de {{FORO}} para dirimir quaisquer questões oriundas deste contrato, com renúncia a qualquer outro, por mais privilegiado que seja.

        E, por estarem de acordo, as partes assinam o presente instrumento.

        {{CIDADE}}, {{DATA_HOJE}}.
        """;

    /// <summary>Ponto de partida de um contrato escrito do zero: só as partes, o objeto e o fechamento.</summary>
    public const string ModeloEmBranco = """
        CONTRATO DE PRESTAÇÃO DE SERVIÇOS
        CONTRATADA: {{CONTRATADA}}.

        CONTRATANTE: {{CONTRATANTE}}.

        CLÁUSULA {{N}}ª - DO OBJETO
        Prestação de serviços para o evento "{{EVENTO_TITULO}}" em {{DATA_EVENTO}}, das {{HORA_INICIO}} às {{HORA_FIM}}, pelo valor total de {{VALOR_TOTAL}} ({{VALOR_TOTAL_EXTENSO}}).

        CLÁUSULA {{N}}ª - (TÍTULO)
        (Escreva aqui as demais condições.)

        E, por estarem de acordo, as partes assinam o presente instrumento.

        {{CIDADE}}, {{DATA_HOJE}}.
        """;

    private static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    /// <param name="modelo">Texto do modelo com os campos; nulo usa o <see cref="ModeloPadrao"/>.</param>
    public static string Gerar(BuffetOptions empresa, Evento evento, IReadOnlyList<ItemComposicao> cardapio, DateOnly hoje,
        string? modelo = null) =>
        CamposContrato.Substituir(modelo ?? ModeloPadrao, Valores(empresa, evento, cardapio, hoje));

    /// <summary>Valor de cada campo do modelo para o evento informado.</summary>
    public static Dictionary<string, string> Valores(BuffetOptions empresa, Evento evento, IReadOnlyList<ItemComposicao> cardapio,
        DateOnly hoje)
    {
        var cliente = evento.Cliente!;
        var espaco = evento.Espaco;
        var enderecoEspaco = espaco?.Endereco.ToString();
        var parcelas = evento.Parcelas.Where(p => p.Status != StatusTitulo.Cancelado).OrderBy(p => p.Vencimento).ToList();

        var contratada = empresa.NomeEmpresa +
                         (string.IsNullOrWhiteSpace(empresa.Documento) ? "" : $", inscrita no CNPJ sob o nº {empresa.Documento}") +
                         (string.IsNullOrWhiteSpace(empresa.Endereco) ? "" : $", com sede em {empresa.Endereco}") +
                         (string.IsNullOrWhiteSpace(empresa.Telefone) ? "" : $", telefone {empresa.Telefone}") +
                         (string.IsNullOrWhiteSpace(empresa.Email) ? "" : $", e-mail {empresa.Email}");

        var itensCardapio = string.Join("\n", cardapio.GroupBy(c => c.Categoria)
            .Select(g => $"• {g.Key}: {string.Join(", ", g.Select(i => i.Item))}"));

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CONTRATADA"] = contratada,
            ["EMPRESA_NOME"] = empresa.NomeEmpresa,
            ["CONTRATANTE"] = QualificarCliente(cliente),
            ["CLIENTE_NOME"] = cliente.Nome,
            ["CLIENTE_DOCUMENTO"] = Documento.Formatar(cliente.Documento),
            ["CLIENTE_ENDERECO"] = cliente.Endereco.ToString(),
            ["CLIENTE_TELEFONE"] = cliente.Celular ?? cliente.Telefone ?? ValidacaoContatos.Primeiro(cliente, TipoContato.Celular,
                TipoContato.WhatsApp, TipoContato.Telefone) ?? "",
            ["CLIENTE_EMAIL"] = cliente.Email ?? ValidacaoContatos.Primeiro(cliente, TipoContato.Email) ?? "",
            ["EVENTO_TITULO"] = evento.Titulo,
            ["EVENTO_CODIGO"] = evento.Codigo,
            ["TIPO_EVENTO"] = evento.TipoEvento?.Nome ?? "",
            ["DATA_EVENTO"] = evento.Inicio.ToString("dd/MM/yyyy", PtBr),
            ["DATA_EVENTO_EXTENSO"] = evento.Inicio.ToString("dddd, dd 'de' MMMM 'de' yyyy", PtBr),
            ["HORA_INICIO"] = evento.Inicio.ToString("HH:mm", PtBr),
            ["HORA_FIM"] = evento.Fim.ToString("HH:mm", PtBr),
            ["ESPACO"] = espaco?.Nome ?? "",
            ["CAPACIDADE"] = espaco?.CapacidadeMaxima.ToString(PtBr) ?? "",
            ["LOCAL_ENDERECO"] = !string.IsNullOrWhiteSpace(enderecoEspaco) ? enderecoEspaco
                : !string.IsNullOrWhiteSpace(empresa.Endereco) ? empresa.Endereco
                : "nas dependências da CONTRATADA",
            ["CONVIDADOS"] = evento.QuantidadeConvidados.ToString(PtBr),
            ["DIAS_CONFIRMACAO"] = empresa.DiasConfirmacaoConvidados.ToString(PtBr),
            ["EXCEDENTES"] = evento.ValorPorPessoa > 0
                ? $"Convidados excedentes serão cobrados ao valor de {Moeda(evento.ValorPorPessoa)} por pessoa, respeitada a capacidade do espaço."
                : "O número de convidados deve respeitar a capacidade do espaço.",
            ["PACOTE"] = evento.Pacote?.Nome ?? "",
            ["VALOR_POR_PESSOA"] = Moeda(evento.ValorPorPessoa),
            ["CARDAPIO"] = cardapio.Count > 0
                ? $"Será servido o cardápio do pacote \"{evento.Pacote?.Nome}\", composto por:\n{itensCardapio}"
                : "O cardápio será definido entre as partes em documento anexo, que integra este contrato.",
            ["SERVICOS"] = evento.Servicos.Count == 0
                ? "Não foram contratados serviços adicionais."
                : "Estão incluídos os seguintes serviços adicionais:\n" + string.Join("\n", evento.Servicos.Select(s =>
                    $"• {s.Descricao}: {s.Quantidade:N0} × {Moeda(s.ValorUnitario)} = {Moeda(s.Total)}")),
            ["VALOR_TOTAL"] = Moeda(evento.ValorTotal),
            ["VALOR_TOTAL_EXTENSO"] = Extenso.Moeda(evento.ValorTotal),
            ["CONDICAO_PAGAMENTO"] = evento.CondicaoPagamento?.Texto
                                     ?? (parcelas.Count > 0 ? "conforme as parcelas abaixo" : "a combinar entre as partes"),
            ["PARCELAS"] = parcelas.Count == 0
                ? "As datas e os valores das parcelas serão acordados entre as partes antes do evento."
                : "O pagamento será feito da seguinte forma:\n" + string.Join("\n", parcelas.Select(p =>
                    $"• {p.Descricao}: {Moeda(p.Valor)} com vencimento em {p.Vencimento:dd/MM/yyyy}" +
                    (p.FormaPagamento is null ? "" : $" ({p.FormaPagamento.Nome})") +
                    (p.Status == StatusTitulo.Pago ? " - quitada" : ""))),
            ["FORO"] = empresa.CidadeForo,
            ["CIDADE"] = empresa.CidadeForo.Split('/')[0],
            ["DATA_HOJE"] = hoje.ToString("dd 'de' MMMM 'de' yyyy", PtBr)
        };
    }

    private static string QualificarCliente(Cliente c)
    {
        var partes = new List<string> { c.Nome };
        if (!string.IsNullOrWhiteSpace(c.Documento))
            partes.Add(c.TipoPessoa == TipoPessoa.Fisica
                ? $"inscrito(a) no CPF sob o nº {Documento.Formatar(c.Documento)}"
                : $"inscrita no CNPJ sob o nº {Documento.Formatar(c.Documento)}");
        var endereco = c.Endereco;
        if (!string.IsNullOrWhiteSpace(endereco.Logradouro))
            partes.Add((c.TipoPessoa == TipoPessoa.Fisica ? "residente em " : "com sede em ") +
                       string.Join(", ", new[] { $"{endereco.Logradouro} {endereco.Numero}".Trim(), endereco.Bairro,
                           $"{endereco.Cidade}/{endereco.Uf}".Trim('/') }.Where(s => !string.IsNullOrWhiteSpace(s))));
        var contato = c.Celular ?? c.Telefone;
        if (contato is not null)
            partes.Add($"telefone {contato}");
        if (c.Email is not null)
            partes.Add($"e-mail {c.Email}");
        return string.Join(", ", partes);
    }
}
