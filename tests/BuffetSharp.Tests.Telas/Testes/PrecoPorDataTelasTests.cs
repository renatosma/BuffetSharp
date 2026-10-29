using BuffetSharp.Tests.Telas.Infra;
using BuffetSharp.WinForms.Controles;
using BuffetSharp.WinForms.Formularios.Comercial;

namespace BuffetSharp.Tests.Telas.Testes;

/// <summary>Calendário de preços: os dias do mês com os ajustes das regras da base de demonstração.</summary>
public class PrecoPorDataTelasTests(AmbienteTelas ambiente)
{
    [Fact]
    public void Calendario_mostra_o_ajuste_de_cada_dia() =>
        Robo.Executar(ambiente, robo =>
        {
            var principal = robo.Entrar();
            var tela = robo.AbrirMenu<FormCalendarioPrecos>(principal, "Calendário de preços");
            var calendario = Robo.Interno<CalendarioPrecos>(tela, "_calendario");
            robo.Esperar(() => calendario.Dias.Count > 0, "dias do mês");

            Assert.Equal(DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), calendario.Dias.Count);
            Assert.All(calendario.Dias.Where(d => d.Data.DayOfWeek == DayOfWeek.Saturday),
                d => Assert.Contains(d.Ajuste.Regras, r => r.Nome is "Sábado" or "Réveillon"));
            Assert.All(calendario.Dias.Where(d => d.Data.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Tuesday),
                d => Assert.Contains(d.Ajuste.Regras, r => r.Nome is "Segunda a quinta" or "Réveillon"));

            robo.Clicar(tela, "▶");
            var proximo = DateTime.Today.AddMonths(1);
            robo.Esperar(() => calendario.Dias[0].Data.Month == proximo.Month, "mês seguinte");
            Assert.Contains("dia(s) com acréscimo", Robo.Interno<Label>(tela, "_legenda").Text);
        });
}
