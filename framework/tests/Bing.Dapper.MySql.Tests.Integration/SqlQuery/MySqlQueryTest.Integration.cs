using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Tests.Models;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL SQL 查询对象真实执行测试。
/// </summary>
public partial class MySqlQueryTest
{
    /// <summary>
    /// 测试 - MySQL应执行参数化列表查询。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteQuery_ShouldReturnRowsForParameterizedList()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await InitProductDataAsync(firstId, "list-first");
        await InitProductDataAsync(secondId, "list-second");

        var result = _sqlQuery.Lambda<Product>()
            .ClearSelect()
            .Select(true)
            .From()
            .Where<Product>(product => product.Id, new object[] { firstId, secondId }, Operator.In)
            .ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { firstId, secondId }.OrderBy(id => id), result.Select(product => product.Id).OrderBy(id => id));
    }

    /// <summary>
    /// 测试 - MySQL应执行Scalar查询。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteScalar_ShouldReturnActualCount()
    {
        await InitProductDataAsync(Guid.NewGuid(), "scalar-first");
        await InitProductDataAsync(Guid.NewGuid(), "scalar-second");

        var result = _sqlQuery.Sql<int>().Count().From("Product").Scalar();

        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - MySQL应执行Single查询。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteSingle_ShouldReturnActualRow()
    {
        var id = Guid.NewGuid();
        await InitProductDataAsync(id, "single");

        var result = _sqlQuery.Lambda<Product>()
            .ClearSelect()
            .Select(true)
            .From()
            .Where(product => product.Id, id)
            .FirstOrDefault();

        Assert.Equal(id, result.Id);
        Assert.Equal("single", result.Code);
    }

    /// <summary>
    /// 测试 - MySQL应正确绑定显式空参数。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteSql_ShouldPersistExplicitNullParameter()
    {
        var id = Guid.NewGuid();
        await _sqlExecutor.ExecuteSqlAsync<Product>(
            "Insert Product(ProductId, Code, Name) Values(@productId, @code, @name)",
            new { productId = id, code = "null", name = (string)null }, map => map
                .Map("productId", product => product.Id)
                .Map("code", product => product.Code)
                .Add("name", product => product.Name, null));

        var result = _sqlQuery.Lambda<Product>()
            .ClearSelect()
            .Select(true)
            .From()
            .Where(product => product.Id, id)
            .FirstOrDefault();

        Assert.Null(result.Name);
    }

    /// <summary>
    /// 测试 - MySQL部分参数映射不应丢失未映射参数。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteSql_ShouldKeepUnmappedParametersWhenPartiallyMapped()
    {
        var id = Guid.NewGuid();
        await _sqlExecutor.ExecuteSqlAsync<Product>(
            "Insert Product(ProductId, Code, Name) Values(@productId, @code, @name)",
            new { productId = id, code = "partial", name = "unmapped-name" }, map =>
                map.Map("code", product => product.Code));

        var result = _sqlQuery.Lambda<Product>()
            .ClearSelect()
            .Select(true)
            .From()
            .Where(product => product.Id, id)
            .FirstOrDefault();

        Assert.Equal("partial", result.Code);
        Assert.Equal("unmapped-name", result.Name);
    }

    /// <summary>
    /// 测试 - MySQL同步列表查询应支持buffered为false。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteQuery_ShouldSupportBufferedFalse()
    {
        await SeedProductsAsync();

        using var query = _sqlQueryFactory.Create<ISqlQuery>();
        var result = query.Lambda<Product>().ClearSelect().Select(true).From().AsEnumerable().ToList();

        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// 测试 - MySQL异步列表查询应支持buffered为false。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteQueryAsync_ShouldSupportBufferedFalse()
    {
        await SeedProductsAsync();

        using var query = _sqlQueryFactory.Create<ISqlQuery>();
        var result = new List<Product>();
        await foreach (var product in query.Lambda<Product>().ClearSelect().Select(true).From().AsAsyncEnumerable())
            result.Add(product);

        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// 测试 - MySQL缓冲和非缓冲列表结果应一致。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteQuery_ShouldReturnSameRowsForBufferedModes()
    {
        await SeedProductsAsync();
        using var bufferedQuery = _sqlQueryFactory.Create<ISqlQuery>();
        using var nonBufferedQuery = _sqlQueryFactory.Create<ISqlQuery>();

        var buffered = bufferedQuery.Lambda<Product>().ClearSelect().Select(true).From().ToList();
        var nonBuffered = nonBufferedQuery.Lambda<Product>().ClearSelect().Select(true).From().AsEnumerable().ToList();

        Assert.Equal(buffered.Select(product => product.Code), nonBuffered.Select(product => product.Code));
    }

    /// <summary>
    /// 测试 - MySQL非缓冲查询仍应返回完整列表。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteQuery_ShouldMaterializeAllRowsWhenBufferedFalse()
    {
        await SeedProductsAsync();

        using var query = _sqlQueryFactory.Create<ISqlQuery>();
        var result = query.Lambda<Product>().ClearSelect().Select(true).From().AsEnumerable().ToList();

        Assert.Equal(new[] { "buffer-one", "buffer-three", "buffer-two" },
            result.Select(product => product.Code).OrderBy(code => code));
    }

    /// <summary>
    /// 测试 - MySQL非缓冲查询应在方法返回前完成物化。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteQuery_ShouldCompleteMaterializationBeforeReturning()
    {
        await SeedProductsAsync();
        List<Product> result;
        using (var query = _sqlQueryFactory.Create<ISqlQuery>())
            result = query.Lambda<Product>().ClearSelect().Select(true).From().AsEnumerable().ToList();

        await InitProductDataAsync(Guid.NewGuid(), "after-materialization");

        Assert.Equal(3, result.Count);
        Assert.Equal(4, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
    }

    /// <summary>
    /// 测试 - MySQL同步流式查询应完整返回数据。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task StreamQuery_ShouldReturnAllRows()
    {
        await SeedProductsAsync();
        using var query = _sqlQueryFactory.Create<ISqlQuery>();

        var result = query.Lambda<Product>().ClearSelect().Select(true).From().AsEnumerable()
            .Select(product => product.Code).OrderBy(code => code).ToList();

        Assert.Equal(new[] { "buffer-one", "buffer-three", "buffer-two" }, result);
    }

    /// <summary>
    /// 测试 - MySQL异步流式查询应完整返回数据。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task StreamAsync_ShouldReturnAllRows()
    {
        await SeedProductsAsync();
        using var query = _sqlQueryFactory.Create<ISqlQuery>();
        var result = new List<string>();

        await foreach (var product in query.Lambda<Product>().ClearSelect().Select(true).From().AsAsyncEnumerable())
            result.Add(product.Code);

        Assert.Equal(new[] { "buffer-one", "buffer-three", "buffer-two" }, result.OrderBy(code => code));
    }

    /// <summary>
    /// 测试 - MySQL流式查询提前终止应释放Reader。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task StreamQuery_ShouldReleaseReaderWhenEnumerationStopsEarly()
    {
        await SeedProductsAsync();
        using (var query = _sqlQueryFactory.Create<ISqlQuery>())
        using (var enumerator = query.Lambda<Product>().ClearSelect().Select(true).From().AsEnumerable().GetEnumerator())
            Assert.True(enumerator.MoveNext());

        await InitProductDataAsync(Guid.NewGuid(), "after-stream");

        Assert.Equal(4, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
    }

    /// <summary>
    /// 测试 - MySQL流式查询取消应释放Reader和Command。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task StreamAsync_ShouldReleaseResourcesWhenCancelled()
    {
        await SeedProductsAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        using var query = _sqlQueryFactory.Create<ISqlQuery>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.Lambda<Product>().ClearSelect().Select(true).From()
                               .AsAsyncEnumerable(cancellationToken: cancellationTokenSource.Token))
            {
            }
        });

        await InitProductDataAsync(Guid.NewGuid(), "after-cancel");
        Assert.Equal(4, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
    }

    /// <summary>
    /// 测试 - MySQL事务提交后数据应持久化。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public Task TransactionScope_ShouldPersistDataAfterCommit()
    {
        using (var scope = _transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
        {
            executor.ExecuteSql("Insert Product(ProductId, Code) Values(@productId, @code)",
                new { productId = Guid.NewGuid(), code = "committed" });
            scope.Commit();
        }

        Assert.Equal(1, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试 - MySQL事务回滚后数据不应持久化。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public Task TransactionScope_ShouldNotPersistDataAfterRollback()
    {
        using (var scope = _transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
        {
            executor.ExecuteSql("Insert Product(ProductId, Code) Values(@productId, @code)",
                new { productId = Guid.NewGuid(), code = "rolled-back" });
            scope.Rollback();
        }

        Assert.Equal(0, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试 - MySQL未完成事务作用域释放时应自动回滚。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public Task TransactionScope_ShouldRollbackWhenDisposedWithoutCompletion()
    {
        using (var scope = _transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
            executor.ExecuteSql("Insert Product(ProductId, Code) Values(@productId, @code)",
                new { productId = Guid.NewGuid(), code = "implicit-rollback" });

        Assert.Equal(0, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试 - MySQL事务作用域中的Query和Executor应共享事务。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public Task TransactionScope_ShouldShareTransactionBetweenQueryAndExecutor()
    {
        using (var scope = _transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
        using (var query = scope.CreateQuery())
        {
            executor.ExecuteSql("Insert Product(ProductId, Code) Values(@productId, @code)",
                new { productId = Guid.NewGuid(), code = "shared" });
            Assert.Equal(1, query.Sql<int>().Count().From("Product").Scalar());
            scope.Commit();
        }

        Assert.Equal(1, _sqlQuery.Sql<int>().Count().From("Product").Scalar());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 测试 - MySQL应正确写入和读取JSON参数。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteSql_ShouldPersistJsonParameter()
    {
        var id = Guid.NewGuid();
        const string json = "{\"name\":\"Bing\",\"enabled\":true}";
        await _sqlExecutor.ExecuteSqlAsync(
            "Insert ParameterSample(ParameterSampleId, JsonValue, DecimalValue) Values(@id, @json, @decimal)",
            new { id, json, @decimal = 1.25m });

        var result = _sqlQuery.Sql<string>().Select("JsonValue").From("ParameterSample")
            .AppendWhere("ParameterSampleId=@id").AddParam("id", id).Scalar();

        Assert.Equal(json, result);
    }

    /// <summary>
    /// 测试 - MySQL应保留Decimal精度。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteSql_ShouldPreserveDecimalPrecision()
    {
        var id = Guid.NewGuid();
        const decimal value = 123456789012.123456m;
        await _sqlExecutor.ExecuteSqlAsync(
            "Insert ParameterSample(ParameterSampleId, DecimalValue) Values(@id, @value)", new { id, value });

        var result = _sqlQuery.Sql<decimal>().Select("DecimalValue").From("ParameterSample")
            .AppendWhere("ParameterSampleId=@id").AddParam("id", id).Scalar();

        Assert.Equal(value, result);
    }

    /// <summary>
    /// 测试 - MySQL应正确写入和读取DateTime。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteSql_ShouldPersistDateTimeParameter()
    {
        var id = Guid.NewGuid();
        var value = new DateTime(2026, 7, 17, 12, 30, 45);
        await _sqlExecutor.ExecuteSqlAsync(
            "Insert ParameterSample(ParameterSampleId, DecimalValue, DateTimeValue) Values(@id, @decimal, @value)",
            new { id, @decimal = 1m, value });

        var result = _sqlQuery.Sql<DateTime>().Select("DateTimeValue").From("ParameterSample")
            .AppendWhere("ParameterSampleId=@id").AddParam("id", id).Scalar();

        Assert.Equal(value, result);
    }

    /// <summary>
    /// 测试 - MySQL存储过程Output参数应可读取。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteProcedure_ShouldExposeOutputParameter()
    {
        var id = Guid.NewGuid();
        await InitProductDataAsync(id, "output-code");
        var parameters = new[]
        {
            new SqlParam("id", id, DbType.String, ParameterDirection.Input, 36),
            new SqlParam("code_output", null, DbType.String, ParameterDirection.Output, 50)
        };

        _sqlExecutor.ExecuteProcedure("Proc_GetProductCode_Output", parameters);

        Assert.Equal("output-code", _sqlExecutor.OutputParameters.GetValue<string>("code_output"));
    }

    /// <summary>
    /// 测试 - MySQL存储过程InputOutput参数应可读取。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public void ExecuteProcedure_ShouldExposeInputOutputParameter()
    {
        var parameters = new[]
        {
            new SqlParam("code_value", "input", DbType.String, ParameterDirection.InputOutput, 50)
        };

        _sqlExecutor.ExecuteProcedure("Proc_AppendProductCode_InputOutput", parameters);

        Assert.Equal("input_output", _sqlExecutor.OutputParameters.GetValue<string>("code_value"));
    }

    /// <summary>
    /// 写入缓冲测试产品。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task SeedProductsAsync()
    {
        await InitProductDataAsync(Guid.NewGuid(), "buffer-one");
        await InitProductDataAsync(Guid.NewGuid(), "buffer-two");
        await InitProductDataAsync(Guid.NewGuid(), "buffer-three");
    }
}