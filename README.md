# BuffetSharp

Sistema de gestão para buffets e casas de eventos em **C# / .NET 10**, interface **Windows Forms**,
**Entity Framework Core 10** (escrita) e **Dapper** (leitura), sobre **SQL Server**.

Ele cobre o ciclo inteiro do buffet: cadastros de apoio, orçamento, evento contratado, equipe,
convidados, estoque e financeiro, com painel gerencial e relatórios.

## Como executar

Pré-requisitos: **.NET SDK 10** e **SQL Server LocalDB**, que vem com o Visual Studio. Outra instância
de SQL Server também funciona: basta ajustar a connection string.

```bash
dotnet run --project src/BuffetSharp.WinForms
```

Na primeira execução, o sistema aplica as migrations, cria o banco `BuffetSharp` e gera uma **base de
demonstração**: cerca de 1 ano de eventos, orçamentos, parcelas, despesas e movimentações de estoque.
Todos os nomes e documentos dessa base são fictícios.

| Usuário       | Senha            | Perfil                                              |
|---------------|------------------|-----------------------------------------------------|
| `admin`       | `Admin@123`      | Administrador (acesso total)                        |
| `comercial`   | `Comercial@123`  | Orçamentos, clientes, eventos                       |
| `financeiro`  | `Financeiro@123` | Contas a receber/pagar, relatórios                  |
| `operacional` | `Operacional@123`| Eventos, cardápio, estoque, equipe                  |

As configurações ficam em `src/BuffetSharp.WinForms/appsettings.json`:

- `ConnectionStrings:Buffet`: banco de dados.
- `Buffet`: dados da empresa (impressos na proposta), intervalo mínimo entre eventos no mesmo espaço
  e validade padrão dos orçamentos.
- `Inicializacao:CriarDadosDemonstracao`: use `false` em produção. Assim, só os usuários iniciais são criados.

## Arquitetura

```
BuffetSharp.WinForms         Telas (Windows Forms), DI com Microsoft.Extensions.Hosting
        │
BuffetSharp.Infrastructure   EF Core 10 (DbContext, mapeamentos, migrations, seed)
        │                    Dapper (consultas SQL), hash de senha PBKDF2
BuffetSharp.Application      Serviços (casos de uso), modelos de entrada, contratos das consultas
        │
BuffetSharp.Domain           Entidades e regras de negócio (sem dependências externas)
```

**Escrita com EF Core, leitura com Dapper (CQRS leve):**

- **EF Core 10** grava tudo que tem regra: as entidades carregam os invariantes (status, valores,
  limites) e o EF persiste o agregado inteiro. Também cuida das migrations. Os enums são gravados
  como texto, para o banco ficar legível. Erros de integridade (duplicidade, FK) viram mensagens de
  negócio.
- **Dapper** atende listagens, painel, agenda e relatórios com SQL explícito. Um exemplo: o painel
  inteiro sai de **uma ida ao banco** (`QueryMultiple` com 5 resultados).
- No desktop, cada operação cria e descarta o próprio `DbContext` (`IDbContextFactory`). Isso evita
  contextos de vida longa presos às telas.

## Do cadastro mais simples ao mais completo

| Nível | Exemplos | Como é modelado |
|-------|----------|-----------------|
| **1 – Tabelas de apoio** | Tipos de evento, seções do cardápio, unidades, formas de pagamento, cargos, espaços, serviços | Entidades simples com DataAnnotations. Uma **única tela genérica** (`FormCadastro<T>`) atende todas, configurada de forma declarativa em `DefinicoesCadastros.cs` |
| **2 – Cadastros intermediários** | Clientes (PF/PJ, CPF/CNPJ, endereço), fornecedores, funcionários, produtos | Validação de documento (`IValidatableObject`), tipo complexo `Endereco`, relacionamentos. O saldo de estoque só muda por movimentação (é protegido contra sobrescrita pela tela) |
| **3 – Entidades ricas** | Ficha técnica, pacote, **orçamento**, **evento**, parcelas, contas a pagar | Setters privados, métodos de domínio e ciclo de vida com estados. Telas dedicadas |

## Funcionalidades

- **Painel**: eventos e faturamento do mês, recebido, inadimplência, contas a vencer, taxa de
  conversão, estoque a repor, gráfico contratado × recebido, próximos eventos.
- **Agenda**: calendário mensal por espaço, com as pré-reservas (orçamentos enviados) tracejadas.
- **Orçamentos**: pacote e espaço com valor negociável, serviços adicionais (fixos ou por pessoa),
  desconto e resumo em tempo real. A disponibilidade do espaço é verificada durante a edição.
  Ciclo Rascunho → Enviado → Aprovado/Recusado, duplicação e **proposta impressa**.
- **Eventos**: criados ao aprovar o orçamento ou em venda direta. A tela tem:
  - resumo com **resultado estimado** (receita − insumos − equipe − despesas);
  - insumos previstos;
  - escala da **equipe** com presença;
  - **convidados**, com importação colada do Excel, RSVP e **check-in**;
  - **plano de pagamento** com entrada e parcelas;
  - encerramento com **baixa automática de estoque** (ficha técnica × convidados);
  - cancelamento;
  - ficha do evento impressa.
- **Cardápio**: itens com ficha técnica (insumos por convidado) e custo por pessoa. Pacotes mostram
  custo e margem.
- **Estoque**: entrada (com **custo médio ponderado**), saída, inventário e kardex completo.
- **Financeiro**: contas a receber e a pagar, com pagamento (juros ou desconto pelo valor pago),
  estorno e despesas recorrentes. Despesas podem ser vinculadas a um evento.
- **Relatórios**: faturamento por tipo de evento, ranking de pacotes, lucratividade por evento
  (com o custo real do estoque após a realização), fluxo de caixa e conversão de orçamentos.
- **Usuários e perfis**: o menu é filtrado pelo perfil e as senhas usam hash PBKDF2-SHA256.
- Em todas as listagens: pesquisa instantânea, ordenação, **impressão** e **exportação CSV** (Excel).

## Principais regras de negócio

- O orçamento respeita o **mínimo de convidados do pacote** e a **capacidade do espaço**. O desconto
  é limitado a 30%.
- Um espaço não aceita dois eventos sobrepostos. A regra considera o **intervalo de montagem**,
  configurável, e eventos que terminam após a meia-noite.
- Não é possível aprovar orçamento expirado, sem espaço ou com data passada.
- O plano de pagamento preserva as parcelas pagas e recalcula o saldo. Os centavos vão para a última
  parcela.
- Cancelar o evento cancela as parcelas em aberto. Evento realizado ou cancelado não pode mais ser
  alterado.
- Não há convidado duplicado nem funcionário escalado duas vezes. O check-in só vale no horário do
  evento.

## Testes

```bash
dotnet test
```

São **56 testes** (xUnit v3):

- **Domínio**: CPF/CNPJ, cálculo do orçamento, ciclo de vida, parcelas, estoque e consumo do pacote.
- **Integração**: criam um banco temporário no LocalDB, aplicam as migrations, carregam a base de
  demonstração e executam todas as consultas Dapper e os fluxos dos serviços (aprovação com conflito
  de agenda, pagamento, cancelamento, autenticação, tradução de erros de banco). O banco é excluído
  no final.

Para usar outro servidor nos testes, defina `BUFFETSHARP_TEST_CONNECTION`.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project src/BuffetSharp.Infrastructure --startup-project src/BuffetSharp.Infrastructure --output-dir Persistencia/Migrations
```

O app aplica as migrations pendentes automaticamente ao iniciar. Em tempo de design, a conexão pode
ser trocada pela variável `BUFFETSHARP_CONNECTION`.

## Estrutura

```
src/
  BuffetSharp.Domain/          Comum, Cadastros, Estoque, Cardapio, Comercial, Eventos, Financeiro, Seguranca
  BuffetSharp.Application/     Abstracoes, Servicos, Consultas (contratos + DTOs), Modelos
  BuffetSharp.Infrastructure/  Persistencia (DbContext, Configuracoes, Migrations, Seed), Consultas (Dapper), Seguranca
  BuffetSharp.WinForms/        Infra (tema, grade, editor de campos, navegação), Controles (gráfico, calendário,
                               indicadores), Formularios (telas por módulo), Impressao
tests/
  BuffetSharp.Tests/           Dominio, Integracao
```
