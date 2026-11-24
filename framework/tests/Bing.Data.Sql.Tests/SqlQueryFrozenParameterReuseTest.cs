using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>验证冻结查询派生时复用内部参数，并隔离公开参数副本。</summary>
public sealed class SqlQueryFrozenParameterReuseTest
{
    [Fact]
    public void SqlAndCountSnapshots_ShouldReuseUnchangedFrozenParameters()
    {
        var source = CreatePlan(hasPage: true);
        var snapshot = source.ExecutionSnapshot;
        var payload = snapshot.FrozenParameters.Single(parameter => parameter.Name == "@payload");
        var pageNames = snapshot.SelectShape;

        var sql = snapshot.WithSql("Select [Name] \r\nFrom [replacement]");
        var count = snapshot.WithoutPage(
            "Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload",
            pageNames.OffsetParameter, pageNames.LimitParameter);

        Assert.Equal("Select [Name] \r\nFrom [replacement]", sql.Sql);
        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", count.Sql);
        var sqlPayload = sql.FrozenParameters.Single(parameter => parameter.Name == "@payload");
        var countPayload = Assert.Single(count.FrozenParameters);
        Assert.Same(payload, sqlPayload);
        Assert.Same(payload, countPayload);
        Assert.Same(payload.Value, sqlPayload.Value);
        Assert.Same(payload.Value, countPayload.Value);
        Assert.Same(PayloadBytes(payload), PayloadBytes(sqlPayload));
        Assert.Same(PayloadBytes(payload), PayloadBytes(countPayload));
        Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(count.FrozenParameters.Single()));
        Assert.Equal("@_p_0", pageNames.OffsetParameter);
        Assert.Equal("@_p_1", pageNames.LimitParameter);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\n" +
            "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only", source.CommandText);
    }

    [Fact]
    public void DebugSnapshot_ShouldReuseFrozenParameters()
    {
        var source = CreatePlan(hasPage: false, includePredicate: false).ExecutionSnapshot;
        var parameter = Assert.Single(source.FrozenParameters);

        var debug = source.WithDebugSql();

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name]", debug.Sql);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name]", debug.DebugSql);
        Assert.True(debug.HasDebugSql);
        var debugParameter = Assert.Single(debug.FrozenParameters);
        Assert.Same(parameter, debugParameter);
        Assert.Same(parameter.Value, debugParameter.Value);
        Assert.Same(PayloadBytes(parameter), PayloadBytes(debugParameter));
    }

    [Fact]
    public void WithoutPage_WhenSourceHasNoPage_ShouldReuseTheFrozenParameterCollection()
    {
        var source = CreatePlan(hasPage: false).ExecutionSnapshot;

        var count = source.WithoutPage("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload",
            source.SelectShape.OffsetParameter, source.SelectShape.LimitParameter);

        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", count.Sql);
        Assert.Same(source.FrozenParameters, count.FrozenParameters);
        Assert.Same(Assert.Single(source.FrozenParameters), Assert.Single(count.FrozenParameters));
    }

    [Fact]
    public void FirstPageSnapshot_ShouldReuseExistingParametersAndCreateIndependentPageSlots()
    {
        var source = CreatePlan(hasPage: false);
        var snapshot = source.ExecutionSnapshot;
        var parameter = snapshot.FrozenParameters.Single(parameter => parameter.Name == "@payload");

        var first = snapshot.WithPage(new Pager(3, 4));
        var second = snapshot.WithPage(new Pager(4, 2));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\n" +
            "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only", first.Sql);
        Assert.Same(parameter, first.FrozenParameters.Single(item => item.Name == "@payload"));
        Assert.Same(parameter, second.FrozenParameters.Single(item => item.Name == "@payload"));
        Assert.Same(parameter.Value, first.FrozenParameters.Single(item => item.Name == "@payload").Value);
        Assert.Same(parameter.Value, second.FrozenParameters.Single(item => item.Name == "@payload").Value);
        var firstPageParameters = first.FrozenParameters.Where(item => item.Name != "@payload").ToArray();
        var secondPageParameters = second.FrozenParameters.Where(item => item.Name != "@payload").ToArray();
        Assert.Equal(new object[] { 8, 4 }, firstPageParameters.Select(item => item.Value));
        Assert.Equal(new object[] { 6, 2 }, secondPageParameters.Select(item => item.Value));
        Assert.Equal(2, firstPageParameters.Length);
        Assert.Equal(2, secondPageParameters.Length);
        Assert.All(firstPageParameters, item => Assert.DoesNotContain(item, secondPageParameters));
        Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(parameter));
    }

    [Fact]
    public void RepeatedPageSnapshot_ShouldReplaceOnlyPageParameters()
    {
        var source = CreatePlan(hasPage: true);
        var snapshot = source.ExecutionSnapshot;
        var payload = snapshot.FrozenParameters.Single(parameter => parameter.Name == "@payload");
        var originalPageParameters = snapshot.FrozenParameters.Where(parameter => parameter.Name != "@payload").ToArray();

        var page = snapshot.WithPage(new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\n" +
            "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.Sql);
        Assert.Same(payload, page.FrozenParameters.Single(parameter => parameter.Name == "@payload"));
        var newPageParameters = page.FrozenParameters.Where(parameter => parameter.Name != "@payload").ToArray();
        Assert.Equal(new object[] { 8, 4 }, newPageParameters.Select(parameter => parameter.Value));
        Assert.Equal(originalPageParameters.Length, newPageParameters.Length);
        Assert.All(newPageParameters, item => Assert.DoesNotContain(item, originalPageParameters));
        Assert.Same(payload.Value, page.FrozenParameters.Single(item => item.Name == "@payload").Value);
    }

    [Fact]
    public void PublicParameterSnapshots_ShouldIsolateMetadataAndNestedValuesAcrossPlans()
    {
        var source = CreatePlan(hasPage: false);
        var sourceSnapshot = source.ExecutionSnapshot;
        var pageSnapshot = sourceSnapshot.WithPage(new Pager(3, 4));
        var page = SqlQueryPlan.Create(source.QueryIr.WithSnapshot(pageSnapshot), source.SplitOn);
        var count = SqlQueryPlan.Create(source.QueryIr.WithSnapshot(
            sourceSnapshot.WithoutPage("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", "", "")), source.SplitOn);

        var sourceParameters = Assert.IsType<SqlParam[]>(source.Parameters);
        var pageParameters = Assert.IsType<SqlParam[]>(page.Parameters);
        var countParameters = Assert.IsType<SqlParam[]>(count.Parameters);
        var publicSnapshotParameters = sourceSnapshot.Parameters.ToArray();

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name]", source.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\n" +
            "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", count.CommandText);

        MutatePublicParameter(sourceParameters.Single());
        MutatePublicParameter(pageParameters.Single(parameter => parameter.Name == "@payload"));
        MutatePublicParameter(countParameters.Single());
        MutatePublicParameter(publicSnapshotParameters.Single());

        Assert.Equal(SqlParameterSource.Basic, Assert.Single(sourceSnapshot.FrozenParameters).Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, Assert.Single(sourceSnapshot.FrozenParameters).MetadataLevel);
        Assert.True(Assert.Single(sourceSnapshot.FrozenParameters).IsProviderValue);
        Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(Assert.Single(sourceSnapshot.FrozenParameters)));
        Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(Assert.Single(pageSnapshot.FrozenParameters, item => item.Name == "@payload")));
        Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(Assert.Single(count.QueryIr.Snapshot.FrozenParameters)));
        Assert.Equal(new byte[] { 3, 4 }, OriginalBytes(Assert.Single(sourceSnapshot.FrozenParameters)));
        Assert.Equal(new byte[] { 3, 4 }, OriginalBytes(Assert.Single(pageSnapshot.FrozenParameters, item => item.Name == "@payload")));
        Assert.Equal(new byte[] { 3, 4 }, OriginalBytes(Assert.Single(count.QueryIr.Snapshot.FrozenParameters)));
    }

    [Fact]
    public async Task ConcurrentPageAndCountDerivation_ShouldIsolatePublicNestedMutations()
    {
        var source = CreatePlan(hasPage: false);
        var sourceSnapshot = source.ExecutionSnapshot;
        var sourceParameter = Assert.Single(sourceSnapshot.FrozenParameters);

        await Task.WhenAll(Enumerable.Range(1, 16).Select(pageNumber => Task.Run(() =>
        {
            var pageSnapshot = sourceSnapshot.WithPage(new Pager(pageNumber, 2));
            var countSnapshot = sourceSnapshot.WithoutPage(
                "Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", null, null);
            var page = SqlQueryPlan.Create(source.QueryIr.WithSnapshot(pageSnapshot), source.SplitOn);
            var count = SqlQueryPlan.Create(source.QueryIr.WithSnapshot(countSnapshot), source.SplitOn);
            var pageParameters = Assert.IsType<SqlParam[]>(page.Parameters);
            var countParameters = Assert.IsType<SqlParam[]>(count.Parameters);

            MutatePublicParameter(Assert.Single(pageParameters, parameter => parameter.Name == "@payload"), pageNumber);
            MutatePublicParameter(Assert.Single(countParameters), pageNumber);

            Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(Assert.Single(pageSnapshot.FrozenParameters, parameter => parameter.Name == "@payload")));
            Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(Assert.Single(countSnapshot.FrozenParameters)));
            Assert.Equal(new byte[] { 3, 4 }, OriginalBytes(Assert.Single(pageSnapshot.FrozenParameters, parameter => parameter.Name == "@payload")));
            Assert.Equal(new byte[] { 3, 4 }, OriginalBytes(Assert.Single(countSnapshot.FrozenParameters)));
            Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", count.CommandText);
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\n" +
                "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        })));

        Assert.Same(sourceParameter, Assert.Single(sourceSnapshot.FrozenParameters));
        Assert.Equal(SqlParameterSource.Basic, sourceParameter.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, sourceParameter.MetadataLevel);
        Assert.True(sourceParameter.IsProviderValue);
        Assert.Equal(new byte[] { 1, 2 }, PayloadBytes(sourceParameter));
        Assert.Equal(new byte[] { 3, 4 }, OriginalBytes(sourceParameter));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name]", source.CommandText);
    }

    private static SqlQueryPlan CreatePlan(bool hasPage, bool includePredicate = true)
    {
        var builder = new TestSqlBuilder().Select("Name").From("samples");
        var payload = new Dictionary<string, object> { ["binary"] = new byte[] { 1, 2 } };
        var parameters = Assert.IsAssignableFrom<IAdvancedParameterManager>(
            ((ISqlCommonPartAccessor)builder).ParameterManager);
        parameters.Add(new SqlParam("@payload", payload, DbType.Object)
        {
            Source = SqlParameterSource.Basic,
            MetadataLevel = SqlParameterMetadataLevel.Full,
            OriginalValue = new Dictionary<string, object> { ["binary"] = new byte[] { 3, 4 } },
            IsProviderValue = true
        });
        if (includePredicate)
            builder.AppendWhere("[Payload]=@payload");
        builder.OrderBy("Name");
        if (hasPage)
            builder.Skip(5).Take(2);

        var plan = SqlQueryPlan.Create(builder);
        ((byte[])payload["binary"])[0] = 9;
        return plan;
    }

    private static byte[] PayloadBytes(SqlParam parameter) =>
        Assert.IsType<byte[]>(Assert.IsType<Dictionary<string, object>>(parameter.Value)["binary"]);

    private static byte[] OriginalBytes(SqlParam parameter) =>
        Assert.IsType<byte[]>(Assert.IsType<Dictionary<string, object>>(parameter.OriginalValue)["binary"]);

    private static void MutatePublicParameter(SqlParam parameter, int mutation = 7)
    {
        parameter.Source = SqlParameterSource.Unknown;
        parameter.MetadataLevel = SqlParameterMetadataLevel.None;
        var value = Assert.IsType<Dictionary<string, object>>(parameter.Value);
        Assert.IsType<byte[]>(value["binary"])[0] = (byte)mutation;
        parameter.Value = value;
        var original = Assert.IsType<Dictionary<string, object>>(parameter.OriginalValue);
        Assert.IsType<byte[]>(original["binary"])[0] = (byte)mutation;
        parameter.OriginalValue = original;
    }
}
