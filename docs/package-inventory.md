# Inventário de Pacotes, Consumidores e Dependências — Softprime.Framework

> Documento de apoio ao [Plano de Modernização](modernization-plan.md) · **Entregável da Issue [#10]** (Fase 0 — Conhecer e proteger).
>
> Data da auditoria: 2026-11-03 · Branch: `issue/10-inventario-consumidores-dependencias` · Base: `main`

---

## 1. Rastreabilidade e metodologia de auditoria

Os dados da matriz abaixo foram obtidos diretamente do repositório por análise estática dos 14 arquivos `.csproj` em `src/`:
- **Fontes factuais confirmadas:** Nós XML `<TargetFrameworks>`, `<PackageReference>` e `<ProjectReference>`.
- **Feed NuGet corporativo:** Inacessível a partir deste repositório (não há `nuget.config` versionado nem pipelines de publicação). Dessa forma, as versões reais publicadas constam formalmente como **`a confirmar`** contra o feed privado da Softprime.
- **Aplicações consumidoras externas:** Os repositórios dos clientes e produtos corporativos da Softprime residem fora deste repositório git. Em observância ao princípio da não-invenção de dados, os consumidores externos e seus respectivos runtimes estão declarados como **`a confirmar`** e acompanhados de roteiro de descoberta.

---

## 2. Matriz completa de inventário

| Pacote (`PackageId`) | Versão no `.csproj` | Versão no feed NuGet | Targets atuais | Dependentes internos (grafo do repo) | Consumidores externos | Targets dos consumidores | Dependências externas diretas | Dependências transitivas restritivas | Bloqueador de upgrade conhecido |
|---|---|---|---|---|---|---|---|---|---|
| `Softprime.Framework.Common` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1` | — (utilitário geral) | `a confirmar` | `a confirmar` | `System.ComponentModel.Annotations (5.0.0)` | Nenhuma | Preservar compatibilidade criptográfica de algoritmos em `Common.Security` para leitura de dados legados (migração #6). |
| `Softprime.Framework.Domain` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `System.ComponentModel.Annotations (5.0.0)`; `→ DAL` | Nenhuma | Contratos de identidade (`EntityWithTypedId`), hooks de ciclo de vida (`BeforeSave`, `AfterSave`) e assinatura de domínio. Depende de `DAL`. |
| `Softprime.Framework.DAL` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | `Domain`, `EFCore3`, `EFCore5`, `EF6`, `NH` | `a confirmar` | `a confirmar` | (nenhuma externa) | Nenhuma | Contrato fundamental de persistência (`IRepository<T>`, `IEntity<TId>`). Mudanças de assinatura quebram todos os ORMs e consumidores. |
| `Softprime.Framework.EFCore3` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `Microsoft.EntityFrameworkCore (3.1.11)`; `→ DAL` | `Microsoft.Extensions.DependencyInjection` 3.1.x, `Microsoft.Extensions.Logging` 3.1.x | Pacote fora de suporte da Microsoft. Incompatível com .NET 8/10 moderno. Deve ser preservado como linha legada isolada enquanto houver consumidores (#5). |
| `Softprime.Framework.EFCore5` | `2.0.0` | `a confirmar` | `netstandard2.1` | — | `a confirmar` | `a confirmar` | `Microsoft.EntityFrameworkCore (5.0.2)`; `→ DAL` | `Microsoft.Extensions.DependencyInjection` 5.0.x | **Não atende `net48`** (somente `netstandard2.1`). Fora de suporte. Incompatível com .NET 8/10 moderno. Preservar como linha legada (#5). |
| `Softprime.Framework.EF6` | `2.0.0` | `a confirmar` | `netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `EntityFramework (6.4.4)`; `→ DAL` | Nenhuma | EF6 é linha de produto e ecossistema totalmente distinto de EF Core. `.NET Standard 2.1 não roda em .NET Framework 4.8` (consumidores `net48` usam o target `net48`). Preservar linha legada sem forçar fusão com EF Core (#5). |
| `Softprime.Framework.NH` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `NHibernate (5.3.5)`, `FluentNHibernate (3.1.0)`; `→ DAL` | `Iesi.Collections (4.0.4)` | Mapeamentos legados em XML/Fluent e proxies dinâmicos gerados em tempo de execução. Linha de ORM independente (#5). |
| `Softprime.Framework.Api.Dto` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1` | `Api`, `Api.Client` | `a confirmar` | `a confirmar` | (nenhuma externa) | Nenhuma | Contrato de transferência puro. Qualquer alteração em propriedades quebra compatibilidade binária e serialização de DTOs. |
| `Softprime.Framework.Api` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1` | — | `a confirmar` | `a confirmar` | `Microsoft.AspNetCore.Mvc (2.2.0)`, `Microsoft.AspNetCore.Mvc.Abstractions (2.2.0)`; `→ Api.Dto` | Pacotes do ecossistema ASP.NET Core 2.2.0 | Pacote `Microsoft.AspNetCore.Mvc 2.2.0` descontinuado (out-of-support desde 2019). Não é referenciável a partir de `net48` nem compatível diretamente com o modelo moderno `FrameworkReference (Microsoft.AspNetCore.App)` do .NET 8/10. Migração crítica (#4). |
| `Softprime.Framework.Api.Client` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1` | — | `a confirmar` | `a confirmar` | `Newtonsoft.Json (12.0.3)`; `→ Api.Dto` | Nenhuma | Dependência de `Newtonsoft.Json 12.0.3`. Consumidores legados podem depender de custom formatters/converters Newtonsoft; avaliar `System.Text.Json` em #6 mantendo compatibilidade. |
| `Softprime.Framework.Mailer` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | `Mailer.AWS`, `Mailer.SendGrid` | `a confirmar` | `a confirmar` | `RazorLight (2.0.0-rc.3)` | `Microsoft.CodeAnalysis.CSharp` (Roslyn in-process) | Dependência em versão Release Candidate instável (`2.0.0-rc.3`). Compilação de templates Razor em runtime causa incompatibilidade com AOT/Trimming e versões recentes do runtime. Avaliar segregação do contrato em relação ao renderizador (#6). |
| `Softprime.Framework.Mailer.AWS` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `AWSSDK.SimpleEmailV2 (3.5.1.25)`; `→ Mailer` | `AWSSDK.Core (3.5.1.57)` | Depende do contrato de `Mailer` e SDK da AWS. |
| `Softprime.Framework.Mailer.SendGrid` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `SendGrid (9.22.0)`; `→ Mailer` | `Newtonsoft.Json` transitivo antigo | Depende do contrato de `Mailer` e SDK do SendGrid. |
| `Softprime.Framework.Multitenancy` | `2.0.0` | `a confirmar` | `netstandard2.0;netstandard2.1;net48` | — | `a confirmar` | `a confirmar` | `Microsoft.AspNetCore (2.2.0)`, `Microsoft.Extensions.Caching.Memory (5.0.0)`, `System.ComponentModel.Annotations (5.0.0)` | Pacotes de hosting e pipeline do ASP.NET Core 2.2 | Acoplamento ao ASP.NET Core 2.2.0 legado em conjunto com `net48`. Bloqueador de runtime moderno (#4). |

---

## 3. Mapa de restrições de target e compatibilidade

A auditoria factual confirma 3 categorias nítidas de restrições no ecossistema:

1. **Pacotes com suporte legado explícito a `.NET Framework 4.8` (`net48`):**
   - `Domain`, `DAL`, `EFCore3`, `EF6`, `NH`, `Mailer`, `Mailer.AWS`, `Mailer.SendGrid`, `Multitenancy`.
   - **Regra inviolável:** Se esses pacotes forem consumidos por aplicações corporativas ainda em `net48`, qualquer evolução deve manter multi-targeting (`net48;net10.0` ou `netstandard2.0;net10.0`).
2. **Pacotes em `.NET Standard 2.0 / 2.1`:**
   - `Common`, `Api.Dto`, `Api.Client`, `Api`.
   - **Alerta de compatibilidade:** `.NET Standard 2.1` **NÃO** roda em `.NET Framework 4.8`. Consumidores `net48` consomem a fatia `netstandard2.0` (ou o target `net48` quando presente).
3. **Pacotes com target assimétrico:**
   - `EFCore5`: Mira **exclusivamente `netstandard2.1`**. Consumidores em `.NET Framework 4.8` são **incapazes** de referenciar este pacote.
   - `EF6`: Mira `netstandard2.1;net48` (não possui `netstandard2.0`).


<!-- Links -->
[#10]: https://github.com/softprime/Softprime.Framework/issues/10
