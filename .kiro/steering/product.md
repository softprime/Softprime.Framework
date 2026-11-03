# Produto

Softprime.Framework é um conjunto de bibliotecas (libs) .NET da Softprime para acelerar o desenvolvimento de aplicações corporativas. Não é uma aplicação executável, e sim um framework distribuído como pacotes NuGet reutilizáveis.

## Propósito

Fornecer abstrações estáveis e implementações plugáveis para as necessidades recorrentes de um sistema de negócio, de forma que o código de aplicação dependa de contratos e não de tecnologias específicas.

## Áreas funcionais

- **general** — funções utilitárias comuns: extensions, helpers, segurança (criptografia, hash, senhas) e validações/annotations.
- **data** — camada de domínio (entidades, value objects) e persistência (repositório + unit of work), com implementações para EF Core 3, EF Core 5, EF6 e NHibernate.
- **api** — abstrações para Web API (contrato CRUD padrão, filtro de API Key), DTOs e cliente HTTP tipado.
- **mail** — envio de e-mail com templating (RazorLight) e provedores plugáveis (AWS SES, SendGrid).
- **multitenant** — multitenancy para ASP.NET Core (resolução de tenant, contexto, extensões de pipeline/DI).

## Público-alvo

Equipes de desenvolvimento da Softprime (e consumidores dos pacotes) que constroem aplicações .NET e querem reaproveitar padrões consolidados de domínio, persistência, API e e-mail.

## Princípios

- Dependa de abstrações, não de implementações concretas (o ORM e o provedor de e-mail são plugáveis).
- Mantenha os pacotes coesos e com responsabilidade única.
- Compatibilidade ampla: alvos `netstandard2.0`/`netstandard2.1` (e `net48` quando aplicável) para servir tanto .NET Framework quanto .NET Core.
