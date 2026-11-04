# Stack Tecnológica

> **Nota de planejamento:** esta página descreve o **estado atual verificado** do repositório. A estratégia de modernização de targets foi formalizada e aprovada na issue [#3] e detalhada em [`docs/target-compatibility-matrix.md`](../../docs/target-compatibility-matrix.md) e [`docs/modernization-plan.md`](../../docs/modernization-plan.md). Enquanto a issue [#12] (versionamento) não for aprovada, **não** assuma `Version 2.0.0` como padrão obrigatório para **novos** pacotes — veja a seção "Diretrizes técnicas".

## Plataforma

- **Linguagem:** C#
- **Alvos:** `netstandard2.0`, `netstandard2.1` e, em alguns projetos, `net48`.
  - `Softprime.Framework.EFCore5` tem como alvo apenas `netstandard2.1`.
  - `Softprime.Framework.EF6` tem como alvo `netstandard2.1` e `net48`.
- **SDK de projeto:** `Microsoft.NET.Sdk` (formato SDK-style `.csproj`).
- **Versão dos pacotes:** `2.0.0` (mantenha consistente entre os projetos).
- **Empacotamento:** `GeneratePackageOnBuild=true` — cada build em Release gera um `.nupkg`. Metadados NuGet (Authors, Company, Description em português, PackageIcon `logo-nuget.png`, licença `LICENSE.txt`) são definidos em cada `.csproj`.

## Principais dependências por projeto

- **Common:** `System.ComponentModel.Annotations`
- **Domain:** `System.ComponentModel.Annotations`
- **EFCore3:** `Microsoft.EntityFrameworkCore` 3.1.x
- **EFCore5:** `Microsoft.EntityFrameworkCore` 5.0.x
- **EF6:** `EntityFramework` 6.4.x
- **NH:** `NHibernate` 5.3.x, `FluentNHibernate` 3.1.x
- **Api:** `Microsoft.AspNetCore.Mvc` 2.2.x
- **Api.Client:** `Newtonsoft.Json` 12.x
- **Mailer:** `RazorLight` 2.0.0-rc.3
- **Mailer.AWS:** `AWSSDK.SimpleEmailV2`
- **Mailer.SendGrid:** `SendGrid` 9.x
- **Multitenancy:** `Microsoft.AspNetCore` 2.2.x, `Microsoft.Extensions.Caching.Memory`, `System.ComponentModel.Annotations`

## Build e empacotamento

A solution principal é `src/Softprime.Framework.All.sln`.

```bash
# Restaurar + compilar tudo
dotnet build src/Softprime.Framework.All.sln

# Release (gera os .nupkg)
dotnet build src/Softprime.Framework.All.sln -c Release
```

Observações para o ambiente Windows/PowerShell:
- Use `;` como separador de comandos (não `&&`).
- Evite iniciar processos de longa duração de forma bloqueante.

## Diretrizes técnicas

- Novos projetos devem ser SDK-style e seguir o padrão de metadados NuGet dos projetos existentes.
- **Alvos de projetos e pacotes:** Siga estritamente a [Matriz de Compatibilidade e Estratégia de Targets](../../docs/target-compatibility-matrix.md) aprovada na issue [#3]:
  - **Contratos e Utilitários Compartilhados:** `netstandard2.0` universal (preservando `net48` nativo onde já existir). `netstandard2.1` **não** deve ser utilizado em novos projetos e será descontinuado nos pacotes compartilhados (não suporta `net48`).
  - **Implementações Web e Modernas:** Multi-target **`net10.0;net8.0`** (usando `<FrameworkReference Include="Microsoft.AspNetCore.App" />`), cobrindo o alvo de modernização primário (.NET 10 LTS) e aplicações corporativas em transição (.NET 8 LTS).
  - **Implementações de Persistência:** Linhas legadas (`EF6`, `EFCore3`, `EFCore5`, `NH`) permanecem preservadas e isoladas; modernização de EF Core ocorre exclusivamente no novo pacote `Softprime.Framework.EFCore10` (`net10.0;net8.0`).
- **Versão de pacote:** a definição de versionamento (SemVer, linhas legadas, publicação) está na issue [#12]. Não assuma `2.0.0` como padrão para novos pacotes até a política ser aprovada.
- Ao adicionar dependências, prefira versões pinadas, estáveis (evite pré-release como o atual `RazorLight` rc) e compatíveis com os alvos escolhidos.
- Mantenha a separação entre projetos de abstração e projetos de implementação; não adicione dependência de um ORM específico num projeto de contrato.

[#3]: https://github.com/softprime/Softprime.Framework/issues/3
[#12]: https://github.com/softprime/Softprime.Framework/issues/12
