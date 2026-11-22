using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MySqlConnection");
if (!string.Equals(Environment.GetEnvironmentVariable("RUN_MYSQL_INTEGRATION_TESTS"), "true",
        StringComparison.OrdinalIgnoreCase) ||
    string.IsNullOrWhiteSpace(connectionString) ||
    !string.Equals(new MySqlConnectionStringBuilder(connectionString).Database, "bing_sql_aot_test",
        StringComparison.Ordinal))
    throw new InvalidOperationException(
        "MySQL Native smoke requires RUN_MYSQL_INTEGRATION_TESTS=true and the bing_sql_aot_test database.");

var services = new ServiceCollection();
var models = PackageModels.CreateModels();
services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
services.AddSqlGeneratedModels(models);
services.AddSqlDataSource("default", DatabaseType.MySql, connectionString);
services.AddMySqlProvider();
services.AddEntityKeyPolicies(keys => keys.ForGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id),
    EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
using var writer = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();

writer.ExecuteSql("CREATE TABLE IF NOT EXISTS `package_rows` (`Id` int NOT NULL PRIMARY KEY, `Name` varchar(200) NULL) ENGINE=InnoDB");
writer.ExecuteSql("CREATE TABLE IF NOT EXISTS `identity_crud_rows` (`Id` int NOT NULL AUTO_INCREMENT PRIMARY KEY, `Name` varchar(200) NULL) ENGINE=InnoDB");

var strictServices = new SqlBuilderServices(
    metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
    entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models));
var builder = new DefaultSqlEntityMutationCommandBuilder(MySqlSqlProvider.Instance, strictServices);
var crudModel = models.Single(item => item.EntityType == typeof(CrudRow));
var rowId = CreateId();
var row = new CrudRow { Id = rowId, Name = "inserted-" + Guid.NewGuid().ToString("N") };
var insert = builder.Insert(row);
CheckCommand(insert, crudModel, "Insert Into `package_rows` (`Id`, `Name`) Values (@_p_0, @_p_1)");
Check(writer.ExecuteWrite(insert) == 1, "generated insert");

row.Name = "updated-" + Guid.NewGuid().ToString("N");
var update = builder.Update(row);
CheckCommand(update, crudModel, "Update `package_rows` Set `Name` = @_p_0 Where `Id` = @_p_1");
Check(writer.ExecuteWrite(update) == 1, "generated update");

using (var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create())
{
    var lambda = query.From<CrudRow>("r").SelectAll().Where<CrudRow, int>(item => item.Id, rowId);
    Check(lambda.ToSql() == "Select `r`.`Id`,`r`.`Name` " + Environment.NewLine +
        "From `package_rows` As `r` " + Environment.NewLine + "Where `r`.`Id`=@_p_0",
        "generated lambda SQL");
    var result = await lambda.ToListAsync<CrudRow>();
    Check(result.Count == 1 && result[0].Id == rowId && result[0].Name == row.Name, "generated lambda query");
}

var delete = builder.Purge(row);
CheckCommand(delete, crudModel, "Delete From `package_rows` Where `Id` = @_p_0");
Check(writer.ExecuteWrite(delete) == 1, "generated delete");
Console.WriteLine("MYSQL_GENERATED_CRUD_NATIVE_PASS");

var identityModel = models.Single(item => item.EntityType == typeof(IdentityCrudRow));
var identityPolicy = EntityKeyPolicy.CreateGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id),
    EntityKeyGenerationKind.DatabaseIdentity, static item => item.Id, static (item, key) => item.Id = key);
var identity = new IdentityCrudRow { Name = "identity-" + Guid.NewGuid().ToString("N") };
var keyCommand = new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(MySqlSqlProvider.Instance, strictServices),
    identityPolicy).Build(identity);
Check(keyCommand.Sql == "Insert Into `identity_crud_rows` (`Name`) Values (@_p_0)" &&
    ReferenceEquals(keyCommand.Sql, identityModel.GeneratedKeyInsertSql?.Invoke("bing.mysql")) &&
    !keyCommand.HasReturning && keyCommand.IsGeneratedExecution, "generated MySQL identity SQL and binder");
var keys = await ((ISqlKeyInsertExecutor)writer).InsertWithKeysAsync<IdentityCrudRow, int>(
    new[] { new InsertRow<IdentityCrudRow>("identity", identity) });
Check(keys.Completion == WriteCompletionState.Committed && identity.Id > 0 &&
    keys.Keys["identity"] == identity.Id, "LAST_INSERT_ID key backfill and commit");
using (var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create())
{
    var persisted = await query.From<IdentityCrudRow>().Where<IdentityCrudRow, int>(item => item.Id, identity.Id)
        .ToListAsync<IdentityCrudRow>();
    Check(persisted.Count == 1 && persisted[0].Name == identity.Name, "committed identity row");
}
Console.WriteLine("MYSQL_IDENTITY_NATIVE_PASS");

var rollbackId = CreateId();
await using (var transaction = await scope.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync())
{
    using var transactionalWriter = transaction.CreateExecutor();
    using var transactionalQuery = transaction.CreateQuery();
    var transactionalRow = new CrudRow { Id = rollbackId, Name = "rollback-" + Guid.NewGuid().ToString("N") };
    Check(await transactionalWriter.InsertAsync(transactionalRow) == 1, "shared transaction insert");
    var visible = await transactionalQuery.From<CrudRow>().Where<CrudRow, int>(item => item.Id, rollbackId)
        .ToListAsync<CrudRow>();
    Check(visible.Count == 1 && visible[0].Name == transactionalRow.Name, "shared transaction query");
    await transaction.RollbackAsync();
}
using (var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create())
{
    var rolledBack = await query.From<CrudRow>().Where<CrudRow, int>(item => item.Id, rollbackId)
        .ToListAsync<CrudRow>();
    Check(rolledBack.Count == 0, "shared transaction rollback");
}
Console.WriteLine("MYSQL_GENERATED_NATIVE_PASS");

void CheckCommand(SqlWriteCommand command, Bing.Data.Sql.Metadata.EntityModelMetadata model, string expected)
{
    Check(command.Sql == expected && command.IsGeneratedExecution &&
        ReferenceEquals(command.Sql, model.GeneratedMutationSql?.Invoke("bing.mysql", command.OperationKind)),
        "generated SQL and binder: " + expected);
}

static int CreateId()
{
    var value = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue;
    return value == 0 ? 1 : value;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
