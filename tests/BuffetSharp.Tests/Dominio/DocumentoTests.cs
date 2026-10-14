using BuffetSharp.Domain.Comum;

namespace BuffetSharp.Tests.Dominio;

public class DocumentoTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-24", false)]
    [InlineData("111.111.111-11", false)]
    [InlineData("123", false)]
    [InlineData(null, false)]
    public void Valida_cpf(string? cpf, bool esperado) => Assert.Equal(esperado, Documento.CpfValido(cpf));

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000181", true)]
    [InlineData("11.222.333/0001-80", false)]
    [InlineData("00.000.000/0000-00", false)]
    public void Valida_cnpj(string cnpj, bool esperado) => Assert.Equal(esperado, Documento.CnpjValido(cnpj));

    [Fact]
    public void Formata_documentos()
    {
        Assert.Equal("529.982.247-25", Documento.Formatar("52998224725"));
        Assert.Equal("11.222.333/0001-81", Documento.Formatar("11222333000181"));
    }
}
