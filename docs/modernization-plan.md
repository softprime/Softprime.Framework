# Plano de Modernização — Softprime.Framework

> Documento de **planejamento e gestão de backlog**. Não descreve modernização concluída, e sim o estado atual verificado e o plano para evoluir com segurança. Decisões que dependem de informação ainda inacessível estão marcadas como **Pendente de validação**.
>
> Data da auditoria: 2026-11-03 · Branch base: `main` (commit `27ca3c1`).

## Sumário

- [Estado atual verificado](#estado-atual-verificado)
- [Inventário de consumidores e dependências (#10)](#inventário-de-consumidores-e-dependências-10)
- [Estratégia de testes e baseline comportamental (#7)](#estratégia-de-testes-e-baseline-comportamental-7)
- [Arquitetura alvo proposta](#arquitetura-alvo-proposta)
- [Matriz de compatibilidade](#matriz-de-compatibilidade)
- [Política NuGet](#política-nuget)
- [Padronização de metadados e versões (#9)](#padronização-de-metadados-e-versões-9)
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
- Centralização de `Version`/metadados em `Directory.Build.props` planejada na seção abaixo, referente à [#9]; Central Package Management (`Directory.Packages.props`) avaliado **à parte**.

---

## Padronização de metadados e versões (#9)

> **Entregável da issue [#9] (Fase 1 — Definir e automatizar)**.

### Especificação proposta (#9 — somente planejamento)

A proposta é criar **`src/Directory.Build.props` em uma implementação futura**, centralizando os metadados comuns dos 14 projetos. Esta issue entrega apenas a especificação e a avaliação de CPM: não cria arquivos de build nem altera `.csproj`.

O arquivo proposto terá um elemento raiz `Project` e um `PropertyGroup` com os seguintes valores, extraídos dos projetos atuais:

| Propriedade | Valor proposto, preservando o atual |
|---|---|
| `Authors` | `Marcelo Paiva` |
| `Company`, `Copyright` | `Softprime` |
| `PackageProjectUrl`, `RepositoryUrl` | `https://github.com/softprime/Softprime.Framework` |
| `RepositoryType` | `git` |
| `PackageTags` | `Softprime;Framework` |
| `PackageIcon` | `logo-nuget.png` |
| `PackageLicenseFile` | `LICENSE.txt` |
| `PackageRequireLicenseAcceptance` | `false` |
| `GeneratePackageOnBuild` | `true` |
| `Version` | `2.0.0` |

`Version=2.0.0` apenas registra o valor atual a preservar na migração; não é uma nova política para futuros pacotes nem evidência das versões publicadas no feed. O fluxo de alteração da versão central deve ser coordenado com a [#12]. `PackageId` continuará implícito, derivado do nome de cada projeto.

Na implementação futura, deverão permanecer nos `.csproj`:

- `Description` em português e tags específicas de Mailer, Mailer.AWS e Mailer.SendGrid. As tags específicas substituem o valor comum e preservam também `Softprime;Framework`.
- `TargetFramework`/`TargetFrameworks`, dependências externas com suas versões e condições, e referências entre projetos.
- Os itens `None` com `Pack=true` que incluem `../../LICENSE.txt` e o `logo-nuget.png` local na raiz de cada pacote. A proposta não exige mover assets nem mudar o modelo de licença.

Somente as declarações comuns migradas ao props deverão ser removidas dos projetos. `Product`, `PackageIconUrl` e `PackageLicenseExpression` estão vazios; não propagar essas declarações ao arquivo comum. Uma eventual remoção dos campos vazios dos projetos exige conferir a equivalência dos valores efetivos. Manter a licença por arquivo, sem adicionar uma expressão de licença em paralelo a `PackageLicenseFile`.

O MSBuild importa `Directory.Build.props` antes do corpo dos projetos; uma propriedade declarada posteriormente no `.csproj` sobrescreve o valor comum. Assim, as tags específicas dos mailers poderão permanecer nos projetos sem imports manuais. A busca para no primeiro arquivo encontrado ao subir pelos diretórios; futuros props mais próximos precisarão importar explicitamente o arquivo de `src/` se quiserem herdar seus valores. Esse comportamento está descrito na [documentação oficial do MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory).

A localização em `src/` limita a herança às bibliotecas ali contidas: futuros projetos em `tests/` e `samples/` na raiz não receberão essa configuração. Se um projeto sem finalidade de publicação for criado dentro de `src/`, deverá sobrescrever `IsPackable=false` e `GeneratePackageOnBuild=false` no próprio `.csproj`. Não centralizar targets nesta proposta; a matriz de compatibilidade continua na [#3].

### Decisão separada sobre Central Package Management

**Decisão de planejamento: adiar a adoção de CPM.** A futura centralização de metadados não deverá criar `Directory.Packages.props` nem ativar `ManagePackageVersionsCentrally`; manter as versões das dependências nos projetos. Uma eventual adoção será uma alteração independente.

O inventário atual contém 16 referências diretas a 13 IDs distintos. A repetição com a mesma versão se concentra em `System.ComponentModel.Annotations` 5.0.0 (Common, Domain e Multitenancy). `Microsoft.EntityFrameworkCore` tem duas linhas intencionais: 3.1.11 no EFCore3 e 5.0.2 no EFCore5. Centralizar ingenuamente esse ID em uma única versão alteraria o contrato dos pacotes. As demais dependências diretas aparecem em apenas um projeto cada.

A migração para CPM exigiria criar entradas `PackageVersion`, remover as versões dos `PackageReference` e modelar explicitamente a diferença entre os projetos EFCore3 e EFCore5. Condições apenas por target não resolveriam a distinção, pois ambos publicam `netstandard2.1`. Também seria necessário validar restore, grafo transitivo e dependências dos `.nuspec` em todos os targets. O benefício imediato de reduzir duplicação não justifica ampliar a mudança de metadados para esse trabalho; a matriz de targets e a evolução dos ORMs seguem nas issues [#3] e [#5].

Uma futura adoção deve preservar inicialmente as versões atuais, explicitar as exceções por projeto e comparar os grafos restaurados e os grupos de dependências dos pacotes. Não habilitar transitive pinning incidentalmente: essa opção pode promover dependências transitivas a diretas. A configuração e seus efeitos estão documentados em [Central Package Management — NuGet](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).

### Procedimento proposto para validar a futura implementação

Antes da migração, registrar o commit de referência e manter uma cópia isolada da versão anterior. Construir a referência e a versão alterada com o mesmo SDK, configuração Release, fontes NuGet, versões de dependências e targets. Garantir que a referência não herde o props da versão alterada. Usar pastas de saída distintas e sem pacotes de outras execuções.

Procedimento de verificação:

1. Avaliar os 14 projetos com `dotnet msbuild <projeto> -getProperty:<lista>`, antes e depois: autoria, empresa, produto, descrição, copyright, URLs, tipo de repositório, tags, ícone, licença, aceitação da licença, geração de pacote, `Version`, `PackageVersion`, `PackageId` e targets. Confirmar equivalência, inclusive dos campos vazios removidos.
2. Executar restore e `dotnet build <solution> -c Release --no-restore` nas duas versões. Executar `dotnet pack <solution> -c Release --no-build --no-restore -o <pasta>` com diretórios de saída distintos para referência e versão alterada. Não publicar os pacotes.
3. Exigir exatamente os mesmos 14 nomes de pacotes. Abrir os `.nupkg` como ZIP e comparar os `.nuspec` completos como XML, incluindo identidade, versão, descrição, tags, licença, ícone, repositório, grupos de dependências e versões.
4. Comparar a lista de arquivos `lib/<target>/` para conferir os assets publicados. Exigir `LICENSE.txt` e `logo-nuget.png` na raiz de todos os pacotes e comparar seu conteúdo por SHA-256. Não exigir igualdade binária do ZIP ou das DLLs, pois caminhos e outros dados de build podem variar.
5. Registrar SDK, commit de referência, comandos, resultados e avisos preexistentes. Falhas de restore/build/pack são bloqueios, não validação aprovada. Guardar logs e pacotes em uma pasta ignorada pelo Git, com resumo revisável na entrega da implementação.

A implementação futura será considerada validada quando os 14 projetos importarem `src/Directory.Build.props` automaticamente, as propriedades avaliadas forem equivalentes, build e pack passarem nos targets atuais e os pacotes preservarem metadados, dependências e assets conforme as verificações acima. Esta entrega de planejamento não afirma que a migração foi implementada.

---

## Plano de migração (fases e gates)

### Fase 0 — Conhecer e proteger (pré-requisito de qualquer código)
- [#10] Inventário de consumidores e dependências (P0) — *Concluído no planejamento*
- [#11] Baseline de APIs públicas e comportamento (P0)
- [#7] Estratégia de testes / caracterização (P0) — *Concluído no planejamento*

### Fase 1 — Definir e automatizar
- [#3] Matriz de compatibilidade e estratégia de targets (P1)
- [#12] Política de versionamento, suporte e publicação (P1)
- [#8] Planejamento de CI (build/test/pack, sem publish) (P1)
- [#9] Centralização de metadados / avaliar CPM (P2) — *Concluído no planejamento*

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
- [ ] Baseline de APIs e estratégia de regressão definidas ([#11], [#7])
- [ ] Plano para validar consumidores `net48` ([#11])
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
| [#11] | [Qualidade] Baseline de APIs públicas e compatibilidade | P0 | 0 | relaciona #7, #10 | Pendente de planejamento |
| [#7]  | Estratégia de testes / caracterização | P0 | 0 | — | Entregue ([docs/test-strategy.md](test-strategy.md)) |
| [#3]  | [Arquitetura] Matriz de compatibilidade e targets | P1 | 1 | #10, #11 | Bloqueada |
| [#12] | [NuGet] Política de versionamento e publicação | P1 | 1 | relaciona #9, #8 | Bloqueada |
| [#8]  | Planejamento de CI (GitHub Actions) | P1 | 1 | #7, #3 | Bloqueada |
| [#9]  | Centralizar metadados / avaliar CPM | P2 | 1 | relaciona #12, #3 | Entregue (Seção "Padronização de metadados e versões") |
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
