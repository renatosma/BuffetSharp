using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Comercial;

/// <summary>Entrada (ou edição) na lista de espera de uma data, com as datas livres mais próximas como alternativa.</summary>
internal static class DialogosEspera
{
    /// <returns>Verdadeiro se a espera foi gravada.</returns>
    public static async Task<bool> EditarAsync(IWin32Window dono, Navegador navegador, EsperaInput input)
    {
        var lookups = navegador.Servico<IConsultasLookup>();
        var esperas = navegador.Servico<EsperaService>();
        var clientes = await lookups.ListarAsync(TipoLookup.Clientes);
        var leads = (await navegador.Servico<ICadastroService<Lead>>().ListarAsync(q => q.Where(l => l.Status == StatusLead.Novo ||
                l.Status == StatusLead.EmContato || l.Id == input.LeadId).OrderBy(l => l.Nome)))
            .Select(l => new ItemLookup { Id = l.Id, Nome = $"{l.Nome} (LEAD-{l.Id:D5})" }).ToList();
        var espacos = await lookups.ListarAsync(TipoLookup.Espacos);
        var tipos = await lookups.ListarAsync(TipoLookup.TiposEvento);
        var nova = input.Id == 0;

        var editor = new EditorCampos();
        if (nova)
        {
            editor.Lista("Cliente", clientes, () => input.ClienteId, v => input.ClienteId = v is > 0 ? v : null, nameof(input.ClienteId),
                permitirVazio: true);
            editor.Lista("Ou lead do funil", leads, () => input.LeadId, v => input.LeadId = v is > 0 ? v : null, nameof(input.LeadId),
                permitirVazio: true);
        }
        editor.Data("Data desejada", () => input.Data, v => input.Data = v ?? input.Data, nameof(input.Data));
        editor.Lista("Espaço (vazio = qualquer um)", espacos, () => input.EspacoId, v => input.EspacoId = v is > 0 ? v : null, nameof(input.EspacoId),
            permitirVazio: true);
        editor.Hora("Início", () => input.HoraInicio, v => input.HoraInicio = v);
        editor.Hora("Término", () => input.HoraFim, v => input.HoraFim = v);
        editor.Lista("Tipo de evento", tipos, () => input.TipoEventoId, v => input.TipoEventoId = v is > 0 ? v : null, nameof(input.TipoEventoId),
            permitirVazio: true);
        editor.Inteiro("Convidados", () => input.Convidados, v => input.Convidados = v, nameof(input.Convidados), 1, 10_000);
        editor.Texto("Observação", () => input.Observacao, v => input.Observacao = v, nameof(input.Observacao), EsperaData.TamanhoObservacao,
            span: 2, linhas: 2);

        using var dialogo = new FormEdicao(nova ? "Entrar na lista de espera" : "Editar espera",
            "Quem espera a mesma data e espaço forma uma fila pela ordem de entrada. Quando a data libera, o primeiro aparece nos lembretes " +
            "com o aviso pronto.", editor, () => esperas.SalvarAsync(input), 680);
        var alternativas = Tema.BotaoSecundario("Datas alternativas", Icones.Calendario);
        alternativas.Click += async (_, _) =>
        {
            try
            {
                editor.Aplicar();
                await MostrarAlternativasAsync(dialogo, esperas, input.Data, input.HoraInicio, input.HoraFim, input.EspacoId, input.Convidados);
            }
            catch (Exception ex)
            {
                Mensagens.Erro(dialogo, ex);
            }
        };
        dialogo.AdicionarAcao(alternativas);
        dialogo.TextoSalvar(nova ? "Entrar na espera" : "Salvar");
        return dialogo.ShowDialog(dono) == DialogResult.OK;
    }

    /// <summary>As datas livres mais próximas no mesmo dia da semana (para oferecer no lugar da ocupada).</summary>
    public static async Task MostrarAlternativasAsync(IWin32Window dono, EsperaService esperas, DateOnly data, TimeOnly inicio, TimeOnly fim,
        int? espacoId, int convidados)
    {
        var datas = await esperas.AlternativasAsync(data, inicio, fim, espacoId, convidados);
        Mensagens.Info(dono, datas.Count == 0
                ? "Não há data livre no mesmo dia da semana nos próximos seis meses (antes ou depois)."
                : $"Datas livres mais próximas de {data.ToString("dddd, dd/MM/yyyy", Textos.PtBr)}, no mesmo dia da semana:" + Environment.NewLine +
                  string.Join(Environment.NewLine, datas.Select(d => $"• {d.Data.ToString("dd/MM/yyyy (ddd)", Textos.PtBr)} - {d.Espaco}")),
            "Datas alternativas");
    }
}
