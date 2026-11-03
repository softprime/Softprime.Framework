# Softprime.Framework

Conjunto de bibliotecas (libs) .NET da Softprime para acelerar o desenvolvimento de aplicações corporativas. O framework oferece abstrações reutilizáveis para persistência de dados, construção de Web APIs, clientes HTTP, envio de e-mails e multitenancy, além de uma rica coleção de funções utilitárias.

Todos os pacotes são distribuídos como bibliotecas NuGet, têm como alvo principal `netstandard2.0` / `netstandard2.1` (alguns também `net48`) e podem ser consumidos tanto por aplicações .NET Framework quanto .NET Core.

## Sumário

- [Visão geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Pacotes](#pacotes)
- [Requisitos](#requisitos)
- [Como compilar](#como-compilar)
- [Exemplos de uso](#exemplos-de-uso)
- [Convenções](#convenções)
- [Licença](#licença)

## Visão geral

O Softprime.Framework é organizado em cinco áreas funcionais, cada uma com um ou mais projetos:

| Área | Responsabilidade |
|------|------------------|
| **general** | Funções utilitárias comuns (extensions, helpers, segurança, validações). |
| **data** | Abstrações de domínio e persistência (Domain, DAL) e suas implementações (EF Core 3/5, EF6, NHibernate). |
| **api** | Abstrações para Web API, DTOs e cliente HTTP tipado. |
| **mail** | Abstração de envio de e-mails com templating e provedores (AWS SES, SendGrid). |
| **multitenant** | Módulo de multitenancy para aplicações ASP.NET Core. |

A versão atual de todos os pacotes é **2.0.0**.

## Arquitetura

O framework segue um padrão de **abstração + implementações plugáveis**. As interfaces e contratos ficam em projetos de abstração (ex.: `Softprime.Framework.DAL`, `Softprime.Framework.Mailer`) e cada tecnologia concreta tem seu próprio projeto que implementa esses contratos. Isso permite trocar o ORM ou o provedor de e-mail sem alterar o código de negócio.

Camada de dados (fluxo típico):

```
Softprime.Framework.Domain      (Entity, ValueObject, BaseObject, ValidatableObject)
          │  referencia
          ▼
Softprime.Framework.DAL         (IEntity, IRepository<T>, IUnitOfWork, ITenant)
          ▲  implementam
          │
  ┌───────┴───────┬───────────────┬───────────────┐
EFCore3        EFCore5          EF6            NH (NHibernate)
```

- `Softprime.Framework.Domain` fornece a base de entidades de domínio (`Entity`, `EntityWithTypedId<TId>`) com igualdade por identidade, domain signature e hooks de ciclo de vida (`BeforeSave`, `AfterSave`, `BeforeDelete`, `AfterDelete`).
- `Softprime.Framework.DAL` define os contratos de persistência: `IRepository<T>` (CRUD síncrono e assíncrono, consultas com `Expression<Func<T, bool>>` e paginação) e `IUnitOfWork`.
- As implementações (`EFCore3`, `EFCore5`, `EF6`, `NH`) fornecem repositórios e unidades de trabalho concretos para cada ORM.

## Pacotes

### general

- **Softprime.Framework.Common** — funções utilitárias comuns, agrupadas em:
  - `Extensions`: `Strings`, `Dates`, `Numeric`, `Linq`, `Objects`, `IO`, `Streams`, `Database`, `Exceptions`, `Inflector`, `SlugHelper`, `RegexPattern`, `Validation`.
  - `Helpers`: `AsyncHelper`, `EnumHelper`, `GeoHelper`, `NumberToText`, `PluginLoader`, `ResourceExtractor`, `ResourcesHelper`.
  - `Security`: `Crypto`, `CryptoFile`, `CryptoString`, `RandomPassword`, `SimpleHash`.
  - `Annotations`: data annotations customizadas (`CpfCnpjAttribute`, `RequiredIfAttribute`, `CompareAttribute`, `DateRangeAttribute`, `ConditionalAttributeBase`).

### data

- **Softprime.Framework.Domain** — camada de domínio: `Entity`, `EntityWithTypedId<TId>`, `ValueObject`, `BaseObject`, `ValidatableObject`, `DomainSignatureAttribute`. (`System.ComponentModel.Annotations`)
- **Softprime.Framework.DAL** — interfaces de persistência: `IEntity`, `IEntity<T>`, `IRepository<T>`, `IUnitOfWork`, `IUnitOfWorkFactory`, `ITenant`, `IHasAssignedId`.
- **Softprime.Framework.EFCore3** — implementação para Entity Framework Core 3.1. (`Microsoft.EntityFrameworkCore` 3.1.x)
- **Softprime.Framework.EFCore5** — implementação para Entity Framework Core 5. (`Microsoft.EntityFrameworkCore` 5.0.x)
- **Softprime.Framework.EF6** — implementação para Entity Framework 6. (`EntityFramework` 6.4.x)
- **Softprime.Framework.NH** — implementação para NHibernate. (`NHibernate`, `FluentNHibernate`)

### api

- **Softprime.Framework.Api.Dto** — contratos e DTOs base: `IDto`, `IPagedResult`, `DtoBase`, `PagedResultDto<TDto>`.
- **Softprime.Framework.Api** — abstração de Web API: `IApi<TDto, TId>` (contrato CRUD padrão) e `ApiKeyAuthorizeFilter`. (`Microsoft.AspNetCore.Mvc`)
- **Softprime.Framework.Api.Client** — cliente HTTP tipado: `ApiClientBase`, `ApiClient`, `ApiClientTyped`, `IApiClient`, `ApiException`. (`Newtonsoft.Json`)

### mail

- **Softprime.Framework.Mailer** — abstração de envio de e-mail com templating via RazorLight: `ISoftprimeMailer`, `ISoftprimeMailerDispatcher`, `SoftprimeMailer`, modelos e helpers. (`RazorLight`)
- **Softprime.Framework.Mailer.AWS** — dispatcher via Amazon SES. (`AWSSDK.SimpleEmailV2`)
- **Softprime.Framework.Mailer.SendGrid** — dispatcher via SendGrid. (`SendGrid`)

### multitenant

- **Softprime.Framework.Multitenancy** — multitenancy para ASP.NET Core: resolução de tenant (`ITenantResolver`, `MemoryCacheTenantResolver`), contexto (`TenantContext`), extensões de pipeline/DI (`MultitenancyApplicationBuilderExtensions`, `MultitenancyServiceCollectionExtensions`, `UsePerTenantApplicationBuilderExtensions`). (`Microsoft.AspNetCore`, `Microsoft.Extensions.Caching.Memory`)

## Requisitos

- SDK do .NET compatível com os alvos (`netstandard2.0`, `netstandard2.1`, `net48`).
- Visual Studio 2019+ ou `dotnet` CLI.

## Como compilar

A solution principal está em `src/Softprime.Framework.All.sln`.

```bash
# Restaurar e compilar toda a solution
dotnet build src/Softprime.Framework.All.sln

# Compilar em Release
dotnet build src/Softprime.Framework.All.sln -c Release
```

Cada projeto tem `GeneratePackageOnBuild=true`, então a compilação em Release produz os pacotes `.nupkg` de cada biblioteca.

## Exemplos de uso

### Repositório genérico (EF Core)

```csharp
// Em uma entidade de domínio
public class Cliente : Entity   // Entity usa Id do tipo long?
{
    [DomainSignature]
    public virtual string Documento { get; set; }
    public virtual string Nome { get; set; }
}

// Consumindo o repositório via abstração
IRepository<Cliente> repositorio = new EFRepository<Cliente>(dbContext);

var cliente = repositorio.Get(c => c.Documento == "00000000000");
var pagina  = repositorio.Find(c => c.Nome.StartsWith("A"), out int total, index: 0, size: 50);

await repositorio.SaveAsync(new Cliente { Nome = "Novo" });
```

### Web API CRUD

```csharp
// Implemente o contrato padrão para expor um CRUD consistente
public class ClientesController : ControllerBase, IApi<ClienteDto, long>
{
    // Get(), Get(page, size), Get(id), Post(dto), Put(id, dto), Delete(id)
}
```

### Envio de e-mail

```csharp
// ISoftprimeMailer resolve o template e delega para um ISoftprimeMailerDispatcher
// (AWS SES ou SendGrid), de acordo com o provedor configurado.
await mailer.Send("boas-vindas", model, subject: "Bem-vindo");
```

## Convenções

- Namespace raiz: `Softprime.Framework.*`, seguindo o nome do projeto.
- Abstrações (interfaces/contratos) ficam separadas das implementações concretas.
- Entidades de domínio herdam de `Entity` / `EntityWithTypedId<TId>` e usam `[DomainSignature]` para igualdade de negócio.
- Repositórios expõem variantes síncronas e assíncronas; consultas usam `Expression<Func<T, bool>>`.
- Documentação XML (`///`) em português nos membros públicos.

## Licença

Distribuído sob a licença [MIT](src/LICENSE.txt). Copyright (c) 2021 Softprime.
