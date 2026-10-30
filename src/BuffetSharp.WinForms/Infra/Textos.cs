using System.Globalization;
using System.Text.RegularExpressions;
using BuffetSharp.Domain.Auditoria;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
using BuffetSharp.Domain.Fiscal;
using BuffetSharp.Domain.Seguranca;

namespace BuffetSharp.WinForms.Infra;

/// <summary>Textos de exibição e cores para os enums do domínio.</summary>
internal static class Textos
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    public static string Exibir(Enum valor) => valor switch
    {
        TipoPessoa.Fisica => "Pessoa Física",
        TipoPessoa.Juridica => "Pessoa Jurídica",
        StatusOrcamento.Rascunho => "Rascunho",
        StatusOrcamento.Enviado => "Enviado",
        StatusOrcamento.Aprovado => "Aprovado",
        StatusOrcamento.Recusado => "Recusado",
        StatusEvento.Confirmado => "Confirmado",
        StatusEvento.Realizado => "Realizado",
        StatusEvento.Cancelado => "Cancelado",
        StatusTitulo.Aberto => "Em aberto",
        StatusTitulo.Pago => "Pago",
        StatusTitulo.Cancelado => "Cancelado",
        StatusRsvp.Pendente => "Pendente",
        StatusRsvp.Confirmado => "Confirmado",
        StatusRsvp.Recusado => "Não vai",
        MotivoIndisponibilidade.Ferias => "Férias",
        TipoRegraPreco.DiaSemana => "Dia da semana",
        TipoRegraPreco.DataEspecial => "Data especial",
        TipoMovimentacao.Entrada => "Entrada",
        TipoMovimentacao.Saida => "Saída",
        TipoMovimentacao.Ajuste => "Ajuste",
        TipoMovimentacao.Estorno => "Estorno de entrada",
        StatusPedidoCompra.Aberto => "Aberto",
        StatusPedidoCompra.ParcialmenteRecebido => "Recebido em parte",
        StatusPedidoCompra.Recebido => "Recebido",
        StatusPedidoCompra.Encerrado => "Encerrado (saldo dispensado)",
        StatusPedidoCompra.Cancelado => "Cancelado",
        StatusCotacao.Aberta => "Aberta",
        StatusCotacao.Concluida => "Concluída",
        StatusCotacao.Cancelada => "Cancelada",
        StatusRecebimento.AFaturar => "A faturar (sem nota)",
        StatusRecebimento.Faturado => "Faturado",
        StatusRecebimento.Estornado => "Estornado",
        StatusNotaEntrada.Lancada => "Lançada",
        StatusNotaEntrada.Cancelada => "Cancelada",
        StatusCobranca.Pendente => "Pendente",
        StatusCobranca.Paga => "Paga",
        StatusCobranca.Cancelada => "Cancelada",
        TipoCobranca.Boleto => "Boleto (com PIX)",
        TipoCobranca.Pix => "PIX",
        TipoCobranca.ClienteEscolhe => "Cliente escolhe na fatura",
        AmbienteCobranca.Simulado => "Simulado (treino, sem cobrança de verdade)",
        AmbienteCobranca.Sandbox => "Sandbox do Asaas (testes)",
        AmbienteCobranca.Producao => "Produção (cobrança de verdade)",
        StatusNfse.Processando => "Aguardando prefeitura",
        StatusNfse.Autorizada => "Autorizada",
        StatusNfse.Erro => "Recusada",
        StatusNfse.Cancelada => "Cancelada",
        AmbienteNfse.Simulado => "Simulado (treino, sem valor fiscal)",
        AmbienteNfse.Homologacao => "Homologação (teste oficial, sem valor fiscal)",
        AmbienteNfse.Producao => "Produção (nota com valor fiscal)",
        RegimeTributario.SimplesNacional => "Simples Nacional",
        RegimeTributario.Mei => "MEI",
        RegimeTributario.LucroPresumido => "Lucro presumido",
        RegimeTributario.LucroReal => "Lucro real",
        CategoriaDespesa.Manutencao => "Manutenção",
        CategoriaDespesa.Servicos => "Serviços",
        CategoriaDespesa.Comissoes => "Comissões",
        CategoriaDespesa.LocacaoMateriais => "Locação de materiais",
        CategoriaDespesa.FaturaCartao => "Fatura de cartão",
        TipoFormaPagamento.Pix => "PIX",
        TipoFormaPagamento.Transferencia => "Transferência / TED",
        TipoFormaPagamento.CartaoDebito => "Cartão de débito",
        TipoFormaPagamento.CartaoCredito => "Cartão de crédito",
        TipoFormaPagamento.Outros => "Outros (sem conta)",
        TipoContaFinanceira.Caixa => "Caixa (dinheiro)",
        TipoContaFinanceira.ContaCorrente => "Conta corrente",
        TipoContaFinanceira.Poupanca => "Poupança",
        TipoContaFinanceira.Aplicacao => "Aplicação",
        OrigemMovimento.Baixa => "Recebimento/pagamento",
        OrigemMovimento.Lancamento => "Lançamento avulso",
        OrigemMovimento.Transferencia => "Transferência",
        OrigemMovimento.AjusteFechamento => "Ajuste de fechamento",
        StatusContrato.AguardandoAssinatura => "Aguardando assinatura",
        StatusContrato.Assinado => "Assinado",
        StatusContrato.Cancelado => "Cancelado",
        StatusDegustacao.Agendada => "Agendada",
        StatusDegustacao.Realizada => "Realizada",
        StatusDegustacao.NaoCompareceu => "Não compareceu",
        StatusDegustacao.Cancelada => "Cancelada",
        CanalEnvio.Email => "E-mail",
        CanalEnvio.WhatsApp => "WhatsApp",
        CategoriaMaterial.Louca => "Louça",
        CategoriaMaterial.Talheres => "Talheres",
        CategoriaMaterial.CoposETacas => "Copos e taças",
        CategoriaMaterial.Enxoval => "Enxoval",
        CategoriaMaterial.Mobiliario => "Mobiliário",
        CategoriaMaterial.Decoracao => "Decoração",
        CategoriaMaterial.Utensilios => "Utensílios",
        AcaoAuditoria.Inclusao => "Inclusão",
        AcaoAuditoria.Alteracao => "Alteração",
        AcaoAuditoria.Exclusao => "Exclusão",
        PerfilUsuario.Administrador => "Administrador",
        PerfilUsuario.Comercial => "Comercial",
        PerfilUsuario.Financeiro => "Financeiro",
        PerfilUsuario.Operacional => "Operacional",
        TipoContato.Celular => "Celular",
        TipoContato.Telefone => "Telefone",
        TipoContato.WhatsApp => "WhatsApp",
        TipoContato.Email => "E-mail",
        OrigemLead.Indicacao => "Indicação",
        OrigemLead.Site => "Site",
        OrigemLead.Feira => "Feira / evento",
        OrigemLead.Visita => "Visita ao buffet",
        StatusLead.Novo => "Novo",
        StatusLead.EmContato => "Em contato",
        StatusLead.Convertido => "Convertido",
        StatusLead.Perdido => "Perdido",
        TipoLembrete.OrcamentoVencendo => "Proposta vencendo",
        TipoLembrete.Degustacao => "Degustação",
        TipoLembrete.Aniversario => "Aniversário",
        TipoLembrete.ParcelaAVencer => "Parcela a vencer",
        TipoLembrete.ParcelaVencida => "Parcela vencida",
        TipoLembrete.RetornoLead => "Retorno de lead",
        TipoLembrete.ContaPagarAVencer => "Conta a pagar",
        TipoLembrete.MaterialNaoDevolvido => "Material não devolvido",
        TipoLembrete.TarefaEvento => "Tarefa de evento",
        TipoLembrete.EsperaData => "Data liberada (espera)",
        TipoLembrete.PesquisaSatisfacao => "Pesquisa de satisfação",
        TipoLembrete.Detrator => "Cliente insatisfeito",
        TipoLembrete.AvaliacaoGoogle => "Avaliação no Google",
        TipoLembrete.ValidadeLote => "Validade de lote",
        TipoLembrete.AprovacaoDesconto => "Aprovação de desconto",
        SituacaoDesconto.DentroDaAlcada => "Dentro da alçada",
        SituacaoDesconto.Aguardando => "Aguardando",
        SituacaoDesconto.Aprovado => "Aprovado",
        SituacaoDesconto.Recusado => "Recusado",
        TipoMovimentacao.Perda => "Perda (descarte)",
        TipoMovimentacao.Devolucao => "Devolução ao fornecedor",
        MotivoDevolucao.Avariado => "Avariado",
        MotivoDevolucao.Vencido => "Vencido",
        MotivoDevolucao.ProdutoErrado => "Produto errado",
        MotivoDevolucao.ForaDoPedido => "Fora do pedido",
        MotivoDevolucao.Qualidade => "Qualidade",
        MotivoDevolucao.Outro => "Outro",
        ArredondamentoPreco.Centavos => "Manter centavos",
        ArredondamentoPreco.Reais => "Real inteiro",
        ArredondamentoPreco.CincoReais => "Múltiplo de R$ 5",
        StatusReajuste.Agendado => "Agendado",
        StatusReajuste.Aplicado => "Aplicado",
        StatusReajuste.Desfeito => "Desfeito / cancelado",
        TipoItemReajuste.Pacote => "Pacote",
        TipoItemReajuste.ServicoAdicional => "Serviço",
        TipoItemReajuste.Espaco => "Espaço",
        _ => valor.ToString()
    };

    public static Color Cor(Enum valor) => valor switch
    {
        SituacaoDesconto.Aguardando => Tema.Alerta,
        SituacaoDesconto.Aprovado => Tema.Sucesso,
        SituacaoDesconto.Recusado => Tema.Perigo,
        StatusOrcamento.Rascunho => Tema.Neutro,
        StatusOrcamento.Enviado => Tema.Info,
        StatusOrcamento.Aprovado => Tema.Sucesso,
        StatusOrcamento.Recusado => Tema.Perigo,
        StatusEvento.Confirmado => Tema.Info,
        StatusEvento.Realizado => Tema.Sucesso,
        StatusEvento.Cancelado => Tema.Perigo,
        StatusTitulo.Aberto => Tema.Info,
        StatusTitulo.Pago => Tema.Sucesso,
        StatusTitulo.Cancelado => Tema.Neutro,
        StatusRsvp.Confirmado => Tema.Sucesso,
        SituacaoFornecedorEvento.Contratado => Tema.Info,
        SituacaoFornecedorEvento.Confirmado => Tema.Sucesso,
        SituacaoFornecedorEvento.Cancelado => Tema.Neutro,
        StatusRsvp.Recusado => Tema.Perigo,
        TipoMovimentacao.Entrada => Tema.Sucesso,
        TipoMovimentacao.Saida => Tema.Perigo,
        TipoMovimentacao.Ajuste => Tema.Alerta,
        TipoMovimentacao.Estorno => Tema.Perigo,
        StatusPedidoCompra.Aberto => Tema.Info,
        StatusPedidoCompra.ParcialmenteRecebido => Tema.Alerta,
        StatusPedidoCompra.Recebido => Tema.Sucesso,
        StatusPedidoCompra.Encerrado => Tema.Sucesso,
        StatusPedidoCompra.Cancelado => Tema.Neutro,
        StatusCotacao.Aberta => Tema.Info,
        StatusCotacao.Concluida => Tema.Sucesso,
        StatusCotacao.Cancelada => Tema.Neutro,
        StatusRecebimento.AFaturar => Tema.Alerta,
        StatusRecebimento.Faturado => Tema.Sucesso,
        StatusRecebimento.Estornado => Tema.Neutro,
        StatusNotaEntrada.Lancada => Tema.Sucesso,
        StatusNotaEntrada.Cancelada => Tema.Neutro,
        StatusCobranca.Pendente => Tema.Info,
        StatusCobranca.Paga => Tema.Sucesso,
        StatusCobranca.Cancelada => Tema.Neutro,
        StatusNfse.Processando => Tema.Alerta,
        StatusNfse.Autorizada => Tema.Sucesso,
        StatusNfse.Erro => Tema.Perigo,
        StatusNfse.Cancelada => Tema.Neutro,
        StatusContrato.AguardandoAssinatura => Tema.Alerta,
        StatusContrato.Assinado => Tema.Sucesso,
        StatusContrato.Cancelado => Tema.Neutro,
        StatusDegustacao.Agendada => Tema.Info,
        StatusDegustacao.Realizada => Tema.Sucesso,
        StatusDegustacao.NaoCompareceu => Tema.Alerta,
        StatusDegustacao.Cancelada => Tema.Neutro,
        AcaoAuditoria.Inclusao => Tema.Sucesso,
        AcaoAuditoria.Alteracao => Tema.Info,
        AcaoAuditoria.Exclusao => Tema.Perigo,
        StatusLead.Novo => Tema.Info,
        StatusLead.EmContato => Tema.Alerta,
        StatusLead.Convertido => Tema.Sucesso,
        StatusLead.Perdido => Tema.Neutro,
        StatusReajuste.Agendado => Tema.Info,
        StatusReajuste.Aplicado => Tema.Sucesso,
        StatusReajuste.Desfeito => Tema.Neutro,
        _ => Tema.Texto
    };

    /// <summary>Converte o texto do enum (como gravado no banco) para exibição, ex.: "CoposETacas" → "Copos e taças".</summary>
    public static string Exibir<TEnum>(string? valor) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(valor, out var convertido) ? Exibir(convertido) : valor ?? string.Empty;

    private static readonly Dictionary<string, string> NomesEntidades = new()
    {
        ["TipoEvento"] = "Tipo de evento", ["CategoriaProduto"] = "Categoria de produto", ["CategoriaCardapio"] = "Seção do cardápio",
        ["UnidadeMedida"] = "Unidade de medida", ["FormaPagamento"] = "Forma de pagamento", ["Cargo"] = "Cargo",
        ["Cliente"] = "Cliente", ["Fornecedor"] = "Fornecedor", ["Funcionario"] = "Funcionário", ["Espaco"] = "Espaço",
        ["ServicoAdicional"] = "Serviço adicional", ["Produto"] = "Produto", ["MovimentacaoEstoque"] = "Movimentação de estoque",
        ["ItemCardapio"] = "Item do cardápio", ["FichaTecnica"] = "Ficha técnica", ["Pacote"] = "Pacote", ["PacoteItem"] = "Item do pacote",
        ["Orcamento"] = "Orçamento", ["OrcamentoServico"] = "Serviço do orçamento", ["EnvioProposta"] = "Envio de proposta",
        ["Evento"] = "Evento", ["EventoServico"] = "Serviço do evento", ["EventoFuncionario"] = "Equipe do evento",
        ["Convidado"] = "Convidado", ["EventoMaterial"] = "Material do evento", ["Parcela"] = "Parcela (a receber)",
        ["ContaPagar"] = "Conta a pagar", ["Usuario"] = "Usuário", ["Material"] = "Material", ["Contrato"] = "Contrato",
        ["Degustacao"] = "Degustação", ["FechamentoComissao"] = "Fechamento de comissão",
        ["ContatoAdicional"] = "Contato adicional", ["EnderecoAdicional"] = "Endereço adicional",
        ["CondicaoPagamento"] = "Condição de pagamento", ["ModeloContrato"] = "Modelo de contrato",
        ["ProdutoEmbalagem"] = "Embalagem do produto", ["ContaFinanceira"] = "Conta (caixa/banco)",
        ["MovimentoConta"] = "Movimento de conta", ["FechamentoConta"] = "Fechamento de conta", ["CartaoCredito"] = "Cartão de crédito",
        ["Baixa"] = "Recebimento/pagamento", ["BaixaTitulo"] = "Conta quitada", ["BaixaForma"] = "Forma de pagamento usada",
        ["Lead"] = "Lead (interessado)", ["LeadInteracao"] = "Contato com lead", ["LembreteTratado"] = "Lembrete tratado",
        ["ReajustePreco"] = "Reajuste de preços", ["ReajusteItem"] = "Item do reajuste",
        ["ModeloChecklist"] = "Modelo de checklist", ["ModeloChecklistItem"] = "Tarefa do modelo de checklist",
        ["EventoTarefa"] = "Tarefa do evento", ["ModeloCronograma"] = "Modelo de cronograma",
        ["ModeloCronogramaItem"] = "Etapa do modelo de cronograma", ["EventoEtapa"] = "Etapa do cronograma do evento",
        ["EventoMesa"] = "Mesa do evento", ["EventoFornecedor"] = "Fornecedor do evento",
        ["IndisponibilidadeFuncionario"] = "Indisponibilidade da equipe", ["RegraPreco"] = "Regra de preço por data", ["EsperaData"] = "Lista de espera de data", ["PesquisaSatisfacao"] = "Pesquisa de satisfação", ["LoteEstoque"] = "Lote do estoque", ["DevolucaoCompra"] = "Devolução ao fornecedor", ["EspacoMesa"] = "Mesa do mapa do espaço", ["FechamentoEquipe"] = "Pagamento da equipe",
        ["PedidoCompra"] = "Pedido de compra", ["PedidoCompraItem"] = "Item do pedido de compra",
        ["CotacaoCompra"] = "Cotação de compra", ["CotacaoItem"] = "Produto da cotação", ["CotacaoParticipante"] = "Participante da cotação",
        ["CotacaoPreco"] = "Preço da cotação", ["RecebimentoCompra"] = "Recebimento de compra", ["RecebimentoItem"] = "Item recebido",
        ["NotaFiscalEntrada"] = "Nota fiscal de entrada", ["ConfiguracaoFiscal"] = "Configuração fiscal (NFS-e)",
        ["NotaFiscalServico"] = "Nota fiscal de serviço (NFS-e)", ["ConfiguracaoCobranca"] = "Configuração da cobrança",
        ["CobrancaRegistrada"] = "Cobrança registrada (boleto/PIX)"
    };

    /// <summary>Nome amigável da entidade gravado na auditoria.</summary>
    public static string Entidade(string nome) => NomesEntidades.GetValueOrDefault(nome, nome);

    /// <summary>"QuantidadeConvidados" → "Quantidade convidados"; "Endereco.Cep" → "Endereço › Cep".</summary>
    public static string Campo(string nome) => string.Join(" › ", nome.Split('.').Select(parte =>
    {
        var palavras = Regex.Replace(parte, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
        return char.ToUpperInvariant(palavras[0]) + palavras[1..];
    }));

    /// <summary>Itens para combos de enum, na ordem de declaração.</summary>
    public static List<OpcaoEnum<TEnum>> Opcoes<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => new OpcaoEnum<TEnum>(v, Exibir(v))).ToList();
}

internal sealed record OpcaoEnum<TEnum>(TEnum Valor, string Texto) where TEnum : struct, Enum
{
    public override string ToString() => Texto;
}
