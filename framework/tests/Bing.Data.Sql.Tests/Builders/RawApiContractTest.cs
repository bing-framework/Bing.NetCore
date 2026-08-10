using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 原始 SQL API 合同测试。
/// </summary>
public class RawApiContractTest
{
    /// <summary>
    /// 测试目的：公开流式 API 仅暴露 Append 原始 SQL 入口，不再暴露 Raw 入口。
    /// </summary>
    [Fact]
    public void PublicRawApis_ShouldBeRemoved()
    {
        var extensionMethods = typeof(Extensions).GetMethods(BindingFlags.Public | BindingFlags.Static);

        Assert.Contains(extensionMethods, method => method.Name == "AppendFrom");
        Assert.Contains(extensionMethods, method => method.Name == "AppendJoin");
        Assert.Contains(extensionMethods, method => method.Name == "AppendLeftJoin");
        Assert.Contains(extensionMethods, method => method.Name == "AppendRightJoin");
        Assert.DoesNotContain(extensionMethods, method => method.Name is "FromRaw" or "JoinRaw" or "LeftJoinRaw" or "RightJoinRaw");
        Assert.Null(typeof(IFromClause).GetMethod("FromRaw"));
        Assert.Null(typeof(IJoinClause).GetMethod("JoinRaw"));
        Assert.Null(typeof(IJoinClause).GetMethod("LeftJoinRaw"));
        Assert.Null(typeof(IJoinClause).GetMethod("RightJoinRaw"));
    }

    /// <summary>
    /// 测试目的：字符串表名 API 只能接收表名和可选别名，泛型实体 API 仍保留 schema 参数。
    /// </summary>
    [Fact]
    public void StringTableApis_ShouldExposeOnlyTableAndAlias()
    {
        AssertStringTableSignature(typeof(IFromClause), "From");
        AssertStringTableSignature(typeof(IJoinClause), "Join");
        AssertStringTableSignature(typeof(IJoinClause), "LeftJoin");
        AssertStringTableSignature(typeof(IJoinClause), "RightJoin");
        Assert.Equal(2, typeof(IFromClause).GetMethods().Single(method => method.Name == "From" &&
            method.IsGenericMethodDefinition).GetParameters().Length);
        Assert.All(typeof(IJoinClause).GetMethods().Where(method => method.IsGenericMethodDefinition &&
            method.Name is "Join" or "LeftJoin" or "RightJoin"), method => Assert.Equal(2, method.GetParameters().Length));
    }

    /// <summary>
    /// 测试目的：Fluent 字符串表名扩展只能接收表名和可选别名，Append 不得接收别名。
    /// </summary>
    [Fact]
    public void FluentStringTableApis_ShouldExposeOnlySupportedParameters()
    {
        AssertFluentStringTableSignature("From");
        AssertFluentStringTableSignature("Join");
        AssertFluentStringTableSignature("LeftJoin");
        AssertFluentStringTableSignature("RightJoin");
        AssertAppendSignature("AppendFrom");
        AssertAppendSignature("AppendJoin");
        AssertAppendSignature("AppendLeftJoin");
        AssertAppendSignature("AppendRightJoin");
    }

    /// <summary>
    /// 测试目的：结构化表引用在 Clause、ISqlBuilder 和 IJoin Fluent 层都应支持三种 Join 类型。
    /// </summary>
    [Fact]
    public void StructuredTableReferenceJoinApis_ShouldBeSymmetric()
    {
        AssertStructuredJoinClauseSignature("Join");
        AssertStructuredJoinClauseSignature("LeftJoin");
        AssertStructuredJoinClauseSignature("RightJoin");
        AssertStructuredBuilderExtension("Join");
        AssertStructuredBuilderExtension("LeftJoin");
        AssertStructuredBuilderExtension("RightJoin");
        AssertStructuredFluentExtension("Join");
        AssertStructuredFluentExtension("LeftJoin");
        AssertStructuredFluentExtension("RightJoin");
    }

    /// <summary>
    /// 断言字符串表名接口签名。
    /// </summary>
    /// <param name="type">接口类型。</param>
    /// <param name="methodName">方法名。</param>
    private static void AssertStringTableSignature(Type type, string methodName)
    {
        var method = type.GetMethods().Single(item => item.Name == methodName && item.IsGenericMethod == false &&
            item.GetParameters().FirstOrDefault()?.ParameterType == typeof(string));
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("table", parameters[0].Name);
        Assert.Equal("alias", parameters[1].Name);
    }

    /// <summary>
    /// 断言 Fluent 字符串表名扩展签名。
    /// </summary>
    private static void AssertFluentStringTableSignature(string methodName)
    {
        var method = typeof(Extensions).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(item =>
            item.Name == methodName && item.IsGenericMethodDefinition && item.GetParameters().Length == 3 &&
            item.GetParameters()[1].ParameterType == typeof(string) && item.GetParameters()[1].Name == "table");
        var parameters = method.GetParameters();
        Assert.Equal("table", parameters[1].Name);
        Assert.Equal("alias", parameters[2].Name);
    }

    /// <summary>
    /// 断言 Append 扩展签名没有别名参数。
    /// </summary>
    private static void AssertAppendSignature(string methodName)
    {
        var methods = typeof(Extensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(item => item.Name == methodName && item.IsGenericMethodDefinition).ToList();
        Assert.Equal(2, methods.Count);
        Assert.All(methods, method =>
        {
            var parameters = method.GetParameters();
            Assert.Equal("sql", parameters[1].Name);
            Assert.DoesNotContain(parameters, parameter => parameter.Name == "alias");
        });
    }

    /// <summary>
    /// 断言结构化 Join Clause 签名。
    /// </summary>
    private static void AssertStructuredJoinClauseSignature(string methodName) =>
        Assert.Contains(typeof(IJoinClause).GetMethods(), method => method.Name == methodName &&
            method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(SqlTableReference));

    /// <summary>
    /// 断言 ISqlBuilder 的结构化 Join 扩展签名。
    /// </summary>
    private static void AssertStructuredBuilderExtension(string methodName) =>
        Assert.Contains(typeof(Extensions).GetMethods(BindingFlags.Public | BindingFlags.Static), method =>
            method.Name == methodName && method.IsGenericMethod == false && method.ReturnType == typeof(ISqlBuilder) &&
            method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(ISqlBuilder) &&
            method.GetParameters()[1].ParameterType == typeof(SqlTableReference));

    /// <summary>
    /// 断言 IJoin 的结构化 Fluent 扩展签名。
    /// </summary>
    private static void AssertStructuredFluentExtension(string methodName) =>
        Assert.Contains(typeof(Extensions).GetMethods(BindingFlags.Public | BindingFlags.Static), method =>
            method.Name == methodName && method.IsGenericMethodDefinition && method.GetParameters().Length == 2 &&
            method.GetParameters()[1].ParameterType == typeof(SqlTableReference));
}