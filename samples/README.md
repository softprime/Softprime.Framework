# Trilha de Exemplos Didáticos (Samples) — Softprime.Framework

> Trilha de exemplos executáveis e autossuficientes do `Softprime.Framework` para aprendizado, experimentação e smoke testing em esteiras de integração contínua (CI).

---

## Estrutura da Trilha de Samples

Cada diretório abaixo é um projeto autocontido focado em um componente ou cenário específico, referenciando apenas **APIs públicas** do framework e operando **sem dependências de serviços externos ou credenciais pagas**:

| Diretório | Projeto | Componentes Exercitados | Status |
|---|---|---|---|
| [`01-Common`](01-Common/) | `Softprime.Samples.Common` | Extensões, validação (CPF/CNPJ), helpers, criptografia | ✅ Implementado |
| [`02-Domain`](02-Domain/) | `Softprime.Samples.Domain` | Entidades, Value Objects, DomainSignature, lifecycle | ✅ Implementado |
| [`03-Data-EFCore`](03-Data-EFCore/) | `Softprime.Samples.DataEFCore` | Repositório genérico, Unit of Work e paginação com SQLite na linha EFCore5/`net6.0` | ✅ Implementado |
| [`04-WebApi`](04-WebApi/) | `Softprime.Samples.WebApi` | Endpoints REST, contratos CRUD (`IApi`), DTOs, validação e `ApiKeyAuthorizeFilter` | ✅ Implementado |
| [`05-ApiClient`](05-ApiClient/) | `Softprime.Samples.ApiClient` | Cliente HTTP tipado, paginação, headers `ApiKey`/`TenantKey`, `ApiException` e timeout | ✅ Implementado |
| [`06-Mailer`](06-Mailer/) | `Softprime.Samples.Mailer` | Templates Razor com despacho em memória (`FakeMailerDispatcher`) | ✅ Implementado |
| [`07-Multitenancy`](07-Multitenancy/) | `Softprime.Samples.Multitenancy` | Resolução por cabeçalho/query/subdomínio, cache, isolamento e rejeição de tenant inválido | ✅ Implementado |
| [`08-CompleteSample`](08-CompleteSample/) | `Softprime.Samples.CompleteSample` | Cadastros e isolamento de tenants com SQLite e EFCore10 | ✅ Implementado |
| `99-Net48-Consumo` | `Softprime.Samples.Net48Consumo` | Validação de consumo em .NET Framework 4.8 | ⏳ Opcional |

---

## Diretrizes e Governança

1. **Solution Dedicada e Desacoplada:** Os samples são agrupados na solution desacoplada `samples/Softprime.Framework.Samples.sln`, contendo exclusivamente os projetos de sample (`Softprime.Samples.*`). As referências a `src/` são mantidas via `ProjectReference`, compilando apenas os alvos modernos necessários (`netstandard2.0`/`netstandard2.1`).
2. **Isolamento de Pacotes:** O arquivo `samples/Directory.Build.props` aplica `<IsPackable>false</IsPackable>` e `<GeneratePackageOnBuild>false</GeneratePackageOnBuild>`, impedindo que qualquer exemplo gere pacotes NuGet inadvertidamente.
3. **Contrato Canônico de README:** Cada sample segue o padrão fixo de 7 seções documentado em [`docs/samples-design.md`](../docs/samples-design.md).
4. **Smoke Test Integrado:** Todos os samples suportam a flag `--smoke-test` para validação rápida com código de saída `0` para sucesso. O script unificado `samples/run-smoke-tests.ps1` executa a bateria completa.
5. **Automação no CI (GitHub Actions):** O workflow [`.github/workflows/samples-ci.yml`](../.github/workflows/samples-ci.yml) compila a solution inteira e roda os smoke tests em runner Windows (`windows-latest`) a cada Pull Request e push.

---

## Como Compilar e Executar os Samples

### Validação completa do CI

```powershell
powershell -ExecutionPolicy Bypass -File ./samples/validate-ci.ps1
```

O script executa 14 combinações de sample e runtime com referências a projetos
e repete as 14 com pacotes NuGet locais. Os samples 01/02/04/05/06/07 rodam em
`net10.0` e `net8.0`, o 03 em `net6.0` com EFCore5 e o 08 em `net10.0` com EFCore10.

Os samples 01–07 usam `ProjectReference` por padrão. O sample 08 usa
`PackageReference` com o feed gerado pelo script. Em modo pacote, o script usa
`UseInstalledPackages=true` e fornece `FrameworkPackageVersion`. Ele gera uma versão exclusiva
de validação dos pacotes de produção, restaura em cache vazio e confere nos
assets e metadados que todos os pacotes Softprime vieram do feed local.
Dependências externas são restauradas do NuGet.org; após restore, os exemplos
executam sem serviços externos. Nenhum pacote é publicado.

Os binários e intermediários ficam separados em `bin/projects`, `obj/projects`
e `bin/packages`, `obj/packages`. O script de smoke tests executa os binários
Release já compilados; use `-UseInstalledPackages` para testar o segundo modo.
Os artefatos de validação ficam em `artifacts/samples-ci/<identificador>/`.
Cada sample tem `IsPackable` avaliado e é empacotado em diretório vazio;
qualquer `.nupkg` ou `.snupkg` produzido causa falha, independentemente do nome.

### 1. Compilação da Solution de Samples

```powershell
dotnet build samples/Softprime.Framework.Samples.sln -c Release -p:UseInstalledPackages=false
```

---

### 2. Execução da Bateria Completa de Smoke Tests

Via script PowerShell unificado (compatível com Windows PowerShell 5.1 e PowerShell Core):
```powershell
powershell -ExecutionPolicy Bypass -File ./samples/run-smoke-tests.ps1
# ou em PowerShell Core:
pwsh ./samples/run-smoke-tests.ps1
```

---

### 3. Execução Individual de Smoke Tests por Sample

```powershell
dotnet run --project samples/01-Common/Softprime.Samples.Common.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/02-Domain/Softprime.Samples.Domain.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj --framework net6.0 -- --smoke-test
dotnet run --project samples/04-WebApi/Softprime.Samples.WebApi.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/05-ApiClient/Softprime.Samples.ApiClient.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/06-Mailer/Softprime.Samples.Mailer.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj --framework net10.0 -- --smoke-test
```
