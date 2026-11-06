# Produto

Softprime.Framework é um conjunto de bibliotecas (libs) .NET corporativas da Softprime desenvolvido para acelerar a construção de aplicações corporativas robustas e escaláveis. Não se trata de uma aplicação executável monolítica, mas sim de uma coleção modular de pacotes NuGet reutilizáveis.

## Propósito

Fornecer abstrações consolidadas e implementações plugáveis para as necessidades recorrentes de sistemas de negócio, permitindo que as equipes desenvolvam aplicações desacopladas de fornecedores ou tecnologias de infraestrutura transitórias.

## Áreas Funcionais

- **general** — funções utilitárias essenciais: extensões ricas (strings, datas, coleções, reflexão), helpers, segurança (criptografia simétrica/assimétrica, hash com rehash transparente) e anotações customizadas de validação.
- **data** — camada de domínio (entidades com domain signature, value objects, lifecycle hooks) e abstrações de persistência (`IRepository<T>`, `IUnitOfWork`), com implementações para EF Core moderno (`EFCore10`) e sustentação de linhas legadas (`EF Core 3`, `EF Core 5`, `EF6`, `NHibernate`).
- **api** — contratos e infraestrutura para Web APIs corporativas: padronização CRUD (`IApi<TDto, TId>`), filtros de segurança (API Key), contratos DTO (`IDto`, `PagedResultDto`) e cliente HTTP tipado (`ApiClient`).
- **mail** — motor de templates e despacho de e-mails corporativos desacoplado da engine de renderização, com provedores plugáveis para AWS SES e SendGrid.
- **multitenant** — motor de multitenancy para ASP.NET Core: resolução dinâmica de tenants (estratégias de cabeçalho, rota, subdomínio, cache), isolamento de contexto e middleware de injeção de dependência por tenant.

## Público-alvo

Equipes de engenharia de software da Softprime e sistemas parceiros que:
1. Mantêm sistemas legados corporativos de missão crítica em **.NET Framework 4.8** (`net48`).
2. Operam aplicações corporativas consolidadas em produção sob **.NET 8.0 LTS** (`net8.0`).
3. Desenvolvem novos microsserviços e aplicações modernas sob o alvo de referência **.NET 10.0 LTS** (`net10.0`).

## Princípios Fundamentais

- **Dependa de abstrações, não de implementações:** O código de aplicação deve depender unicamente de contratos (`Softprime.Framework.DAL`, `Softprime.Framework.Domain`, `Softprime.Framework.Mailer`). O ORM, mecanismo de template ou provedor de nuvem são detalhes de implementação intercambiáveis.
- **Estratégia de targets consciente e inclusiva:**
  - Contratos e bibliotecas compartilhadas adotam universalmente **`netstandard2.0`** (preservando `net48` nativo onde já existente), garantindo interoperabilidade entre .NET Framework clássico e .NET moderno.
  - Implementações modernas utilizam multi-targeting **`net10.0;net8.0`** para máxima performance e uso dos shared frameworks modernos (`Microsoft.AspNetCore.App`).
  - **`netstandard2.1` não é utilizado como padrão amplo** devido à sua incompatibilidade estrutural com o .NET Framework 4.8.
- **Governança de versão e coexistência pacífica (SemVer 2.0.0):**
  - A **Linha 2.x** é mantida para sustentação, segurança e correções em `.NET Framework 4.8` sem risco de regressão.
  - A **Linha 3.x** materializa a modernização com runtimes modernos (.NET 8/10 LTS) sem forçar upgrades destrutivos aos sistemas legados.
- **Regra dos Três Pilares de Qualidade:**
  - Todo componente do framework deve vir acompanhado de testes automatizados (`tests/`), governança de superfícies públicas de API com baseline aprovada (`docs/`) e exemplos didáticos executáveis sem segredos externos (`samples/`).
