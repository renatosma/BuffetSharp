using BuffetSharp.Application.Consultas;
using BuffetSharp.Application.Modelos;
using BuffetSharp.Application.Servicos;
using BuffetSharp.Domain.Cadastros;
using BuffetSharp.Domain.Eventos;
using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Formularios.Eventos;
using BuffetSharp.WinForms.Infra;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Aba Equipe: o funcionário ocupado aparece como tal na escolha e o administrador escala com exceção e motivo.</summary>
public class EscalaEquipeTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Administrador_escala_funcionario_ocupado_informando_o_motivo() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var lookups = robo.Servico<IConsultasLookup>();
            var eventos = robo.Servico<EventoService>();
            var espacos = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Espacos)).Where(e => e.Numero >= 40).OrderBy(e => e.Numero).Take(2).ToList();
            var clienteId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Clientes)).First().Id;
            var tipoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.TiposEvento)).First().Id;
            var dia = DateOnly.FromDateTime(DateTime.Today).AddYears(4).AddDays(130);
            int Evento(int espacoId, int hora) => robo.Rodar(() => eventos.SalvarAsync(new EventoInput
            {
                Titulo = $"Escala do teste de tela {hora}h", ClienteId = clienteId, TipoEventoId = tipoId, EspacoId = espacoId, Data = dia,
                HoraInicio = new TimeOnly(hora, 0), HoraFim = new TimeOnly(hora + 3, 0), Convidados = 40, ValorTotal = 5000m
            }));
            var primeiro = Evento(espacos[0].Id, 12);
            var segundo = Evento(espacos[1].Id, 18);
            var cargoId = robo.Rodar(() => lookups.ListarAsync(TipoLookup.Cargos)).First().Id;
            var funcionario = robo.Rodar(() => robo.Servico<ICadastroService<Funcionario>>().SalvarAsync(new Funcionario
            {
                Nome = "Garçom da escala de tela", CargoId = cargoId, ValorDiaria = 180, Ativo = true
            }));
            robo.Rodar(() => eventos.EscalarFuncionarioAsync(primeiro, funcionario.Id, "Garçom", null));

            var navegador = robo.Servico<Navegador>();
            navegador.Abrir($"evento:{segundo}", () => navegador.Criar<FormEventoDetalhe>(segundo));
            robo.Esperar(() => Robo.AbaAtual(principal) is FormEventoDetalhe tela && Robo.Carregado(tela), "central do evento");
            var evento = (FormEventoDetalhe)Robo.AbaAtual(principal)!;
            robo.SelecionarAba(evento, "Equipe");

            var combo = Robo.Interno<ComboBox>(evento, "_funcionario");
            var situacao = combo.Items.Cast<SituacaoFuncionario>().Single(s => s.FuncionarioId == funcionario.Id);
            Assert.StartsWith("ocupado", situacao.Resumo);
            combo.SelectedItem = situacao;

            robo.Dialogo<FormEdicao>(() => robo.Clicar(evento, "Escalar"), dialogo =>
            {
                robo.Preencher(dialogo, "Motivo da exceção", "Cobrir a falta de um garçom");
                robo.Clicar(dialogo, "Escalar mesmo assim");
            }, "exceção ao conflito");

            var equipe = Robo.Interno<Grade<EventoFuncionario>>(evento, "_equipe");
            robo.Esperar(() => equipe.Itens.Any(q => q.FuncionarioId == funcionario.Id), "funcionário na equipe");
            Assert.Equal(("Cobrir a falta de um garçom", "admin"),
                (equipe.Itens.Single().ExcecaoConflito, equipe.Itens.Single().ExcecaoAutorizadaPor));
        });
}
