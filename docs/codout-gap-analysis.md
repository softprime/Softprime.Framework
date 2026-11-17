# Análise comparativa — Codout.Framework × Softprime.Framework

> Pesquisa documental da issue [#22]. Revisão: 2026-11-17.
> Recomendações técnicas para aprovação; nenhuma adoção ou implementação está aprovada por este documento. Nenhum código foi copiado ou executado.

## Referências e método

A comparação usa o Codout no commit [`0e746b50697693228bbd36926be3cc8d2d1f457c`][codout] e a `upstream/main` do Softprime no commit [`f732c52`][softprime]. Links de código abaixo são permanentes. Versões mencionadas são declarações nos projetos, não comprovação de publicação no NuGet nem recomendação de atualização automática.

Foram inspecionados árvore, projetos, contratos, implementações selecionadas, testes e histórico público. A existência de testes não demonstra cobertura, correção ou CI verde: não executamos o Codout. Defeitos registrados pelo próprio projeto são identificados como relatos; observações de leitura não são reproduções de falhas.

## O que vale aproveitar primeiro

1. **Validação de pacotes e testes de caracterização:** práticas úteis para detectar quebras antes da modernização. Já pertencem a #11/#8 e #7; não exigem novos componentes.
2. **Separar renderização de templates do envio de e-mail:** referência de arquitetura para #6, preservando os contratos e os consumidores legados do Mailer.
3. **Resultado explícito de verificação de senha que indique necessidade de rehash:** referência para #6. O consumidor ainda precisa persistir a atualização; a interface não migra automaticamente hashes do Softprime.
4. **Storage e filtros de grids:** candidatos com benefício plausível, mas sem demanda confirmada. Primeiro identificar aplicação consumidora e avaliar um contrato menor; não abrir implementação agora.
5. **Application, Mongo, Image e MCP:** manter fora da implementação deste ciclo. Há custo de integração, dependências e semânticas que não se justificam apenas pela existência no Codout.

## Fatos verificados e limites

- O [props central][props] define `net10.0`, metadados, analisadores e SourceLink. Há exceções: [Softprime.Multitenancy][tenant-project] declara `netstandard2.0`. Não se deve generalizar o target para todos os projetos.
- [DynamicLinq][dynamic-project], [Storage][storage-project], [Mongo][mongo-project] e [Security.Core][security-project], entre os projetos examinados, habilitam Package Validation e declaram baseline. O [MCP][mcp-project] tem versão/ciclo próprios e não declara essas propriedades; removida a generalização anterior de que todos os pacotes as usam.
- O [workflow de PR/push][ci] contém restore, build, testes e um gate configurado de cobertura. A configuração não comprova o resultado de nenhuma execução. O runner Ubuntu e o percentual do Codout não são escolhas automaticamente aplicáveis ao Softprime Windows/net48.
- A [árvore fixada][tree] contém projetos de teste em `tests/`, governança e MCP com projeto de teste próprio; não contém diretório raiz `samples/`. Isso só descreve esta revisão, sem provar autoria ou originalidade de exemplos futuros.
- O [índice de findings][findings] registra defeitos preexistentes em Security, Mongo, DynamicLinq, Application, Mailer, Multitenancy e outros componentes. Esses relatos são motivo para investigação e testes próprios, não para concluir que qualquer pacote é seguro ou pronto para adoção.

## Matriz de recomendações por componente

Prioridade indica ordem de **avaliação**, não compromisso de entrega. Esforço estimado: P = até 2 dias; M = 3–5 dias; G = 1–2 semanas; GG = mais de 2 semanas, para uma primeira adaptação limitada com testes/documentação, após demanda e desenho aprovados. São estimativas sem protótipo; migração de consumidores e homologação podem ampliá-las.

| Componente / evidência | Equivalente ou lacuna no Softprime | Benefício e dependências observadas | Compatibilidade / risco de adaptação | Esforço | Prioridade | Recomendação neste ciclo |
|---|---|---|---|---|---|---|
| Package Validation: [projeto][dynamic-project] | Baseline planejada em #11 e CI em #8; não automatizada na main examinada | Detectar incompatibilidade no pack; depende do SDK e de baseline real disponível | Não detecta regressão comportamental; confirmar versão publicada, não presumir que 2.0.0 está no feed | P–M | Alta | **Incorporar** a prática via #11/#8, sem copiar a baseline do Codout |
| Testes/governança: [árvore][tree], [findings][findings], [CI][ci] | Estratégia em #7; diretrizes propostas na #13/PR #33 | Caracterizar comportamento e registrar defeitos; exemplos de testes por componente | Não importar percentuais, bibliotecas ou severidades sem validar o contexto; regra de testes + docs + sample já rastreada em #13 | M–G | Alta | **Incorporar** as práticas pelas issues existentes |
| Security: [contrato][hasher], [resultado][verification], [Argon2][argon] | Common.Security possui utilitários legados; falta fluxo próprio de migração de senha | Resultado de rehash e providers separados; DI e bibliotecas de hash | Codout Core declara net10.0; compatibilidade net48 e formatos antigos do Softprime não demonstrada. Findings relata rehash repetido no Argon2 | G | Alta | **Adaptar** o desenho na #6, após caracterização; não portar provider diretamente |
| Mailer.Razor: [contrato][template], [projeto][razor] | Mailer Softprime acopla RazorLight ao envio | Renderizador substituível permite testes e engines distintas; implementação Codout usa ASP.NET Core e RuntimeCompilation 10.0.3 | Engine moderna não atende net48; preservar contrato legado e avaliar cache/erros com testes próprios | M–G | Alta | **Adaptar** a separação de responsabilidades pela #6 |
| Storage/Azure: [contrato][storage], [provider][azure-project], [teste][storage-test] | Sem pacote equivalente na solution Softprime | Upload/download/metadados; provider depende de Azure.Storage.Blobs, MimeTypesMap e extensões Microsoft | Herdam net10.0; contrato inclui GetBlobUri/GetSasUriAsync. Abstração para netstandard2.0 exigiria redesenho. Não há implementação produtiva S3/FileSystem evidenciada | M–G | Média | **Investigar** após demanda; não assumir contrato neutro ou adoção de Azure |
| DynamicLinq: [projeto][dynamic-project], [consulta][dynamic], [testes][dynamic-tests] | Há paginação em DAL/DTOs, sem wrapper equivalente para grids dinâmicos | Filtros/ordenação/agregação com System.Linq.Dynamic.Core 1.7.1 e formato de datasource | Herda net10.0; portabilidade para netstandard2.0 não testada. Avaliar campos permitidos, custo de consultas e isolamento de tenant. Findings relata exceções em overloads e filtro ignorado | M–G | Média | **Investigar**; substitui a recomendação prematura de adaptar imediatamente |
| Application: [projeto][application-project], [CRUD][application] | Sem AppService genérico equivalente | Reduz CRUD repetido, mas impõe AutoMapper 16.1.1, DynamicLinq, Domain e DTOs compartilhados | Herda net10.0. CRUD mistura Task.Run e commits síncronos; política transacional/autorização precisa ser desenhada para Softprime. Licenças dos terceiros precisam de avaliação própria | G | Baixa | **Investigar** apenas com repetição de casos reais demonstrada |
| Mongo: [projeto][mongo-project], [UoW][mongo] | DAL existe; não há implementação Mongo na solution | Alternativa documental com MongoDB.Driver 3.9.0 e DI/configuração | Herda net10.0. Semântica de IDs, paginação e transações difere dos ORMs atuais; findings relata repository fora da sessão do UoW e total incorreto. Integração transacional requer validação de topologia/sessão | G–GG | Baixa | **Investigar** somente com consumidor Mongo identificado; risco maior que o indicado antes |
| Image.Extensions: [projeto][image-project] | Sem componente equivalente | Manipulação de imagem com ImageSharp 3.1.12 e Drawing 2.1.7 | Herda net10.0. Avaliar limites de recursos, formatos e licenças dos terceiros antes de qualquer incorporação | M–G | Baixa | **Não adotar neste ciclo**, sem caso de uso informado |
| MCP: [projeto][mcp-project] | Não há servidor MCP no Softprime | Exposição de catálogo/recipes incorporados como recursos; SDK ModelContextProtocol 1.1.0 e Hosting/Options | net10.0 e distribuição como ferramenta; exige conteúdo confiável, manutenção e definição dos clientes. Utilidade depende da qualidade da documentação | G–GG | Baixa | **Não adotar neste ciclo**; reavaliar quando houver fluxo de uso definido |
| Domain/Data/Common, EF/NH, Api/DTO/Client: [árvore][tree], [findings][findings] | Famílias equivalentes já existem nos 14 projetos Softprime | Referência de testes e evolução, sem lacuna que justifique duplicar pacotes | Contratos e comportamento não são intercambiáveis por semelhança de nomes; não migrar targets nem remover ORMs nesta pesquisa | G–GG | Baixa | **Não adotar** substituição em bloco; melhorias específicas pertencem a #4/#5/#6/#7 |
| Multitenancy: [projeto compatível][tenant-project], [resolver][tenant] | Já existe Softprime.Framework.Multitenancy | Referência de separação entre build moderno e compatível | Nome Softprime.Multitenancy no Codout não prova compatibilidade com Softprime.Framework.Multitenancy; há relato de divergência de chaves de cache | M–G | Média | **Investigar** proveniência; evolução funcional pertence à #4 |

Targets nesta tabela descrevem o Codout inspecionado. Se houver adoção futura, aplicar a matriz Softprime aprovada na #3 e a política #12: preservar consumidores legados dos contratos compartilhados; justificar implementações modernas e qualquer transição net8.0/net10.0, sem importar TFMs indiscriminadamente.

## Proveniência de Multitenancy

**Comprovado:** o [commit Codout de 2020-02-27][tenant-history] adiciona o projeto `Softprime.Multitenancy.csproj`, já com target netstandard2.0 e referências ASP.NET Core 2.x. No Softprime, o [commit de 2021-01-15][softprime-history] contém a área de Multitenancy. Na revisão atual do Codout, a [descrição do projeto][tenant-project] identifica o nome Softprime como build de compatibilidade.

**Observação de leitura:** os dois resolvers têm nomes e fluxo semelhantes: leitura com identificador de contexto e gravação com identificador do tenant ([Codout][tenant], [Softprime][softprime-tenant]). Isso fundamenta comparação técnica; não prova direção de cópia, autorização ou ancestralidade.

**Não comprovado:** a cronologia de entrada nesses repositórios não estabelece a origem do código, possíveis fontes comuns ou acordos entre mantenedores. Pergunta para confirmação: qual é a origem compartilhada, houve reaproveitamento entre os projetos e quais avisos devem acompanhar os arquivos? Até essa confirmação, não atribuir a autoria de um projeto ao outro nem afirmar equivalência dos pacotes.

## Licença e requisitos para eventual reaproveitamento

Os arquivos examinados declaram MIT no [Codout][license] (copyright 2017 Codout) e no [Softprime][softprime-license] (copyright 2021 Softprime). O texto MIT exige preservar o aviso de copyright e de permissão em cópias ou porções substanciais. A licença do Codout fornece uma base para avaliar reaproveitamento, não dispensa conferir a proveniência dos arquivos e as licenças de dependências.

Nesta entrega não há código incorporado nem nova dependência. Em entrega futura que reutilize material, registrar origem/commit/arquivos, preservar os avisos e o texto aplicável junto ao material distribuído, inclusive pacotes, e revisar dependências e recursos separadamente. Manter os avisos existentes do Softprime. A dúvida histórica acima permanece explícita; não declarar autorização histórica específica sem evidência.

## Encaminhamento sem duplicar backlog

Os comentários abaixo já existem e foram conferidos nesta revisão. Não serão republicados:

| Assunto | Destino e comentário existente |
|---|---|
| Hash/rehash e renderização de templates | [#6 — achado original](https://github.com/softprime/Softprime.Framework/issues/6#issuecomment-6619931999) |
| Testes de caracterização | [#7 — achado original](https://github.com/softprime/Softprime.Framework/issues/7#issuecomment-6619932911) |
| Gate de empacotamento | [#8 — achado original](https://github.com/softprime/Softprime.Framework/issues/8#issuecomment-6619932419) |
| Baseline pública/binária | [#11 — achado original](https://github.com/softprime/Softprime.Framework/issues/11#issuecomment-6619931579) |
| Governança de novos componentes | [#13 — achado original](https://github.com/softprime/Softprime.Framework/issues/13#issuecomment-6619933319) |

Ressalvas novas de Security/Mailer serão encaminhadas à #6 e a observação de cache à #4. Os achados não autorizam atualizações, correções ou mudanças comportamentais nessa pesquisa. Issues de planejamento encerradas não representam implementação concluída.

## Aprovação e pendências

- Relatório revisado e matriz de recomendações entregues para aprovação do mantenedor; não marcar recomendações como decisões já aprovadas.
- Demanda por novos componentes **não confirmada**. Nenhuma sub-issue de implementação criada. Para abrir uma: informar consumidor, problema concreto, limites, targets e critérios de teste/documentação/sample.
- Proveniência histórica de Multitenancy permanece pendente de confirmação; fontes e limites da investigação estão registrados acima.
- A #22 permanece aberta para aprovação e tratamento dessa pendência. O PR desta revisão não usa fechamento automático.
- Não houve build, teste de execução, auditoria exaustiva de vulnerabilidades ou homologação de terceiros. Validação desta entrega: fontes, links, consistência das tabelas e diff documental.

[codout]: https://github.com/Codout/Codout.Framework/tree/0e746b50697693228bbd36926be3cc8d2d1f457c
[tree]: https://github.com/Codout/Codout.Framework/tree/0e746b50697693228bbd36926be3cc8d2d1f457c
[softprime]: https://github.com/softprime/Softprime.Framework/tree/f732c52afd663a0a082119ae8df6fbe0f7a455cc
[#22]: https://github.com/softprime/Softprime.Framework/issues/22
[props]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Directory.Build.props
[ci]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/.github/workflows/dotnet.yml
[findings]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/tests/FINDINGS.md
[dynamic-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.DynamicLinq/Codout.DynamicLinq.csproj
[dynamic]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.DynamicLinq/QueryableExtensions.cs
[dynamic-tests]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/tests/Codout.DynamicLinq.Tests/FilterAndSortExpressionTests.cs
[storage-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Storage/Codout.Framework.Storage.csproj
[storage]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Storage/IStorage.cs
[azure-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Storage.Azure/Codout.Framework.Storage.Azure.csproj
[storage-test]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/tests/Codout.Framework.Storage.Tests/InMemoryStorageContractTests.cs
[application-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Application/Codout.Framework.Application.csproj
[application]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Application/CrudAppServiceBase.cs
[mongo-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Mongo/Codout.Framework.Mongo.csproj
[mongo]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Mongo/MongoUnitOfWork.cs
[security-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/src/Security/Codout.Security.Core/Codout.Security.Core.csproj
[hasher]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/src/Security/Codout.Security.Core/IPasswordHasher.cs
[verification]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/src/Security/Codout.Security.Core/PasswordVerificationResult.cs
[argon]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/src/Security/Codout.Security.Argon2/ArgonPasswordHash.cs
[template]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Mailer/Interfaces/ITemplateEngine.cs
[razor]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Mailer.Razor/Codout.Mailer.Razor.csproj
[image-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Image.Extensions/Codout.Image.Extensions.csproj
[mcp-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Framework.Mcp/src/Tools/Codout.Framework.Mcp/Codout.Framework.Mcp.csproj
[tenant-project]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Multitenancy/Softprime.Multitenancy.csproj
[tenant]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/Codout.Multitenancy/MemoryCacheTenantResolver.cs
[license]: https://github.com/Codout/Codout.Framework/blob/0e746b50697693228bbd36926be3cc8d2d1f457c/LICENSE
[tenant-history]: https://github.com/Codout/Codout.Framework/commit/04f0743a7eed54a7e8d5ade0e8f9ffed85d6c308
[softprime-history]: https://github.com/softprime/Softprime.Framework/commit/2dafe89af6b0874099f04c60ff8e599954bf9e1c
[softprime-tenant]: https://github.com/softprime/Softprime.Framework/blob/f732c52afd663a0a082119ae8df6fbe0f7a455cc/src/multitenant/Softprime.Framework.Multitenancy/MemoryCacheTenantResolver.cs
[softprime-license]: https://github.com/softprime/Softprime.Framework/blob/f732c52afd663a0a082119ae8df6fbe0f7a455cc/src/LICENSE.txt
