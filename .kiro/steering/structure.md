# Estrutura do Projeto

## Organização de pastas

A raiz do repositório contém `README.md`, `.gitignore` e a pasta `src/`. Todo o código e a solution ficam em `src/`.

```
src/
├── Softprime.Framework.All.sln   # solution que agrupa todos os projetos
├── LICENSE.txt                   # MIT
├── general/
│   └── Softprime.Framework.Common/      # Extensions, Helpers, Security, Annotations, Validations
├── data/
│   ├── Softprime.Framework.Domain/      # Entity, EntityWithTypedId<TId>, ValueObject, BaseObject, ValidatableObject
│   ├── Softprime.Framework.DAL/         # IEntity, IRepository<T>, IUnitOfWork, ITenant (abstrações)
│   ├── Softprime.Framework.EFCore3/     # implementação EF Core 3
│   ├── Softprime.Framework.EFCore5/     # implementação EF Core 5
│   ├── Softprime.Framework.EF6/         # implementação EF6
│   └── Softprime.Framework.NH/          # implementação NHibernate
├── api/
│   ├── Softprime.Framework.Api.Dto/     # IDto, IPagedResult, DtoBase, PagedResultDto<TDto>
│   ├── Softprime.Framework.Api/         # IApi<TDto, TId>, ApiKeyAuthorizeFilter
│   └── Softprime.Framework.Api.Client/  # ApiClientBase, ApiClient, ApiClientTyped, ApiException
├── mail/
│   ├── Softprime.Framework.Mailer/          # ISoftprimeMailer, ISoftprimeMailerDispatcher, templating
│   ├── Softprime.Framework.Mailer.AWS/      # dispatcher Amazon SES
│   └── Softprime.Framework.Mailer.SendGrid/ # dispatcher SendGrid
└── multitenant/
    └── Softprime.Framework.Multitenancy/    # resolução de tenant, contexto, extensões ASP.NET Core
```

As pastas `general`, `data`, `api`, `mail` e `multitenant` também são pastas de solução (solution folders) no `.sln`.

## Convenções de nomenclatura

- Projetos e namespaces seguem o prefixo `Softprime.Framework.*`, com o namespace raiz igual ao nome do projeto.
- Subnamespaces espelham as subpastas (ex.: `Softprime.Framework.Common.Security`, `Softprime.Framework.Domain.DAL`).
- Interfaces usam prefixo `I` (ex.: `IRepository<T>`, `IApi<TDto, TId>`, `ISoftprimeMailer`).
- Cada projeto inclui `logo-nuget.png` e referencia o `LICENSE.txt` da raiz de `src/`.

## Padrões de arquitetura

- **Abstração + implementações plugáveis:** contratos ficam em projetos de abstração (`DAL`, `Mailer`, `Api.Dto`); cada tecnologia concreta tem seu próprio projeto que implementa esses contratos. Trocar o ORM ou provedor de e-mail não deve exigir mudança no código de negócio.
- **Camada de dados:** `Domain` referencia `DAL`; as implementações (`EFCore3`, `EFCore5`, `EF6`, `NH`) referenciam `DAL` e fornecem `IRepository<T>`/`IUnitOfWork` concretos.
  - Entidades herdam de `Entity` (Id `long?`) ou `EntityWithTypedId<TId>`, com igualdade por identidade e `[DomainSignature]` para igualdade de negócio, além de hooks `BeforeSave`/`AfterSave`/`BeforeDelete`/`AfterDelete`.
  - `IRepository<T>` expõe variantes síncronas e assíncronas; consultas usam `Expression<Func<T, bool>>` e há sobrecarga de `Find` com paginação (`out int total`, `index`, `size`).
- **API:** `IApi<TDto, TId>` define o contrato CRUD padrão (`Get`, `Get(page,size)`, `Get(id)`, `Post`, `Put`, `Delete`) retornando `IActionResult`. DTOs implementam `IDto<TId>`.

## Onde adicionar código novo

- Nova função utilitária geral → `general/Softprime.Framework.Common/` (na subpasta adequada: Extensions, Helpers, Security, Annotations).
- Novo contrato de persistência → `data/Softprime.Framework.DAL/`; a implementação correspondente, no projeto do ORM-alvo.
- Novo provedor de e-mail → novo projeto `mail/Softprime.Framework.Mailer.<Provedor>/` implementando `ISoftprimeMailerDispatcher`.
- Sempre registre o novo projeto no `Softprime.Framework.All.sln`, na pasta de solução correta.

## Idioma

Descrições de pacote e comentários de documentação XML (`///`) são escritos em português. Mantenha esse padrão.
