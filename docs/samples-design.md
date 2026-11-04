# Desenho dos Samples — Softprime.Framework

> Documento de **planejamento (somente design)**. Nenhum projeto `samples/` é criado nesta execução. Comandos marcados como **(proposto)** ainda não foram verificados em build/execução.
>
> Data: 2026-11-03 · Base: `main` (commit `86be181`). Alinhado ao [`modernization-plan.md`](./modernization-plan.md).

## Objetivo

Criar uma trilha didática e executável em `samples/` que ensine o uso do Softprime.Framework **apenas por APIs públicas** dos pacotes, sem replicar implementações de produção, sem credenciais reais e sem infraestrutura paga. Os samples **não** são pacotes NuGet e **não** entram no empacotamento/publicação.

## Pré-condição importante (dependência de APIs estáveis)

Os samples exercitam APIs públicas dos pacotes. Pela ordem do `modernization-plan.md`, as APIs/targets só se estabilizam após a Fase 1 (**#3** targets, **#12** versionamento). Portanto:

- **Agora (paralelo à Fase 0):** apenas este design e o padrão didático são aprovados. **Nenhum** projeto de sample é criado.
- **Após #3 + estabilização do pacote correspondente:** implementar samples isolados por componente.
- **Após os isolados + pacotes necessários prontos:** implementar o `08-CompleteSample`.
- **Junto à implementação:** CI/smoke tests (aproveitando **#8**).

## Árvore proposta

```text
Softprime.Framework/
├── src/                          # bibliotecas publicadas (inalterado)
├── tests/                        # testes automatizados (ver #7)
├── samples/                      # exemplos didáticos — NÃO são pacotes NuGet
│   ├── README.md                  # índice, pré-requisitos, comandos, progressão de aprendizado
│   ├── 01-Common/                 # extensions, validação, helpers (sem práticas inseguras)
│   ├── 02-Domain/                 # entidades, value objects, invariantes, igualdade
│   ├── 03-Data-EFCore/            # repository, unit of work, consultas, transações (SQLite local)
│   ├── 04-WebApi/                 # endpoints, DTOs, validação, DI, tratamento de erros
│   ├── 05-ApiClient/              # HTTP client tipado, serialização, erro, cancelamento
│   ├── 06-Mailer/                 # templates + dispatcher fake/local (sem envio real)
│   ├── 07-Multitenancy/           # resolução de tenant, isolamento, cenários negativos
│   ├── 08-CompleteSample/         # app de referência integrada (empresas/usuários/produtos)
│   └── 99-Net48-Consumo/          # (opcional) consumo net48 de lib netstandard2.0 — Windows
└── docs/                         # guias e relatórios (este arquivo, gap-analysis, plan)
```

**Nota:** a numeração reflete uma progressão de aprendizado. `99-Net48-Consumo` fica fora da sequência principal porque é condicional a Windows e só agrega valor se a compatibilidade real for confirmada.

## Contrato didático (todo sample deve seguir)

Cada diretório de sample contém um `README.md` com, nesta ordem:

1. **O que ensina** — objetivo didático em 1–2 frases.
2. **Pré-requisitos** — SDK, pacotes exercitados, SO (quando aplicável).
3. **Comandos exatos** — restaurar, build, executar (Windows PowerShell e Linux bash).
4. **Fluxo de uso** — passo a passo do que o exemplo faz.
5. **Saída esperada** — o que o usuário deve ver.
6. **Testes** — smoke test associado e como rodá-lo.
7. **Troubleshooting** — erros comuns e correções.

Regras de desenho:

- **Somente APIs públicas** dos pacotes; nada de reimplementar o que o pacote já faz.
- **Referência que simule consumo real:** preferir **NuGet local/pré-release** quando viável; `ProjectReference` é aceitável para desenvolvimento **com justificativa**, acompanhado de um teste adicional que consuma o **pacote empacotado** (`.nupkg`).
- **Sem segredos/infra paga:** usar `appsettings.example.json`/`.env.example`, dispatcher de e-mail **fake/local**, e **SQLite local** na app integrada.
- **Demos modernas em `net10.0`**; registrar explicitamente qualquer bloqueio de compatibilidade em vez de inventar API ou alterar biblioteca para "fazer funcionar".
- **Samples fora do pack:** nenhum `samples/**` com `IsPackable` verdadeiro; CI apenas compila e roda smoke tests.

## Padrões transversais

- **Nomenclatura/numeração:** `NN-Area` com dois dígitos; subprojetos `Softprime.Samples.<Area>`.
- **Dados de seed:** fictícios e determinísticos (empresas/usuários/produtos de exemplo), idealmente idempotentes.
- **SDK:** fixar via `global.json` **dentro de `samples/`** (proposto) para reprodutibilidade, sem afetar `src/`.
- **Execução offline:** restore a partir de feed local quando usar NuGet local; SQLite evita dependência de servidor.
- **Documentação de comandos:** sempre em duas variantes (PowerShell `;` e bash `&&`).

## Matriz por sample

> Comandos marcados **(proposto)** — a validar na implementação. "Issue predecessora" indica o que precisa estar estável antes de implementar.

| Sample | Pacote(s) exercitado(s) | Target | Dependências de infra | Comando de execução (proposto) | Resultado esperado | Bloqueios | Critério de aceite | Predecessora |
|---|---|---|---|---|---|---|---|---|
| 01-Common | `Softprime.Framework.Common` | `net10.0` | nenhuma | `dotnet run --project samples/01-Common` | Saídas de extensions/validação no console | — | Build + smoke test verdes; usa só API pública | #3 |
| 02-Domain | `Domain` (+ `DAL`) | `net10.0` | nenhuma | `dotnet run --project samples/02-Domain` | Demonstra igualdade/IsTransient/value objects | — | Idem | #3 |
| 03-Data-EFCore | impl. EF Core + `DAL`/`Domain` | `net10.0` | **SQLite local** | `dotnet run --project samples/03-Data-EFCore` | CRUD + paginação + transação sobre SQLite | Depende da impl. EF Core moderna (#5) | CRUD e paginação demonstrados e testados | #3, #5 |
| 04-WebApi | `Api`, `Api.Dto` | `net10.0` | nenhuma (in-memory/SQLite) | `dotnet run --project samples/04-WebApi` | API sobe; endpoints CRUD; erros 4xx/5xx corretos | Depende da migração ASP.NET Core (#4) | Endpoints + validação + erros demonstrados | #3, #4 |
| 05-ApiClient | `Api.Client` | `net10.0` | a 04-WebApi rodando (ou mock) | `dotnet run --project samples/05-ApiClient` | Chamada tipada, serialização, erro e cancelamento | — | Sucesso + caminho de erro + cancelamento | #3 |
| 06-Mailer | `Mailer` (+ dispatcher **fake**) | `net10.0` | nenhuma (sem envio real) | `dotnet run --project samples/06-Mailer` | Template renderizado; dispatcher fake registra envio | Depende de decisão do Mailer em #6 | Render + dispatch fake demonstrados; **sem** envio real | #3, #6 |
| 07-Multitenancy | `Multitenancy` | `net10.0` | nenhuma | `dotnet run --project samples/07-Multitenancy` | Resolução de tenant + **cenário negativo** (tenant inválido) | Depende da migração (#4) | Resolução + isolamento demonstrados **com teste**; tenant inválido rejeitado | #3, #4 |
| 08-CompleteSample | Domain+DTO+Api+persistência+ApiClient+Mailer fake+Multitenancy | `net10.0` | **SQLite local** | `dotnet run --project samples/08-CompleteSample` | App integrada: cadastro de empresas/usuários/produtos, multi-tenant | Depende de **todos** os pacotes acima estáveis | Fluxo feliz + exemplos negativos (validação, erro, tenant inválido, isolamento **testado**) | #3,#4,#5,#6 |
| 99-Net48-Consumo (opcional) | uma lib `netstandard2.0` (ex.: `Common`) | `net48` | Windows | `dotnet build` / MSBuild (proposto) | Console `net48` consome a lib `netstandard2.0` | **Só** se compat. real confirmada; Windows | Compila e roda em `net48` consumindo `netstandard2.0` | #3 |

## CI / smoke tests (liga-se a #8)

- CI deve **compilar `samples/`** e rodar **smoke tests** (ex.: `dotnet run` com verificação de exit code / saída esperada), garantindo que os exemplos não apodreçam.
- CI **não** publica pacotes dos samples e **não** os inclui no `pack` da solution principal.
- Smoke test da app integrada usa SQLite em arquivo temporário, criado e descartado no job.

## Regra permanente (proposta — registrar em CONTRIBUTING / #13)

> Todo novo componente do framework deve vir com **testes + documentação de APIs + pelo menos um exemplo executável** (ou justificativa aprovada para exceção). Onde registrar: `CONTRIBUTING.md` (a criar) e/ou diretrizes do Kiro — coordenar com **#13** para não conflitar com a atualização de documentação já planejada.

## Pendências / a validar

- Todos os comandos são **(proposto)**; serão confirmados ao implementar cada sample.
- A escolha NuGet local vs. `ProjectReference` depende da política de versionamento (**#12**) e do CI (**#8**).
- `08-CompleteSample` só é viável **após** os pacotes correspondentes estarem estáveis (não antecipar).
- Uso de SQLite depende de a implementação EF Core moderna (**#5**) suportá-lo; se houver bloqueio, registrar e propor alternativa.

## Issues relacionadas (criadas nesta leva de planejamento)

Agrupadas sob a issue-mãe **[#23] [Frente B] Samples e experiência do desenvolvedor** (via sub-issues nativas do GitHub):

| # | Título | Papel |
|---|---|---|
| [#18] | [DX] Definir padrão de samples e documentação | Aprova este desenho (paralelo à Fase 0) |
| [#19] | [Samples] Exemplos isolados por componente (01–07) | Implementação após #3 |
| [#20] | [Samples] Aplicação demonstrativa integrada (08) | Após isolados + pacotes estáveis |
| [#21] | [CI] Compilar e executar smoke tests dos samples | Junto à implementação, estende #8 |
| [#17] | [Research] Comparativo Codout × Softprime | Fonte de componentes candidatos |

[#17]: https://github.com/softprime/Softprime.Framework/issues/17
[#18]: https://github.com/softprime/Softprime.Framework/issues/18
[#19]: https://github.com/softprime/Softprime.Framework/issues/19
[#20]: https://github.com/softprime/Softprime.Framework/issues/20
[#21]: https://github.com/softprime/Softprime.Framework/issues/21

[#23]: https://github.com/softprime/Softprime.Framework/issues/23
