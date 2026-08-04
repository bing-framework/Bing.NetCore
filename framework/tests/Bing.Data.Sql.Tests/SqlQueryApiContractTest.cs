using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 独立 SQL 查询公开契约单元测试。
/// </summary>
public class SqlQueryApiContractTest
{
    /// <summary>
    /// 测试目的：Root 查询只负责资源和描述创建，不得再次公开 Fluent 操作集合或 Builder 逃逸入口。
    /// </summary>
    [Fact]
    public void RootQuery_WhenPublicApiInspected_ShouldNotExposeFluentOperationOrBuilder()
    {
        // Arrange
        var type = typeof(ISqlQuery);

        // Assert
        Assert.False(typeof(ISqlQueryOperation).IsAssignableFrom(type));
        Assert.DoesNotContain(type.GetInterfaces(), item => item.Name == "ISqlOptions");
        Assert.DoesNotContain(typeof(ISqlExecutor).GetInterfaces(), item => item.Name == "ISqlOptions");
        Assert.Null(type.GetProperty("SqlBuilder"));
        Assert.Null(type.GetMethod("GetBuilder"));
    }

    /// <summary>
    /// 测试目的：独立查询描述应公开受控 SQL 输出，而不是公开可变 Builder。
    /// </summary>
    [Fact]
    public void FluentDescription_WhenPublicApiInspected_ShouldNotExposeBuilder()
    {
        Assert.Null(typeof(SqlQuery<>).GetMethod("GetBuilder"));
        Assert.NotNull(typeof(SqlQuery<>).GetMethod("ToSql"));
    }

    /// <summary>
    /// 测试目的：查询描述不得通过方言、参数、CTE 或 Union 访问器泄露可变 Builder 状态。
    /// </summary>
    [Fact]
    public void FluentDescription_WhenPublicApiInspected_ShouldNotExposeNonClauseBuilderAccessors()
    {
        // Arrange
        var restrictedInterfaces = new[]
        {
            typeof(Bing.Data.Sql.Builders.ISqlCommonPartAccessor),
            typeof(Bing.Data.Sql.Builders.Params.IGetParameter),
            typeof(Bing.Data.Sql.Builders.Params.IClearParameters),
            typeof(Bing.Data.Sql.Builders.IUnionAccessor),
            typeof(Bing.Data.Sql.Builders.ICteAccessor),
            typeof(Bing.Data.Sql.Builders.ISqlQueryClauseAccessor)
        };

        // Act
        var typedInterfaces = typeof(SqlQuery<>).GetInterfaces();

        // Assert
        Assert.DoesNotContain(typedInterfaces, type => restrictedInterfaces.Contains(type));
    }

    /// <summary>
    /// 测试目的：根查询只应公开类型化 Fluent 和原生文本查询入口，避免无类型描述绕开结果映射约束。
    /// </summary>
    [Fact]
    public void Sql_WhenPublicApiInspected_ShouldExposeTypedInstanceQueryEntryPoints()
    {
        // Arrange
        var methods = typeof(ISqlQuery).GetMethods();

        // Act
        var typed = methods.Single(method => method.Name == "Sql" &&
                                            method.IsGenericMethodDefinition &&
                                            method.GetParameters().Length == 0);
        var raw = methods.Single(method => method.Name == "Sql" &&
                                          method.IsGenericMethodDefinition &&
                                          method.GetParameters().Length == 2);

        // Assert
        Assert.Equal(typeof(SqlQuery<>), typed.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlTextQuery<>), raw.ReturnType.GetGenericTypeDefinition());
        Assert.True(typeof(SqlQuery<>).IsPublic);
        Assert.True(typeof(SqlTextQuery<>).IsPublic);
        Assert.DoesNotContain(methods, method => method.Name == "Sql" &&
                            method.IsGenericMethod == false &&
                            method.GetParameters().Length == 0);
        Assert.Empty(typeof(SqlQueryExtensions).GetMethods()
            .Where(method => method.Name == "Sql" && method.IsStatic));
    }

    /// <summary>
    /// 测试目的：指定结果类型的 Fluent 查询应直接公开自身契约，不应通过公开的无类型描述继承成员。
    /// </summary>
    [Fact]
    public void TypedFluentQuery_WhenPublicApiInspected_ShouldNotInheritPublicUntypedDescription()
    {
        Assert.Equal(typeof(object), typeof(SqlQuery<>).BaseType);
    }

    /// <summary>
    /// 测试目的：根查询应公开参数化插值 SQL 入口，避免调用方手工拼接参数值。
    /// </summary>
    [Fact]
    public void SqlInterpolated_WhenPublicApiInspected_ShouldExposeTypedEntryPoint()
    {
        var method = typeof(ISqlQuery).GetMethod("SqlInterpolated");

        Assert.NotNull(method);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(FormattableString), method.GetParameters().Single().ParameterType);
        Assert.Equal(typeof(SqlTextQuery<>), method.ReturnType.GetGenericTypeDefinition());
    }

    /// <summary>
    /// 测试目的：根查询应公开指定结果类型的存储过程描述入口，避免继续依赖旧过程查询终结方法。
    /// </summary>
    [Fact]
    public void Procedure_WhenPublicApiInspected_ShouldExposeTypedEntryPoint()
    {
        // Arrange
        var method = typeof(ISqlQuery).GetMethod("Procedure");

        // Act
        var parameters = method?.GetParameters();

        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(SqlProcedureQuery<>), method.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(new[] { typeof(string), typeof(object) }, parameters.Select(parameter => parameter.ParameterType));
        Assert.True(parameters[1].HasDefaultValue);
        Assert.True(typeof(SqlProcedureQuery<>).IsPublic);
        Assert.True(typeof(SqlProcedureQuery<>).IsSealed);
    }

    /// <summary>
    /// 测试目的：根查询应公开使用实体映射初始化的 Lambda 查询入口。
    /// </summary>
    [Fact]
    public void Lambda_WhenPublicApiInspected_ShouldExposeTypedEntryPoint()
    {
        var method = typeof(ISqlQuery).GetMethod("Lambda");

        Assert.NotNull(method);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(SqlLambdaQuery<>), method.ReturnType.GetGenericTypeDefinition());
        Assert.True(method.GetGenericArguments().Single().GenericParameterAttributes
            .HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint));
    }

    /// <summary>
    /// 测试目的：Lambda 查询应公开受控的类型化连接、分组、投影和聚合成员，并通过 SqlQuery 切换结果映射类型。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicApiInspected_ShouldExposeTypedCompositionAndResultTransitions()
    {
        // Arrange
        var lambdaType = typeof(SqlLambdaQuery<>);

        // Act
        var join = lambdaType.GetMethods().Single(method => method.Name == "Join" && method.IsGenericMethodDefinition);
        var leftJoin = lambdaType.GetMethods().Single(method => method.Name == "LeftJoin" && method.IsGenericMethodDefinition);
        var on = lambdaType.GetMethods().Single(method => method.Name == "On" && method.IsGenericMethodDefinition);
        var projection = lambdaType.GetMethods().Single(method => method.Name == "Select" && method.IsGenericMethodDefinition);
        var aggregate = lambdaType.GetMethods().Single(method => method.Name == "Aggregate" && method.IsGenericMethodDefinition);

        // Assert
        Assert.Equal(typeof(SqlLambdaQuery<>), join.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<>), leftJoin.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<>), on.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlQuery<>), projection.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlQuery<>), aggregate.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(2, lambdaType.GetMethods().Count(method => method.Name == "GroupBy" && method.DeclaringType == lambdaType));
    }

    /// <summary>
    /// 测试目的：Lambda 查询应公开替换投影、显式追加投影和跨实体布尔筛选能力，避免调用方依赖隐式追加语义。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicApiInspected_ShouldExposeCrossEntityCompositionMembers()
    {
        // Arrange
        var lambdaType = typeof(SqlLambdaQuery<>);

        // Act
        var clearSelect = lambdaType.GetMethod("ClearSelect");
        var selectFrom = lambdaType.GetMethods().Single(method => method.Name == "SelectFrom" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1);
        var appendSelectFrom = lambdaType.GetMethods().Single(method => method.Name == "AppendSelectFrom" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1);
        var whereFrom = lambdaType.GetMethods().Single(method => method.Name == "WhereFrom" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1);

        // Assert
        Assert.NotNull(clearSelect);
        Assert.Equal(typeof(SqlLambdaQuery<>), clearSelect.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<>), selectFrom.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<>), appendSelectFrom.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<>), whereFrom.ReturnType.GetGenericTypeDefinition());
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开与异步终结方法对称的同步执行方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeSynchronousTerminals()
    {
        // Arrange
        var expectedMethodNames = new[] { "ToList", "First", "FirstOrDefault", "Single", "SingleOrDefault", "Scalar" };
        var fluentMethods = typeof(SqlQuery<>).GetMethods();
        var textMethods = typeof(SqlTextQuery<>).GetMethods();

        // Act
        var fluentTerminals = fluentMethods.Where(method => method.DeclaringType == typeof(SqlQuery<>) &&
            method.IsGenericMethod == false && expectedMethodNames.Contains(method.Name)).ToList();
        var textTerminals = textMethods.Where(method => method.DeclaringType == typeof(SqlTextQuery<>) &&
            method.IsGenericMethod == false && expectedMethodNames.Contains(method.Name)).ToList();

        // Assert
        Assert.Equal(expectedMethodNames.Length, fluentTerminals.Count);
        Assert.Equal(expectedMethodNames.Length, textTerminals.Count);
        Assert.All(fluentTerminals, method => Assert.Equal(typeof(int?), method.GetParameters().Single().ParameterType));
        Assert.All(textTerminals, method => Assert.Equal(typeof(int?), method.GetParameters().Single().ParameterType));
        Assert.Equal(typeof(List<>), fluentTerminals.Single(method => method.Name == "ToList").ReturnType
            .GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textTerminals.Single(method => method.Name == "ToList").ReturnType
            .GetGenericTypeDefinition());
    }

    /// <summary>
    /// 测试目的：查询描述应公开可选超时和取消令牌的异步标量终结方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeAsynchronousScalarTerminals()
    {
        // Arrange
        var expectedParameterTypes = new[] { typeof(int?), typeof(CancellationToken) };

        // Act
        var fluent = typeof(SqlQuery<>).GetMethod("ScalarAsync");
        var text = typeof(SqlTextQuery<>).GetMethod("ScalarAsync");

        // Assert
        Assert.NotNull(fluent);
        Assert.NotNull(text);
        Assert.Equal(typeof(Task<>), fluent.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), text.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedParameterTypes, fluent.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(expectedParameterTypes, text.GetParameters().Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// 测试目的：结构化 Fluent 查询描述应公开同步和异步分页终结方法，且 Lambda 描述通过继承获得相同能力。
    /// </summary>
    [Fact]
    public void FluentQueryDescriptions_WhenPublicApiInspected_ShouldExposePagingTerminals()
    {
        // Arrange
        var expectedSyncParameters = new[] { typeof(Bing.Data.IPager), typeof(int?) };
        var expectedAsyncParameters = new[] { typeof(Bing.Data.IPager), typeof(int?), typeof(CancellationToken) };

        // Act
        var sync = typeof(SqlQuery<>).GetMethod("ToPage");
        var async = typeof(SqlQuery<>).GetMethod("ToPageAsync");
        var lambdaSync = typeof(SqlLambdaQuery<>).GetMethod("ToPage");
        var lambdaAsync = typeof(SqlLambdaQuery<>).GetMethod("ToPageAsync");

        // Assert
        Assert.NotNull(sync);
        Assert.NotNull(async);
        Assert.NotNull(lambdaSync);
        Assert.NotNull(lambdaAsync);
        Assert.Equal(typeof(Bing.Data.PagerList<>), sync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), async.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameters, sync.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(expectedAsyncParameters, async.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(Bing.Data.PagerList<>), lambdaSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), lambdaAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameters, lambdaSync.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(expectedAsyncParameters, lambdaAsync.GetParameters().Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开可选超时的同步流终结方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeSynchronousStreamingTerminals()
    {
        // Arrange
        var expectedParameterType = typeof(int?);

        // Act
        var fluent = typeof(SqlQuery<>).GetMethod("AsEnumerable");
        var text = typeof(SqlTextQuery<>).GetMethod("AsEnumerable");

        // Assert
        Assert.NotNull(fluent);
        Assert.NotNull(text);
        Assert.Equal(typeof(IEnumerable<>), fluent.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(IEnumerable<>), text.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedParameterType, fluent.GetParameters().Single().ParameterType);
        Assert.Equal(expectedParameterType, text.GetParameters().Single().ParameterType);
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开双对象多映射的同步和异步物化方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeTwoTypeMappingTerminals()
    {
        // Arrange
        var expectedSyncParameterTypes = new[] { typeof(Func<,,>), typeof(Nullable<>) };
        var expectedAsyncParameterTypes = new[] { typeof(Func<,,>), typeof(Nullable<>), typeof(CancellationToken) };

        // Act
        var fluentSync = GetTwoTypeMappingMethod(typeof(SqlQuery<>), "ToList");
        var textSync = GetTwoTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetTwoTypeMappingMethod(typeof(SqlQuery<>), "ToListAsync");
        var textAsync = GetTwoTypeMappingMethod(typeof(SqlTextQuery<>), "ToListAsync");

        // Assert
        Assert.Equal(typeof(List<>), fluentSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), fluentAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), textAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameterTypes, fluentSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedSyncParameterTypes, textSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, fluentAsync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, textAsync.GetParameters().Select(GetGenericTypeDefinition));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应允许配置 Dapper 多映射分段列。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeSplitOnConfiguration()
    {
        // Arrange
        var expectedParameterType = typeof(string);

        // Act
        var fluent = typeof(SqlQuery<>).GetMethod("SplitOn");
        var text = typeof(SqlTextQuery<>).GetMethod("SplitOn");

        // Assert
        Assert.NotNull(fluent);
        Assert.NotNull(text);
        Assert.Equal(typeof(SqlQuery<>), fluent.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlTextQuery<>), text.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedParameterType, fluent.GetParameters().Single().ParameterType);
        Assert.Equal(expectedParameterType, text.GetParameters().Single().ParameterType);
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开三对象多映射的同步和异步物化方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeThreeTypeMappingTerminals()
    {
        // Arrange
        var expectedSyncParameterTypes = new[] { typeof(Func<,,,>), typeof(Nullable<>) };
        var expectedAsyncParameterTypes = new[] { typeof(Func<,,,>), typeof(Nullable<>), typeof(CancellationToken) };

        // Act
        var fluentSync = GetThreeTypeMappingMethod(typeof(SqlQuery<>), "ToList");
        var textSync = GetThreeTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetThreeTypeMappingMethod(typeof(SqlQuery<>), "ToListAsync");
        var textAsync = GetThreeTypeMappingMethod(typeof(SqlTextQuery<>), "ToListAsync");

        // Assert
        Assert.Equal(typeof(List<>), fluentSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), fluentAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), textAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameterTypes, fluentSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedSyncParameterTypes, textSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, fluentAsync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, textAsync.GetParameters().Select(GetGenericTypeDefinition));
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开四对象多映射的同步和异步物化方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeFourTypeMappingTerminals()
    {
        // Arrange
        var expectedSyncParameterTypes = new[] { typeof(Func<,,,,>), typeof(Nullable<>) };
        var expectedAsyncParameterTypes = new[] { typeof(Func<,,,,>), typeof(Nullable<>), typeof(CancellationToken) };

        // Act
        var fluentSync = GetFourTypeMappingMethod(typeof(SqlQuery<>), "ToList");
        var textSync = GetFourTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetFourTypeMappingMethod(typeof(SqlQuery<>), "ToListAsync");
        var textAsync = GetFourTypeMappingMethod(typeof(SqlTextQuery<>), "ToListAsync");

        // Assert
        Assert.Equal(typeof(List<>), fluentSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), fluentAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), textAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameterTypes, fluentSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedSyncParameterTypes, textSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, fluentAsync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, textAsync.GetParameters().Select(GetGenericTypeDefinition));
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开五对象多映射的同步和异步物化方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeFiveTypeMappingTerminals()
    {
        // Arrange
        var expectedSyncParameterTypes = new[] { typeof(Func<,,,,,>), typeof(Nullable<>) };
        var expectedAsyncParameterTypes = new[] { typeof(Func<,,,,,>), typeof(Nullable<>), typeof(CancellationToken) };

        // Act
        var fluentSync = GetFiveTypeMappingMethod(typeof(SqlQuery<>), "ToList");
        var textSync = GetFiveTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetFiveTypeMappingMethod(typeof(SqlQuery<>), "ToListAsync");
        var textAsync = GetFiveTypeMappingMethod(typeof(SqlTextQuery<>), "ToListAsync");

        // Assert
        Assert.Equal(typeof(List<>), fluentSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), fluentAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), textAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameterTypes, fluentSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedSyncParameterTypes, textSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, fluentAsync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, textAsync.GetParameters().Select(GetGenericTypeDefinition));
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开六对象多映射的同步和异步物化方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeSixTypeMappingTerminals()
    {
        // Arrange
        var expectedSyncParameterTypes = new[] { typeof(Func<,,,,,,>), typeof(Nullable<>) };
        var expectedAsyncParameterTypes = new[] { typeof(Func<,,,,,,>), typeof(Nullable<>), typeof(CancellationToken) };

        // Act
        var fluentSync = GetSixTypeMappingMethod(typeof(SqlQuery<>), "ToList");
        var textSync = GetSixTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetSixTypeMappingMethod(typeof(SqlQuery<>), "ToListAsync");
        var textAsync = GetSixTypeMappingMethod(typeof(SqlTextQuery<>), "ToListAsync");

        // Assert
        Assert.Equal(typeof(List<>), fluentSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), fluentAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), textAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameterTypes, fluentSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedSyncParameterTypes, textSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, fluentAsync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, textAsync.GetParameters().Select(GetGenericTypeDefinition));
    }

    /// <summary>
    /// 测试目的：指定结果类型和原生文本查询描述应公开七对象多映射的同步和异步物化方法。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldExposeSevenTypeMappingTerminals()
    {
        // Arrange
        var expectedSyncParameterTypes = new[] { typeof(Func<,,,,,,,>), typeof(Nullable<>) };
        var expectedAsyncParameterTypes = new[] { typeof(Func<,,,,,,,>), typeof(Nullable<>), typeof(CancellationToken) };

        // Act
        var fluentSync = GetSevenTypeMappingMethod(typeof(SqlQuery<>), "ToList");
        var textSync = GetSevenTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetSevenTypeMappingMethod(typeof(SqlQuery<>), "ToListAsync");
        var textAsync = GetSevenTypeMappingMethod(typeof(SqlTextQuery<>), "ToListAsync");

        // Assert
        Assert.Equal(typeof(List<>), fluentSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(List<>), textSync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), fluentAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), textAsync.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(expectedSyncParameterTypes, fluentSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedSyncParameterTypes, textSync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, fluentAsync.GetParameters().Select(GetGenericTypeDefinition));
        Assert.Equal(expectedAsyncParameterTypes, textAsync.GetParameters().Select(GetGenericTypeDefinition));
    }

    /// <summary>
    /// 获取指定声明类型中的双对象多映射方法。
    /// </summary>
    /// <param name="type">公开查询描述类型。</param>
    /// <param name="methodName">方法名称。</param>
    /// <returns>双对象多映射方法。</returns>
    private static System.Reflection.MethodInfo GetTwoTypeMappingMethod(Type type, string methodName) =>
        type.GetMethods().Single(method => method.Name == methodName && method.DeclaringType == type &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2);

    /// <summary>
    /// 获取指定声明类型中的三对象多映射方法。
    /// </summary>
    /// <param name="type">公开查询描述类型。</param>
    /// <param name="methodName">方法名称。</param>
    /// <returns>三对象多映射方法。</returns>
    private static System.Reflection.MethodInfo GetThreeTypeMappingMethod(Type type, string methodName) =>
        type.GetMethods().Single(method => method.Name == methodName && method.DeclaringType == type &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 3);

    /// <summary>
    /// 获取指定声明类型中的四对象多映射方法。
    /// </summary>
    /// <param name="type">公开查询描述类型。</param>
    /// <param name="methodName">方法名称。</param>
    /// <returns>四对象多映射方法。</returns>
    private static System.Reflection.MethodInfo GetFourTypeMappingMethod(Type type, string methodName) =>
        type.GetMethods().Single(method => method.Name == methodName && method.DeclaringType == type &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 4);

    /// <summary>
    /// 获取指定声明类型中的五对象多映射方法。
    /// </summary>
    /// <param name="type">公开查询描述类型。</param>
    /// <param name="methodName">方法名称。</param>
    /// <returns>五对象多映射方法。</returns>
    private static System.Reflection.MethodInfo GetFiveTypeMappingMethod(Type type, string methodName) =>
        type.GetMethods().Single(method => method.Name == methodName && method.DeclaringType == type &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 5);

    /// <summary>
    /// 获取指定声明类型中的六对象多映射方法。
    /// </summary>
    /// <param name="type">公开查询描述类型。</param>
    /// <param name="methodName">方法名称。</param>
    /// <returns>六对象多映射方法。</returns>
    private static System.Reflection.MethodInfo GetSixTypeMappingMethod(Type type, string methodName) =>
        type.GetMethods().Single(method => method.Name == methodName && method.DeclaringType == type &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 6);

    /// <summary>
    /// 获取指定声明类型中的七对象多映射方法。
    /// </summary>
    /// <param name="type">公开查询描述类型。</param>
    /// <param name="methodName">方法名称。</param>
    /// <returns>七对象多映射方法。</returns>
    private static System.Reflection.MethodInfo GetSevenTypeMappingMethod(Type type, string methodName) =>
        type.GetMethods().Single(method => method.Name == methodName && method.DeclaringType == type &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 7);

    /// <summary>
    /// 获取参数类型的泛型定义；非泛型类型直接返回自身。
    /// </summary>
    /// <param name="parameter">方法参数信息。</param>
    /// <returns>泛型定义或原始参数类型。</returns>
    private static Type GetGenericTypeDefinition(System.Reflection.ParameterInfo parameter) =>
        parameter.ParameterType.IsGenericType ? parameter.ParameterType.GetGenericTypeDefinition() : parameter.ParameterType;
}