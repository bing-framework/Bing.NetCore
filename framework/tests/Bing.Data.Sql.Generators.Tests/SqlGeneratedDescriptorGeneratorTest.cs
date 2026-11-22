using Bing.Data.Sql.Generators;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>固定的查询描述应在调用点复用，避免每次进入生成方法时重新分配。</summary>
    [Fact]
    public void Generate_WhenQueryDescriptorsAreStatic_ShouldNotAllocateThemInsideInterceptors()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class DescriptorRow
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            public static class DescriptorConsumer
            {
                public static SqlLambdaQuery Where(SqlLambdaQuery q, int value) =>
                    q.Where<DescriptorRow, int>(row => row.Id, value);
                public static SqlLambdaQuery Optional(SqlLambdaQuery q, bool enabled, int value) =>
                    q.WhereIf<DescriptorRow>(enabled, row => row.Id, value);
                public static SqlLambdaQuery Projection(SqlLambdaQuery q) =>
                    q.Select<DescriptorRow>(row => new { row.Id, row.Name });
                public static SqlLambdaQuery Group(SqlLambdaQuery q) =>
                    q.GroupBy<DescriptorRow>(row => new { row.Id, row.Name });
                public static SqlLambdaQuery Having(SqlLambdaQuery q) =>
                    q.Having<DescriptorRow>(row => SqlFn.Count(row.Id) > 1);
                public static SqlLambdaQuery Order(SqlLambdaQuery q) =>
                    q.OrderBy<DescriptorRow>(row => row.Id);
            }
            """;

        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");

        var methods = CSharpSyntaxTree.ParseText(result.GeneratedSource).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText.StartsWith("Call_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(6, methods.Length);
        foreach (var method in methods)
        {
            Assert.DoesNotContain(method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>(),
                node => node.Type.ToString().EndsWith("SqlGeneratedColumn", StringComparison.Ordinal));
            Assert.DoesNotContain(method.DescendantNodes().OfType<ArrayCreationExpressionSyntax>(),
                node => node.Type.ElementType.ToString() is "global::System.Type" or "string" or
                    "global::Bing.Data.Sql.SqlGeneratedColumn");
        }
    }

    /// <summary>DTO 原型终结点应复用编译期固定的投影成员，而不是每次创建数组。</summary>
    [Fact]
    public void Generate_WhenPrototypeTerminalsAreUsed_ShouldReuseProjectionMembers()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Bing.Data.Sql;
            public sealed class ResultRow { public int Id { get; set; } public string Name { get; set; } }
            public static class PrototypeConsumer
            {
                public static List<ResultRow> List(SqlLambdaQuery q) => q.ToList(new ResultRow());
                public static ResultRow One(SqlLambdaQuery q) => q.To(new ResultRow());
                public static Task<List<ResultRow>> ListAsync(SqlLambdaQuery q) => q.ToListAsync(new ResultRow());
                public static Task<ResultRow> OneAsync(SqlLambdaQuery q) => q.ToAsync(new ResultRow());
            }
            """;

        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        var root = CSharpSyntaxTree.ParseText(result.GeneratedSource).GetRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText.StartsWith("Call_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, methods.Length);
        foreach (var method in methods)
        {
            Assert.DoesNotContain(method.DescendantNodes().OfType<ArrayCreationExpressionSyntax>(),
                node => node.Type.ElementType.ToString() == "string");
            Assert.Contains(".ProjectionMembers", method.ToString(), StringComparison.Ordinal);
        }
        var fields = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(field => field.Identifier.ValueText == "ProjectionMembers").ToArray();
        Assert.Equal(4, fields.Length);
        Assert.All(fields, field => Assert.Equal("new string[]{\"Id\",\"Name\"}", field.Initializer?.Value.ToString()));
    }
}
