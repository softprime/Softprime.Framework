# Plano de Modernização — Softprime.Framework

> Documento de **planejamento e gestão de backlog**. Não descreve modernização concluída, e sim o estado atual verificado e o plano para evoluir com segurança. Decisões que dependem de informação ainda inacessível estão marcadas como **Pendente de validação**.
>
> Data da auditoria: 2026-11-03 · Branch base: `main` (commit `27ca3c1`).

## Sumário

- [Estado atual verificado](#estado-atual-verificado)
- [Inventário de consumidores e dependências (#10)](#inventário-de-consumidores-e-dependências-10)
- [Estratégia de testes e baseline comportamental (#7)](#estratégia-de-testes-e-baseline-comportamental-7)
- [Baseline de APIs públicas e compatibilidade binária/comportamental (#11)](#baseline-de-apis-públicas-e-compatibilidade-bináriacomportamental-11)
- [Arquitetura alvo proposta](#arquitetura-alvo-proposta)
- [Matriz de compatibilidade](#matriz-de-compatibilidade)
- [Política NuGet](#política-nuget)
- [Plano de migração (fases e gates)](#plano-de-migração-fases-e-gates)
- [Backlog de issues](#backlog-de-issues)
- [Riscos / pendências](#riscos--pendências)

---

## Estado atual verificado

Fatos confirmados por leitura direta do repositório (não inferidos):

- Solution única: `src/Softprime.Framework.All.sln` com **14 projetos de biblioteca** em 5 áreas (general, data, api, mail, multitenant).
- **Todos** os `.csproj` em `Version=2.0.0`, sem `PackageId` explícito (o id do pacote é o nome do projeto). `GeneratePackageOnBuild=true` em todos.
- **Não existem:** projetos de teste, `.github/workflows` (CI), `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `global.json`, `packages.lock.json`, tags git ou milestones.
- Metadados NuGet (Authors `Marcelo Paiva`, Company `Softprime`, licença `LICENSE.txt` MIT, ícone `logo-nuget.png`, URLs do GitHub) estão **duplicados** em cada `.csproj`.
- A dependência `Domain → DAL` existe e é intencional no desenho atual (entidades de domínio referenciam contratos de persistência). Classificar como "a corrigir" exigiria decisão — **não** refatorar agora.

### Matriz factual de projetos (verificada)

| Projeto | Área | Targets atuais | Dependências diretas relevantes | Dependentes internos |
|---|---|---|---|---|
| Common | general | `netstandard2.0;netstandard2.1` | `System.ComponentModel.Annotations` 5.0.0 | — (utilitário) |
| Domain | data | `netstandard2.0;netstandard2.1;net48` | `System.ComponentModel.Annotations` 5.0.0; → DAL | — |
| DAL | data | `netstandard2.0;netstandard2.1;net48` | (nenhuma externa) | Domain, EFCore3, EFCore5, EF6, NH |
| EFCore3 | data | `netstandard2.0;netstandard2.1;net48` | `Microsoft.EntityFrameworkCore` 3.1.11; → DAL | — |
| EFCore5 | data | `netstandard2.1` | `Microsoft.EntityFrameworkCore` 5.0.2; → DAL | — |
| EF6 | data | `netstandard2.1;net48` | `EntityFramework` 6.4.4; → DAL | — |
| NH | data | `netstandard2.0;netstandard2.1;net48` | `NHibernate` 5.3.5, `FluentNHibernate` 3.1.0; → DAL | — |
| Api.Dto | api | `netstandard2.0;netstandard2.1` | (nenhuma externa) | Api, Api.Client |
| Api | api | `netstandard2.0;netstandard2.1` | `Microsoft.AspNetCore.Mvc` 2.2.0 (+ Abstractions); → Api.Dto | — |
| Api.Client | api | `netstandard2.0;netstandard2.1` | `Newtonsoft.Json` 12.0.3; → Api.Dto | — |
| Mailer | mail | `netstandard2.0;netstandard2.1;net48` | `RazorLight` 2.0.0-rc.3 | Mailer.AWS, Mailer.SendGrid |
| Mailer.AWS | mail | `netstandard2.0;netstandard2.1;net48` | `AWSSDK.SimpleEmailV2` 3.5.1.25; → Mailer | — |
| Mailer.SendGrid | mail | `netstandard2.0;netstandard2.1;net48` | `SendGrid` 9.22.0; → Mailer | — |
| Multitenancy | multitenant | `netstandard2.0;netstandard2.1;net48` | `Microsoft.AspNetCore` 2.2.0, `Microsoft.Extensions.Caching.Memory` 5.0.0, `System.ComponentModel.Annotations` 5.0.0 | — |

**Consumidores externos:** desconhecidos neste repositório — ver inventário detalhado abaixo na issue [#10] (`a confirmar`).

### Observações factuais relevantes

- A documentação preliminar (`tech.md`) afirmava apenas `netstandard2.0`/`2.1`; a auditoria confirma que **vários projetos também miram `net48`** e que **EFCore5 mira apenas `netstandard2.1`** e **EF6 mira `netstandard2.1;net48`**.
- `Version=2.0.0` é a versão de **build**; **não** há evidência, neste repositório, do que está efetivamente publicado no feed NuGet. **Pendente de validação** (issue [#10]).

---

## Inventário de consumidores e dependências (#10)

> **Entregável da issue [#10] (Fase 0 — Conhecer e proteger)**.

O mapeamento detalhado dos 14 pacotes da solution, suas dependências internas e transitivas, e os bloqueadores de upgrade conhecidos estão documentados na íntegra no documento dedicado:

👉 **[Inventário de Pacotes, Consumidores e Dependências (`docs/package-inventory.md`)](package-inventory.md)**

### Síntese executiva dos achados:

- **14 pacotes catalogados:** Todos com `Version=2.0.0` no `.csproj` (versão declarada de build; versões efetivamente publicadas marcadas como `a confirmar`).
- **Suporte explícito a `net48` (9 pacotes):** `Domain`, `DAL`, `EFCore3`, `EF6`, `NH`, `Mailer`, `Mailer.AWS`, `Mailer.SendGrid`, `Multitenancy`. Consumidores legados corporativos precisam ser preservados via multi-targeting.
- **Assimetrias e bloqueadores críticos identificados:**
  - `EFCore5`: Mira **apenas `netstandard2.1`** (incompatível com `.NET Framework 4.8`).
  - `EF6`: Linha isolada (`netstandard2.1;net48`), ecossistema distinto de EF Core.
  - `Api` e `Multitenancy`: Acoplamento ao `Microsoft.AspNetCore.Mvc 2.2.0` / `Microsoft.AspNetCore 2.2.0` (fora de suporte desde 2019 e incompatível com .NET 8/10 moderno).
  - `Common.Security`: Exige auditoria e preservação comportamental de algoritmos criptográficos para não quebrar a leitura de dados legados já armazenados.
  - `Mailer`: Depende de versão Release Candidate instável (`RazorLight 2.0.0-rc.3`) com compilação Roslyn em runtime.

---

## Estratégia de testes e baseline comportamental (#7)

> **Entregável da issue [#7] (Fase 0 — Conhecer e proteger)**.

A estratégia completa de testes de caracterização para fixar o comportamento atual pré-migração está documentada na íntegra no documento dedicado:

👉 **[Estratégia de Testes e Caracterização (`docs/test-strategy.md`)](test-strategy.md)**

### Síntese executiva da estratégia:

- **Framework, asserções e convenção aprovados:** `xUnit` + `Fluent Assertions 7.x`, com testes no padrão Arrange, Act, Assert (AAA). `Moq 4.x` como padrão para mocks de dependências externas; fakes manuais quando mais simples. Usar objetos reais para regras puras e banco de integração para persistência, sem mocks de `DbSet`/`IQueryable`. Fixar versões exatas e verificar execução na implementação. SQL Server e Oracle foram informados como bancos usuais dos consumidores; confirmar combinações de ORM/provider/versões/runtime no inventário. SQLite é apenas complementar.
- **Estrutura 1:1 aprovada:**
  - Um projeto `tests/<NomeDaBiblioteca>.Tests/<NomeDaBiblioteca>.Tests.csproj` por biblioteca, criado junto dos primeiros testes relevantes, sem projetos vazios ou declaração automática de cobertura.
  - Domain, Common, DTOs e cada ORM têm projetos separados, com dependências e fixtures próprias. Contratos de persistência são exercitados nas implementações; `DAL.Tests` será criado quando houver comportamento concreto relevante.
  - `tests/Softprime.Framework.SmokeTests.Net48`, `Net10` e `Net8` quando aplicável: consumo dos pacotes NuGet reais (`.nupkg`), com cache isolado e registro do asset selecionado.
- **Matriz de execução aprovada para o planejamento:** `net48;net10.0` como ponto de partida para bibliotecas compartilhadas, com exceções por compatibilidade; EFCore5 somente em runtime moderno. Api/Multitenancy exigem validação legada separada da implementação modernizada. `net8.0` inicialmente nos smoke tests aplicáveis. Compilar e exercitar explicitamente o asset `netstandard2.0`, além de verificar a seleção normal do NuGet. Falhas de compatibilidade serão registradas como bloqueadores, não como execução bem-sucedida.
- **Ordem de prioridade de caracterização em 3 etapas:**
  1. *Etapa 1 (P0 - Crítica):* `EntityWithTypedId` (igualdade, `IsTransient`, hash em cache e hooks atualmente sem chamadas nos repositórios), paginação/CRUD e golden tests de criptografia em `Common.Security`. Preservar o `out total` atual, calculado após paginação; sua correção será mudança funcional separada.
  2. *Etapa 2 (P1 - Alta):* Mapeamentos e consultas relacionais dos ORMs (`EFCore3`, `EFCore5`, `EF6`, `NH`) e extensions selecionadas de `Common`, antes de migrar os respectivos pacotes.
  3. *Etapa 3 (P2 - Média):* DTOs, serialização HTTP e dispatchers de e-mail.
- **Invariante da Fase 0:** Nenhum projeto de teste foi criado no código-fonte nesta rodada; a criação dos `.csproj` ocorrerá no início da fase de desenvolvimento.
- **Caracterização e smoke tests aprovados:** registrar a baseline antes da migração e executar os cenários críticos e o consumo dos `.nupkg` por pacote. Incluir operações representativas de domínio, utilitários, persistência, HTTP, e-mail e hosting conforme aplicabilidade. Não enviar e-mails reais; executar persistência em ambientes de teste isolados SQL Server/Oracle nas combinações confirmadas. Infraestrutura e compatibilidade ainda não verificadas permanecem lacunas explícitas, sem declaração de cobertura.

---

## Baseline de APIs públicas e compatibilidade binária/comportamental (#11)

> **Entregável da issue [#11] (Fase 0 — Conhecer e proteger)**.

A especificação completa da baseline de superfícies públicas, critérios de detecção de breaking changes e plano de homologação com consumidores está documentada na íntegra no documento dedicado:

👉 **[Estratégia de Baseline de APIs Públicas e Compatibilidade (`docs/api-baseline-strategy.md`)](api-baseline-strategy.md)**

### Síntese executiva da especificação:

- **Abordagem híbrida de validação aprovada:**
  - *Camada 1 (Desenvolvimento e Git):* Analisador Roslyn `Microsoft.CodeAnalysis.PublicApiAnalyzers` com arquivos declarativos `PublicAPI.Shipped.txt` (congelando a versão `2.0.0`) e `PublicAPI.Unshipped.txt` (novas APIs). Fornece feedback imediato na compilação local e torna qualquer alteração na superfície pública 100% visível nos diffs de Pull Request no GitHub.
  - *Camada 2 (Empacotamento e CI):* Recurso nativo do SDK `EnablePackageValidation` com `PackageValidationBaselineVersion = 2.0.0`, validando os binários dos `.nupkg` contra o feed no `dotnet pack` e garantindo paridade entre fatias de multi-targeting (`net48`, `netstandard2.0`, `net10.0`), conforme prática comprovada no `Codout.Framework`.
- **Critérios objetivos de breaking changes definidos:**
  - *Quebra Binária:* Remoção/renomeação de tipos ou membros públicos/protegidos, mudança de namespace, alteração de assinaturas, adição de parâmetros (mesmo opcionais, que quebram binários antigos em C# sem recompilação), adição do modificador `sealed` e adição de métodos a interfaces públicas em runtimes sem Default Interface Methods (`net48`/`netstandard2.0`).
  - *Quebra de Compilação:* Ambiguidade de sobrecargas e renomeação de parâmetros que quebrem invocação com argumentos nomeados.
  - *Quebra Comportamental:* Proteção mandatória dos 3 pontos críticos identificados: estabilidade de hash e igualdade em `EntityWithTypedId`, preservação da semântica atual do parâmetro `out total` na paginação de `IRepository.Find`, e *golden tests* de criptografia em `Common.Security` para garantir decifragem contínua de dados legados já gravados em produção.
- **Matriz de targets e testes mínimos de regressão:**
  - Priorização em 3 etapas alinhada à issue [#7]: Etapa 1 (P0 - Entidades, Criptografia, CRUD), Etapa 2 (P1 - Mapeamentos relacionais de ORMs em SQL Server e Oracle), Etapa 3 (P2 - DTOs, HTTP e e-mail).
  - Execução nos runtimes `net48` (runner Windows) e `net10.0` (LTS moderna), com smoke tests em `net8.0` quando aplicável.
  - Invariante da Fase 0 respeitada: nenhuma cobertura de testes é declarada atingida.
- **Plano de homologação com 2 aplicações-piloto:**
  - Validação pré-release com 1 aplicação corporativa piloto em `.NET Framework 4.8` (validando o legado de persistência) e 1 aplicação moderna em `.NET 8+` (validando contratos modernos).
  - Publicação de pacotes pré-lançamento (ex.: `-preview.1` ou `-rc.1`) em feed NuGet interno isolado, com execução de testes de integração e gate de aprovação manual (*sign-off*) antes de qualquer publicação definitiva.

---

## Arquitetura alvo proposta

> **Proposta**, sujeita à aprovação nas issues [#3] e [#12]. Não é estado atual.

- Separar claramente **contratos/utilitários compartilhados** (precisam servir `net48`) de **implementações modernas** (podem ser `net10.0`).
- `.NET 10` é a referência LTS moderna (GA 2025-11-11, suporte até 2028-11-10).
- Multi-targeting apenas **com justificativa** por pacote, nunca indiscriminado.

### Invariantes de compatibilidade (fatos, não negociáveis)

1. **`.NET Standard 2.1` NÃO é compatível com `.NET Framework 4.8`.** Para servir `net48`, use `netstandard2.0` ou `net48` explícito.
2. Um pacote `netstandard2.0;net10.0` atende: `net48` → escolhe `netstandard2.0`; `net10.0` → escolhe `net10.0`; `net8.0` → cai em `netstandard2.0` (não pode usar a variante `net10.0`).
3. ASP.NET Core moderno (`Microsoft.AspNetCore.App`) **não** é referenciável a partir de `net48`.
4. **EF6 é linha distinta de EF Core.** Não consolidar nem remover automaticamente.

---

## Matriz de compatibilidade

> Decisão **por pacote** a ser aprovada na issue [#3] após o inventário [#10]. Abaixo, recomendação preliminar **Pendente de validação**.

| Pacote | Papel | Precisa servir `net48`? | Recomendação preliminar de target | Observação |
|---|---|---|---|---|
| Common | utilitário compartilhado | Provável (a confirmar) | `netstandard2.0` (+ `net10.0` se usar APIs novas) | depende de #10 |
| Domain | contrato de domínio | Provável | `netstandard2.0` | — |
| DAL | contrato de persistência | Provável | `netstandard2.0` | — |
| Api.Dto | contrato de DTO | Provável | `netstandard2.0` | — |
| Api.Client | cliente HTTP | A confirmar (`net48` + Newtonsoft) | `netstandard2.0` (+ `net10.0`) | avaliar `System.Text.Json` em #6 |
| Api | impl. ASP.NET Core | Não (ASP.NET Core moderno) | `net10.0` | migração em #4 |
| Multitenancy | impl. ASP.NET Core | Não | `net10.0` | migração em #4 |
| EFCore3 / EFCore5 | impl. legada EF Core | Manter enquanto houver consumidor | preservar | decisão em #5 |
| EF6 | impl. EF6 (linha própria) | Sim (net48) | preservar | **não** remover (#5) |
| NH | impl. NHibernate | A confirmar | preservar | decisão em #5 |
| (novo) EF Core 10 | impl. moderna | Não | `net10.0` | planejar em #5 |
| Mailer + dispatchers | contrato + mecanismo | A confirmar | avaliar separar contrato do RazorLight | #6 |

---

## Política NuGet

> A definir integralmente na issue [#12]. Princípios propostos (Pendente de validação):

- **SemVer** com critérios objetivos para major/minor/patch; subir **target** não implica automaticamente subir **major**, mas **breaking change** de API sim.
- **Nunca** sobrescrever uma versão já publicada.
- Linhas legadas (que servem `net48`) mantidas em paralelo às modernas enquanto houver consumidores.
- Publicação com **aprovação manual** — sem publish automático neste ciclo (ver [#8]).
- Centralização de `Version`/metadados em `Directory.Build.props` avaliada em [#9]; Central Package Management (`Directory.Packages.props`) avaliado **à parte**.

---

## Plano de migração (fases e gates)

### Fase 0 — Conhecer e proteger (pré-requisito de qualquer código)
- [#10] Inventário de consumidores e dependências (P0) — *Concluído no planejamento*
- [#11] Baseline de APIs públicas e comportamento (P0) — *Concluído no planejamento*
- [#7] Estratégia de testes / caracterização (P0) — *Concluído no planejamento*

### Fase 1 — Definir e automatizar
- [#3] Matriz de compatibilidade e estratégia de targets (P1)
- [#12] Política de versionamento, suporte e publicação (P1)
- [#8] Planejamento de CI (build/test/pack, sem publish) (P1)
- [#9] Centralização de metadados / avaliar CPM (P2)

### Fase 2 — Modernizar seletivamente (só após os gates)
- [#4] ASP.NET Core / MVC 2.2 → moderno (P1)
- [#5] Legado EF Core/EF6/NH + implementação EF Core 10 (P1)
- [#6] Auditoria/atualização de dependências + segurança `Common.Security` (P1)

### Fase 3 — Consolidar e homologar
- [#13] Atualizar documentação/diretrizes para a arquitetura aprovada (P1)
- Validação com consumidores, release candidate (prerelease) e **aprovação manual** de publicação.

### Gates obrigatórios antes do primeiro PR de modernização
- [x] Inventário auditado e lacunas de consumidores explicitadas ([#10])
- [ ] Decisão de targets aprovada por pacote ([#3])
- [x] Baseline de APIs e estratégia de regressão definidas ([#11], [#7])
- [x] Plano para validar consumidores `net48` ([#11])
- [ ] Regras de versionamento e rollback definidas ([#12])

### Gates antes de release
- [ ] Build e testes passando nos targets aplicáveis
- [ ] Testes de integração essenciais + smoke tests de consumo (`net48`, `net10.0`, `net8.0` quando aplicável)
- [ ] Verificação de compatibilidade pública (API surface)
- [ ] Avaliação de breaking changes
- [ ] Pacote prerelease validado
- [ ] Aprovação manual de publicação

---

## Backlog de issues

| # | Título | Prioridade | Fase | Bloqueada por | Status / Entregável |
|---|---|---|---|---|---|
| [#10] | [Arquitetura] Inventariar consumidores e dependências | P0 | 0 | — | Entregue ([docs/package-inventory.md](package-inventory.md)) |
| [#11] | [Qualidade] Baseline de APIs públicas e compatibilidade | P0 | 0 | relaciona #7, #10 | Entregue ([docs/api-baseline-strategy.md](api-baseline-strategy.md)) |
| [#7]  | Estratégia de testes / caracterização | P0 | 0 | — | Entregue ([docs/test-strategy.md](test-strategy.md)) |
| [#3]  | [Arquitetura] Matriz de compatibilidade e targets | P1 | 1 | #10, #11 | Bloqueada |
| [#12] | [NuGet] Política de versionamento e publicação | P1 | 1 | relaciona #9, #8 | Bloqueada |
| [#8]  | Planejamento de CI (GitHub Actions) | P1 | 1 | #7, #3 | Bloqueada |
| [#9]  | Centralizar metadados / avaliar CPM | P2 | 1 | relaciona #12, #3 | Bloqueada |
| [#4]  | ASP.NET Core / MVC 2.2 → moderno | P1 | 2 | #3, #10, #11 | Bloqueada |
| [#5]  | Legado EF/NH + EF Core 10 | P1 | 2 | #3, #10 | Bloqueada |
| [#6]  | Dependências + segurança `Common.Security` | P1 | 2 | #3, #11 | Bloqueada |
| [#13] | [Docs/Kiro] Atualizar diretrizes | P1 | 3 | #3, #12 | Bloqueada |

### Primeiro lote seguro de execução

Começar **somente** pela Fase 0: [#10], [#11] e [#7]. São somente planejamento/levantamento, não alteram código e destravam todo o resto. Nenhum PR de modernização de código antes de os gates da Fase 0/1 fecharem.

---

## Riscos / pendências

- **Consumidores externos e estado do feed NuGet (Pendente de validação):** O inventário factual do repositório foi concluído na issue [#10] em [docs/package-inventory.md](package-inventory.md). As lacunas de repositórios consumidores externos e a listagem de versões reais do feed NuGet corporativo foram formalizadas com status `a confirmar`.
- **`Common.Security`:** qualquer mudança de algoritmo precisa preservar a leitura de dados já criptografados; auditar antes de alterar (#6). Não modificar nesta fase.
- **`net48` + ASP.NET Core moderno:** incompatível; exige decisão sobre manter linha legada vs. descontinuar (#4).
- **Runners de CI:** targets `net48` exigem runner Windows (#8).
- **Documentação:** `tech.md` originalmente sugeria `netstandard2.1`/`2.0.0` como padrão para novos projetos — já sinalizado e a corrigir formalmente em [#13].

---

<!-- Links -->
[#3]: https://github.com/softprime/Softprime.Framework/issues/3
[#4]: https://github.com/softprime/Softprime.Framework/issues/4
[#5]: https://github.com/softprime/Softprime.Framework/issues/5
[#6]: https://github.com/softprime/Softprime.Framework/issues/6
[#7]: https://github.com/softprime/Softprime.Framework/issues/7
[#8]: https://github.com/softprime/Softprime.Framework/issues/8
[#9]: https://github.com/softprime/Softprime.Framework/issues/9
[#10]: https://github.com/softprime/Softprime.Framework/issues/10
[#11]: https://github.com/softprime/Softprime.Framework/issues/11
[#12]: https://github.com/softprime/Softprime.Framework/issues/12
[#13]: https://github.com/softprime/Softprime.Framework/issues/13
