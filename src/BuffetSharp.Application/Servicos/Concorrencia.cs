using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Application.Servicos;

/// <summary>
/// Concorrência otimista nas telas de edição: a tela guarda a <see cref="Entidade.Versao"/> que leu e o serviço confere,
/// antes de aplicar a alteração, se o registro no banco ainda está nessa versão. O DbContext repete a conferência no
/// UPDATE (a versão é um token de concorrência), cobrindo quem salvar no mesmo instante.
/// </summary>
public static class Concorrencia
{
    public const string Mensagem =
        "Este registro foi alterado ou excluído por outro usuário depois que você o abriu. Feche a tela, abra o registro de novo e refaça a alteração.";

    /// <param name="versaoLida">Versão lida pela tela; nula quando a tela não controla versão.</param>
    public static void Conferir(Entidade atual, int? versaoLida)
    {
        if (versaoLida is { } versao && atual.Versao != versao)
            throw new RegraNegocioException(Mensagem);
    }
}
