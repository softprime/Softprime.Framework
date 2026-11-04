# Estratégia de Baseline de APIs Públicas e Compatibilidade — Softprime.Framework

> Documento de apoio ao [Plano de Modernização](modernization-plan.md) · **Entregável da Issue [#11]** (Fase 0 — Conhecer e proteger).
>
> Data de elaboração: 2026-11-04 · Branch: `issue/11-baseline-apis-publicas-compatibilidade` · Base: `main`

---

## 1. Contexto e Objetivo

A auditoria factual do `Softprime.Framework` confirmou que a solution é composta por **14 bibliotecas de produção** gerando pacotes NuGet (`Version=2.0.0`), sem nenhuma suíte de testes automatizados nem baseline formal de superfícies públicas de API.

Nas fases seguintes de modernização (migração de targets para .NET 10, modernização do ASP.NET Core 2.2 e dos provedores de Entity Framework), qualquer alteração acidental de tipos, assinaturas, contratos ou comportamento interno acarreta risco severo de introduzir **breaking changes silenciosas** nos produtos e aplicações corporativas que consomem o framework.

O objetivo deste documento é **estabelecer a especificação de baseline de APIs públicas e de compatibilidade binária e comportamental**, definindo:
1. O que capturar e como comparar a superfície pública dos pacotes, adotando a **abordagem híbrida** de validação.
2. Critérios objetivos para detecção e classificação de breaking changes (binárias, de compilação e comportamentais).
3. O conjunto mínimo de testes planejado por target (`net48`, `net10.0` e `net8.0`).
4. O plano de homologação com consumidores externos através de aplicações-piloto.

> [!IMPORTANT]
> **Natureza de Planejamento (Fase 0):** Em estrita conformidade com os critérios de aceite da Fase 0 e da issue [#11], este documento é **exclusivamente de planejamento e especificação**. **Nenhuma ferramenta de captura de API é executada nesta rodada**, **nenhum código ou teste é implementado**, e **nenhuma cobertura de testes é declarada atingida**. A execução ocorrerá no início da fase de desenvolvimento.

---

## 2. Especificação da Baseline de APIs (O que Capturar e Como Comparar)

### 2.1. Escopo de Captura da Superfície Pública

A baseline cobrirá todos os 14 pacotes da solution, com prioridade máxima nos **Projetos de Contrato e Abstração** que definem as interfaces entre o framework e as aplicações consumidoras:

| Projeto | Papel | Elementos Críticos da Superfície Pública |
|---|---|---|
| `Softprime.Framework.Common` | Utilitário geral e segurança | Classes estáticas de extensão (`Extensions/*`), utilitários de reflexão, tipos criptográficos em `Common.Security` (`Crypto`, `SimpleHash`, `RandomPassword`). |
| `Softprime.Framework.Domain` | Contratos de Domínio | `EntityWithTypedId<TId>`, `IAggregateRoot`, interfaces de entidade, contratos de validação, hooks de ciclo de vida (`BeforeSave`, `AfterSave`). |
| `Softprime.Framework.DAL` | Contratos de Persistência | Interfaces centrais `IRepository<T>`, `IUnitOfWork`, `IDataContext`, assinaturas de consultas paginadas e operações CRUD. |
| `Softprime.Framework.Api.Dto` | Contratos de Dados (DTO) | DTOs de transporte, paginação e respostas padronizadas da API; propriedades públicas e tipos de serialização. |
| `Softprime.Framework.Api` | Integração ASP.NET Core | Controllers base, action filters, middleware de exceção e pipeline HTTP. |
| `Softprime.Framework.Api.Client` | Cliente HTTP | Clientes HTTP tipados, interfaces de chamada remota e métodos de integração REST. |
| `Softprime.Framework.Multitenancy` | Resolução Multitenant | Contratos de identificação de tenant (`ITenant`, `ITenantContext`), middleware de resolução e extensões de injeção de dependência. |
| `Softprime.Framework.Mailer` (+ AWS / SendGrid) | Notificações e E-mail | Interfaces de despacho de mensagens, contratos de templates, configurações de envio e adaptadores de infraestrutura. |
| `EFCore3`, `EFCore5`, `EF6`, `NH` | Implementações de ORM | Classes de repositório concreto (`EFRepository<T>`, `NHRepository<T>`), contexto de dados e extensões de mapeamento. |

### 2.2. Elementos que Compõem a Superfície Pública de Cada Biblioteca

Para cada assembly compilado, os seguintes elementos devem ser capturados e fixados na baseline:
- **Tipos expostos:** Classes, interfaces, structs, enums e delegates com visibilidade `public` ou `protected` (em classes não-seladas). Tipos `internal` e `private` são excluídos da baseline.
- **Identidade e Estrutura dos Tipos:** Namespace, nome do tipo, classe base herdada e todas as interfaces públicas implementadas.
- **Membros:** Métodos, propriedades (getters/setters), construtores, campos públicos/constantes e eventos.
- **Modificadores:** `abstract`, `virtual`, `sealed`, `static`, `readonly`, `async`.
- **Assinaturas de Métodos/Construtores:** Tipos e nomes de parâmetros, modificadores (`ref`, `out`, `in`, `params`), valores padrão de parâmetros opcionais e tipo de retorno.
- **Tipos Genéricos:** Parâmetros de tipo genérico e todas as suas restrições (`where T : class`, `where T : struct`, `where T : new()`, interfaces bases).
- **Atributos de Contrato:** Atributos públicos que afetam serialização ou validação (ex.: anotações de validação de modelo, atributos JSON/XML).

---

### 2.3. Abordagem Híbrida Aprovada para Validação de API

A estratégia de validação adota uma **arquitetura híbrida em duas camadas complementares**, combinando governança declarativa de código no Git com validação binária de pacotes no empacotamento:

```
┌────────────────────────────────────────────────────────────────────────┐
│               Arquitetura Híbrida de Baseline e Proteção               │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ CAMADA 1: Governança no Código-Fonte e no Git                          │
│ Ferramenta: Microsoft.CodeAnalysis.PublicApiAnalyzers (Roslyn)         │
│ • Arquivos texto na pasta de cada projeto:                             │
│   - PublicAPI.Shipped.txt (APIs congeladas da versão 2.0.0)            │
│   - PublicAPI.Unshipped.txt (novas APIs introduzidas)                  │
│ • Feedback em tempo real durante dotnet build no ambiente local        │
│ • Visibilidade total e transparente no diff de Pull Requests no GitHub │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ CAMADA 2: Validação Binária de Pacotes no MSBuild / CI                 │
│ Ferramenta: EnablePackageValidation do .NET SDK                        │
│ • Configuração centralizada em Directory.Build.props                   │
│ • PackageValidationBaselineVersion = 2.0.0                             │
│ • Executada durante dotnet pack na esteira de CI                       │
│ • Compara o .nupkg gerado contra o pacote publicado no feed NuGet      │
│ • Garante compatibilidade estrita entre fatias (net48 vs net10.0)      │
│ • Prática auditada e validada no Codout.Framework (achado #9)          │
└────────────────────────────────────────────────────────────────────────┘
```

#### Detalhamento da Camada 1: `Microsoft.CodeAnalysis.PublicApiAnalyzers`
- Integrada ao compilador Roslyn sem necessidade de ferramentas externas em tempo de build.
- **Estrutura por projeto:**
  - `PublicAPI.Shipped.txt`: Lista exata de todos os símbolos públicos da versão estável `2.0.0`.
  - `PublicAPI.Unshipped.txt`: Registra novas APIs adicionadas no ciclo de desenvolvimento em andamento.
- **Comportamento em tempo de compilação:**
  - Adição de novo membro público sem registro gera aviso/erro `RS0016` (resolvível via *Quick Action* `Ctrl + .` no IDE).
  - Remoção ou alteração de membro já publicado em `PublicAPI.Shipped.txt` gera erro `RS0017`, bloqueando compilações acidentais que quebrem compatibilidade.
- **Benefício no Code Review:** Qualquer alteração de contrato torna-se uma linha adicionada ou removida diretamente no diff do Git no Pull Request, eliminando quebras silenciosas.

#### Detalhamento da Camada 2: `EnablePackageValidation` (MSBuild / CI)
- Recurso nativo do SDK do .NET (aprovado na análise do `Codout.Framework` em [docs/codout-gap-analysis.md](codout-gap-analysis.md)).
- Disparado automaticamente no `dotnet pack`.
- Compara a montagem e as referências binárias do novo pacote contra o pacote `2.0.0` do feed de baseline (`PackageValidationBaselineVersion`).
- Valida multi-targeting: garante que membros públicos presentes no target `netstandard2.0` ou `net48` não sejam acidentalmente omitidos no target `net10.0`.

---

## 3. Critérios Objetivos de Detecção de Breaking Changes

As alterações devem ser categorizadas em três classes distintas, cada uma com critérios objetivos de classificação:

### 3.1. Quebra Binária (Binary Breaking Change)

> **Definição:** Ocorre quando uma aplicação consumidora previamente compilada contra a versão anterior falha ao carregar ou executar com a nova versão (`TypeLoadException`, `MissingMethodException`, `MissingFieldException`), **sem ser recompilada**.

São consideradas quebras binárias imediatas:
- **Remoção ou renomeação:**
  - Remoção ou renomeação de qualquer tipo público ou protegido.
  - Remoção ou renomeação de métodos, propriedades, campos constantes ou eventos públicos/protegidos.
  - Alteração no namespace de qualquer tipo exposto.
- **Alteração de assinaturas:**
  - Alteração no tipo de retorno de método ou propriedade.
  - Alteração no tipo, ordem ou quantidade de parâmetros de métodos ou construtores existentes.
  - Alteração de modificadores de parâmetro (adicionar, alterar ou remover `ref`, `out`, `in`).
  - **Adição de novos parâmetros a métodos ou construtores existentes, mesmo com valor padrão (optional parameter):** No compilador C#, valores padrão de parâmetros opcionais são embutidos no binário do chamador no momento da compilação. Um binário previamente compilado continuará invocando a assinatura antiga sem o parâmetro e falhará com `MissingMethodException`. A regra exige preservar a assinatura antiga como sobrecarga chamando o novo método.
- **Modificadores e herança:**
  - Adição do modificador `sealed` a uma classe pública previamente extensível.
  - Alteração de um membro de `virtual` para `non-virtual`.
  - Adição de membros abstratos a classes não-seladas.
  - **Adição de membros a interfaces públicas:** Em bibliotecas que atendem `.NET Framework 4.8` ou `.NET Standard 2.0`, a adição de novos métodos a interfaces públicas quebra imediatamente os consumidores que implementam essas interfaces (nestes runtimes legados não há suporte a *Default Interface Methods*).
  - Alteração ou endurecimento de restrições genéricas (ex.: adicionar `where T : struct`).
  - Redução de visibilidade de membros (ex.: `public` → `internal` ou `protected` → `private`).

### 3.2. Quebra de Código-Fonte / Compilação (Source / Compile Breaking Change)

> **Definição:** Ocorre quando o código-fonte de uma aplicação consumidora, ao ser recompilado contra a nova versão do framework, falha na compilação.

São consideradas quebras de compilação:
- **Ambiguidade de sobrecargas:** Introdução de novas sobrecargas de método que tornam chamadas existentes de consumidores ambíguas para a resolução de sobrecargas do compilador C#.
- **Renomeação de parâmetros:** Renomear parâmetros em métodos públicos, o que quebra consumidores que utilizam invocação com argumentos nomeados (ex.: `repository.Find(filter: x, pageSize: 10)`).
- **Alteração de tipo para tipos menos específicos:** Mudar tipos de retorno para interfaces mais restritivas ou tipos base que exijam novos casts no código consumidor.

### 3.3. Quebra Comportamental (Behavioral Breaking Change)

> **Definição:** O código compila e linka sem erros binários, mas a semântica, o estado resultante ou os efeitos colaterais da operação divergem do comportamento esperado em tempo de execução.

No contexto específico do `Softprime.Framework`, foram alinhados **três pontos críticos de atenção prioritária**:

| Componente | Comportamento Atual Verificado | Risco de Quebra Comportamental | Regra de Proteção Obrigatória |
|---|---|---|---|
| **`EntityWithTypedId<TId>`** | Igualdade referencial para transientes (`Id == default`); comparação por valor de `Id` para persistidos; cache estrito de `GetHashCode()`. | Alterar o cálculo de hash ou a lógica de `Equals` corrompe coleções baseadas em hash (`HashSet<T>`, `Dictionary<TKey, TValue>`) em aplicações ativas em memória. | O cálculo de hash deve permanecer estável mesmo após a persistência da entidade (`Id` alterado de 0 para valor gerado). Testes de invariante de hash são obrigatórios antes de qualquer migração. |
| **`IRepository<T>.Find(...)`** | Paginação com parâmetro `out total`: calcula `total` como a contagem de registros da página atual, e não o total absoluto da tabela no banco. | Mudar silenciosamente o cálculo de `total` para a contagem total quebrará componentes de interface e regras de paginação já construídos nas aplicações consumidoras para contornar essa particularidade. | Preservar conscientemente a semântica atual de `out total` durante toda a modernização estrutural. Qualquer correção futura deve ser tratada como alteração funcional formal e separada. |
| **`Common.Security`** | Criptografia simétrica (`Crypto`) e algoritmos de hash (`SimpleHash`) com implementações e encodings legados. | Alterar algoritmo, salt, vetor de inicialização ou encoding torna dados e senhas já armazenados em bancos de dados de produção completamente ilegíveis ou inválidos. | Criar *golden tests* com vetores de teste estáticos garantindo que textos cifrados na versão 2.0.0 continuem sendo decifrados com sucesso. Modernização de hash deve usar modelo incremental (rehash no login), nunca quebra direta. |

Outros comportamentos a proteger:
- **Hooks de Ciclo de Vida:** Ordem e momento de disparo dos métodos `BeforeSave`, `AfterSave`, etc., nas entidades de domínio e repositórios.
- **Tipos de Exceção Lançados:** Preservar a hierarquia e o tipo de exceção disparado por validações do framework para não quebrar blocos `catch` específicos nas aplicações clientes.
- **Tradução SQL de ORMs:** Monitorar consultas relacionais em `EFCore3`, `EFCore5`, `EF6` e `NH` para assegurar que atualizações de SDK não alterem a semântica de tradução LINQ nem gerem avaliação no cliente indesejada.

---

## 4. Conjunto Mínimo de Testes Planejado por Target

Para blindar o comportamento contra quebras antes das migrações, é especificada a matriz de testes mínimos de caracterização, em total consonância com a [Estratégia de Testes (#7)](test-strategy.md):

### 4.1. Priorização dos Testes de Caracterização

```
┌────────────────────────────────────────────────────────────────────────┐
│               Matriz de Prioridade dos Testes Mínimos                  │
└────────────────────────────────────────────────────────────────────────┘
  Etapa 1 (P0 - Crítica):   Entidades, Identidade, Criptografia e CRUD Básico
  Etapa 2 (P1 - Alta):      Mapeamentos de ORMs e Consultas Relacionais
  Etapa 3 (P2 - Média):     DTOs, Serialização HTTP e Envio de E-mails
```

1. **Etapa 1 (P0 - Crítica — Contratos Fundamentais):**
   - **`EntityWithTypedId<TId>`:**
     - Igualdade entre instâncias transientes (duas instâncias com `Id == 0` não são iguais entre si, exceto se forem a mesma referência em memória).
     - Igualdade entre instâncias persistidas (duas instâncias distintas com mesmo `Id` e mesmo tipo real são consideradas iguais).
     - Imutabilidade do valor retornado por `GetHashCode()` após persistência (o hash calculado quando transiente deve ser mantido em cache).
     - Comportamento booleano exato de `IsTransient()`.
   - **`Common.Security` (Golden Tests de Preservação):**
     - Vetores de teste fixos com entradas conhecidas e saídas cifradas legadas salvas como *golden masters*.
     - Validação de que `Crypto.Encrypt` / `Crypto.Decrypt` e `SimpleHash` continuam decifrando e validando payloads gerados na versão 2.0.0.
   - **`IRepository<T>` (Contratos de Persistência):**
     - Operações básicas de `GetById`, `Save`, `Update`, `Delete`.
     - Preservação da semântica atual de `Find(..., out total, index, size)`.
2. **Etapa 2 (P1 - Alta — Provedores de Persistência):**
   - Mapeamentos de entidades e execução de consultas relacionais em cada implementação de ORM: `EFCore3`, `EFCore5`, `EF6` e `NH`.
   - Execução contra bancos de dados de integração (SQL Server e Oracle, conforme inventário de consumidores).
3. **Etapa 3 (P2 - Média — Serviços e Transporte):**
   - Serialização e compatibilidade de contratos DTO em `Api.Dto`.
   - Clientes HTTP em `Api.Client` com `HttpMessageHandler` mockado.
   - Dispatchers de e-mail em `Mailer` (orquestração e renderização de templates).

### 4.2. Matriz de Targets de Execução e Smoke Tests

| Target de Teste | Papel no Framework | Ambiente de Execução | Escopo de Validação |
|---|---|---|---|
| **`net48`** | Consumidores legados corporativos (.NET Framework 4.8) | Runner Windows | Valida projetos com multi-targeting explícito a `net48` e consumo de fatias `netstandard2.0`. Execução de smoke tests consumindo o `.nupkg` real. |
| **`net10.0`** | Plataforma alvo moderna (LTS de referência) | Runner Linux / Windows | Valida todas as bibliotecas sob o runtime moderno .NET 10, garantindo compatibilidade futura e ausência de APIs obsoletas bloqueantes. |
| **`net8.0`** | Plataforma moderna atual (LTS intermediária) | Runner Linux / Windows | Smoke tests de consumo quando houver aplicações consumidoras corporativas em .NET 8. |

> [!NOTE]
> **Ausência de Declaração de Cobertura:** A Fase 0 define os cenários mínimos necessários para garantir paridade. Nenhuma métrica ou porcentagem de cobertura de código é declarada nesta etapa, visto que os projetos de teste serão criados no início da Fase de Desenvolvimento.

---

## 5. Plano de Homologação com Consumidores

Dado que o [Inventário de Pacotes e Consumidores (#10)](package-inventory.md) identificou que as aplicações consumidoras corporativas da Softprime residem em repositórios externos fora desta solution, foi aprovado o fluxo estruturado de validação com **duas aplicações-piloto** antes de qualquer publicação definitiva:

```
┌────────────────────────────────────────────────────────────────────────┐
│                   Fluxo de Homologação com Consumidores                │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 1. Seleção de Duas Aplicações-Piloto Representativas                   │
│    • Piloto Legado: Aplicação corporativa em .NET Framework 4.8        │
│      (consumindo Domain, DAL, EF6 ou NH)                               │
│    • Piloto Moderno: Aplicação em .NET Core / .NET 8+                  │
│      (consumindo Common, Api, Multitenancy ou EFCore)                  │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 2. Publicação de Pacotes Prerelease em Feed Isolado                    │
│    • Empacotamento com sufixo semver de pré-lançamento (ex: -rc.1)     │
│    • Publicação restrita a feed NuGet corporativo de homologação       │
│    • Nenhum pacote prerelease é publicado em feeds públicos           │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 3. Smoke Test de Consumo Real nas Aplicações-Piloto                    │
│    • Atualização dos .csproj das aplicações para a versão prerelease   │
│    • Compilação completa das soluções consumidoras                     │
│    • Execução dos testes automatizados e fluxos críticos de negócio    │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 4. Gate de Homologação e Aprovação Manual (Sign-off)                   │
│    • Avaliação técnica dos mantenedores do framework e donos de app    │
│    • Aprovação formal exigida antes de qualquer publicação definitiva  │
└────────────────────────────────────────────────────────────────────────┘
```

### Fases Detalhadas do Plano de Homologação

1. **Fase A — Seleção das Aplicações-Piloto:**
   - **Piloto Legado:** Uma aplicação corporativa rodando em `.NET Framework 4.8` que consuma `Domain`, `DAL` e um dos ORMs legados (`EF6` ou `NH`). Esse piloto valida a compatibilidade de runtime com o ecossistema clássico.
   - **Piloto Moderno:** Uma aplicação rodando em `.NET Core / .NET 8+` que consuma `Common`, `Api`, `Multitenancy` ou `EFCore`. Esse piloto valida a experiência de desenvolvimento moderno e a ausência de incompatibilidades de runtime.
2. **Fase B — Empacotamento em Feed Isolado de Homologação:**
   - Gerar pacotes NuGet de pré-lançamento com versionamento SemVer explícito (ex.: `2.1.0-preview.1` ou `3.0.0-rc.1`).
   - Publicar exclusivamente em feed NuGet interno de homologação da Softprime, mantendo feeds de produção inalterados.
3. **Fase C — Testes de Consumo e Regressão nos Pilotos:**
   - Atualizar a referência dos pacotes para a versão pré-lançamento em branch dedicada de teste da aplicação consumidora.
   - Executar compilação completa para verificar ausência de quebras de compilação.
   - Executar a suíte de testes existente da aplicação consumidora.
   - Realizar validação funcional (*smoke test*) das rotinas críticas (autenticação, paginação em telas, persistência e envio de e-mails).
4. **Fase D — Gate de Homologação e Aprovação Manual:**
   - O avanço para publicação definitiva no feed NuGet corporativo exige aprovação manual formal registrada pelos mantenedores do framework e pelos responsáveis técnicos das aplicações piloto.
5. **Plano de Rollback:**
   - Caso qualquer regressão binária ou comportamental seja identificada durante a homologação, o pacote pré-lançamento é descontinuado (*unlisted*) no feed interno, e a issue de modernização correspondente é reaberta com a evidência da falha para correção antes de um novo ciclo.

---

<!-- Links -->
[#7]: https://github.com/softprime/Softprime.Framework/issues/7
[#10]: https://github.com/softprime/Softprime.Framework/issues/10
[#11]: https://github.com/softprime/Softprime.Framework/issues/11
