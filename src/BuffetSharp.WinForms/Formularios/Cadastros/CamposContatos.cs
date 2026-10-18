using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comum;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.WinForms.Formularios.Cadastros;

/// <summary>
/// Seções "Outros telefones e e-mails" e "Outros endereços" dos cadastros de pessoas (clientes, fornecedores e
/// funcionários). Os contatos principais continuam nos campos do próprio cadastro.
/// </summary>
internal static class CamposContatos
{
    public static void Adicionar(EditorCampos e, IPossuiContatos dono)
    {
        e.Secao("Outros telefones e e-mails");
        var contatos = new GradeEditavel<ContatoAdicional>(() => new ContatoAdicional(), "Novo telefone / e-mail", "Remover contato")
            .Opcoes("Tipo", nameof(ContatoAdicional.Tipo),
                Textos.Opcoes<TipoContato>().Select(o => new OpcaoGrade<TipoContato>(o.Valor, o.Texto)), 22)
            .Texto("Número ou e-mail", nameof(ContatoAdicional.Valor), 45, 150)
            .Texto("Observação (de quem é, para que serve)", nameof(ContatoAdicional.Observacao), 45, 100);
        // Telefones ficam no formato (11) 98765-4321 ao sair da célula.
        contatos.Controle.CellEndEdit += (_, ev) =>
        {
            if (contatos.Controle.Rows[ev.RowIndex].DataBoundItem is ContatoAdicional { EhEmail: false } contato)
            {
                contato.Valor = Documento.FormatarTelefone(contato.Valor);
                contatos.Controle.InvalidateRow(ev.RowIndex);
            }
        };
        e.Livre(contatos.Painel);
        e.Associar(nameof(IPossuiContatos.Contatos), contatos.Controle);
        e.AoCarregar(() => contatos.Carregar(dono.Contatos));
        e.AoAplicar(() =>
        {
            contatos.ConcluirEdicao();
            dono.Contatos.Clear();
            dono.Contatos.AddRange(contatos.Itens.Where(c => !string.IsNullOrWhiteSpace(c.Valor) || !c.EhNovo));
        });

        e.Secao("Outros endereços");
        var enderecos = new List<EnderecoAdicional>();
        var grade = new Grade<EnderecoAdicional>()
            .Texto("Identificação", x => x.Identificacao, 25)
            .Texto("Endereço", x => x.Endereco.ToString(), 100);
        grade.Controle.Dock = DockStyle.Fill;
        grade.Controle.Height = 110;
        grade.Controle.MinimumSize = new Size(0, 110);
        grade.Controle.MaximumSize = new Size(0, 110);

        var incluir = Tema.BotaoSecundario("Novo endereço", Icones.Adicionar);
        var editar = Tema.BotaoSecundario("Editar endereço", Icones.Editar);
        var remover = Tema.BotaoSecundario("Remover endereço", Icones.Excluir);
        incluir.Click += (_, _) =>
        {
            var novo = new EnderecoAdicional();
            if (EditarEndereco(grade.Controle.FindForm(), novo, "Novo endereço"))
            {
                enderecos.Add(novo);
                grade.Carregar(enderecos);
                grade.Selecionar(novo);
            }
        };
        void EditarSelecionado()
        {
            if (grade.Selecionado is { } item && EditarEndereco(grade.Controle.FindForm(), item, "Editar endereço"))
            {
                grade.Carregar(enderecos);
                grade.Selecionar(item);
            }
        }
        editar.Click += (_, _) => EditarSelecionado();
        grade.DuploClique += _ => EditarSelecionado();
        remover.Click += (_, _) =>
        {
            if (grade.Selecionado is { } item)
            {
                enderecos.Remove(item);
                grade.Carregar(enderecos);
            }
        };

        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
        barra.Controls.AddRange([incluir, editar, remover]);
        var painel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        painel.Controls.Add(barra);
        painel.Controls.Add(grade.Controle);
        e.Livre(painel);
        e.Associar(nameof(IPossuiContatos.Enderecos), grade.Controle);
        e.AoCarregar(() =>
        {
            enderecos.Clear();
            enderecos.AddRange(dono.Enderecos);
            grade.Carregar(enderecos);
        });
        e.AoAplicar(() =>
        {
            dono.Enderecos.Clear();
            dono.Enderecos.AddRange(enderecos);
        });
    }

    /// <summary>Edita uma cópia do endereço; os valores só voltam para o item se o usuário salvar.</summary>
    private static bool EditarEndereco(IWin32Window? dono, EnderecoAdicional item, string titulo)
    {
        var identificacao = item.Identificacao;
        var origem = item.Endereco;
        var copia = new Endereco
        {
            Cep = origem.Cep, Logradouro = origem.Logradouro, Numero = origem.Numero, Complemento = origem.Complemento,
            Bairro = origem.Bairro, Cidade = origem.Cidade, Uf = origem.Uf
        };

        var e = new EditorCampos(3);
        e.Texto("Identificação", () => identificacao, v => identificacao = v, "Identificacao", 60, span: 3,
            dica: "Ex.: Cobrança, Entrega, Casa de praia");
        DefinicoesCadastros.CamposEndereco(e, copia);
        using var dialogo = new FormEdicao(titulo, "O endereço só é gravado quando o cadastro for salvo.", e, () =>
        {
            Validador.Garantir(!string.IsNullOrWhiteSpace(copia.Logradouro) && !string.IsNullOrWhiteSpace(copia.Cidade),
                "Informe ao menos o logradouro e a cidade.");
            return Task.CompletedTask;
        }, 760);
        if (dialogo.ShowDialog(dono) != DialogResult.OK)
            return false;

        item.Identificacao = identificacao;
        item.Endereco = copia;
        return true;
    }
}
