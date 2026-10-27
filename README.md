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
- `Pix`: chave PIX do buffet (CPF/CNPJ, e-mail, celular ou chave aleatória), nome do recebedor e cidade. Sem chave, a
  cobrança por PIX fica desligada.
- `Nfse`: tokens do [Focus NFe](https://focusnfe.com.br) para emitir a NFS-e (`TokenHomologacao` e `TokenProducao`). Coloque-os
  só no `appsettings.Local.json`. Sem token, só o ambiente simulado funciona (nota de treino, sem valor fiscal).
- `Asaas`: chaves da API do [Asaas](https://www.asaas.com) para o boleto e o PIX registrados (`ChaveSandbox` e `ChaveProducao`, só no
  `appsettings.Local.json`) e `IntervaloConsultaMinutos`, de quanto em quanto tempo os pagamentos são conferidos (0 = só pelo botão).
- `Lembretes`: antecedência (em dias) para avisar das propostas, parcelas e contas a pagar que vencem,
  `EmailAutomatico` para enviar sozinho os e-mails de parcela a vencer, degustação e aniversário, e `WhatsAppAutomatico`
  para enviar sozinho pelo WhatsApp do buffet esses lembretes e o da parcela vencida (depois do login e a cada meia hora).
- `WhatsApp`: `Modo` `Link` (padrão: abre o WhatsApp do computador com a mensagem pronta), `Pasta` (grava cada mensagem
  em `.txt`, para treinar) ou `ZApi` (envia direto pelo número do buffet conectado na [Z-API](https://www.z-api.io), com
  `Instancia`, `Token` e `ClientToken` só no `appsettings.Local.json`). `HorarioInicio` e `HorarioFim` limitam os envios
  automáticos ao horário comercial.
- `Backup`: pasta das cópias de segurança, cópia automática (a cada `IntervaloHoras`, depois do login), quantas cópias
  manter e o caminho do `pg_dump`/`mysqldump` (`Ferramenta`) quando não estiverem no PATH. No SQL Server, o próprio
  servidor grava o arquivo: num servidor em outra máquina, use uma pasta do servidor.
- `Log` e `Logging`: pasta e retenção do log em arquivo (um por dia) e os níveis gravados.

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
  desconto, condição de pagamento com a simulação das parcelas, forma de pagamento (PIX, cartão, boleto...) e resumo em tempo real. A disponibilidade do espaço é verificada durante a edição.
  Ciclo Rascunho → Enviado → Aprovado/Recusado, duplicação e **proposta impressa**.
- **Faixas etárias**: cada pacote define até que idade a criança é isenta, até que idade paga meia e o percentual da
  meia (padrão: até 5 anos isenta, de 6 a 10 anos meia de 50%). No orçamento e no evento informam-se o total de
  convidados e quantas crianças pagam meia ou são isentas. A meia pode ser negociada. A regra fica congelada no
  orçamento e passa para o evento, a proposta (impressa, e-mail e WhatsApp) separa inteira, meia e isentos, e o contrato
  ganha os campos `{{CONVIDADOS_DETALHE}}`, `{{CONVIDADOS_INTEIRA}}`, `{{CONVIDADOS_MEIA}}`, `{{CONVIDADOS_ISENTOS}}`,
  `{{FAIXAS_ETARIAS}}` e `{{VALOR_MEIA}}`.
- **Eventos**: criados ao aprovar o orçamento ou em venda direta. A tela tem:
  - resumo com **resultado estimado** (receita − insumos − equipe − despesas);
  - insumos previstos;
  - escala da **equipe** com presença;
  - **convidados**, com importação colada do Excel, RSVP e **check-in**;
  - **convidados excedentes**: a contagem de quem veio (o check-in já vem preenchido, mais quem entrou pela portaria, com
    as crianças de meia e isentas) é comparada com o contratado e o que passou vira uma cobrança numa parcela própria, ao
    valor por pessoa do contrato, que pode sair na hora pelo PIX. Ao realizar o evento, se o check-in passou do
    contratado, o sistema oferece apurar antes. A apuração pode ser desfeita enquanto a cobrança não foi paga;
  - **plano de pagamento** com entrada e parcelas, preenchido pela condição de pagamento (mensais ou a cada N dias);
  - encerramento com **baixa automática de estoque** (ficha técnica × convidados, contando os excedentes já apurados);
  - cancelamento;
  - ficha do evento impressa.
- **Cardápio**: itens com ficha técnica (insumos por convidado) e custo por pessoa. Pacotes mostram
  custo e margem.
- **Estoque**: entrada (com **custo médio ponderado**), saída, inventário e kardex completo. A entrada aceita a
  **embalagem do fornecedor** (ex.: 2 caixas com 12 = 24 UN): quantidade e custo são convertidos para a unidade do
  estoque e o kardex mostra o que foi comprado. Na mesma tela, a compra já é lançada em **contas a pagar** (marcado por
  padrão): conta única ou parcelada, com categoria, nota fiscal e vencimento, gravada junto com a entrada. A entrada fica
  ligada à conta: o kardex mostra a situação do pagamento e a conta mostra de qual entrada veio. Uma entrada lançada por
  engano é **estornada** pelo kardex: o saldo e o custo médio voltam e a conta da compra é cancelada (recusado se a conta
  já foi paga ou se o produto já foi usado; nesse caso, corrija pelo inventário).
- **Contatos e endereços**: clientes, fornecedores e funcionários têm, além do telefone, e-mail e endereço principais,
  quantos telefones, WhatsApps, e-mails e endereços adicionais forem precisos (com observação: "mãe da noiva",
  "financeiro", "cobrança"...). A pesquisa de clientes e fornecedores encontra também os contatos adicionais.
- **Condições de pagamento**: cada parcela definida com o seu prazo em dias e o seu percentual (ex.: 28 dias 50% + 42
  dias 50%, ou 30% à vista + 35% em 30 dias + 35% em 60 dias), ou a regra de entrada + parcelas iguais; com quitação
  até N dias antes do evento. As parcelas ficam numa grade, com o vencimento e o valor de exemplo ao lado, o total dos
  percentuais e um preenchimento rápido (N parcelas, a 1ª em X dias, a cada Y dias, com entrada). Cada parcela pode ter a
  sua forma (ex.: entrada no PIX, demais no boleto); sem ela, vale a forma geral do pedido, da nota ou do evento, e cada
  conta a pagar ou parcela do evento nasce com a sua forma. Ficam separadas das
  formas de pagamento (PIX, cartão...). A condição e a forma escolhidas no orçamento aparecem na
  proposta (impressa, e-mail e WhatsApp), passam ao evento e ao contrato (`{{CONDICAO_PAGAMENTO}}` e `{{FORMA_PAGAMENTO}}`)
  e preenchem o plano de pagamento.
- **Financeiro**: contas a receber e a pagar, com pagamento (juros ou desconto pelo valor pago),
  estorno e despesas recorrentes. Despesas podem ser vinculadas a um evento.
- **Parcelamento de contas a pagar**: como o plano de pagamento do evento. A **compra parcelada** divide o valor total
  em entrada + parcelas, mensais ou a cada N dias, e pode ser preenchida por uma condição de pagamento. Em **juntar e
  parcelar**, várias contas em aberto do mesmo fornecedor viram um parcelamento novo: as originais ficam canceladas e
  ligadas a ele. As parcelas levam o código do parcelamento (`PAR-00001`) e o documento (NF, boleto). Enquanto nenhuma
  parcela for paga, o parcelamento pode ser desfeito e as contas juntadas voltam a ficar em aberto. Pagar várias contas
  do mesmo fornecedor de uma vez continua no "Pagar", como no recebimento.
- **Relatórios**: faturamento por tipo de evento, ranking de pacotes, lucratividade por evento
  (com o custo real do estoque após a realização), fluxo de caixa, conversão de orçamentos, funil de leads por canal de
  origem e **DRE** gerencial mês a mês (regime de caixa ou de competência), com a receita por tipo de evento e os custos
  e despesas por centro de custo.
- **Leads (funil de vendas)**: o interessado entra antes de virar cliente, com a origem (Instagram, indicação, feira...),
  o tipo de evento, a data e os convidados pretendidos e o responsável. Cada contato fica no histórico e combina o
  próximo retorno. Convertido, vira cliente (cadastrado com os dados do lead ou ligado a um existente) e já abre o
  orçamento preenchido; perdido, guarda o motivo e pode ser reaberto.
- **Lembretes do dia**: propostas perto de vencer, degustações de hoje e amanhã, aniversários de clientes, retornos de
  leads, parcelas a vencer e vencidas, contas a pagar, materiais não devolvidos e tarefas atrasadas dos eventos, conforme
  o perfil. As mensagens ao cliente saem prontas para o WhatsApp ou o e-mail (a cobrança leva o boleto registrado ou o PIX "copia e cola");
  depois de tratado, o lembrete some. O painel mostra quantos há. Opcionalmente, os e-mails saem sozinhos.
- **Reajuste de preços**: percentual sobre os pacotes, serviços e espaços, com arredondamento (centavos, real inteiro ou
  múltiplo de R$ 5), simulação antes de aplicar e vigência futura (aplicado sozinho na data). Pode ser desfeito.
- **Compras** (cotação → pedido → recebimento → nota fiscal de entrada):
  - **Cotação**: os mesmos produtos orçados com vários participantes, que não precisam estar cadastrados como
    fornecedores (basta nome e contato). No mapa de preços, o menor preço de cada produto fica em destaque e vence por
    padrão; dá para escolher outro participante (marca ou qualidade pedida pelo cliente) informando o motivo. A lista
    de preços sai pelo WhatsApp para cada participante e o mapa sai impresso. Ao concluir, gera um pedido de compra por
    vencedor, já na embalagem do fornecedor (ou na que vale para qualquer um), em embalagens inteiras; quem ainda não tem cadastro é cadastrado como fornecedor nessa hora (com confirmação).
  - **Pedido de compra**: vários produtos de um fornecedor, cada um na unidade de compra (embalagem) do fornecedor.
    Feito à mão, pela lista de compras ou pela cotação; sai impresso ou pelo WhatsApp. Tem a condição e a forma de
    pagamento combinadas com o fornecedor: na nota de entrada, a condição preenche os vencimentos (contados da emissão,
    sem digitar prazos; a grade só ajusta datas e valores aos boletos) e a forma vai para as contas a pagar.
  - **Recebimento**: o que chegou do pedido, inteiro ou em partes. Entra no estoque na hora; o saldo continua no pedido
    (que fica "recebido em parte") até chegar ou ser encerrado. Pode ser estornado enquanto não tiver nota.
  - **Nota fiscal de entrada**: junta um ou mais recebimentos do mesmo fornecedor, com o valor da nota (frete, desconto)
    e os vencimentos dos boletos, e vira uma conta a pagar ou parcelas. Cancelar a nota cancela as contas (se nada foi
    pago) e devolve os recebimentos para "a faturar".
  - **Importação do XML da NF-e**: o fornecedor é reconhecido pelo CNPJ e a nota vem preenchida (número, série, emissão,
    valor e boletos), com a chave de acesso guardada para a mesma nota não entrar duas vezes. **Com pedido**, os
    recebimentos do fornecedor vêm marcados e a nota é conferida com eles (o que veio a mais ou a menos aparece na hora).
    **Sem pedido** (compra no atacado, por exemplo), cada item é ligado a um produto do sistema e à unidade em que veio:
    pelo vínculo de uma nota anterior, pelo código do fornecedor na embalagem ou pelo nome (sugestão para conferir). Itens
    que não são de estoque ficam só no valor da nota. Ao salvar, o fornecedor é cadastrado (se preciso), os itens entram
    no estoque e a nota vira contas a pagar, de uma vez; os vínculos ficam gravados para a próxima nota.
- **Lista de compras**: o que comprar para os eventos confirmados de um período (ficha técnica × pessoas, menos o
  estoque, opcionalmente repondo o mínimo), na embalagem do fornecedor, impressa por fornecedor ou enviada como pedido
  pelo WhatsApp.
- **Produção da cozinha**: ordem de produção de cada evento do dia, com os pratos por seção, as porções e os insumos já
  multiplicados. Também sai pela tela do evento.
- **Checklist do evento**: modelos de tarefas com prazo em dias antes do evento (um por tipo de evento e um geral). O
  evento novo já nasce com o checklist; as tarefas são concluídas na aba Checklist e as atrasadas viram lembretes.
- **Pagamento da equipe**: as diárias dos eventos realizados (escalas com presença) viram uma conta a pagar por
  funcionário, na categoria Pessoal. O fechamento pode ser desfeito enquanto a conta não foi paga.
- **Cobrança por PIX**: QR Code e código "copia e cola" de cada parcela (BR Code do Banco Central), para copiar, salvar
  ou enviar pelo WhatsApp ou por e-mail (com o QR Code anexo).
- **Boleto e PIX registrados (Asaas)**: em Contas a receber, o botão "Boleto / PIX registrado" emite a cobrança da parcela
  (boleto com QR Code do PIX, só PIX, ou o cliente escolhe na página da fatura), com multa e juros, e mostra a linha
  digitável, o PIX "copia e cola" e o QR Code para copiar ou enviar por WhatsApp e e-mail (com o link da fatura). Depois do
  login e a cada intervalo configurado, o sistema pergunta ao Asaas quais cobranças foram pagas e baixa as parcelas sozinho
  na conta configurada (com os juros, pela forma PIX, boleto ou cartão). O que não dá para baixar (parcela já recebida por
  outro meio, por exemplo) fica com o aviso em Financeiro › Cobranças (boleto e PIX), que também mostra a taxa, cancela e
  dá a segunda via. Com boleto pendente, o plano de pagamento do evento não pode ser refeito nem o evento cancelado. O
  ambiente **simulado** tem o botão "Simular pagamento" para treinar; o **sandbox** é a conta de testes do Asaas.
- **NFS-e (nota fiscal de serviço)**: uma nota por evento, emitida pelo botão "NFS-e" da tela do evento, com o cliente
  como tomador, o valor do evento (pode ser menor) e a descrição montada do modelo da configuração fiscal (evento, data,
  convidados, local). A emissão vai pelo Focus NFe, que fala com a prefeitura: a nota fica "aguardando prefeitura" até a
  resposta. Em Financeiro › Notas fiscais de serviço a situação é atualizada, a nota é aberta no site da prefeitura, o XML
  é baixado e a nota é cancelada (com motivo). Nota recusada mostra o motivo e libera o evento para emitir de novo. Os
  dados do buffet (CNPJ, inscrição municipal, município, regime, item da LC 116, ISS e retenção) ficam no botão
  "Configuração fiscal" dessa tela, que a primeira emissão já oferece abrir. O ambiente **simulado** autoriza na hora,
  sem internet, para treinar; **homologação** é o teste oficial do Focus.
- **Conciliação bancária**: o extrato do banco em OFX é comparado com os movimentos da conta. O sistema sugere os pares
  (mesmo valor, até 5 dias de diferença), concilia os confirmados, lança o que só o banco tem (tarifas, rendimentos) e
  fecha a conta com o saldo do extrato.
- **Backup e logs**: cópia de segurança do banco (SQL Server nativo, `pg_dump` ou `mysqldump`) feita sozinha uma vez por
  dia e sob demanda, com as últimas cópias guardadas e instruções de restauração. Erros e acessos vão para um log em
  arquivo por dia. A barra de status mostra o último backup.
- **Envio da proposta**: por e-mail (corpo em HTML e proposta em PDF anexa) ou por WhatsApp (com a Z-API, sai direto
  pelo número do buffet, com a proposta em PDF; sem ela, abre o WhatsApp com o resumo pronto). Cada envio fica no
  histórico e o rascunho passa a "Enviado".
- **WhatsApp do buffet (Z-API)**: com o envio direto configurado, todos os botões de WhatsApp (lembretes, PIX, boleto
  registrado, cotação, pedido de compra) enviam a mensagem revisada sem abrir o aplicativo. Os lembretes de parcela a
  vencer e vencida (com o link do boleto/PIX registrado, quando houver), de degustação e de aniversário podem sair
  sozinhos, uma única vez e só no horário comercial. O botão "WhatsApp do buffet" dos Lembretes mostra se o número está
  conectado e envia uma mensagem de teste.
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
- **Usuários e perfis**: o menu é filtrado pelo perfil e as senhas usam hash PBKDF2-SHA256. Cinco senhas erradas
  seguidas bloqueiam o login por 15 minutos (o administrador pode desbloquear antes).
- **Edição simultânea**: se dois usuários abrem o mesmo registro e um salva, o outro é avisado ao salvar, em vez de
  apagar sem saber o que o primeiro gravou.
- Em todas as listagens: pesquisa instantânea, ordenação, **impressão** e **exportação CSV** (Excel).

## Principais regras de negócio

- O orçamento respeita o **mínimo de convidados do pacote** e a **capacidade do espaço**. O desconto
  é limitado a 30%.
- O mínimo do pacote conta só os **pagantes** (inteira + meia). A capacidade do espaço, os insumos e os materiais
  consideram todas as pessoas, inclusive os isentos. Os serviços cobrados por pessoa acompanham os pagantes. O valor da
  meia é arredondado por criança.
- Um espaço não aceita dois eventos sobrepostos. A regra considera o **intervalo de montagem**,
  configurável, e eventos que terminam após a meia-noite.
- Não é possível aprovar orçamento expirado, sem espaço ou com data passada.
- Só se juntam contas em aberto do mesmo fornecedor, da mesma categoria e do mesmo evento (ou todas sem evento), para os
  relatórios de despesa e o resultado do evento continuarem corretos. A fatura do cartão não é reparcelada (ela se
  desfaz pelo estorno do pagamento feito com o cartão). Uma parcela não é excluída sozinha nem troca de fornecedor.
- O plano de pagamento preserva as parcelas pagas e recalcula o saldo. Os centavos vão para a última
  parcela. A cobrança dos excedentes fica fora do plano: refazê-lo não a altera.
- Os excedentes são contados por faixa. A vaga de quem paga mais e não veio cobre quem paga menos (uma criança da meia
  no lugar de um adulto que faltou não é cobrada), nunca o contrário. Isentos excedentes não pagam. A contagem não pode
  ser menor que o check-in, a apuração vale a partir do início do evento (também depois de realizado) e é feita uma vez:
  para refazer, desfaz-se a anterior, o que exige a cobrança sem pagamento. Depois da apuração, os convidados contratados
  não mudam. Se o evento já foi realizado, o estoque baixado não muda (ajuste pelo inventário).
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
- **Concorrência otimista**: cada registro tem uma versão, que muda quando um dado do cadastro muda. Campos mantidos pelo
  sistema (saldo de estoque, último acesso) não mudam a versão, então uma movimentação de estoque não impede quem está
  com o produto aberto de salvar.
- Um lead precisa de telefone ou e-mail. Só leads em negociação recebem contatos, são convertidos ou perdidos; o
  convertido não pode ser excluído.
- O reajuste muda só os itens ativos e guarda o valor anterior de cada um. Desfazer devolve o preço anterior apenas aos
  itens que não foram alterados depois.
- Só entram no pagamento da equipe as escalas com presença registrada de eventos realizados, e cada diária é paga uma
  única vez.
- Um movimento só é conciliado com um lançamento do extrato de mesmo valor, uma única vez.

## Testes

```bash
dotnet test
```

São **352 testes** (xUnit v3): 323 de domínio, integração e bancos, e 29 das telas.

- **Domínio**: CPF/CNPJ, cálculo do orçamento, ciclo de vida, parcelas, estoque e consumo do pacote, condições de
  pagamento, modelos de contrato, valor por extenso, conversão de embalagens, contatos adicionais, faixas etárias,
  parcelamento de contas a pagar, bloqueio de login, leads, reajuste de preços, checklist, convidados excedentes, diárias da equipe, BR Code do
  PIX, leitura de OFX e conciliação, cotação (vencedor por produto), recebimento parcial, nota fiscal de entrada, leitura do XML da NF-e,
  regras da NFS-e e o envio ao Focus NFe, cobrança registrada e a API do Asaas, e o envio pela Z-API (HTTP simulado).
- **Integração**: criam um banco temporário (por padrão no LocalDB), aplicam as migrations, carregam a
  base de demonstração e executam todas as consultas Dapper e os fluxos dos serviços (aprovação com
  conflito de agenda, pagamento, cancelamento, autenticação e bloqueio, tradução de erros de banco, edição simultânea,
  backup, funil, lembretes, reajuste, excedentes, lista de compras, cotação, recebimento, nota de entrada e importação da NF-e, NFS-e, boleto/PIX registrado com baixa automática, lembretes automáticos pelo WhatsApp, ordem de produção,
  checklist, pagamento da equipe, PIX, conciliação e DRE). O banco é excluído no final.
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
- auditoria;
- lead cadastrado e convertido, lembrete resolvido, tarefa do checklist concluída e excedentes cobrados e desfeitos.

Como funcionam:

- Rodam dentro do processo de teste, num banco temporário com a base de demonstração.
- As janelas são criadas em uma **área de trabalho oculta do Windows**. Nada aparece na tela e o foco
  não é roubado, então dá para continuar trabalhando enquanto os testes rodam.
- As mensagens (TaskDialog) passam por um interceptador. Um diálogo modal que o teste não esperava é
  fechado e o teste falha com o nome do diálogo, em vez de travar.

```bash
dotnet test tests/BuffetSharp.Tests.Telas
```

### Integração contínua

O GitHub Actions (`.github/workflows/ci.yml`) compila a solução a cada push e pull request e roda os testes de domínio e
de bancos no Windows e a suíte inteira do `BuffetSharp.Tests` contra PostgreSQL e MySQL em contêineres. Os testes das
telas precisam de uma área de trabalho do Windows e são rodados localmente.

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

O `atualizar.cmd NomeDaMigration` faz isso nos quatro bancos de uma vez, compila e roda os testes (a saída vai para
`atualizacao-log.txt`). Gerar migrations não exige servidor. O app aplica as pendentes automaticamente ao iniciar. Para
`dotnet ef database update`, informe a conexão na variável `BUFFETSHARP_CONNECTION`.

## Estrutura

```
src/
  BuffetSharp.Domain/          Comum, Cadastros, Estoque, Cardapio, Comercial, Eventos, Financeiro, Seguranca
  BuffetSharp.Application/     Abstracoes, Servicos, Consultas (contratos + DTOs), Modelos
  BuffetSharp.Infrastructure/  Persistencia (DbContext, Configuracoes, Provedores, Seed), Consultas (Dapper), Email, Seguranca,
                               Manutencao (backup, log em arquivo, QR Code)
  BuffetSharp.Infrastructure.SqlServer|PostgreSql|MySql|Oracle/   Provedor e Migrations de cada banco
  BuffetSharp.WinForms/        Infra (tema, grade, editor de campos, navegação), Controles (gráfico, calendário,
                               indicadores), Formularios (telas por módulo), Impressao
tests/
  BuffetSharp.Tests/           Dominio, Integracao, Bancos
  BuffetSharp.Tests.Telas/     Infra (robô de telas, desktop oculto), Testes
```
