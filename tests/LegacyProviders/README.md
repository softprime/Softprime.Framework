# Validação de EF6 e NHibernate

Os dois projetos executam em Windows com `net48`, usando EF 6.4.4 e NHibernate 5.3.5. O provedor Oracle de EF6 e o driver Oracle dos dois projetos são 19.14.0.

- SQL Server Express LocalDB usa `MSSQLLocalDB` por padrão. Para escolher outra instância LocalDB existente, defina `SOFTPRIME_TEST_LOCALDB_INSTANCE` com seu nome. Cada cenário cria e remove um banco exclusivo.
- Oracle usa `SOFTPRIME_TEST_ORACLE_CONNECTION` e `SOFTPRIME_TEST_ORACLE_SCHEMA`. Configure esses valores no ambiente autorizado de testes. O login deve corresponder ao schema dedicado e ter permissão e quota para criar e remover suas próprias tabelas. Cada cenário usa uma tabela exclusiva.
- Os relatórios TRX incluem o runtime, a versão do ORM, do driver e do servidor para os cenários relacionais executados. Quando a infraestrutura está ausente, os casos correspondentes são marcados como ignorados com o motivo.

```powershell
dotnet test tests/Softprime.Framework.EF6.Tests -c Release -f net48 --results-directory artifacts/legacy-providers --logger 'trx;LogFileName=ef6-net48.trx'
dotnet test tests/Softprime.Framework.NH.Tests -c Release -f net48 --results-directory artifacts/legacy-providers --logger 'trx;LogFileName=nh-net48.trx'
```

Antes de integrar a #54, execute ambos os projetos com SQL Server e Oracle disponíveis e registre o commit e os dois relatórios. As credenciais Oracle ficam no ambiente de testes autorizado.
