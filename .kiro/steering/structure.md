# Estrutura do Repositório

## Organização de Pastas

A raiz do repositório organiza o ciclo de vida completo do framework em quatro diretórios principais, além dos arquivos normativos de governança:

```
Softprime.Framework/
├── src/                          # Código-fonte das bibliotecas de produção
│   ├── Softprime.Framework.All.sln # Solution principal com todos os projetos
│   ├── LICENSE.txt               # Licença MIT do framework
│   ├── Directory.Build.props     # Metadados e propriedades comuns do NuGet (#9)
│   ├── general/
│   │   └── Softprime.Framework.Common/
│   ├── data/
│   │   ├── Softprime.Framework.Domain/
│   │   ├── Softprime.Framework.DAL/
│   │   ├── Softprime.Framework.EFCore10/ # Novo ORM moderno (net10.0;net8.0) (#5)
│   │   ├── Softprime.Framework.EFCore3/  # Legado mantido
│   │   ├── Softprime.Framework.EFCore5/  # Legado mantido
│   │   ├── Softprime.Framework.EF6/      # Legado mantido
│   │   └── Softprime.Framework.NH/       # Legado mantido
│   ├── api/
│   │   ├── Softprime.Framework.Api.Dto/
│   │   ├── Softprime.Framework.Api/
│   │   └── Softprime.Framework.Api.Client/
│   ├── mail/
│   │   ├── Softprime.Framework.Mailer/
│   │   ├── Softprime.Framework.Mailer.AWS/
│   │   └── Softprime.Framework.Mailer.SendGrid/
│   └── multitenant/
│       └── Softprime.Framework.Multitenancy/
├── tests/                        # Projetos de testes automatizados (#7)
│   ├── Softprime.Framework.Common.Tests/
│   ├── Softprime.Framework.Domain.Tests/
│   └── ...
├── samples/                      # Trilha didática de exemplos executáveis (#18, #23)
│   ├── 01-CommonSample/
│   ├── 02-DomainSample/
│   ├── 03-DataEFCoreSample/
│   ├── ...
│   └── 08-CompleteSample/
├── docs/                         # Documentação arquitetural, auditorias e políticas
│   ├── modernization-plan.md
│   ├── target-compatibility-matrix.md
│   ├── nuget-versioning-policy.md
│   ├── ci-pipeline-spec.md
│   ├── test-strategy.md
│   ├── samples-design.md
│   └── ...
├── CONTRIBUTING.md               # Guia de contribuição e governança de qualidade
└── README.md                     # Visão geral pública do framework
```

---

## Convenções de Nomenclatura

- **Namespaces e Projetos:** Prefixo `Softprime.Framework.*`, onde o namespace raiz corresponde exatamente ao nome do projeto.
- **Subnamespaces:** Espelham a hierarquia de pastas físicas (ex.: `Softprime.Framework.Common.Security`, `Softprime.Framework.Common.Extensions`).
- **Interfaces e Contratos:** Prefixo `I` (ex.: `IRepository<T>`, `IUnitOfWork`, `IApi<TDto, TId>`, `ISoftprimeMailer`).
- **Projetos de Teste:** Sufixo `.Tests` espelhando o nome do projeto testado em `tests/` (ex.: `Softprime.Framework.Domain.Tests`).
- **Samples:** Numeração sequencial em `samples/` com prefixo de dois dígitos (ex.: `01-CommonSample`).

---

## Padrões de Arquitetura

1. **Abstração Desacoplada e Implementações Plugáveis:**
   - As interfaces e contratos fundamentais residem nos projetos de contrato (`DAL`, `Domain`, `Api.Dto`, `Mailer`).
   - Tecnologias de terceiros residem exclusivamente em bibliotecas de implementação (`EFCore10`, `EF6`, `NH`, `Mailer.AWS`, `Mailer.SendGrid`).
   - O código de negócio nunca deve depender de assemblies de terceiros ou de ORMs concretos.
2. **Camada de Dados e Persistência:**
   - `Domain` define a semântica de entidades (`Entity`, `EntityWithTypedId<TId>`), objetos de valor (`ValueObject`), lifecycle hooks (`BeforeSave`, `AfterSave`, `BeforeDelete`, `AfterDelete`) e identidade de negócio via `[DomainSignature]`.
   - `DAL` define as abstrações de repositório (`IRepository<T>`) síncronas e assíncronas com paginação idiomática (`out int total`), e unidade de trabalho (`IUnitOfWork`).
   - Implementações de dados suportam tanto a linha moderna (`EFCore10`) quanto as implementações legadas congeladas (`EF6`, `EFCore3`, `EFCore5`, `NH`).
3. **Web APIs e Multitenancy:**
   - `IApi<TDto, TId>` unifica os contratos REST com respostas padronizadas e mapeamento de DTOs (`IDto`).
   - `Multitenancy` atua no pipeline HTTP do ASP.NET Core resolvendo a identidade do locatário e isolando o escopo de injeção de dependência.
4. **Envio de E-mail:**
   - `ISoftprimeMailer` gerencia o ciclo de renderização e delega o despacho para `ISoftprimeMailerDispatcher`, desacoplando o mecanismo de template dos clientes de transporte (AWS SES, SendGrid).

---

## Onde Adicionar Código Novo

### A Regra dos Três Pilares
Ao submeter qualquer novo componente ou biblioteca em `src/`, é **obrigatório** atender simultaneamente aos três pilares:
1. **Código e Projeto em `src/`:** Registrar o projeto na pasta de solução correspondente em `src/Softprime.Framework.All.sln`.
2. **Testes em `tests/`:** Criar o projeto de testes espelho (`tests/<Projeto>.Tests`) cobrindo cenários positivos, negativos e regressão.
3. **Docs e Baseline em `docs/`:** Registrar a especificação técnica e aprovar o baseline de APIs públicas via `PublicApiAnalyzers`.
4. **Exemplo Didático em `samples/`:** Criar ou atualizar um exemplo funcional, autossuficiente e sem dependências pagas externas.

### Diretrizes de Destino e Targets
- **Nova função utilitária / helper geral:** Adicionar em `general/Softprime.Framework.Common/` sob o subnamespace pertinente (Extensions, Helpers, Security, Annotations). Alvo: `netstandard2.0` universal.
- **Novo contrato de persistência:** Adicionar em `data/Softprime.Framework.DAL/` e implementar no respectivo ORM. Alvo: `netstandard2.0` universal (+ `net48` nativo).
- **Novo componente web ou persistência moderna:** Adicionar em `api/` ou `data/`. Alvo: multi-targeting **`net10.0;net8.0`** (nunca usar `netstandard2.1`).
- **Novo provedor de e-mail:** Criar projeto `mail/Softprime.Framework.Mailer.<Provedor>/` implementando `ISoftprimeMailerDispatcher`.

---

## Idioma e Documentação

- Descrições de pacotes NuGet, mensagens de exceção e comentários de documentação XML (`///`) em tipos e membros públicos devem ser redigidos em **português**.
- Mensagens de commit seguem o padrão Conventional Commits em português ou inglês minúsculo padronizado.
