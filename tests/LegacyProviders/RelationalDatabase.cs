using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Oracle.ManagedDataAccess.Client;
using Xunit;
using Xunit.Abstractions;

namespace Softprime.Framework.Tests.LegacyProviders
{
    public enum Provider
    {
        SqlServer,
        Oracle
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DatabaseFactAttribute : FactAttribute
    {
        public DatabaseFactAttribute(Provider provider)
        {
            Skip = RelationalDatabase.GetUnavailableReason(provider);
        }
    }

    internal sealed class RelationalDatabase : IDisposable
    {
        private const string OracleConnectionVariable = "SOFTPRIME_TEST_ORACLE_CONNECTION";
        private const string OracleSchemaVariable = "SOFTPRIME_TEST_ORACLE_SCHEMA";
        private const string LocalDbInstanceVariable = "SOFTPRIME_TEST_LOCALDB_INSTANCE";
        private const string LocalDbRegistryKey = @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions";

        private readonly string masterConnectionString;
        private readonly string databaseName;

        public RelationalDatabase(Provider provider, ITestOutputHelper output)
        {
            Provider = provider;
            if (provider == Provider.SqlServer)
            {
                var instance = Environment.GetEnvironmentVariable(LocalDbInstanceVariable) ?? "MSSQLLocalDB";
                if (!Regex.IsMatch(instance, @"\A[A-Za-z0-9_-]+\z"))
                    throw new InvalidOperationException("Use a LocalDB instance name without a server or connection string.");

                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = @"(localdb)\" + instance,
                    InitialCatalog = "master",
                    IntegratedSecurity = true,
                    Pooling = false,
                    ConnectTimeout = 30
                };
                databaseName = "SoftprimeTests_" + Guid.NewGuid().ToString("N");
                masterConnectionString = builder.ConnectionString;
                builder.InitialCatalog = databaseName;
                ConnectionString = builder.ConnectionString;
                Schema = "dbo";
                Table = "SoftprimeItems";
                using (var master = new SqlConnection(masterConnectionString))
                {
                    master.Open();
                    Execute(master, $"CREATE DATABASE [{databaseName}]");
                }

                try
                {
                    using (var connection = CreateConnection())
                    {
                        connection.Open();
                        Execute(connection, $"CREATE TABLE [{Schema}].[{Table}] (ID INT NOT NULL PRIMARY KEY, LABEL NVARCHAR(100) NOT NULL)");
                        WriteProviderEvidence(connection, output);
                    }
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }
            else
            {
                var connectionString = Environment.GetEnvironmentVariable(OracleConnectionVariable);
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException("Set " + OracleConnectionVariable + " to the dedicated Oracle test connection.");

                var builder = new OracleConnectionStringBuilder(connectionString)
                {
                    Pooling = false
                };
                Schema = Environment.GetEnvironmentVariable(OracleSchemaVariable)?.ToUpperInvariant();
                if (string.IsNullOrEmpty(Schema) || !Regex.IsMatch(Schema, @"\A[A-Z][A-Z0-9_]{0,29}\z")
                    || !string.Equals(builder.UserID, Schema, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Connect as the dedicated Oracle test schema named in " + OracleSchemaVariable + ".");
                }

                ConnectionString = builder.ConnectionString;
                Table = "SPT_" + Guid.NewGuid().ToString("N").Substring(0, 24).ToUpperInvariant();
                using (var connection = CreateConnection())
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";
                        var activeSchema = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        if (!string.Equals(activeSchema, Schema, StringComparison.Ordinal))
                            throw new InvalidOperationException("The Oracle session is outside the dedicated test schema.");
                    }

                    WriteProviderEvidence(connection, output);
                    Execute(connection, $"CREATE TABLE {Schema}.{Table} (ID NUMBER(10,0) NOT NULL PRIMARY KEY, LABEL NVARCHAR2(100) NOT NULL)");
                }
            }
        }

        public Provider Provider { get; }
        public string ConnectionString { get; }
        public string Schema { get; }
        public string Table { get; }

        public DbConnection CreateConnection()
        {
            return Provider == Provider.SqlServer
                ? (DbConnection)new SqlConnection(ConnectionString)
                : new OracleConnection(ConnectionString);
        }

        public void Dispose()
        {
            if (Provider == Provider.SqlServer)
            {
                using (var master = new SqlConnection(masterConnectionString))
                {
                    master.Open();
                    Execute(master, $"DROP DATABASE [{databaseName}]");
                }
            }
            else
            {
                using (var connection = CreateConnection())
                {
                    connection.Open();
                    Execute(connection, $"DROP TABLE {Schema}.{Table} PURGE");
                }
            }
        }

        internal static string GetUnavailableReason(Provider provider)
        {
            if (provider == Provider.Oracle)
            {
                return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OracleConnectionVariable))
                    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OracleSchemaVariable))
                    ? $"Oracle requires {OracleConnectionVariable} and {OracleSchemaVariable} for a dedicated test schema."
                    : null;
            }

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LocalDbInstanceVariable)))
                return null;

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var versions = machine.OpenSubKey(LocalDbRegistryKey))
                {
                    if (versions != null && versions.SubKeyCount != 0)
                        return null;
                }
            }

            return "SQL Server Express LocalDB is not installed.";
        }

        private static void Execute(DbConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static void WriteProviderEvidence(DbConnection connection, ITestOutputHelper output)
        {
            var assembly = connection.GetType().Assembly;
            var version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
            output.WriteLine($"CLR {Environment.Version}; driver {connection.GetType().FullName} {version}; server {connection.ServerVersion}");
        }
    }
}
