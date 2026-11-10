# Trilha de Exemplos Didáticos (Samples) — Softprime.Framework

> Trilha de exemplos executáveis e autossuficientes do `Softprime.Framework` para aprendizado, experimentação e smoke testing em esteiras de integração contínua (CI).

---

## Estrutura da Trilha de Samples

Cada diretório abaixo é um projeto autocontido focado em um componente ou cenário específico, referenciando apenas **APIs públicas** do framework e operando **sem dependências de serviços externos ou credenciais pagas**:

| Diretório | Projeto | Componentes Exercitados | Status |
|---|---|---|---|
| [`01-Common`](01-Common/) | `Softprime.Samples.Common` | Extensões, validação (CPF/CNPJ), helpers, criptografia | ✅ Implementado |
| [`02-Domain`](02-Domain/) | `Softprime.Samples.Domain` | Entidades, Value Objects, DomainSignature, lifecycle | ✅ Implementado |
| [`03-Data-EFCore`](03-Data-EFCore/) | `Softprime.Samples.DataEFCore` | Repositório genérico, Unit of Work, paginação (SQLite local via EFCore5/`net6.0` até #5) | ✅ Implementado |
| `04-WebApi` | `Softprime.Samples.WebApi` | Endpoints REST, contratos CRUD (`IApi`), DTOs, filtros | ⏳ Planejado |
| `05-ApiClient` | `Softprime.Samples.ApiClient` | Cliente HTTP tipado, tratamento de erros, cancelamento | ⏳ Planejado |
| `06-Mailer` | `Softprime.Samples.Mailer` | Templates Razor com despacho em memória (`FakeMailerDispatcher`) | ⏳ Planejado |
| `07-Multitenancy` | `Softprime.Samples.Multitenancy` | Resolução por cabeçalho/subdomínio e rejeição de tenant inválido | ⏳ Planejado |
| `08-CompleteSample` | `Softprime.Samples.CompleteSample` | Aplicação corporativa integrada ponta a ponta | ⏳ Planejado |
| `99-Net48-Consumo` | `Softprime.Samples.Net48Consumo` | Validação de consumo em .NET Framework 4.8 | ⏳ Opcional |

---

## Diretrizes e Governança

1. **Solution Dedicada:** Os samples são agrupados na solution desacoplada `samples/Softprime.Framework.Samples.sln`.
2. **Isolamento de Pacotes:** O arquivo `samples/Directory.Build.props` aplica `<IsPackable>false</IsPackable>` e `<GeneratePackageOnBuild>false</GeneratePackageOnBuild>`, impedindo que qualquer exemplo gere pacotes NuGet inadvertidamente.
3. **Contrato Canônico de README:** Cada sample segue o padrão fixo de 7 seções documentado em [`docs/samples-design.md`](../docs/samples-design.md).
4. **Smoke Test Integrado:** Todos os samples suportam a flag `--smoke-test` para validação rápida no pipeline de CI com código de saída `0` para sucesso.

---

## Como Compilar e Executar Todos os Samples

Para compilar a solution inteira de samples:
```powershell
dotnet build samples/Softprime.Framework.Samples.sln -c Release
```

Para rodar o smoke test dos samples implementados:
```powershell
dotnet run --project samples/01-Common/Softprime.Samples.Common.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/02-Domain/Softprime.Samples.Domain.csproj --framework net10.0 -- --smoke-test
dotnet run --project samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj -- --smoke-test
```
