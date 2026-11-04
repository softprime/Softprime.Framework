# Análise comparativa — Codout.Framework × Softprime.Framework

> Documento de **planejamento (somente leitura/pesquisa)**. Separa **fatos comprovados** (verificados em arquivos/commits) de **recomendações**. Nenhum código foi copiado nesta fase.
>
> Data da pesquisa: 2026-11-03 · Repositório de referência: [`Codout/Codout.Framework`](https://github.com/Codout/Codout.Framework) (branch `master`).

## Proveniência e licença (fato comprovado)

- Licença do Codout.Framework: **MIT, Copyright (c) 2017 Codout** — verificado via API de licença do GitHub (`spdx_id: MIT`).
- Licença do Softprime.Framework: **MIT, Copyright (c) 2021 Softprime** (`src/LICENSE.txt`).
- **Compatibilidade de licença:** MIT → MIT permite reaproveitar código, desde que o aviso de copyright e a permissão MIT do Codout sejam **preservados/atribuídos** no material reaproveitado. Qualquer incorporação futura de código deve manter a atribuição ao Codout.
- Observação factual curiosa: o repositório Codout contém `Codout.Multitenancy/Softprime.Multitenancy.csproj` e o README do Codout descreve "Softprime.Multitenancy" como "o build netstandard2.0 de compatibilidade". **Há relação histórica entre os dois projetos** — a confirmar com o mantenedor antes de assumir direção de origem.

## Estado do Codout (fatos comprovados pela árvore `git/trees?recursive=1`)

- **~26 projetos de biblioteca** + **~18 projetos de teste** em `tests/` + um **servidor MCP** (`Codout.Framework.Mcp`).
- Versões dos pacotes na casa **6.x** (ex.: Domain 6.5.0, maioria 6.4.0).
- Usa **`net10.0`** (ex.: `Codout.Security.Core` declara `net10.0`; demais herdam de `Directory.Build.props` — vários `.csproj` não declaram `TargetFramework`, confirmando centralização).
- Usa **`EnablePackageValidation` + `PackageValidationBaselineVersion`** em todos os pacotes inspecionados — validação automática de compatibilidade de API contra a versão publicada.
- Possui `tests/`, `.github/` (CI), `CHANGELOG.md`, `CONTRIBUTING.md`, `ROADMAP.md`, `CLAUDE.md`.
- **Não possui pasta `samples/`** (apenas `Codout.Framework.Mcp/docs/ai/mcp/sample-payloads.json`). A arquitetura de samples do Softprime (Frente B) é contribuição original, não cópia.

## Mapa de componentes (fato comprovado)

Projetos do Codout e correspondência no Softprime:

| Componente Codout | Softprime equivalente | Situação |
|---|---|---|
| Domain, Data, Common | Domain, DAL, Common | **Ambos têm** (conceitos equivalentes) |
| EF, NH | EFCore3/5, EF6, NH | **Ambos têm** |
| Api, Api.Dto, Api.Client | Api, Api.Dto, Api.Client | **Ambos têm** |
| Mailer (+AWS, +SendGrid) | Mailer (+AWS, +SendGrid) | **Ambos têm** |
| Multitenancy | Multitenancy | **Ambos têm** |
| **Mongo** | — | **Só Codout** |
| **Application** (CRUD/AppService) | — | **Só Codout** |
| **DynamicLinq** | — | **Só Codout** |
| **Storage (+Azure)** | — | **Só Codout** |
| **Security.Core (+Argon2/Bcrypt/Scrypt)** | Common.Security (Crypto, SimpleHash, RandomPassword) | **Codout mais avançado** |
| **Mailer.Razor** (template engine separado) | Mailer usa RazorLight embutido | **Abordagem diferente** |
| **Image.Extensions** | — | **Só Codout** |
| **Mcp** (servidor MCP p/ agentes) | — | **Só Codout** |

## Tabela de lacunas e decisões propostas

> Esforço em faixas: P (≤2 dias), M (3–5 dias), G (1–2 semanas), GG (>2 semanas). Compatibilidade indica o target recomendado **se** o Softprime adotar o componente, respeitando os invariantes do `modernization-plan.md`.

| # | Componente Codout | Evidência | Equivalente Softprime | Lacuna / melhoria concreta | Dependências | Compat. recomendada | Impacto | Risco | Esforço | Prio | Decisão proposta |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Security.Core + Argon2/Bcrypt/Scrypt | `src/Security/*.csproj`, `net10.0`, DI abstractions | `Common.Security` (hash/cripto utilitários) | Hash de senha moderno com **upgrade incremental/rehash transparente** no login; algoritmos atuais | `Microsoft.Extensions.DependencyInjection.Abstractions`; libs de hash | `net10.0` (impl. moderna) | Alto (segurança) | Médio — **retrocompat. de leitura** de hashes antigos é obrigatória (ver #6) | M–G | **Investigar** (candidato forte; coordenar com #6) |
| 2 | Storage (+Azure) | `Codout.Framework.Storage.csproj` (abstração), `.Azure` | — | Abstração de storage de arquivos (Azure/S3/FileSystem) plugável | abstração: nenhuma; Azure: SDK Azure Blob | contrato `netstandard2.0`; impl. `net10.0` | Médio | Baixo (novo, isolado) | M | **Adaptar** (contrato próprio + 1 impl.) |
| 3 | Application (AppService CRUD) | `Codout.Framework.Application.csproj` → AutoMapper 16, DynamicLinq, Domain | — | Serviços de aplicação CRUD genéricos sobre `IRepository<T>` | `AutoMapper`, DI; DynamicLinq | `net10.0` | Médio | Médio (opinativo; AutoMapper é escolha de design) | M | **Investigar** (avaliar se alinha ao estilo Softprime) |
| 4 | DynamicLinq | `Codout.DynamicLinq.csproj` → `System.Linq.Dynamic.Core` 1.7.1 | — | Filtro/ordenação/paginação/agregação dinâmicos para grids | `System.Linq.Dynamic.Core` | `netstandard2.0` (+ `net10.0`) | Médio | Baixo–Médio (segurança de query dinâmica) | P–M | **Adaptar** (útil p/ APIs de listagem) |
| 5 | Mongo | `Codout.Framework.Mongo.csproj` | — | Implementação `IRepository`/`IUnitOfWork` para MongoDB | MongoDB.Driver | `net10.0` | Médio | Baixo | M | **Investigar** (só se houver demanda real) |
| 6 | Mailer.Razor | `Codout.Mailer.Razor.csproj` → `FrameworkReference AspNetCore.App` | `Mailer` usa RazorLight embutido | Separar o **contrato** do mecanismo de template (Razor nativo vs. RazorLight) | ASP.NET Core | impl. `net10.0` (não serve `net48`) | Médio | Médio (muda design do Mailer; alinhado a #6) | M | **Investigar** (casa com "separar contrato do RazorLight" em #6) |
| 7 | Image.Extensions | `Codout.Image.Extensions.csproj` | — | Extração/manipulação de regiões de imagem | lib de imagem | `net10.0` | Baixo | Baixo | P–M | **Não adotar agora** (nicho; sem demanda declarada) |
| 8 | Mcp (servidor MCP) | `Codout.Framework.Mcp/` (projeto + testes + sln) | — | Servidor MCP expondo conhecimento do framework a agentes de IA | SDK MCP | `net10.0` | Alto (DX/IA) | Médio (novo domínio) | G–GG | **Investigar** (estratégico; fora do ciclo atual) |
| 9 | Package Validation (prática) | `EnablePackageValidation` + `PackageValidationBaselineVersion` em todos | — (Softprime não tem) | Gate automático de compatibilidade de API no `pack` | SDK .NET | aplicável a todos | Alto (qualidade) | Baixo | P | **Incorporar** a prática → alimenta **#11** e **#8** |
| 10 | Estrutura `tests/` + CONTRIBUTING/CHANGELOG/ROADMAP | árvore do repo | — (Softprime não tem) | Convenção de testes por projeto e governança documentada | — | — | Médio | Baixo | P–M | **Incorporar** convenção → alimenta **#7**, **#13** |

## Relação com o backlog existente (não duplicar)

Vários achados **reforçam** issues já abertas, e devem ser anexados a elas em vez de gerar issues novas:

- Achado **#9** (Package Validation) → alimenta **#11** (baseline de API) e **#8** (CI). **Comentar nessas issues**, não criar nova.
- Achado **#1** (Security moderno) → alimenta **#6** (segurança `Common.Security` + retrocompat.). **Comentar em #6.**
- Achado **#6/tabela** (separar contrato do Mailer) → já previsto em **#6**. **Comentar em #6.**
- Achado **#10** (tests/governança) → alimenta **#7** (testes) e **#13** (docs/diretrizes).

Os componentes genuinamente **novos** (Storage, Application, DynamicLinq, Mongo, Image, MCP) são consolidados em **uma issue de research** (ver backlog em `samples-design.md` e issues criadas nesta execução), com subdivisão futura **somente quando houver demanda**.

## Recomendações (não são decisão aprovada)

1. **Não** adotar componentes em bloco. Priorizar por demanda real: Security moderno (via #6) e a prática de Package Validation (via #11/#8) têm o melhor custo/benefício imediato.
2. Qualquer reaproveitamento de código do Codout deve **preservar a atribuição MIT** e passar por revisão de proveniência (há relação histórica entre os projetos a esclarecer).
3. Componentes novos (Storage, Application, DynamicLinq, Mongo, MCP) ficam como **candidatos de research**, cada um exigindo testes + docs + sample antes de virar pacote (regra permanente de novos componentes).
4. Respeitar os invariantes de target do `modernization-plan.md`: contratos que sirvam `net48` → `netstandard2.0`; implementações modernas → `net10.0`.

## Pendências / a confirmar

- **Demanda real** por Mongo, Storage, Application, DynamicLinq, Image e MCP — nenhum consumidor declarado neste repositório (liga-se a #10).
- **Origem/relação** entre `Softprime.Multitenancy` (dentro do repo Codout) e este repositório — confirmar com o mantenedor.
- Esforços são **faixas estimadas**, não compromissos; dependem das decisões de #3 (targets) e #12 (versionamento).

## Issues relacionadas (criadas/comentadas nesta leva)

- **[#22]** — **[Frente A] Expansão de componentes** (issue-mãe; agrupa as sub-issues via sub-issues nativas do GitHub).
- **[#17]** — [Research] Comparativo Codout × Softprime (esta análise; sub-issue de #22).
- Achados anexados como **comentários** (sem duplicar) em: **[#6]** (security/Mailer), **[#7]** (tests/governança), **[#8]** (CI + package validation), **[#11]** (baseline/package validation), **[#13]** (regra de novos componentes em CONTRIBUTING).
- Samples relacionados: **[#18]**, **[#19]**, **[#20]**, **[#21]**.

[#6]: https://github.com/softprime/Softprime.Framework/issues/6
[#7]: https://github.com/softprime/Softprime.Framework/issues/7
[#8]: https://github.com/softprime/Softprime.Framework/issues/8
[#11]: https://github.com/softprime/Softprime.Framework/issues/11
[#13]: https://github.com/softprime/Softprime.Framework/issues/13
[#17]: https://github.com/softprime/Softprime.Framework/issues/17
[#18]: https://github.com/softprime/Softprime.Framework/issues/18
[#19]: https://github.com/softprime/Softprime.Framework/issues/19
[#20]: https://github.com/softprime/Softprime.Framework/issues/20
[#21]: https://github.com/softprime/Softprime.Framework/issues/21

[#22]: https://github.com/softprime/Softprime.Framework/issues/22
