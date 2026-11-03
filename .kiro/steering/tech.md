# Stack Tecnológica

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

- Novos projetos devem ser SDK-style, seguir o padrão de metadados NuGet dos projetos existentes e manter a versão `2.0.0` alinhada.
- Ao adicionar dependências, prefira versões pinadas e compatíveis com os alvos `netstandard`.
- Mantenha a separação entre projetos de abstração e projetos de implementação; não adicione dependência de um ORM específico num projeto de contrato.
