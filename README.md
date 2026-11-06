# Softprime.Framework

Conjunto de bibliotecas (libs) .NET corporativas da Softprime para acelerar o desenvolvimento de aplicações robustas e escaláveis. O framework oferece abstrações reutilizáveis para persistência de dados, construção de Web APIs, clientes HTTP, envio de e-mails e multitenancy, além de uma rica coleção de funções utilitárias.

Todos os pacotes são distribuídos como bibliotecas NuGet modulares:
- **Contratos e utilitários compartilhados:** Alvo universal em **`netstandard2.0`** (com fatias nativas em **`net48`**), assegurando interoperabilidade perene entre aplicações legadas em .NET Framework e sistemas em .NET moderno.
- **Implementações Web e modernas:** Multi-targeting moderno em **`net10.0;net8.0`** aproveitando o shared framework `Microsoft.AspNetCore.App` e o novo ORM `Softprime.Framework.EFCore10`.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Arquitetura e Camadas](#arquitetura-e-camadas)
- [Pacotes e Módulos](#pacotes-e-módulos)
- [Arquitetura Alvo e Estratégia de Modernização](#arquitetura-alvo-e-estratégia-de-modernização)
- [Requisitos e Ambiente](#requisitos-e-ambiente)
- [Como Compilar](#como-compilar)
- [Exemplos de Uso](#exemplos-de-uso)
- [Diretrizes de Contribuição e Qualidade](#diretrizes-de-contribuição-e-qualidade)
- [Documentação Arquitetural e Governança](#documentação-arquitetural-e-governança)
- [Convenções](#convenções)
- [Licença](#licença)

---

## Visão Geral

O Softprime.Framework é organizado em cinco áreas funcionais, cada uma composta por contratos de abstração e implementações plugáveis:

| Área | Responsabilidade Principal |
|---|---|
| **general** | Funções utilitárias comuns (extensions, helpers, segurança/criptografia, validações e annotations). |
| **data** | Abstrações de domínio (`Domain`), contratos de persistência (`DAL`) e implementações de ORM (`EFCore10` moderno, além de `EFCore3`, `EFCore5`, `EF6` e `NHibernate` mantidos). |
| **api** | Contratos DTOs, abstração CRUD padronizada para Web APIs e cliente HTTP fortemente tipado. |
| **mail** | Motor corporativo de e-mails com templating desacoplado e provedores plugáveis (AWS SES, SendGrid). |
| **multitenant** | Motor de multitenancy para aplicações ASP.NET Core com isolamento por contexto e pipeline de DI. |

A baseline existente de pacotes é a **2.0.0** (linha de sustentação estável). A evolução e modernização seguem na linha **3.0.0** sob SemVer 2.0.0.

---

## Arquitetura e Camadas

O framework adota o princípio de **abstração desacoplada + implementações plugáveis**. As interfaces fundamentais residem em projetos de contrato (ex.: `Softprime.Framework.DAL`, `Softprime.Framework.Mailer`) e cada tecnologia de infraestrutura possui seu próprio pacote isolado. Isso permite substituir o ORM ou o provedor de e-mail sem qualquer alteração no código de negócio.

Fluxo arquitetural da camada de dados:

```
Softprime.Framework.Domain      (Entity, ValueObject, BaseObject, ValidatableObject)
          │  referencia
          ▼
Softprime.Framework.DAL         (IEntity, IRepository<T>, IUnitOfWork, ITenant)
          ▲  implementam
          │
  ┌───────┴───────┬───────────────┬───────────────┬───────────────┐
EFCore10       EFCore3         EFCore5          EF6            NH (NHibernate)
(Moderno)      (Legado)        (Legado)        (Legado)        (Legado)
```

- **Domain:** Fornece entidades de domínio com identidade de negócio via `[DomainSignature]`, controle de igualdade por valor/identidade e hooks de ciclo de vida (`BeforeSave`, `AfterSave`, `BeforeDelete`, `AfterDelete`).
- **DAL:** Define os contratos de repositório genérico síncrono e assíncrono (`IRepository<T>`) com suporte a expressões lambda (`Expression<Func<T, bool>>`), paginação e controle de transação via `IUnitOfWork`.
- **Implementações:** Cada provedor de persistência encapsula as peculiaridades de sua tecnologia mantendo a transparência para os serviços de aplicação.

---

## Pacotes e Módulos

### general
- **Softprime.Framework.Common** — Funções utilitárias corporativas:
  - `Extensions`: Strings, Dates, Numeric, Linq, Objects, IO, Streams, Database, Exceptions, Inflector, SlugHelper, RegexPattern, Validation.
  - `Helpers`: AsyncHelper, EnumHelper, GeoHelper, NumberToText, PluginLoader, ResourceExtractor, ResourcesHelper.
  - `Security`: Governança criptográfica (CryptoString com envelope dinâmico, CryptoFile em streaming AES, SimpleHash e hash de senhas com rehash transparente).
  - `Annotations`: Data annotations customizadas (`CpfCnpjAttribute`, `RequiredIfAttribute`, `CompareAttribute`, `DateRangeAttribute`, `ConditionalAttributeBase`).

### data
- **Softprime.Framework.Domain** — Contratos e classes base de domínio: `Entity`, `EntityWithTypedId<TId>`, `ValueObject`, `BaseObject`, `ValidatableObject`, `DomainSignatureAttribute`.
- **Softprime.Framework.DAL** — Interfaces de persistência: `IEntity`, `IEntity<T>`, `IRepository<T>`, `IUnitOfWork`, `IUnitOfWorkFactory`, `ITenant`, `IHasAssignedId`.
- **Softprime.Framework.EFCore10** — Implementação moderna para Entity Framework Core no ecossistema .NET moderno (`net10.0;net8.0`).
- **Softprime.Framework.EFCore3** — Linha legada para EF Core 3.1.
- **Softprime.Framework.EFCore5** — Linha legada para EF Core 5.0.
- **Softprime.Framework.EF6** — Linha legada para Entity Framework 6.4.
- **Softprime.Framework.NH** — Linha legada para NHibernate 5.x / FluentNHibernate.

### api
- **Softprime.Framework.Api.Dto** — Contratos e DTOs base: `IDto`, `IPagedResult`, `DtoBase`, `PagedResultDto<TDto>`.
- **Softprime.Framework.Api** — Abstração de Web API: contrato CRUD padronizado `IApi<TDto, TId>` e filtros de segurança (`ApiKeyAuthorizeFilter`).
- **Softprime.Framework.Api.Client** — Cliente HTTP fortemente tipado: `ApiClientBase`, `ApiClient`, `ApiClientTyped`, `IApiClient`, `ApiException`.

### mail
- **Softprime.Framework.Mailer** — Abstração de envio de e-mails corporativos com templating: `ISoftprimeMailer`, `ISoftprimeMailerDispatcher`.
- **Softprime.Framework.Mailer.AWS** — Dispatcher via Amazon Simple Email Service (SES).
- **Softprime.Framework.Mailer.SendGrid** — Dispatcher via SendGrid API.

### multitenant
- **Softprime.Framework.Multitenancy** — Multitenancy para ASP.NET Core: resolução dinâmica de locatário (`ITenantResolver`, `MemoryCacheTenantResolver`), contexto (`TenantContext`) e extensões de middleware e pipeline de DI.

---

## Arquitetura Alvo e Estratégia de Modernização

Com base nas decisões formalizadas nas issues [#3], [#12] e [#9], o Softprime.Framework opera sob as seguintes diretrizes:

1. **Estratégia de Targets:**
   - **`netstandard2.0` universal:** Utilizado em todos os contratos e pacotes utilitários para permitir consumo sem atrito por aplicações .NET Framework e .NET moderno.
   - **`net10.0;net8.0` multi-targeting:** Utilizado nas implementações modernas do ASP.NET Core e EF Core, integrando o shared framework `Microsoft.AspNetCore.App`.
   - **Descontinuação de `netstandard2.1`:** Não utilizado em novos projetos e removido de pacotes compartilhados devido à falta de suporte em `.NET Framework 4.8`.
2. **Governança de Versões (SemVer 2.0.0):**
   - **Linha 2.x:** Mantida para suporte e correções de segurança em `.NET Framework 4.8`.
   - **Linha 3.x:** Destinada às bibliotecas modernizadas em .NET 8 e .NET 10 LTS.
3. **Qualidade e CI:**
   - Topologia de build padronizada 100% em Windows (`windows-latest` + `pwsh`).
   - Verificação contínua de superfícies de API pública e validação binária de pacotes (`EnablePackageValidation`).

---

## Requisitos e Ambiente

- **Para compilação e desenvolvimento completo:**
  - SDK do .NET 10 LTS e SDK do .NET 8 LTS instalados.
  - Suporte a ferramentas de build do .NET Framework 4.8 Developer Pack (ambiente Windows).
  - Visual Studio 2022+ ou VS Code com extensão C# Dev Kit / `dotnet` CLI.

---

## Como Compilar

A solution principal está localizada em `src/Softprime.Framework.All.sln`.

```powershell
# Restaurar e compilar toda a solution
dotnet build src/Softprime.Framework.All.sln

# Compilar em modo Release
dotnet build src/Softprime.Framework.All.sln -c Release
```

---

## Exemplos de Uso

### Repositório Genérico de Domínio
```csharp
// Entidade de domínio com DomainSignature
public class Cliente : Entity
{
    [DomainSignature]
    public virtual string Documento { get; set; }
    public virtual string Nome { get; set; }
}

// Consumo via abstração IRepository<T>
IRepository<Cliente> repositorio = new EFRepository<Cliente>(dbContext);

var cliente = await repositorio.GetAsync(c => c.Documento == "00000000000");
var pagina  = repositorio.Find(c => c.Nome.StartsWith("A"), out int total, index: 0, size: 50);

await repositorio.SaveAsync(new Cliente { Nome = "Novo Cliente", Documento = "11122233344" });
```

### Web API Padronizada (CRUD)
```csharp
[ApiController]
[Route("api/[controller]")]
public class ClientesController : ControllerBase, IApi<ClienteDto, long>
{
    // Expõe contratos padronizados: Get(), Get(page, size), Get(id), Post(dto), Put(id, dto), Delete(id)
}
```

---

## Diretrizes de Contribuição e Qualidade

Qualquer contribuição ao Softprime.Framework deve seguir as normas estabelecidas no [Guia de Contribuição (`CONTRIBUTING.md`)](CONTRIBUTING.md).

> [!IMPORTANT]
> **A Regra dos Três Pilares para Novos Componentes:**
> Todo novo componente em `src/` exige obrigatoriamente:
> 1. **Testes automatizados** correspondentes em `tests/` ([#7]).
> 2. **Especificação de API pública** e documentação XML em português em `docs/` ([#11]).
> 3. **Exemplo didático executável** e autossuficiente em `samples/` ([#18], [#23]).

---

## Documentação Arquitetural e Governança

Para detalhes técnicos e históricos das decisões de arquitetura, consulte a pasta `docs/`:

- [Plano Geral de Modernização (`docs/modernization-plan.md`)](docs/modernization-plan.md)
- [Matriz de Compatibilidade e Estratégia de Targets (`docs/target-compatibility-matrix.md`)](docs/target-compatibility-matrix.md)
- [Política de Versionamento e Publicação NuGet (`docs/nuget-versioning-policy.md`)](docs/nuget-versioning-policy.md)
- [Especificação da Esteira de CI/CD (`docs/ci-pipeline-spec.md`)](docs/ci-pipeline-spec.md)
- [Estratégia de Testes Automatizados (`docs/test-strategy.md`)](docs/test-strategy.md)
- [Desenho da Trilha de Samples Didáticos (`docs/samples-design.md`)](docs/samples-design.md)
- [Análise Comparativa com Codout.Framework (`docs/codout-gap-analysis.md`)](docs/codout-gap-analysis.md)

---

## Convenções

- **Namespace raiz:** `Softprime.Framework.*`, espelhando a estrutura física das pastas.
- **Abstrações:** Contratos separados de implementações concretas; proibido acoplar contratos a tecnologias específicas.
- **Entidades:** Herdam de `Entity` ou `EntityWithTypedId<TId>` e utilizam `[DomainSignature]` para igualdade semântica.
- **Commits:** Conventional Commits (`feat(...)`, `fix(...)`, `docs(...)`, etc.).
- **Idioma:** Documentação XML (`///`), descrições de pacotes e documentação técnica redigidas em português.

---

## Licença

Distribuído sob a licença [MIT](src/LICENSE.txt). Copyright (c) 2021-2026 Softprime.

[#3]: https://github.com/softprime/Softprime.Framework/issues/3
[#7]: https://github.com/softprime/Softprime.Framework/issues/7
[#9]: https://github.com/softprime/Softprime.Framework/issues/9
[#11]: https://github.com/softprime/Softprime.Framework/issues/11
[#12]: https://github.com/softprime/Softprime.Framework/issues/12
[#18]: https://github.com/softprime/Softprime.Framework/issues/18
[#23]: https://github.com/softprime/Softprime.Framework/issues/23
