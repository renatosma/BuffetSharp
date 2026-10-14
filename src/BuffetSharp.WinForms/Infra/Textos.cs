using System.Globalization;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Comercial;
using BuffetSharp.Domain.Estoque;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Domain.Financeiro;
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
        TipoMovimentacao.Entrada => "Entrada",
        TipoMovimentacao.Saida => "Saída",
        TipoMovimentacao.Ajuste => "Ajuste",
        CategoriaDespesa.Manutencao => "Manutenção",
        CategoriaDespesa.Servicos => "Serviços",
        PerfilUsuario.Administrador => "Administrador",
        PerfilUsuario.Comercial => "Comercial",
        PerfilUsuario.Financeiro => "Financeiro",
        PerfilUsuario.Operacional => "Operacional",
        _ => valor.ToString()
    };

    public static Color Cor(Enum valor) => valor switch
    {
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
        StatusRsvp.Recusado => Tema.Perigo,
        TipoMovimentacao.Entrada => Tema.Sucesso,
        TipoMovimentacao.Saida => Tema.Perigo,
        TipoMovimentacao.Ajuste => Tema.Alerta,
        _ => Tema.Texto
    };

    /// <summary>Itens para combos de enum, na ordem de declaração.</summary>
    public static List<OpcaoEnum<TEnum>> Opcoes<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => new OpcaoEnum<TEnum>(v, Exibir(v))).ToList();
}

internal sealed record OpcaoEnum<TEnum>(TEnum Valor, string Texto) where TEnum : struct, Enum
{
    public override string ToString() => Texto;
}
