# 08 — Aplicação integrada

## 1. Objetivo

Cadastrar empresas, usuários e produtos fictícios e acompanhar o fluxo desde o cliente HTTP tipado até o SQLite. A aplicação envia um e-mail de boas-vindas em memória e demonstra como restringir consultas e alterações ao tenant da requisição.

## 2. Pré-requisitos

- SDK .NET 10 conforme [`global.json`](../global.json). A validação local e o CI usam Windows.
- O restore precisa de acesso às dependências NuGet. A execução usa apenas o servidor HTTP local e um arquivo SQLite temporário.
- O sample exercita Domain, DAL, EFCore10, Api, Api.Dto, Api.Client, Mailer e Multitenancy. O provedor SQLite usa a mesma versão do EF Core 10 do pacote EFCore10.

## 3. Comandos PowerShell e Bash

Execute a partir da raiz do repositório. O script gera pacotes locais com uma
versão exclusiva de validação, usa o feed em `artifacts/samples-ci/` e compila
o sample com `PackageReference`. O log da execução identifica o feed utilizado.

**PowerShell**

```powershell
powershell -ExecutionPolicy Bypass -File ./samples/validate-ci.ps1
dotnet ./samples/08-CompleteSample/bin/packages/Release/net10.0/Softprime.Samples.CompleteSample.dll
```

**Bash no Windows com PowerShell 7**

```bash
pwsh ./samples/validate-ci.ps1 &&
dotnet ./samples/08-CompleteSample/bin/packages/Release/net10.0/Softprime.Samples.CompleteSample.dll
```

Para depurar alterações nas bibliotecas diretamente do checkout, compile com
`-p:UseInstalledPackages=false` e execute o binário equivalente em `bin/projects`.

## 4. Fluxo passo a passo

1. O executável cria o banco SQLite e sobe o ASP.NET Core em uma porta livre de `127.0.0.1`.
2. O `ApiClientTyped` envia os cabeçalhos `ApiKey` e `TenantKey`. O middleware do framework resolve `alpha` ou `beta`. O cabeçalho identifica o tenant fictício da demonstração. Uma aplicação real deve autorizar o tenant a partir da identidade autenticada.
3. O controller cadastra empresas, um usuário e produtos por meio do repositório e da unidade de trabalho. O Mailer renderiza o template Razor, e o dispatcher guarda o e-mail em memória.
4. Cada tenant consulta seu catálogo. O roteiro atualiza e exclui produtos autorizados, tenta gravar com dados inválidos e tenta ler ou alterar dados de outro tenant. As consultas filtram pelo tenant resolvido, e as chaves estrangeiras do banco exigem que usuário ou produto e empresa pertençam ao mesmo tenant.
5. O roteiro verifica os dados no SQLite e o e-mail em memória. Ao terminar, encerra o servidor e apaga o banco temporário.

## 5. Saída esperada no console

```text
Empresas, usuário e produtos cadastrados via HTTP e gravados no SQLite.
Entradas inválidas, tenant desconhecido e erro HTTP tratado pelo ApiClient.
Leitura e escrita cruzadas bloqueadas. SQLite e e-mail simulado conferidos.
Demonstração concluída. O banco SQLite temporário foi descartado.
```

## 6. Smoke Test

Após compilar por pacotes locais, execute em PowerShell ou Bash a partir da raiz:

```text
dotnet ./samples/08-CompleteSample/bin/packages/Release/net10.0/Softprime.Samples.CompleteSample.dll --smoke-test
```

O smoke retorna `0` e imprime `[SMOKE_TEST_PASSED]` quando os cadastros, o e-mail simulado e as consultas passam. Ele verifica também `400` para entradas inválidas, `403` e `ApiException` para a chave incorreta, e `404` para tenant inválido ou operações cruzadas. Uma leitura da base confirma que os dados originais foram preservados. Qualquer falha retorna `1`.

O [CI de samples](../../.github/workflows/samples-ci.yml) executa esse smoke com referências a projetos e com pacotes NuGet locais. A auditoria de empacotamento verifica que o sample fica fora do pack.

## 7. Troubleshooting

| Sintoma | Como resolver |
|---|---|
| O SDK solicitado não foi encontrado | Execute `dotnet --list-sdks` e confira a versão declarada em `samples/global.json`. |
| O restore não consegue acessar o NuGet | Verifique a disponibilidade do feed. Para repetir o consumo de pacotes Softprime empacotados localmente, use `samples/validate-ci.ps1` na raiz do repositório. |
| O smoke falha ao iniciar o servidor ou criar o banco | Verifique se o processo pode abrir uma porta em `127.0.0.1` e escrever no diretório temporário do sistema. |
