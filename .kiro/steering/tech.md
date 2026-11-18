# Stack Tecnológica

> **Nota de governança:** este documento estabelece as diretrizes técnicas do Softprime.Framework. Para evitar contradições, ele distingue formalmente o **Estado Atual Verificado (Baseline 2.0.0)** da **Arquitetura Alvo e Regras para Novos Projetos**, incorporando as decisões aprovadas nas issues [#3] (Targets), [#12] (Versionamento SemVer), [#9] (Metadados Centralizados), [#8] (CI/CD Windows), [#7] (Estratégia de Testes) e [#17]/[#22] (Governança e Samples).

---

## 1. Estado Atual Verificado (Baseline 2.0.0)

Esta seção descreve a realidade factual do repositório no baseline 2.0.0:

### Plataforma e Targets Históricos
- **Linguagem:** C#
- **SDK de projeto:** `Microsoft.NET.Sdk` (formato SDK-style `.csproj`).
- **Alvos atuais dos 14 projetos legados:** `netstandard2.0`, `netstandard2.1` e, em alguns projetos, `net48`.
  - `Softprime.Framework.EFCore5` tem como alvo apenas `netstandard2.1`.
  - `Softprime.Framework.EF6` tem como alvo `netstandard2.1` e `net48`.
  - Projetos com fatia nativa `net48`: `Domain`, `DAL`, `EF6`, `NH`, `Mailer`.
- **Versão dos pacotes:** `2.0.0` (atualmente duplicada como propriedade individual `<Version>2.0.0</Version>` em cada um dos 14 `.csproj`).
- **Empacotamento:** `GeneratePackageOnBuild=true` duplicado em cada `.csproj`. Metadados NuGet (`Authors`, `Company`, `Description` em português, `PackageIcon` `logo-nuget.png`, licença `LICENSE.txt`) definidos individualmente.

### Dependências Factuais por Projeto
- **Common:** `System.ComponentModel.Annotations` 5.0.0
- **Domain:** `System.ComponentModel.Annotations` 5.0.0
- **EFCore3:** `Microsoft.EntityFrameworkCore` 3.1.x
- **EFCore5:** `Microsoft.EntityFrameworkCore` 5.0.x
- **EF6:** `EntityFramework` 6.4.x
- **NH:** `NHibernate` 5.3.x, `FluentNHibernate` 3.1.x
- **Api:** `Microsoft.AspNetCore.Mvc` 2.2.x
- **Api.Client:** `Newtonsoft.Json` 12.0.x
- **Mailer:** `RazorLight` 2.0.0-rc.3
- **Mailer.AWS:** `AWSSDK.SimpleEmailV2`
- **Mailer.SendGrid:** `SendGrid` 9.x
- **Multitenancy:** `Microsoft.AspNetCore` 2.2.x, `Microsoft.Extensions.Caching.Memory`, `System.ComponentModel.Annotations`

### Build e Empacotamento
A solution principal que compila os 14 projetos legados é `src/Softprime.Framework.All.sln`.

```powershell
# Restaurar e compilar toda a solution
dotnet build src/Softprime.Framework.All.sln

# Compilar em Release (gera os pacotes .nupkg locais em bin/Release)
dotnet build src/Softprime.Framework.All.sln -c Release
```

---

## 2. Arquitetura Alvo e Regras para Novos Projetos (Aprovadas)

Todas as novas implementações, correções e modernizações devem seguir estritamente as decisões normativas vigentes:

### 2.1. Matriz de Targets e Compatibilidade ([#3])
Documento normativo: [`docs/target-compatibility-matrix.md`](../../docs/target-compatibility-matrix.md).

- **Contratos e Utilitários Compartilhados (`Common`, `DAL`, `Domain`, `Api.Dto`, `Mailer`):**
  - Alvo universal: **`netstandard2.0`** (garante consumo seguro tanto em .NET moderno quanto em .NET Framework).
  - Preservar fatia nativa `net48` onde já existir para evitar atrito com assemblies de fachada (*facades*).
  - **Proibição expressa:** **`netstandard2.1` NÃO deve ser utilizado como alvo em novos projetos** e será descontinuado nos pacotes compartilhados (pois não possui suporte em `.NET Framework 4.8`).
- **Implementações Web e Modernas (`Api`, `Multitenancy` e novos componentes modernos):**
  - Multi-targeting obrigatório: **`net10.0;net8.0`**.
  - Devem referenciar o ASP.NET Core moderno via framework compartilhado: `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (eliminando referências legadas fora de suporte como `Microsoft.AspNetCore 2.2.0`).
- **Implementações de Persistência:**
  - As linhas legadas (`EF6`, `EFCore3`, `EFCore5`, `NH`) permanecem congeladas e isoladas para sustentação do ecossistema existente.
  - A modernização da camada de dados ocorre exclusivamente através de um novo pacote independente: **`Softprime.Framework.EFCore10`** com targets `net10.0;net8.0` ([#5]).

### 2.2. Política de Versionamento e Metadados ([#12], [#9])
Documento normativo: [`docs/nuget-versioning-policy.md`](../../docs/nuget-versioning-policy.md).

- **Adoção de SemVer 2.0.0 Estrito:**
  - `MAJOR`: Quebra binária de API, quebra de contrato comportamental ou perda/remoção de target suportado.
  - `MINOR`: Novas APIs ou funcionalidades retrocompatíveis, adição de novo target sem quebra de anteriores.
  - `PATCH`: Correções de bugs internos e segurança sem alteração de assinaturas públicas.
- **Coexistência de Linhas:**
  - **Linha 2.x (`2.0.x`, `2.1.x`):** Reservada para sustentação, correções críticas e segurança de sistemas corporativos em `.NET Framework 4.8`.
  - **Linha 3.x (`3.0.0+`):** Adotada para os pacotes com runtimes modernos (`net10.0;net8.0`) e atualizações estruturais de quebra de compatibilidade.
- **Centralização de Metadados ([#9]):**
  - Novos projetos e a modernização da solução adotam a centralização de propriedades comuns (`Authors`, `Company`, `PackageLicenseFile`, `PackageIcon`, `RepositoryUrl`) em `Directory.Build.props` na raiz de `src/`.
- **Imutabilidade e Governança:**
  - Versões publicadas em feeds NuGet são absolutamente imutáveis. Publicação somente via aprovação manual com sign-off.

### 2.3. Esteira de Integração Contínua — CI ([#8])
Documento normativo: [`docs/ci-pipeline-spec.md`](../../docs/ci-pipeline-spec.md).

- **Topologia Padronizada 100% Windows:**
  - Execução em runners `windows-latest` via PowerShell Core (`pwsh`), assegurando compilação nativa de `net48`, .NET 8 e .NET 10 sem falsos positivos de ambientes Linux.
- **Gates Obrigatórios de Qualidade:**
  - Validação de APIs públicas via Roslyn `Microsoft.CodeAnalysis.PublicApiAnalyzers` ([#11]).
  - Validação binária de compatibilidade no empacotamento via `EnablePackageValidation` contra o baseline `2.0.0` ([#11], [#17]).
  - Validação de samples com smoke tests temporários em SQLite ([#21]).
  - **Invariante No-Push:** O CI não publica pacotes automaticamente. Artefatos de build gerados têm retenção temporária.

### 2.4. A Regra dos Três Pilares para Novos Componentes ([#17]/[#22], [#7], [#18]/[#23])
Detalhada no [Guia de Contribuição (`CONTRIBUTING.md`)](../../CONTRIBUTING.md), qualquer novo componente exige:
1. **Testes Automatizados:** Projeto correspondente em `tests/<NomeDoProjeto>.Tests` cobrindo comportamento e regressão.
2. **Governança de API Pública:** Comentários XML em português e aprovação formal de superfícies de API.
3. **Exemplo Didático Executável:** Projeto demonstrativo em `samples/` autossuficiente e sem segredos/infraestrutura paga.

### 2.5. Boas Práticas de Dependências e Arquitetura
- Dependências externas devem ser sempre **versões pinadas, estáveis e com suporte ativo** (eliminando pré-releases como o `RazorLight` rc).
- Respeitar rigorosamente a separação entre projetos de contrato (`DAL`, `Domain`, `Api.Dto`, `Mailer`) e implementações concretas de terceiros; nunca adicionar dependência de ORM ou tecnologia específica dentro de projetos de abstração.

---

[#3]: https://github.com/softprime/Softprime.Framework/issues/3
[#5]: https://github.com/softprime/Softprime.Framework/issues/5
[#7]: https://github.com/softprime/Softprime.Framework/issues/7
[#8]: https://github.com/softprime/Softprime.Framework/issues/8
[#9]: https://github.com/softprime/Softprime.Framework/issues/9
[#11]: https://github.com/softprime/Softprime.Framework/issues/11
[#12]: https://github.com/softprime/Softprime.Framework/issues/12
[#17]: https://github.com/softprime/Softprime.Framework/issues/17
[#18]: https://github.com/softprime/Softprime.Framework/issues/18
[#21]: https://github.com/softprime/Softprime.Framework/issues/21
[#22]: https://github.com/softprime/Softprime.Framework/issues/22
[#23]: https://github.com/softprime/Softprime.Framework/issues/23
