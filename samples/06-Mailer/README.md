# Sample 06-Mailer — Templates Razor com `SoftprimeMailer` e `FakeMailerDispatcher`

> Exemplo didático executável demonstrando a renderização de e-mails HTML com templates Razor embutidos (`Softprime.Framework.Mailer`) e despacho 100% em memória via `FakeMailerDispatcher`, sem envio pela rede e sem credenciais externas.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática como estruturar o envio de e-mails desacoplado do provedor no `Softprime.Framework`:
- **Subclasse de `SoftprimeMailer`:** Como o construtor de [`SoftprimeMailer`](../../src/mail/Softprime.Framework.Mailer/SoftprimeMailer.cs) é `protected`, o sample demonstra a criação de `SampleMailer` fornecendo `SoftprimeMailerSettings`, `ISoftprimeMailerDispatcher` e o tipo âncora `typeof(TemplateMarker)` para localização dos templates `.cshtml` embutidos.
- **Templates Razor Tipados e Embutidos:** Uso de `<EmbeddedResource Include="Templates\*.cshtml" />` com modelos fortemente tipados herdando `MailerModelBase` (`BoasVindas.cshtml` e `ResumoPedido.cshtml` com iteração `@foreach`).
- **Configuração Crítica do `.csproj`:** Inclusão de `<PreserveCompilationContext>true</PreserveCompilationContext>` para permitir que o `RazorLight 2.0.0-rc.3` localize as referências de compilação Roslyn em `.NET 8` e `.NET 10`.
- **Anexos em Memória:** Envio de `Attachment[]` construído sobre `MemoryStream` e leitura via extensão pública `ReadFully()`.
- **Dispatcher Fake Offline (`FakeMailerDispatcher`):** Implementação de `ISoftprimeMailerDispatcher` em memória para inspecionar os e-mails gerados e simular falhas controladas (`Sent = false`), sem utilizar AWS SES, SendGrid ou rede real.

---

## 2. Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou [.NET SDK 8.0](https://dotnet.microsoft.com/) instalado.
- **Autonomia Total:** Nenhuma conta AWS SES, chave SendGrid ou servidor SMTP é necessário. Tudo roda 100% offline em memória.

---

## 3. Comandos para rodar

### No Windows (PowerShell):
```powershell
dotnet run --project samples/06-Mailer/Softprime.Samples.Mailer.csproj --framework net10.0
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/06-Mailer/Softprime.Samples.Mailer.csproj --framework net10.0
```

*(Para executar sobre .NET 8, substitua `--framework net10.0` por `--framework net8.0`.)*

---

## 4. Fluxo de execução passo a passo

1. **Configuração do Mailer:** Instanciação de `SoftprimeMailerSettings` (remetente padrão), `FakeMailerDispatcher` em memória e `SampleMailer` apontando para `typeof(TemplateMarker)`.
2. **Template Simples (`BoasVindas`):** Renderização do template embutido `Templates/BoasVindas.cshtml` com `BoasVindasEmailModel` e `plainTextContent`, validando o HTML gerado e o remetente na caixa de saída em memória.
3. **Template com Lista + Anexo (`ResumoPedido`):** Renderização do template `Templates/ResumoPedido.cshtml` iterando itens com `@foreach` e anexando um `Attachment` criado em `MemoryStream`.
4. **Cenário Negativo — Falha Simulada no Dispatcher:** Configuração do `FakeMailerDispatcher` para retornar `MailerResponse` com `Sent = false` e `ErrorMessages`, garantindo que mensagens com falha não sejam registradas como enviadas.
5. **Cenário Negativo — Template Inexistente:** Chamada com chave inexistente capturando `TemplateNotFoundException` do `RazorLight`.

---

## 5. Saída esperada no console

Ao executar o comando de demonstração, você verá uma saída semelhante a esta no terminal:

```text
================================================================================
 Softprime.Framework — Sample 06-Mailer (Templates Razor + FakeDispatcher)
================================================================================

[INFO] Modo 100% offline: nenhum e-mail real e enviado pela rede (FakeMailerDispatcher em memoria).

--- 1. Template Razor Tipado (BoasVindas.cshtml) ---
  Para: Ana Silva <ana.silva@cliente.example> | Assunto: 'Bem-vinda ao Softprime Portal' | HTML: 349 chars | Sent=True
--- 2. Template com Loop @foreach + Anexo em Memoria (ResumoPedido.cshtml) ---
  Para: compras@acme.example | Anexo: comprovante-ped-2026-0094.txt (64 bytes) | Sent=True
--- 3. Cenario Negativo: Falha Simulada no Dispatcher ---
  Sent=False | Erro tratado: Provedor SMTP simulado indisponivel (modo offline).
--- 4. Cenario Negativo: Template Inexistente ---
  Excecao TemplateNotFoundException capturada -> Project can not find template with key TemplateInexistente.cshtml

[SUCESSO] Demonstracao Mailer concluida com 100% de exito! (2 e-mails registrados em memoria)
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou verificação automatizada local, o sample suporta `--smoke-test`, retornando código de saída `0` para sucesso ou `1` em caso de falha:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/06-Mailer/Softprime.Samples.Mailer.csproj --framework net10.0 -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/06-Mailer/Softprime.Samples.Mailer.csproj --framework net10.0 -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.Mailer: Renderizacao Razor, anexos, FakeMailerDispatcher e cenarios negativos validados.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The TargetFramework 'net10.0' is not recognized` | SDK do .NET 10 não está instalado localmente. | Execute especificando o target .NET 8 com `--framework net8.0`. |
| `Can't load metadata reference from the entry assembly` | Falta `<PreserveCompilationContext>true</PreserveCompilationContext>` no `.csproj` consumidor do `RazorLight 2.0.0-rc.3`. | Mantenha `<PreserveCompilationContext>true</PreserveCompilationContext>` habilitado em `Softprime.Samples.Mailer.csproj`. |
| `TemplateNotFoundException` para template válido | Arquivo `.cshtml` não marcado como `EmbeddedResource` ou namespace de `templateRootType` divergente da pasta `Templates`. | Garanta `<EmbeddedResource Include="Templates\*.cshtml" />` e mantenha `TemplateMarker` no namespace `Softprime.Samples.Mailer.Templates`. |
| `Warning NU5104` no build | Dependência `RazorLight 2.0.0-rc.3` (pré-release) em `Softprime.Framework.Mailer`. | Aviso esperado antes da modernização `#6`; não afeta o funcionamento do sample. |
