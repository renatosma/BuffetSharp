# BuffetSharp

Sistema de gestão para buffets e casas de eventos em **C# / .NET 10**, interface **Windows Forms**,
**Entity Framework Core 10** (escrita) e **Dapper** (leitura), sobre **SQL Server, PostgreSQL, MySQL ou Oracle**.

Ele cobre o ciclo inteiro do buffet: cadastros de apoio, orçamento, evento contratado, equipe,
convidados, estoque e financeiro, com painel gerencial e relatórios.

## Como executar

Pré-requisitos: **.NET SDK 10** e um banco de dados. Sem configuração nenhuma, o sistema usa o
**SQL Server LocalDB**, que vem com o Visual Studio. Para PostgreSQL, MySQL ou Oracle, veja
[Bancos de dados](#bancos-de-dados).

```bash
dotnet run --project src/BuffetSharp.WinForms
```

Na primeira execução, o sistema aplica as migrations, cria o banco `BuffetSharp` e gera uma **base de
demonstração**: cerca de 1 ano de eventos, orçamentos, parcelas, despesas e movimentações de estoque.
Todos os nomes e documentos dessa base são fictícios.

| Usuário       | Senha            | Perfil                                              |
|---------------|------------------|-----------------------------------------------------|
| `admin`       | `Admin@123`      | Administrador (acesso total)                        |
| `comercial`   | `Comercial@123`  | Orçamentos, clientes, eventos (vendedora, 3% de comissão) |
| `vendas`      | `Vendas@123`     | Comercial (vendedor da base de demonstração, 2,5%)  |
| `financeiro`  | `Financeiro@123` | Contas a receber/pagar, relatórios                  |
| `operacional` | `Operacional@123`| Eventos, cardápio, estoque, equipe                  |

As configurações ficam em `src/BuffetSharp.WinForms/appsettings.json`:

- `BancoDeDados:Provedor` e `ConnectionStrings:Buffet`: banco de dados (veja abaixo). Para usar uma
  conexão com usuário e senha só na sua máquina, copie `appsettings.Local.exemplo.json` para
  `appsettings.Local.json`. Esse arquivo fica fora do git e sobrescreve o `appsettings.json`.
- `Buffet`: dados da empresa (impressos na proposta e no contrato), intervalo mínimo entre eventos no
  mesmo espaço, validade padrão dos orçamentos, foro do contrato, prazo para confirmar convidados e
  limite de degustações por horário.
- `Email`: envio de propostas e contratos. No modo `Pasta` (padrão) cada e-mail vira um arquivo `.eml`
  em `EmailsEnviados`, sem sair da máquina. Para enviar de verdade, use `"Modo": "Smtp"` e preencha
  `Servidor`, `Porta`, `UsarSsl`, `Usuario`, `Senha` e `Remetente`.
- `Inicializacao:CriarDadosDemonstracao`: use `false` em produção. Assim, só os usuários iniciais são criados.

## Bancos de dados

| Banco | Versão mínima | `Provedor` | Exemplo de `ConnectionStrings:Buffet` |
|-------|---------------|------------|----------------------------------------|
| **SQL Server** (padrão) | 2016, Azure SQL, LocalDB | `SqlServer` | `Server=servidor;Database=BuffetSharp;User Id=...;Password=...;TrustServerCertificate=True` |
| **PostgreSQL** | 13 | `PostgreSql` | `Host=servidor;Database=buffetsharp;Username=...;Password=...` |
| **MySQL** | 8.0 | `MySql` | `Server=servidor;Database=buffetsharp;User Id=...;Password=...` |
| **Oracle** | 19c | `Oracle` | `User Id=buffetsharp;Password=...;Data Source=servidor:1521/SERVICO` |

```json
"BancoDeDados": { "Provedor": "PostgreSql" },
"ConnectionStrings": { "Buffet": "Host=localhost;Database=buffetsharp;Username=buffet;Password=..." }
```

Na primeira execução, o sistema cria as tabelas pelas migrations do banco escolhido. Também cria o
banco quando o usuário tem permissão para isso. No Oracle, o "banco" é o esquema do usuário da conexão,
que precisa existir com permissão para criar tabelas.

**Como funciona:**

- Cada banco tem um projeto próprio, `BuffetSharp.Infrastructure.{SqlServer|PostgreSql|MySql|Oracle}`,
  com o provedor do EF Core, as suas migrations, a conexão ADO.NET do Dapper e a tradução dos erros de
  integridade. O núcleo (`BuffetSharp.Infrastructure`) não depende de nenhum banco: carrega o projeto
  indicado em `Provedor`.
- **Nomes físicos:** o PostgreSQL usa nomes em minúsculas e o Oracle em maiúsculas, pois é assim que
  esses bancos tratam identificadores sem aspas. Por isso o mesmo SQL das consultas Dapper funciona nos
  quatro.
- **SQL portável:** as consultas usam SQL padrão. Datas e intervalos são calculados em C#, e `OUTER
  APPLY` vira `JOIN` com `ROW_NUMBER`. O que não tem forma comum fica em marcadores que o dialeto de cada
  banco traduz: booleano, limite de linhas, `FROM DUAL` e o prefixo de parâmetro (`:` no Oracle). No
  Oracle, que não executa vários comandos de uma vez, as consultas do painel rodam em sequência na mesma
  conexão.
- **Diferenças tratadas:**
  - Índices filtrados existem só no SQL Server e no PostgreSQL. Nos outros, a regra "um contrato
    vigente por evento" fica no serviço.
  - No Oracle, os textos longos usam `NCLOB`, as imagens usam `BLOB` e as datas usam `DATE`, tudo
    compatível com o 19c.
  - O PostgreSQL usa `timestamp without time zone`.
  - No MySQL, as datas sem hora são lidas como `DateTime`.

**Validação:** a suíte inteira (integração e telas) roda em **SQL Server, PostgreSQL 18 e MySQL 8.4**. O
**Oracle** não tem versão portátil para os testes locais. Ele é validado sem servidor: modelo do EF,
migrations em dia com o modelo e tradução do dialeto (`tests/BuffetSharp.Tests/Bancos`). Antes de
usar em produção, rode a suíte num Oracle de homologação (veja [Testes](#testes)).

## Arquitetura

```
BuffetSharp.WinForms                 Telas (Windows Forms), DI com Microsoft.Extensions.Hosting
        │
BuffetSharp.Infrastructure.{Banco}   Provedor do EF Core, migrations, conexão e erros de cada banco
        │
BuffetSharp.Infrastructure           EF Core 10 (DbContext, mapeamentos, seed, auditoria), Dapper (consultas
        │                            em SQL portável + dialeto), e-mail, hash de senha PBKDF2
BuffetSharp.Application              Serviços (casos de uso), modelos de entrada, contratos das consultas
        │
BuffetSharp.Domain                   Entidades e regras de negócio (sem dependências externas)
```

**Escrita com EF Core, leitura com Dapper (CQRS leve):**

- **EF Core 10** grava tudo que tem regra: as entidades carregam os invariantes (status, valores,
  limites) e o EF persiste o agregado inteiro. Também cuida das migrations. Os enums são gravados
  como texto, para o banco ficar legível. Erros de integridade (duplicidade, FK) viram mensagens de
  negócio. A auditoria é gravada pelo próprio `DbContext` a partir do ChangeTracker, na mesma transação
  dos dados.
- **Dapper** atende listagens, painel, agenda e relatórios com SQL explícito e portável. Um exemplo: o
  painel inteiro sai de **uma ida ao banco** (lote com 6 consultas), exceto no Oracle.
- No desktop, cada operação cria e descarta o próprio `DbContext` (`IDbContextFactory`). Isso evita
  contextos de vida longa presos às telas.

## Do cadastro mais simples ao mais completo

| Nível | Exemplos | Como é modelado |
|-------|----------|-----------------|
| **1 – Tabelas de apoio** | Tipos de evento, seções do cardápio, unidades, formas e condições de pagamento, modelos de contrato, cargos, espaços, serviços | Entidades simples com DataAnnotations. Uma **única tela genérica** (`FormCadastro<T>`) atende todas, configurada de forma declarativa em `DefinicoesCadastros.cs` |
| **2 – Cadastros intermediários** | Clientes (PF/PJ, CPF/CNPJ, endereço), fornecedores, funcionários, produtos | Validação de documento (`IValidatableObject`), tipo complexo `Endereco`, relacionamentos e listas do próprio cadastro (contatos e endereços adicionais, embalagens do produto), gravadas junto pela tela genérica. O saldo de estoque só muda por movimentação (é protegido contra sobrescrita pela tela) |
| **3 – Entidades ricas** | Ficha técnica, pacote, **orçamento**, **evento**, parcelas, contas a pagar | Setters privados, métodos de domínio e ciclo de vida com estados. Telas dedicadas |

## Funcionalidades

- **Painel**: eventos e faturamento do mês, recebido, inadimplência, contas a vencer, taxa de
  conversão, estoque a repor, gráfico contratado × recebido, próximos eventos.
- **Agenda**: calendário mensal por espaço, com as pré-reservas (orçamentos enviados) tracejadas.
- **Orçamentos**: pacote e espaço com valor negociável, serviços adicionais (fixos ou por pessoa),
  desconto, condição de pagamento com a simulação das parcelas e resumo em tempo real. A disponibilidade do espaço é verificada durante a edição.
  Ciclo Rascunho → Enviado → Aprovado/Recusado, duplicação e **proposta impressa**.
- **Eventos**: criados ao aprovar o orçamento ou em venda direta. A tela tem:
  - resumo com **resultado estimado** (receita − insumos − equipe − despesas);
  - insumos previstos;
  - escala da **equipe** com presença;
  - **convidados**, com importação colada do Excel, RSVP e **check-in**;
  - **plano de pagamento** com entrada e parcelas, preenchido pela condição de pagamento (mensais ou a cada N dias);
  - encerramento com **baixa automática de estoque** (ficha técnica × convidados);
  - cancelamento;
  - ficha do evento impressa.
- **Cardápio**: itens com ficha técnica (insumos por convidado) e custo por pessoa. Pacotes mostram
  custo e margem.
- **Estoque**: entrada (com **custo médio ponderado**), saída, inventário e kardex completo. A entrada aceita a
  **embalagem do fornecedor** (ex.: 2 caixas com 12 = 24 UN): quantidade e custo são convertidos para a unidade do
  estoque e o kardex mostra o que foi comprado.
- **Contatos e endereços**: clientes, fornecedores e funcionários têm, além do telefone, e-mail e endereço principais,
  quantos telefones, WhatsApps, e-mails e endereços adicionais forem precisos (com observação: "mãe da noiva",
  "financeiro", "cobrança"...). A pesquisa de clientes e fornecedores encontra também os contatos adicionais.
- **Condições de pagamento**: regras de prazo (entrada, número de parcelas, intervalo, quitação até N dias antes do
  evento), separadas das formas de pagamento (PIX, cartão...). Aparecem na proposta, no plano de pagamento e no contrato.
- **Financeiro**: contas a receber e a pagar, com pagamento (juros ou desconto pelo valor pago),
  estorno e despesas recorrentes. Despesas podem ser vinculadas a um evento.
- **Relatórios**: faturamento por tipo de evento, ranking de pacotes, lucratividade por evento
  (com o custo real do estoque após a realização), fluxo de caixa e conversão de orçamentos.
- **Envio da proposta**: por e-mail (corpo em HTML e proposta em PDF anexa) ou por WhatsApp (abre o
  WhatsApp com o resumo pronto). Cada envio fica no histórico e o rascunho passa a "Enviado".
- **Degustações**: agenda com limite de degustações por horário, avaliação de 1 a 5, faltas,
  cancelamento e taxa de conversão (clientes que fecharam depois da degustação).
- **Modelos de contrato**: textos-base com campos como `{{CLIENTE_NOME}}`, `{{DATA_EVENTO_EXTENSO}}`,
  `{{LOCAL_ENDERECO}}`, `{{VALOR_TOTAL_EXTENSO}}` e `{{PARCELAS}}`, trocados pelos dados do evento. As cláusulas são
  numeradas sozinhas (`CLÁUSULA {{N}}ª`). Pode haver um modelo por tipo de evento e um padrão; na emissão escolhe-se o
  modelo ou um contrato em branco. A tela de modelos lista os campos, pré-visualiza com um evento e duplica modelos.
- **Contratos com assinatura**: o contrato é gerado do evento pelo modelo (partes, data, local, cardápio, serviços,
  condição de pagamento, parcelas, rescisão e foro), revisado e emitido. O texto fica protegido por um código SHA-256. A
  assinatura do contratante (e do representante do buffet) é desenhada na tela com mouse, caneta ou
  toque. O contrato sai impresso ou em PDF com as assinaturas, e pode ser enviado por e-mail. Também é
  possível cancelar ou registrar o distrato e emitir um novo.
- **Materiais (louça, talheres, taças, enxoval, mobiliário)**: patrimônio próprio, reserva por evento
  com checagem do que está livre na data e aluguel de terceiros para o que faltar. A locação vira conta
  a pagar. Na devolução, as quebras e extravios entram no custo do evento e baixam o patrimônio.
- **Comissão de vendedores**: vendedor no orçamento e no evento, percentual por usuário (congelado na
  venda), apuração sobre os valores efetivamente recebidos e fechamento do período, que gera a conta a
  pagar.
- **Auditoria**: todo registro incluído, alterado ou excluído fica no histórico, com o usuário, a data
  e o valor anterior e o novo de cada campo. Senhas aparecem como "(oculto)". O administrador consulta
  tudo na tela de Auditoria ou o histórico de um registro pelo botão "Alterações".
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
- Só existe um contrato vigente por evento. Um contrato cujo texto não confere com o código de
  integridade não pode ser assinado. A assinatura exige CPF/CNPJ válido e a imagem da assinatura.
- O material próprio fica comprometido do dia anterior ao dia seguinte ao evento (ou até a devolução).
  A devolução precisa fechar com o enviado (devolvidos + perdas).
- A comissão incide sobre o que foi recebido, com o percentual vigente na venda. Períodos fechados de um
  mesmo vendedor não podem se sobrepor, e só se desfaz um fechamento cuja conta ainda não foi paga.
- Não há convidado duplicado nem funcionário escalado duas vezes. O check-in só vale no horário do
  evento.

## Testes

```bash
dotnet test
```

São **150 testes** (xUnit v3): 136 de domínio, integração e bancos, e 14 das telas.

- **Domínio**: CPF/CNPJ, cálculo do orçamento, ciclo de vida, parcelas, estoque e consumo do pacote, condições de
  pagamento, modelos de contrato, valor por extenso, conversão de embalagens e contatos adicionais.
- **Integração**: criam um banco temporário (por padrão no LocalDB), aplicam as migrations, carregam a
  base de demonstração e executam todas as consultas Dapper e os fluxos dos serviços (aprovação com
  conflito de agenda, pagamento, cancelamento, autenticação, tradução de erros de banco). O banco é
  excluído no final.
- **Bancos**: sem servidor, conferem o modelo de cada banco, se as migrations estão em dia e o dialeto SQL.

Para rodar as suítes de integração e de telas em outro banco, copie `BuffetSharp.runsettings.exemplo`
para `BuffetSharp.runsettings`, que fica fora do git porque contém senha, e ajuste o provedor e a conexão.
O Visual Studio e o `dotnet test` passam a usar esse arquivo. Use `{banco}` no lugar do nome do banco
temporário. Sem o arquivo, os testes usam o LocalDB. Também dá para usar variáveis de ambiente:

```bash
BUFFETSHARP_TEST_PROVIDER=PostgreSql BUFFETSHARP_TEST_CONNECTION="Host=localhost;Username=postgres;Password=...;Database={banco}" dotnet test
```

### Testes das telas

O projeto `tests/BuffetSharp.Tests.Telas` opera as **telas reais** do Windows Forms, como um usuário: faz
login, navega pelo menu, preenche campos pelo rótulo, clica nos botões e trata os diálogos. Cobre:

- login e menu de cada perfil;
- abertura de todas as telas do menu;
- cadastro com validação;
- orçamento com cálculo em tempo real e checagem do espaço;
- reserva de material, emissão de contrato e assinatura desenhada;
- degustação, comissão, envio de proposta por e-mail e WhatsApp;
- auditoria.

Como funcionam:

- Rodam dentro do processo de teste, num banco temporário com a base de demonstração.
- As janelas são criadas em uma **área de trabalho oculta do Windows**. Nada aparece na tela e o foco
  não é roubado, então dá para continuar trabalhando enquanto os testes rodam.
- As mensagens (TaskDialog) passam por um interceptador. Um diálogo modal que o teste não esperava é
  fechado e o teste falha com o nome do diálogo, em vez de travar.

```bash
dotnet test tests/BuffetSharp.Tests.Telas
```

## Migrations

Cada banco tem as suas migrations. Ao mudar o modelo, gere a migration **nos quatro** (o teste
`Migrations_do_banco_estao_atualizadas_com_o_modelo` acusa o que faltar):

```bash
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project src/BuffetSharp.Infrastructure.SqlServer --startup-project src/BuffetSharp.Infrastructure.SqlServer -o Migrations
dotnet ef migrations add NomeDaMigration --project src/BuffetSharp.Infrastructure.PostgreSql --startup-project src/BuffetSharp.Infrastructure.PostgreSql -o Migrations
dotnet ef migrations add NomeDaMigration --project src/BuffetSharp.Infrastructure.MySql --startup-project src/BuffetSharp.Infrastructure.MySql -o Migrations
dotnet ef migrations add NomeDaMigration --project src/BuffetSharp.Infrastructure.Oracle --startup-project src/BuffetSharp.Infrastructure.Oracle -o Migrations
```

Gerar migrations não exige servidor. O app aplica as pendentes automaticamente ao iniciar. Para
`dotnet ef database update`, informe a conexão na variável `BUFFETSHARP_CONNECTION`.

## Estrutura

```
src/
  BuffetSharp.Domain/          Comum, Cadastros, Estoque, Cardapio, Comercial, Eventos, Financeiro, Seguranca
  BuffetSharp.Application/     Abstracoes, Servicos, Consultas (contratos + DTOs), Modelos
  BuffetSharp.Infrastructure/  Persistencia (DbContext, Configuracoes, Provedores, Seed), Consultas (Dapper), Email, Seguranca
  BuffetSharp.Infrastructure.SqlServer|PostgreSql|MySql|Oracle/   Provedor e Migrations de cada banco
  BuffetSharp.WinForms/        Infra (tema, grade, editor de campos, navegação), Controles (gráfico, calendário,
                               indicadores), Formularios (telas por módulo), Impressao
tests/
  BuffetSharp.Tests/           Dominio, Integracao, Bancos
  BuffetSharp.Tests.Telas/     Infra (robô de telas, desktop oculto), Testes
```
