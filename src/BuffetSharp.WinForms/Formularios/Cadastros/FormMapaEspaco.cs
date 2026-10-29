using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>
/// Mapa padrão de mesas do espaço: as mesas são montadas e arrastadas na planta e gravadas ao salvar. Os eventos novos no
/// salão já nascem com este mapa.
/// </summary>
internal sealed class FormMapaEspaco : FormBase
{
    private readonly MapaMesasService _servico;
    private readonly Espaco _espaco;
    private readonly MapaMesas _mapa = new() { Dock = DockStyle.Fill };
    private readonly Label _resumo = new() { AutoSize = true, Font = Tema.FonteNegrito, Margin = new Padding(8, 8, 0, 0) };
    private List<MesaInput> _mesas = [];

    public FormMapaEspaco(MapaMesasService servico, Espaco espaco)
    {
        _servico = servico;
        _espaco = espaco;
        Text = $"Mapa de mesas - {espaco.Nome}";
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(Px(1080), Px(720));

        var cabecalho = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            Padding = new Padding(20, 16, 20, 4), BackColor = Tema.Superficie
        };
        cabecalho.Controls.Add(Tema.Titulo(Text));
        cabecalho.Controls.Add(Tema.Subtitulo("Arraste as mesas para a posição delas no salão. Os eventos novos neste espaço já nascem com este mapa."));

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 4, 16, 4), BackColor = Tema.Superficie };
        Botao(barra, Tema.BotaoPrimario("Nova mesa", Icones.Adicionar), () => EditarMesa(null));
        Botao(barra, Tema.BotaoSecundario("Editar mesa", Icones.Editar), () =>
        {
            if (Selecionada() is { } mesa)
                EditarMesa(mesa);
        });
        Botao(barra, Tema.BotaoSecundario("Remover mesa", Icones.Excluir), RemoverMesa);
        Botao(barra, Tema.BotaoSecundario("Várias mesas...", Icones.Lista), IncluirVarias);
        barra.Controls.Add(_resumo);

        _mapa.MesaMovida += (m, x, y) => (_mesas[m.Id - 1].X, _mesas[m.Id - 1].Y) = (x, y);
        _mapa.MesaAberta += m => EditarMesa(_mesas[m.Id - 1]);

        var acoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(12, 10, 16, 10),
            BackColor = Tema.Fundo
        };
        var cancelar = Tema.BotaoSecundario("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        var salvar = Tema.BotaoPrimario("Salvar", Icones.Salvar);
        salvar.Click += async (_, _) =>
        {
            if (await ExecutarAsync(() => _servico.SalvarDoEspacoAsync(_espaco.Id, _mesas), salvar))
                DialogResult = DialogResult.OK;
        };
        acoes.Controls.Add(cancelar);
        acoes.Controls.Add(salvar);
        CancelButton = cancelar;

        var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        corpo.Controls.Add(_mapa);
        Controls.Add(corpo);
        Controls.Add(barra);
        Controls.Add(cabecalho);
        Controls.Add(acoes);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ExecutarAsync(async () =>
        {
            _mesas = [.. await _servico.ObterDoEspacoAsync(_espaco.Id)];
            Exibir();
        });
    }

    private void Botao(FlowLayoutPanel barra, Button botao, Action acao)
    {
        botao.Click += (_, _) =>
        {
            try
            {
                acao();
            }
            catch (Exception ex)
            {
                Mensagens.Erro(this, ex);
            }
        };
        barra.Controls.Add(botao);
    }

    /// <summary>No mapa, o Id de cada mesa é a posição dela na lista (as mesas só são gravadas ao salvar).</summary>
    private void Exibir(int? selecionar = null)
    {
        var anterior = selecionar ?? _mapa.Selecionada?.Id;
        _mapa.Definir(_mesas.Select((m, i) => new MapaMesas.Mesa(i + 1, m.Numero, m.Nome, m.Lugares, m.Formato, m.X ?? 0, m.Y ?? 0, 0)).ToList());
        _mapa.Selecionar(anterior);
        _resumo.Text = $"{_mesas.Count} mesa(s)  •  {_mesas.Sum(m => m.Lugares)} lugares  •  capacidade do espaço: {_espaco.CapacidadeMaxima} pessoas";
    }

    private MesaInput? Selecionada() => _mapa.Selecionada is { } m ? _mesas[m.Id - 1] : null;

    private void EditarMesa(MesaInput? mesa)
    {
        var nova = mesa is null;
        var input = nova ? new MesaInput { Numero = _mesas.Count == 0 ? 1 : _mesas.Max(m => m.Numero) + 1 } : MesaInput.De(mesa!, mesa!.Id);
        int? formato = (int)input.Formato;
        var formatos = Enum.GetValues<FormatoMesa>().Select(f => new ItemLookup { Id = (int)f, Nome = Textos.Exibir(f) }).ToList();

        var editor = new EditorCampos();
        editor.Inteiro("Número", () => input.Numero, v => input.Numero = v, nameof(input.Numero), 1, PlantaSalao.NumeroMaximo);
        editor.Inteiro("Lugares", () => input.Lugares, v => input.Lugares = v, nameof(input.Lugares), 1, PlantaSalao.LugaresMaximo);
        editor.Lista("Formato", formatos, () => formato, v => formato = v);
        editor.Texto("Nome (opcional)", () => input.Nome, v => input.Nome = v, nameof(input.Nome), PlantaSalao.TamanhoNome,
            dica: "Noivos, família, crianças...");

        var indice = nova ? -1 : _mesas.IndexOf(mesa!);
        using var dialogo = new FormEdicao(nova ? "Nova mesa" : $"Editar {PlantaSalao.Descricao(mesa!.Numero, mesa.Nome)}", null, editor, () =>
        {
            input.Formato = (FormatoMesa)(formato ?? 0);
            input.Nome = string.IsNullOrWhiteSpace(input.Nome) ? null : input.Nome.Trim();
            Validador.Garantir(_mesas.Where((_, i) => i != indice).All(m => m.Numero != input.Numero), $"Já existe a mesa {input.Numero} no mapa.");
            Validador.Garantir(input.Lugares is >= 1 and <= PlantaSalao.LugaresMaximo, $"A mesa tem de 1 a {PlantaSalao.LugaresMaximo} lugares.");
            Validador.Garantir(!nova || _mesas.Count < PlantaSalao.MesasMaximo, $"O mapa pode ter no máximo {PlantaSalao.MesasMaximo} mesas.");
            return Task.CompletedTask;
        }, 560);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;

        if (nova)
        {
            (input.X, input.Y) = PlantaSalao.ProximaPosicao(_mesas.Cast<IMesaPlanta>());
            _mesas.Add(input);
            indice = _mesas.Count - 1;
        }
        else
            _mesas[indice] = input;
        Exibir(indice + 1);
    }

    private void RemoverMesa()
    {
        if (Selecionada() is not { } mesa)
            return;
        _mesas.Remove(mesa);
        _mapa.Selecionar(null);
        Exibir();
    }

    /// <summary>Inclui de uma vez várias mesas iguais (ex.: 20 redondas de 10 lugares), numeradas em sequência.</summary>
    private void IncluirVarias()
    {
        var quantidade = Math.Max(1, (_espaco.CapacidadeMaxima - _mesas.Sum(m => m.Lugares)) / PlantaSalao.LugaresPadrao);
        var lugares = PlantaSalao.LugaresPadrao;
        int? formato = (int)FormatoMesa.Redonda;
        var formatos = Enum.GetValues<FormatoMesa>().Select(f => new ItemLookup { Id = (int)f, Nome = Textos.Exibir(f) }).ToList();
        var editor = new EditorCampos();
        editor.Inteiro("Quantidade de mesas", () => quantidade, v => quantidade = v, null, 1, 100);
        editor.Inteiro("Lugares em cada uma", () => lugares, v => lugares = v, null, 1, PlantaSalao.LugaresMaximo);
        editor.Lista("Formato", formatos, () => formato, v => formato = v);
        using var dialogo = new FormEdicao("Incluir várias mesas", "As mesas entram numeradas em sequência, nos lugares livres da planta.", editor, () =>
        {
            Validador.Garantir(_mesas.Count + quantidade <= PlantaSalao.MesasMaximo, $"O mapa pode ter no máximo {PlantaSalao.MesasMaximo} mesas.");
            return Task.CompletedTask;
        }, 520);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;

        var numero = _mesas.Count == 0 ? 0 : _mesas.Max(m => m.Numero);
        for (var i = 0; i < quantidade; i++)
        {
            var mesa = new MesaInput { Numero = ++numero, Lugares = lugares, Formato = (FormatoMesa)(formato ?? 0) };
            (mesa.X, mesa.Y) = PlantaSalao.ProximaPosicao(_mesas.Cast<IMesaPlanta>());
            _mesas.Add(mesa);
        }
        Exibir();
    }
}
