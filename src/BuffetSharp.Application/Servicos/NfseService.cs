using System.Globalization;
using System.Security;
using System.Text;
using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Fiscal;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>O que a tela mostra antes de emitir: tomador, valor, ISS, descrição e o que falta configurar.</summary>
public sealed record PreparoNfse
{
    public int EventoId { get; init; }
    public string Evento { get; init; } = string.Empty;
    public DateTime DataEvento { get; init; }
    public string Tomador { get; init; } = string.Empty;
    public string? TomadorDocumento { get; init; }
    public string? TomadorEmail { get; init; }
    public decimal Valor { get; init; }
    public decimal AliquotaIss { get; init; }
    public bool IssRetido { get; init; }
    public string Discriminacao { get; init; } = string.Empty;
    public AmbienteNfse Ambiente { get; init; }

    /// <summary>O que impede a emissão (configuração fiscal incompleta, cliente sem CPF/CNPJ...).</summary>
    public IReadOnlyList<string> Pendencias { get; init; } = [];

    /// <summary>Nota em processamento ou autorizada do evento (impede outra).</summary>
    public int? NotaAtivaId { get; init; }
    public string? NotaAtiva { get; init; }
}

public sealed class EmissaoNfseInput
{
    public int EventoId { get; set; }
    public decimal Valor { get; set; }
    public string Discriminacao { get; set; } = string.Empty;
}

/// <summary>
/// NFS-e do evento: configuração fiscal do buffet, emissão (uma nota ativa por evento), consulta da situação na prefeitura,
/// cancelamento e o XML. No ambiente simulado a nota é "autorizada" na hora, sem internet.
/// </summary>
public sealed class NfseService(IBuffetDbContextFactory fabrica, IProvedorNfse provedor, NfseOptions opcoes, ISessaoUsuario sessao, TimeProvider relogio)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private DateTime Agora => relogio.GetLocalNow().DateTime;

    public async Task<ConfiguracaoFiscal> ObterConfiguracaoAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await db.ConfiguracoesFiscais.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct) ?? new ConfiguracaoFiscal();
    }

    public async Task SalvarConfiguracaoAsync(ConfiguracaoFiscal configuracao, CancellationToken ct = default)
    {
        configuracao.Cnpj = Vazio(Documento.SomenteDigitos(configuracao.Cnpj));
        configuracao.CodigoMunicipio = Vazio(Documento.SomenteDigitos(configuracao.CodigoMunicipio));
        configuracao.InscricaoMunicipal = Vazio(configuracao.InscricaoMunicipal?.Trim());
        configuracao.ItemListaServico = configuracao.ItemListaServico.Trim();
        Validador.Validar(configuracao);

        await using var db = fabrica.Criar();
        var atual = await db.ConfiguracoesFiscais.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (atual is null)
            db.ConfiguracoesFiscais.Add(configuracao);
        else
        {
            Concorrencia.Conferir(atual, configuracao.Versao);
            db.Entry(atual).CurrentValues.SetValues(configuracao);
            db.Entry(atual).Property(c => c.Id).IsModified = false;
            db.Entry(atual).Property(c => c.CriadoEm).IsModified = false;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<PreparoNfse> PrepararAsync(int eventoId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var configuracao = await db.ConfiguracoesFiscais.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct) ?? new ConfiguracaoFiscal();
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Cliente).Include(e => e.Espaco).FirstOrDefaultAsync(e => e.Id == eventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        var ativa = await db.NotasFiscaisServico.AsNoTracking()
            .Where(n => n.EventoId == eventoId && (n.Status == StatusNfse.Processando || n.Status == StatusNfse.Autorizada))
            .FirstOrDefaultAsync(ct);

        var pendencias = configuracao.Pendencias().Select(p => $"Configuração fiscal: falta {p}").ToList();
        var documento = Documento.SomenteDigitos(evento.Cliente?.Documento);
        if (!(Documento.CpfValido(documento) || Documento.CnpjValido(documento)))
            pendencias.Add("Cliente sem CPF/CNPJ válido no cadastro");
        if (configuracao.Ambiente != AmbienteNfse.Simulado && string.IsNullOrWhiteSpace(opcoes.Token(configuracao.Ambiente)))
            pendencias.Add($"Token do Focus NFe de {(configuracao.Ambiente == AmbienteNfse.Producao ? "produção" : "homologação")} não configurado nesta máquina (appsettings.Local.json)");
        if (evento.Status == Domain.Eventos.StatusEvento.Cancelado)
            pendencias.Add("Evento cancelado");

        return new PreparoNfse
        {
            EventoId = evento.Id,
            Evento = $"{evento.Codigo} - {evento.Titulo}",
            DataEvento = evento.Inicio,
            Tomador = evento.Cliente?.Nome ?? "",
            TomadorDocumento = documento.Length > 0 ? Documento.Formatar(documento) : null,
            TomadorEmail = evento.Cliente?.Email,
            Valor = evento.ValorTotal,
            AliquotaIss = configuracao.AliquotaIss,
            IssRetido = configuracao.IssRetido,
            Discriminacao = Discriminacao(configuracao.Discriminacao, evento),
            Ambiente = configuracao.Ambiente,
            Pendencias = pendencias,
            NotaAtivaId = ativa?.Id,
            NotaAtiva = ativa is null ? null
                : ativa.Status == StatusNfse.Autorizada ? $"NFS-e nº {ativa.Numero} autorizada em {ativa.DataEmissao:dd/MM/yyyy}"
                : "NFS-e enviada, aguardando a prefeitura"
        };
    }

    /// <summary>
    /// Emite a NFS-e do evento. O pedido é gravado antes do envio (com a referência), assim uma falha de comunicação não perde
    /// o pedido: a situação pode ser consultada depois. Retorna a nota.
    /// </summary>
    public async Task<int> EmitirAsync(EmissaoNfseInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var configuracao = await db.ConfiguracoesFiscais.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct) ?? new ConfiguracaoFiscal();
        var faltam = configuracao.Pendencias();
        Validador.Garantir(faltam.Count == 0, $"Complete a configuração fiscal (Financeiro › Notas fiscais de serviço › Configuração fiscal): falta {string.Join(", ", faltam)}.");
        Validador.Garantir(configuracao.Ambiente == AmbienteNfse.Simulado || !string.IsNullOrWhiteSpace(opcoes.Token(configuracao.Ambiente)),
            $"Informe o token do Focus NFe em appsettings.Local.json (seção \"{NfseOptions.Secao}\") ou use o ambiente simulado.");
        var evento = await db.Eventos.Include(e => e.Cliente).FirstOrDefaultAsync(e => e.Id == input.EventoId, ct)
                     ?? throw new RegraNegocioException("Evento não encontrado.");
        var ativa = await db.NotasFiscaisServico.AnyAsync(n => n.EventoId == evento.Id &&
                                                                (n.Status == StatusNfse.Processando || n.Status == StatusNfse.Autorizada), ct);
        Validador.Garantir(!ativa, "Este evento já tem NFS-e emitida (ou aguardando a prefeitura). Para emitir outra, cancele a atual.");

        var cliente = evento.Cliente!;
        var nota = new NotaFiscalServico(evento, configuracao.Ambiente, input.Valor, configuracao.AliquotaIss, configuracao.IssRetido,
            input.Discriminacao, cliente.Documento ?? "", cliente.Nome, cliente.Email, sessao.Usuario?.Login, Agora);
        db.NotasFiscaisServico.Add(nota);
        await db.SaveChangesAsync(ct);

        if (nota.Ambiente == AmbienteNfse.Simulado)
            nota.RegistrarAutorizacao($"SIM{nota.Id:D6}", Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Agora, null, null);
        else
        {
            var pedido = new PedidoNfse(nota.Referencia, nota.Ambiente, Agora, configuracao.Regime,
                new PrestadorNfse(configuracao.Cnpj!, configuracao.InscricaoMunicipal!, configuracao.CodigoMunicipio!),
                new TomadorNfse(nota.TomadorDocumento, nota.TomadorNome, nota.TomadorEmail, cliente.Celular ?? cliente.Telefone, cliente.Endereco),
                new ServicoNfse(nota.ValorServicos, nota.AliquotaIss, nota.IssRetido, configuracao.ItemListaServico, configuracao.CodigoTributarioMunicipio,
                    configuracao.CodigoCnae, nota.Discriminacao, configuracao.CodigoMunicipio!));
            try
            {
                Aplicar(nota, await provedor.EmitirAsync(pedido, ct));
            }
            catch (RegraNegocioException ex)
            {
                nota.RegistrarErro(ex.Message);
                await db.SaveChangesAsync(ct);
                throw;
            }
        }
        await db.SaveChangesAsync(ct);
        return nota.Id;
    }

    /// <summary>Pergunta ao provedor a situação de uma nota em processamento (a prefeitura costuma levar alguns segundos).</summary>
    public async Task<StatusNfse> AtualizarSituacaoAsync(int notaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var nota = await db.NotasFiscaisServico.FirstOrDefaultAsync(n => n.Id == notaId, ct)
                   ?? throw new RegraNegocioException("Nota fiscal não encontrada.");
        if (nota.Status != StatusNfse.Processando || nota.Ambiente == AmbienteNfse.Simulado)
            return nota.Status;
        Aplicar(nota, await provedor.ConsultarAsync(nota.Ambiente, nota.Referencia, ct));
        await db.SaveChangesAsync(ct);
        return nota.Status;
    }

    public async Task CancelarAsync(int notaId, string motivo, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var nota = await db.NotasFiscaisServico.FirstOrDefaultAsync(n => n.Id == notaId, ct)
                   ?? throw new RegraNegocioException("Nota fiscal não encontrada.");
        nota.ConferirCancelamento(motivo);
        if (nota.Ambiente != AmbienteNfse.Simulado)
        {
            var resposta = await provedor.CancelarAsync(nota.Ambiente, nota.Referencia, motivo.Trim(), ct);
            Validador.Garantir(resposta.Status == StatusNfse.Cancelada,
                $"A prefeitura não cancelou a nota: {resposta.Mensagem ?? "sem detalhes"}");
        }
        nota.RegistrarCancelamento(motivo, Agora);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>XML da nota (no simulado, um XML de conferência montado aqui, sem valor fiscal).</summary>
    public async Task<(string Nome, byte[] Conteudo)> BaixarXmlAsync(int notaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var nota = await db.NotasFiscaisServico.AsNoTracking().FirstOrDefaultAsync(n => n.Id == notaId, ct)
                   ?? throw new RegraNegocioException("Nota fiscal não encontrada.");
        Validador.Garantir(nota.Status is StatusNfse.Autorizada or StatusNfse.Cancelada, "A nota ainda não foi autorizada.");
        var nome = $"NFS-e {nota.Numero ?? nota.Referencia}.xml";
        if (nota.Ambiente == AmbienteNfse.Simulado)
            return (nome, XmlSimulado(nota));
        Validador.Garantir(!string.IsNullOrWhiteSpace(nota.CaminhoXml), "O provedor não informou o XML desta nota. Atualize a situação e tente de novo.");
        return (nome, await provedor.BaixarXmlAsync(nota.Ambiente, nota.CaminhoXml!, ct));
    }

    private static void Aplicar(NotaFiscalServico nota, RespostaNfse resposta)
    {
        switch (resposta.Status)
        {
            case StatusNfse.Autorizada:
                nota.RegistrarAutorizacao(resposta.Numero, resposta.CodigoVerificacao, resposta.DataEmissao, resposta.Url, resposta.CaminhoXml);
                break;
            case StatusNfse.Erro:
                nota.RegistrarErro(resposta.Mensagem ?? "");
                break;
            case StatusNfse.Cancelada:
                nota.RegistrarAutorizacao(resposta.Numero, resposta.CodigoVerificacao, resposta.DataEmissao, resposta.Url, resposta.CaminhoXml);
                nota.RegistrarCancelamento(resposta.Mensagem is { Length: >= 15 } m ? m : "Cancelada no provedor da NFS-e.", DateTime.Now);
                break;
            default:
                nota.RegistrarProcessamento(resposta.Mensagem);
                break;
        }
    }

    /// <summary>Texto da descrição com os campos do evento trocados.</summary>
    internal static string Discriminacao(string modelo, Domain.Eventos.Evento evento) => modelo
        .Replace("{{EVENTO}}", evento.Titulo)
        .Replace("{{CODIGO}}", evento.Codigo)
        .Replace("{{DATA}}", evento.DataEvento.ToString("dd/MM/yyyy", PtBr))
        .Replace("{{CONVIDADOS}}", evento.PessoasAtendidas.ToString(PtBr))
        .Replace("{{LOCAL}}", evento.Espaco?.Nome ?? "local combinado")
        .Replace("{{CONTRATANTE}}", evento.Cliente?.Nome ?? "")
        .Trim();

    private static byte[] XmlSimulado(NotaFiscalServico nota)
    {
        static string E(string? texto) => SecurityElement.Escape(texto ?? "");
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <NfseSimulada aviso="Ambiente simulado do BuffetSharp - sem valor fiscal">
              <Numero>{E(nota.Numero)}</Numero>
              <CodigoVerificacao>{E(nota.CodigoVerificacao)}</CodigoVerificacao>
              <DataEmissao>{nota.DataEmissao:yyyy-MM-ddTHH:mm:ss}</DataEmissao>
              <Tomador><Documento>{E(nota.TomadorDocumento)}</Documento><Nome>{E(nota.TomadorNome)}</Nome></Tomador>
              <Servico>
                <ValorServicos>{nota.ValorServicos.ToString("0.00", CultureInfo.InvariantCulture)}</ValorServicos>
                <Aliquota>{nota.AliquotaIss.ToString("0.00", CultureInfo.InvariantCulture)}</Aliquota>
                <ValorIss>{nota.ValorIss.ToString("0.00", CultureInfo.InvariantCulture)}</ValorIss>
                <IssRetido>{(nota.IssRetido ? "true" : "false")}</IssRetido>
                <Discriminacao>{E(nota.Discriminacao)}</Discriminacao>
              </Servico>
            </NfseSimulada>
            """;
        return Encoding.UTF8.GetBytes(xml);
    }

    private static string? Vazio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;
}
