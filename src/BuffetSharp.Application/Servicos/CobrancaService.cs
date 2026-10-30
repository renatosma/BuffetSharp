using BuffetSharp.Application.Abstracoes;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace BuffetSharp.Application.Servicos;

/// <summary>O que a tela mostra antes de emitir a cobrança registrada de uma parcela.</summary>
public sealed record PreparoCobranca
{
    public int ParcelaId { get; init; }
    public string Parcela { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public string? ClienteDocumento { get; init; }
    public decimal Valor { get; init; }
    public DateOnly VencimentoParcela { get; init; }

    /// <summary>O vencimento da parcela ou, se ele já passou, daqui a 3 dias.</summary>
    public DateOnly VencimentoSugerido { get; init; }

    public TipoCobranca TipoPadrao { get; init; }
    public decimal MultaPercentual { get; init; }
    public decimal JurosMensalPercentual { get; init; }
    public AmbienteCobranca Ambiente { get; init; }
    public IReadOnlyList<string> Pendencias { get; init; } = [];

    /// <summary>Cobrança pendente da parcela (abre a segunda via em vez de emitir outra).</summary>
    public int? CobrancaPendenteId { get; init; }
}

public sealed class EmissaoCobrancaInput
{
    public int ParcelaId { get; set; }
    public TipoCobranca Tipo { get; set; }
    public DateOnly Vencimento { get; set; }
}

/// <summary>Cobrança pronta para mostrar e enviar ao cliente.</summary>
public sealed record CobrancaDetalhe
{
    public int Id { get; init; }
    public int? ParcelaId { get; init; }
    public string Parcela { get; init; } = string.Empty;
    public int ClienteId { get; init; }
    public string Cliente { get; init; } = string.Empty;
    public AmbienteCobranca Ambiente { get; init; }
    public TipoCobranca Tipo { get; init; }
    public StatusCobranca Status { get; init; }
    public decimal Valor { get; init; }
    public DateOnly Vencimento { get; init; }
    public string? IdExterno { get; init; }
    public string? LinhaDigitavel { get; init; }
    public string? UrlBoleto { get; init; }
    public string? UrlFatura { get; init; }
    public string? PixCopiaECola { get; init; }
    public decimal MultaPercentual { get; init; }
    public decimal JurosMensalPercentual { get; init; }
}

/// <summary>Resultado da consulta dos pagamentos ao provedor.</summary>
public sealed record ResultadoConsultaCobrancas(int Consultadas, int Pagas, int Baixadas, IReadOnlyList<string> Avisos)
{
    public static readonly ResultadoConsultaCobrancas Vazio = new(0, 0, 0, []);
}

/// <summary>
/// Boleto e PIX registrados das parcelas (Asaas): emissão, segunda via, cancelamento e a consulta dos pagamentos, que baixa
/// as parcelas pagas sozinha na conta configurada. No ambiente simulado tudo acontece sem internet e o pagamento é simulado.
/// </summary>
public sealed class CobrancaService(IBuffetDbContextFactory fabrica, IProvedorCobranca provedor, AsaasOptions opcoes, BaixaService baixas,
    ISessaoUsuario sessao, TimeProvider relogio)
{
    /// <summary>Taxa usada no ambiente simulado (parecida com a do boleto/PIX do Asaas).</summary>
    public const decimal TaxaSimulada = 1.99m;

    private DateTime Agora => relogio.GetLocalNow().DateTime;
    private DateOnly Hoje => DateOnly.FromDateTime(Agora);

    public async Task<ConfiguracaoCobranca> ObterConfiguracaoAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        return await Configuracao(db, ct);
    }

    public async Task SalvarConfiguracaoAsync(ConfiguracaoCobranca configuracao, CancellationToken ct = default)
    {
        Validador.Validar(configuracao);
        await using var db = fabrica.Criar();
        if (configuracao.ContaFinanceiraId is { } contaId)
        {
            var conta = await db.ContasFinanceiras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contaId, ct)
                        ?? throw new RegraNegocioException("Conta não encontrada.");
            Validador.Garantir(!conta.EhCaixa, "Os pagamentos do boleto e do PIX caem numa conta bancária, não no caixa.");
        }
        var atual = await db.ConfiguracoesCobranca.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (atual is null)
            db.ConfiguracoesCobranca.Add(configuracao);
        else
        {
            Concorrencia.Conferir(atual, configuracao.Versao);
            db.Entry(atual).CurrentValues.SetValues(configuracao);
            db.Entry(atual).Property(c => c.Id).IsModified = false;
            db.Entry(atual).Property(c => c.CriadoEm).IsModified = false;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<PreparoCobranca> PrepararAsync(int parcelaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var configuracao = await Configuracao(db, ct);
        var parcela = await ParcelaAsync(db, parcelaId, ct);
        var pendente = await db.CobrancasRegistradas.AsNoTracking()
            .Where(c => c.ParcelaId == parcelaId && c.Status == StatusCobranca.Pendente).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);

        var cliente = parcela.Evento!.Cliente!;
        var documento = Documento.SomenteDigitos(cliente.Documento);
        return new PreparoCobranca
        {
            ParcelaId = parcela.Id,
            Parcela = $"{parcela.Descricao} - {parcela.Evento.Codigo} {parcela.Evento.Titulo}",
            Cliente = cliente.Nome,
            ClienteDocumento = documento.Length > 0 ? Documento.Formatar(documento) : null,
            Valor = parcela.Valor,
            VencimentoParcela = parcela.Vencimento,
            VencimentoSugerido = parcela.Vencimento >= Hoje ? parcela.Vencimento : Hoje.AddDays(3),
            TipoPadrao = configuracao.TipoPadrao,
            MultaPercentual = configuracao.MultaPercentual,
            JurosMensalPercentual = configuracao.JurosMensalPercentual,
            Ambiente = configuracao.Ambiente,
            Pendencias = Pendencias(configuracao, parcela, documento),
            CobrancaPendenteId = pendente
        };
    }

    /// <summary>Emite o boleto/PIX da parcela no provedor e grava a cobrança. Retorna o Id.</summary>
    public async Task<int> EmitirAsync(EmissaoCobrancaInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var configuracao = await Configuracao(db, ct);
        var parcela = await ParcelaAsync(db, input.ParcelaId, ct, rastrear: true);
        var cliente = parcela.Evento!.Cliente!;
        var documento = Documento.SomenteDigitos(cliente.Documento);
        var pendencias = Pendencias(configuracao, parcela, documento);
        Validador.Garantir(pendencias.Count == 0, "Não dá para emitir a cobrança: " + string.Join("; ", pendencias) + ".");
        Validador.Garantir(!await db.CobrancasRegistradas.AnyAsync(c => c.ParcelaId == parcela.Id && c.Status == StatusCobranca.Pendente, ct),
            "Esta parcela já tem uma cobrança pendente. Use a segunda via ou cancele a cobrança antes.");

        var cobranca = new CobrancaRegistrada(parcela, configuracao.Ambiente, input.Tipo, input.Vencimento, Hoje, sessao.Usuario?.Login, Agora);
        if (cobranca.Ambiente == AmbienteCobranca.Simulado)
        {
            cobranca.RegistrarEmissao($"sim_{Guid.NewGuid():N}"[..16], null, LinhaSimulada(cobranca), $"{parcela.Id:D8}", null, null,
                BrCodePix.Montar("simulado@buffetsharp.com.br", "BUFFETSHARP SIMULADO", "SAO PAULO", cobranca.Valor, cobranca.Referencia));
        }
        else
        {
            var emitida = await provedor.EmitirAsync(new PedidoCobranca(cobranca.Ambiente,
                new PagadorCobranca(documento, cliente.Nome, cliente.Email, cliente.Celular ?? cliente.Telefone, cliente.Endereco.Cep,
                    cliente.Endereco.Numero, $"cliente-{cliente.Id}"),
                cobranca.Tipo, cobranca.Valor, cobranca.Vencimento, $"{parcela.Descricao} - {parcela.Evento.Codigo} {parcela.Evento.Titulo}",
                cobranca.Referencia, configuracao.MultaPercentual, configuracao.JurosMensalPercentual), ct);
            cobranca.RegistrarEmissao(emitida.Id, emitida.ClienteId, emitida.LinhaDigitavel, emitida.NossoNumero, emitida.UrlBoleto,
                emitida.UrlFatura, emitida.PixCopiaECola);
        }
        db.CobrancasRegistradas.Add(cobranca);
        await db.SaveChangesAsync(ct);
        return cobranca.Id;
    }

    public async Task<CobrancaDetalhe> ObterAsync(int cobrancaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var c = await db.CobrancasRegistradas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cobrancaId, ct)
                ?? throw new RegraNegocioException("Cobrança não encontrada.");
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Cliente).FirstAsync(e => e.Id == c.EventoId, ct);
        var configuracao = await Configuracao(db, ct);
        return new CobrancaDetalhe
        {
            Id = c.Id, ParcelaId = c.ParcelaId, Parcela = $"{c.Descricao} - {evento.Codigo} {evento.Titulo}",
            ClienteId = evento.ClienteId, Cliente = evento.Cliente?.Nome ?? "", Ambiente = c.Ambiente, Tipo = c.Tipo, Status = c.Status,
            Valor = c.Valor, Vencimento = c.Vencimento, IdExterno = c.IdExterno, LinhaDigitavel = c.LinhaDigitavel, UrlBoleto = c.UrlBoleto,
            UrlFatura = c.UrlFatura, PixCopiaECola = c.PixCopiaECola, MultaPercentual = configuracao.MultaPercentual,
            JurosMensalPercentual = configuracao.JurosMensalPercentual
        };
    }

    /// <summary>Cancela a cobrança no provedor (o boleto deixa de valer). A parcela continua em aberto.</summary>
    public async Task CancelarAsync(int cobrancaId, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var cobranca = await db.CobrancasRegistradas.FirstOrDefaultAsync(c => c.Id == cobrancaId, ct)
                       ?? throw new RegraNegocioException("Cobrança não encontrada.");
        Validador.Garantir(cobranca.Pendente, "Só uma cobrança pendente pode ser cancelada.");
        if (cobranca.Ambiente != AmbienteCobranca.Simulado && cobranca.IdExterno is { } id)
        {
            // Se o cliente pagou nesse meio tempo, o pagamento vale mais que o cancelamento.
            var situacao = await provedor.ConsultarAsync(cobranca.Ambiente, id, ct);
            Validador.Garantir(situacao.Status != StatusCobranca.Paga,
                "O cliente já pagou esta cobrança. Use \"Verificar pagamentos\" para baixar a parcela.");
            if (situacao.Status != StatusCobranca.Cancelada)
                await provedor.CancelarAsync(cobranca.Ambiente, id, ct);
        }
        cobranca.Cancelar(Agora);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Treino: marca a cobrança simulada como paga (e baixa a parcela, como faria o pagamento de verdade).</summary>
    public async Task SimularPagamentoAsync(int cobrancaId, DateOnly? data = null, decimal? valorPago = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Criar();
        var cobranca = await db.CobrancasRegistradas.FirstOrDefaultAsync(c => c.Id == cobrancaId, ct)
                       ?? throw new RegraNegocioException("Cobrança não encontrada.");
        Validador.Garantir(cobranca.Ambiente == AmbienteCobranca.Simulado, "Só a cobrança do ambiente simulado pode ter o pagamento simulado.");
        var valor = valorPago ?? cobranca.Valor;
        await RegistrarPagamentoAsync(db, cobranca, new SituacaoCobranca(StatusCobranca.Paga, data ?? Hoje, valor, valor - TaxaSimulada,
            cobranca.Tipo == TipoCobranca.Pix ? TipoFormaPagamento.Pix : TipoFormaPagamento.Boleto), ct);
    }

    /// <summary>
    /// Pergunta ao provedor a situação das cobranças pendentes e baixa as parcelas pagas. Também avisa das cobranças pendentes
    /// de parcelas que deixaram de estar em aberto (canceladas ou recebidas por outro meio).
    /// </summary>
    public async Task<ResultadoConsultaCobrancas> VerificarPagamentosAsync(CancellationToken ct = default)
    {
        List<int> ids;
        await using (var db = fabrica.Criar())
        {
            ids = await db.CobrancasRegistradas.Where(c => c.Status == StatusCobranca.Pendente).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);
        }
        if (ids.Count == 0)
            return ResultadoConsultaCobrancas.Vazio;

        int consultadas = 0, pagas = 0, baixadas = 0;
        var avisos = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                var (consultada, paga, baixada) = await VerificarAsync(id, ct);
                consultadas += consultada ? 1 : 0;
                pagas += paga ? 1 : 0;
                baixadas += baixada ? 1 : 0;
            }
            catch (Exception ex) when (ex is RegraNegocioException or DbUpdateConcurrencyException)
            {
                avisos.Add(ex.Message);
            }
        }
        return new ResultadoConsultaCobrancas(consultadas, pagas, baixadas, avisos.Distinct().ToList());
    }

    /// <summary>Consulta uma cobrança pendente (consultada no provedor, paga, parcela baixada).</summary>
    private async Task<(bool Consultada, bool Paga, bool Baixada)> VerificarAsync(int cobrancaId, CancellationToken ct)
    {
        await using var db = fabrica.Criar();
        var cobranca = await db.CobrancasRegistradas.Include(c => c.Parcela).FirstAsync(c => c.Id == cobrancaId, ct);
        if (!cobranca.Pendente)
            return (false, false, false);

        if (cobranca.Ambiente != AmbienteCobranca.Simulado && cobranca.IdExterno is { } idExterno)
        {
            var situacao = await provedor.ConsultarAsync(cobranca.Ambiente, idExterno, ct);
            if (situacao.Status == StatusCobranca.Paga)
            {
                var baixada = await RegistrarPagamentoAsync(db, cobranca, situacao, ct);
                return (true, true, baixada);
            }
            if (situacao.Status == StatusCobranca.Cancelada)
                cobranca.Cancelar(Agora);
            else
                cobranca.Avisar(AvisoParcela(cobranca.Parcela));
            await db.SaveChangesAsync(ct);
            return (true, false, false);
        }

        cobranca.Avisar(AvisoParcela(cobranca.Parcela));
        await db.SaveChangesAsync(ct);
        return (false, false, false);
    }

    /// <summary>
    /// Grava o pagamento (a versão da cobrança impede que duas máquinas baixem a mesma parcela) e baixa a parcela na conta
    /// configurada. Se a baixa não der certo, o motivo fica na cobrança para a baixa manual.
    /// </summary>
    private async Task<bool> RegistrarPagamentoAsync(IBuffetDbContext db, CobrancaRegistrada cobranca, SituacaoCobranca situacao, CancellationToken ct)
    {
        var data = situacao.DataPagamento is { } d && d <= Hoje ? d : Hoje;
        var valorPago = situacao.ValorPago ?? cobranca.Valor;
        cobranca.RegistrarPagamento(data, valorPago, situacao.ValorLiquido, situacao.PagaCom);
        await db.SaveChangesAsync(ct);

        string? motivo = null;
        var parcela = await db.Parcelas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == cobranca.ParcelaId, ct);
        var configuracao = await Configuracao(db, ct);
        var tipoForma = situacao.PagaCom ?? (cobranca.Tipo == TipoCobranca.Pix ? TipoFormaPagamento.Pix : TipoFormaPagamento.Boleto);
        var forma = await db.FormasPagamento.AsNoTracking().Where(f => f.Ativo && f.Tipo == tipoForma).OrderBy(f => f.Id).FirstOrDefaultAsync(ct);
        if (parcela is null)
            motivo = "A parcela foi excluída (plano de pagamento refeito), mas o cliente pagou a cobrança: registre o valor no evento.";
        else if (parcela.Status != StatusTitulo.Aberto)
            motivo = parcela.Status == StatusTitulo.Pago
                ? "A parcela já estava recebida por outro meio: confira se o cliente pagou duas vezes."
                : "A parcela foi cancelada, mas o cliente pagou a cobrança: confira o plano de pagamento do evento.";
        else if (forma is null)
            motivo = $"Cadastre uma forma de pagamento ativa do tipo {tipoForma} e registre o recebimento.";
        else if (configuracao.ContaFinanceiraId is null)
            motivo = "Escolha a conta bancária na configuração da cobrança e registre o recebimento.";
        else
        {
            try
            {
                await baixas.RegistrarAsync(new BaixaInput
                {
                    Automatica = true,
                    Tipo = TipoBaixa.Recebimento,
                    Data = data,
                    Observacao = $"Cobrança {cobranca.IdExterno} paga ({(cobranca.Ambiente == AmbienteCobranca.Simulado ? "simulado" : "Asaas")})",
                    Titulos =
                    [
                        new BaixaTituloInput
                        {
                            TituloId = parcela.Id,
                            Valor = parcela.Valor,
                            Juros = Math.Max(0, valorPago - parcela.Valor),
                            Desconto = Math.Max(0, parcela.Valor - valorPago)
                        }
                    ],
                    Formas = [new BaixaFormaInput { FormaPagamentoId = forma.Id, Valor = valorPago, ContaFinanceiraId = configuracao.ContaFinanceiraId }]
                }, ct);
            }
            catch (RegraNegocioException ex)
            {
                motivo = $"Pagamento confirmado, mas a parcela não foi baixada: {ex.Message}";
            }
        }

        cobranca.RegistrarBaixa(motivo is null, motivo);
        await db.SaveChangesAsync(ct);
        return motivo is null;
    }

    private static string? AvisoParcela(Parcela? parcela) => parcela?.Status switch
    {
        null => "A parcela foi excluída (plano de pagamento refeito): cancele esta cobrança.",
        StatusTitulo.Cancelado => "A parcela foi cancelada: cancele esta cobrança para o cliente não pagar.",
        StatusTitulo.Pago => "A parcela já foi recebida por outro meio: cancele esta cobrança.",
        _ => null
    };

    private IReadOnlyList<string> Pendencias(ConfiguracaoCobranca configuracao, Parcela parcela, string documento)
    {
        var pendencias = configuracao.Pendencias().Select(p => $"Configuração da cobrança: falta {p}").ToList();
        if (configuracao.Ambiente != AmbienteCobranca.Simulado && string.IsNullOrWhiteSpace(opcoes.Chave(configuracao.Ambiente)))
            pendencias.Add($"Chave da API do Asaas ({(configuracao.Ambiente == AmbienteCobranca.Producao ? "produção" : "sandbox")}) não " +
                           $"configurada nesta máquina (appsettings.Local.json, seção \"{AsaasOptions.Secao}\")");
        if (!(Documento.CpfValido(documento) || Documento.CnpjValido(documento)))
            pendencias.Add("Cliente sem CPF/CNPJ válido no cadastro");
        if (parcela.Status != StatusTitulo.Aberto)
            pendencias.Add("A parcela não está em aberto");
        else if (parcela.Valor < 5)
            pendencias.Add("O Asaas só registra cobranças a partir de R$ 5,00");
        return pendencias;
    }

    private static async Task<ConfiguracaoCobranca> Configuracao(IBuffetDbContext db, CancellationToken ct) =>
        await db.ConfiguracoesCobranca.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct) ?? new ConfiguracaoCobranca();

    private static async Task<Parcela> ParcelaAsync(IBuffetDbContext db, int parcelaId, CancellationToken ct, bool rastrear = false)
    {
        var consulta = db.Parcelas.Include(p => p.Evento!).ThenInclude(e => e.Cliente).AsQueryable();
        if (!rastrear)
            consulta = consulta.AsNoTracking();
        return await consulta.FirstOrDefaultAsync(p => p.Id == parcelaId, ct) ?? throw new RegraNegocioException("Parcela não encontrada.");
    }

    /// <summary>Linha digitável de mentira (47 dígitos, com vencimento e valor no fim, como num boleto de verdade).</summary>
    private static string LinhaSimulada(CobrancaRegistrada cobranca)
    {
        var fator = Math.Clamp(cobranca.Vencimento.DayNumber - new DateOnly(2025, 2, 22).DayNumber + 1000, 1000, 9999);
        var numeros = new string(cobranca.Referencia.Where(char.IsAsciiDigit).ToArray()).PadRight(27, '0');
        var digitos = ("99990" + numeros)[..32] + "9" + $"{fator:D4}{(long)(cobranca.Valor * 100):D10}";
        return $"{digitos[..5]}.{digitos[5..10]} {digitos[10..15]}.{digitos[15..21]} {digitos[21..26]}.{digitos[26..32]} {digitos[32]} {digitos[33..]}";
    }
}
