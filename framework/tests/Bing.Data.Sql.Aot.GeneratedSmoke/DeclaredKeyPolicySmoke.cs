using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Extensions.DependencyInjection;

/// <summary>严格生成模式下，基类写入合同不能被派生对象的主键策略替换。</summary>
internal static class DeclaredKeyPolicySmoke
{
    internal static async Task Run(string path)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
        services.AddSqlGeneratedModels(GeneratedModels.CreateModels());
        services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
        services.AddSqliteProvider();
        services.AddEntityKeyPolicies(keys => keys
            .ForGenerated<ClientKeyBaseRow, long>(nameof(ClientKeyBaseRow.Id), EntityKeyGenerationKind.ClientSnowflake,
                static row => row.Id, static (row, value) => row.Id = value)
            .ForGenerated<ClientKeyDerivedRow, long>(nameof(ClientKeyDerivedRow.Id), EntityKeyGenerationKind.Assigned,
                static row => row.Id, static (row, value) => row.Id = value)
            .ForGenerated<IdentityKeyBaseRow, long>(nameof(IdentityKeyBaseRow.Id), EntityKeyGenerationKind.DatabaseIdentity,
                static row => row.Id, static (row, value) => row.Id = value)
            .ForGenerated<IdentityKeyDerivedRow, long>(nameof(IdentityKeyDerivedRow.Id), EntityKeyGenerationKind.Assigned,
                static row => row.Id, static (row, value) => row.Id = value));
        services.AddSingleton<IClientKeyGenerator<long>, Counter>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var writer = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        writer.ExecuteSql("CREATE TABLE DeclaredClientKeys(Id INTEGER PRIMARY KEY, Name TEXT);" +
            "CREATE TABLE DeclaredIdentityKeys(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");

        var first = new ClientKeyDerivedRow { Id = 900, Name = "single" };
        Check(writer.Insert<ClientKeyBaseRow>(first) == 1 && ((ClientKeyBaseRow)first).Id == 101 && first.Id == 900,
            "declared generated policy single");
        var second = new ClientKeyDerivedRow { Id = 901, Name = "async" };
        Check(await writer.InsertAsync<ClientKeyBaseRow>(second) == 1 && ((ClientKeyBaseRow)second).Id == 102 && second.Id == 901,
            "declared generated policy async");
        ClientKeyBaseRow[] batch = { new ClientKeyDerivedRow { Id = 902, Name = "batch-a" }, new ClientKeyDerivedRow { Id = 903, Name = "batch-b" } };
        Check(await writer.InsertBatchAsync(batch) == 2 && batch.Select(row => row.Id).SequenceEqual(new long[] { 103, 104 }) &&
            batch.Cast<ClientKeyDerivedRow>().Select(row => row.Id).SequenceEqual(new long[] { 902, 903 }),
            "declared generated policy batch");
        var keyed = new ClientKeyDerivedRow { Id = 904, Name = "keys" };
        var result = await ((ISqlKeyInsertExecutor)writer).InsertWithKeysAsync<ClientKeyBaseRow, long>(
            new[] { new InsertRow<ClientKeyBaseRow>("row", keyed) });
        Check(result.Completion == WriteCompletionState.Committed && result.Keys["row"] == 105 &&
            ((ClientKeyBaseRow)keyed).Id == 105 && keyed.Id == 904, "declared generated policy returned key");
        Check(query.Sql("SELECT Id FROM DeclaredClientKeys ORDER BY Id").ToList<long>().SequenceEqual(new long[] { 101, 102, 103, 104, 105 }),
            "declared generated policy persisted keys");

        var invalid = new IdentityKeyDerivedRow { Id = 905, Name = "invalid" };
        ((IdentityKeyBaseRow)invalid).Id = 77;
        for (var entrypoint = 0; entrypoint < 5; entrypoint++)
        {
            var rejected = false;
            try
            {
                switch (entrypoint)
                {
                    case 0: writer.Insert<IdentityKeyBaseRow>(invalid); break;
                    case 1: await writer.InsertAsync<IdentityKeyBaseRow>(invalid); break;
                    case 2: writer.InsertBatch(new IdentityKeyBaseRow[] { invalid }); break;
                    case 3: await writer.InsertBatchAsync(new IdentityKeyBaseRow[] { invalid }); break;
                    default:
                        await ((ISqlKeyInsertExecutor)writer).InsertWithKeysAsync<IdentityKeyBaseRow, long>(
                            new[] { new InsertRow<IdentityKeyBaseRow>("invalid", invalid) });
                        break;
                }
            }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "declared identity policy rejects assigned key " + entrypoint);
        }
        Check(query.Sql("SELECT COUNT(*) FROM DeclaredIdentityKeys").Scalar<int>() == 0,
            "declared identity policy rejects before insert");
    }

    private static void Check(bool success, string name)
    {
        if (!success) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }

    private sealed class Counter : IClientKeyGenerator<long>
    {
        private long _value = 100;
        public string Name => "default";
        public long Next(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.Increment(ref _value);
        }
    }
}

[Table("DeclaredClientKeys")]
public class ClientKeyBaseRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public long Id { get; set; }
    public string Name { get; set; }
}

public sealed class ClientKeyDerivedRow : ClientKeyBaseRow
{
    public new long Id { get; set; }
}

[Table("DeclaredIdentityKeys")]
public class IdentityKeyBaseRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
    public string Name { get; set; }
}

public sealed class IdentityKeyDerivedRow : IdentityKeyBaseRow
{
    public new long Id { get; set; }
}
