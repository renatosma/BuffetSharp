using System.Globalization;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Consultas;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Modelo do contrato de prestação de serviços: monta o texto (cláusulas numeradas) com os dados do evento,
/// do cliente, do cardápio e do plano de pagamento. Requer o evento com cliente, tipo, espaço, pacote,
/// serviços e parcelas carregados.
/// </summary>
public static class ModeloContrato
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Gerar(BuffetOptions empresa, Evento evento, IReadOnlyList<ItemComposicao> cardapio, DateOnly hoje) =>
        new Gerador(empresa, evento, cardapio, hoje).Gerar();

    private sealed class Gerador(BuffetOptions empresa, Evento evento, IReadOnlyList<ItemComposicao> cardapio, DateOnly hoje)
    {
        private readonly StringBuilder _texto = new();
        private int _clausula;

        private static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

        public string Gerar()
        {
            var cliente = evento.Cliente!;

            Linha("CONTRATO DE PRESTAÇÃO DE SERVIÇOS DE BUFFET");
            Paragrafo($"CONTRATADA: {empresa.NomeEmpresa}" +
                      (string.IsNullOrWhiteSpace(empresa.Documento) ? "" : $", inscrita no CNPJ sob o nº {empresa.Documento}") +
                      (string.IsNullOrWhiteSpace(empresa.Endereco) ? "" : $", com sede em {empresa.Endereco}") +
                      (string.IsNullOrWhiteSpace(empresa.Telefone) ? "" : $", telefone {empresa.Telefone}") +
                      (string.IsNullOrWhiteSpace(empresa.Email) ? "" : $", e-mail {empresa.Email}") + ".");
            Paragrafo($"CONTRATANTE: {QualificarCliente(cliente)}.");
            Paragrafo("As partes acima identificadas têm entre si justo e contratado o presente instrumento, que se regerá pelas cláusulas e condições seguintes.");

            Clausula("DO OBJETO",
                $"O presente contrato tem por objeto a prestação de serviços de buffet para o evento \"{evento.Titulo}\" " +
                $"({evento.TipoEvento?.Nome}), registrado sob o código {evento.Codigo}, conforme as especificações deste instrumento.");

            Clausula("DA DATA, DO HORÁRIO E DO LOCAL",
                $"O evento será realizado em {evento.Inicio.ToString("dddd, dd 'de' MMMM 'de' yyyy", PtBr)}, das {evento.Inicio:HH:mm} às {evento.Fim:HH:mm}, " +
                $"no ambiente \"{evento.Espaco?.Nome}\", cuja capacidade máxima é de {evento.Espaco?.CapacidadeMaxima} pessoas. " +
                "A prorrogação do horário dependerá da disponibilidade do espaço e será cobrada à parte.");

            Clausula("DOS CONVIDADOS",
                $"Os serviços foram contratados para {evento.QuantidadeConvidados} convidados. A CONTRATANTE deverá confirmar o número final " +
                $"de convidados com antecedência mínima de {empresa.DiasConfirmacaoConvidados} dias da data do evento" +
                (evento.ValorPorPessoa > 0
                    ? $". Convidados excedentes serão cobrados ao valor de {Moeda(evento.ValorPorPessoa)} por pessoa, respeitada a capacidade do espaço."
                    : ", respeitada a capacidade do espaço."));

            if (cardapio.Count > 0)
            {
                var itens = string.Join("\n", cardapio.GroupBy(c => c.Categoria)
                    .Select(g => $"• {g.Key}: {string.Join(", ", g.Select(i => i.Item))}"));
                Clausula("DO CARDÁPIO", $"Será servido o cardápio do pacote \"{evento.Pacote?.Nome}\", composto por:\n{itens}");
            }
            else
            {
                Clausula("DO CARDÁPIO", "O cardápio será definido entre as partes em documento anexo, que integra este contrato.");
            }

            Clausula("DOS SERVIÇOS ADICIONAIS", evento.Servicos.Count == 0
                ? "Não foram contratados serviços adicionais."
                : "Estão incluídos os seguintes serviços adicionais:\n" + string.Join("\n", evento.Servicos.Select(s =>
                    $"• {s.Descricao}: {s.Quantidade:N0} × {Moeda(s.ValorUnitario)} = {Moeda(s.Total)}")));

            var parcelas = evento.Parcelas.Where(p => p.Status != StatusTitulo.Cancelado).OrderBy(p => p.Vencimento).ToList();
            Clausula("DO PREÇO E DA FORMA DE PAGAMENTO",
                $"Pelos serviços contratados, a CONTRATANTE pagará à CONTRATADA o valor total de {Moeda(evento.ValorTotal)}" +
                (parcelas.Count == 0
                    ? ", em condições a serem acordadas entre as partes antes do evento."
                    : ", da seguinte forma:\n" + string.Join("\n", parcelas.Select(p =>
                        $"• {p.Descricao}: {Moeda(p.Valor)} com vencimento em {p.Vencimento:dd/MM/yyyy}" +
                        (p.FormaPagamento is null ? "" : $" ({p.FormaPagamento.Nome})") +
                        (p.Status == StatusTitulo.Pago ? " - quitada" : "")))) +
                "\nO atraso no pagamento sujeitará a CONTRATANTE à multa de 2% e juros de mora de 1% ao mês sobre o valor em atraso.");

            Clausula("DA DESISTÊNCIA E DA RESCISÃO",
                "Em caso de desistência pela CONTRATANTE, a título de compensação pela reserva da data, a CONTRATADA reterá: " +
                "20% do valor total, se a desistência ocorrer com mais de 90 dias de antecedência; 50%, entre 90 e 30 dias; " +
                "e 100%, com menos de 30 dias. A comunicação deverá ser feita por escrito.");

            Clausula("DAS OBRIGAÇÕES DA CONTRATADA",
                "Fornecer os alimentos e bebidas do cardápio contratado observando as normas sanitárias vigentes; disponibilizar a equipe " +
                "necessária ao bom atendimento dos convidados; entregar o espaço limpo, montado e decorado conforme contratado no horário do evento.");

            Clausula("DAS OBRIGAÇÕES DA CONTRATANTE",
                "Efetuar os pagamentos nas datas acordadas; respeitar o horário e a capacidade do espaço; zelar pelo espaço, mobiliário, " +
                "louças, talheres, taças e enxoval, respondendo pelos danos e extravios causados por seus convidados, que serão cobrados " +
                "pelo valor de reposição.");

            Clausula("DO FORO",
                $"Fica eleito o foro da comarca de {empresa.CidadeForo} para dirimir quaisquer questões oriundas deste contrato, " +
                "com renúncia a qualquer outro, por mais privilegiado que seja.");

            Paragrafo("E, por estarem de acordo, as partes assinam o presente instrumento.");
            Paragrafo($"{empresa.CidadeForo.Split('/')[0]}, {hoje.ToString("dd 'de' MMMM 'de' yyyy", PtBr)}.");
            return _texto.ToString().TrimEnd();
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

        private void Clausula(string titulo, string texto)
        {
            _clausula++;
            Linha($"CLÁUSULA {_clausula}ª - {titulo}");
            Paragrafo(texto);
        }

        private void Linha(string texto) => _texto.Append(texto).Append('\n');

        private void Paragrafo(string texto) => _texto.Append(texto).Append("\n\n");
    }
}
