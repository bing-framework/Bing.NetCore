using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 独立 SQL 查询公开契约单元测试。
/// </summary>
public class SqlQueryApiContractTest
{
    /// <summary>
    /// 测试 - 当查询未实现运行时绑定控制器时，资源绑定入口应明确拒绝，避免使用全局注册表隐式绑定资源。
    /// </summary>
    [Fact]
    public void RuntimeBinding_WhenQueryDoesNotImplementController_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var query = new UnsupportedRuntimeBindingQuery();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlQueryRuntimeBinding.BindDatabaseContext(query, new DatabaseContext()));

        // Assert
        Assert.Equal("当前 SQL 查询对象不支持框架运行时资源绑定。", exception.Message);
    }

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
        Assert.Null(type.GetProperty("ContextId"));
        Assert.Null(type.GetMethod("Config"));
        Assert.Null(type.GetMethod("GetBuilder"));
        Assert.Null(type.GetMethod("DisableDebugLog"));
    }

    /// <summary>
    /// 测试目的：Root 执行器不得重新公开运行时配置或共享 Builder，所有 Mutation 终结入口必须接收冻结写入命令。
    /// </summary>
    [Fact]
    public void RootExecutor_WhenPublicApiInspected_ShouldUseFrozenSqlWriteCommandContract()
    {
        // Arrange
        var type = typeof(ISqlExecutor);
        var returning = type.GetMethod(nameof(ISqlExecutor.ExecuteReturningAsync));

        // Act
        var builderFactory = type.GetMethod(nameof(ISqlExecutor.CreateWriteBuilder));

        // Assert
        Assert.Null(type.GetMethod("Config"));
        Assert.Null(type.GetMethod("GetBuilder"));
        Assert.NotNull(builderFactory);
        Assert.Equal(typeof(ISqlBuilder), builderFactory.ReturnType);
        Assert.NotNull(returning);
        Assert.Equal(typeof(SqlWriteCommand), returning.GetParameters().First().ParameterType);
        Assert.Equal(typeof(SqlWriteCommand), type.GetMethod(nameof(ISqlExecutor.ExecuteWrite))
            ?.GetParameters().First().ParameterType);
        Assert.Equal(typeof(SqlWriteCommand), type.GetMethod(nameof(ISqlExecutor.ExecuteWriteAsync))
            ?.GetParameters().First().ParameterType);
        Assert.NotNull(type.GetMethod(nameof(ISqlExecutor.ExecuteReturning)));
        Assert.Null(type.GetMethod("Execute", new[] { typeof(SqlWriteCommand), typeof(int?) }));
        Assert.Null(type.GetMethod("ExecuteAsync", new[]
        {
            typeof(SqlWriteCommand), typeof(int?), typeof(CancellationToken)
        }));
        Assert.NotNull(type.GetMethod(nameof(ISqlExecutor.ExecuteSql)));
        Assert.NotNull(type.GetMethod(nameof(ISqlExecutor.ExecuteSqlAsync)));
        Assert.Null(type.GetMethod("ExecuteText"));
        Assert.Null(type.GetMethod("ExecuteTextAsync"));
        Assert.Null(type.GetMethod("ExecuteReturningQuery"));
        Assert.Null(type.GetMethod("ExecuteReturningQueryAsync"));
    }

    /// <summary>
    /// 测试目的：写入命令只保存冻结 SQL、参数和语义，不得持有可变 Builder 或执行资源。
    /// </summary>
    [Fact]
    public void SqlWriteCommand_WhenPublicApiInspected_ShouldNotExposeBuilderOrExecutionResources()
    {
        // Arrange
        var type = typeof(SqlWriteCommand);
        var properties = type.GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();

        // Assert
        Assert.Equal(new[]
        {
            "HasReturning", "OperationKind", "Parameters", "ProviderKey", "ProviderProfile", "Sql", "ValidateAffectedRows"
        }, properties);
        Assert.Null(type.GetProperty("Builder"));
        Assert.Null(type.GetProperty("Connection"));
        Assert.Null(type.GetProperty("Transaction"));
    }

    /// <summary>
    /// 测试目的：执行后的 Builder 清理是固定生命周期行为，不得通过可变 Options 开关改变。
    /// </summary>
    [Fact]
    public void SqlOptions_WhenPublicApiInspected_ShouldNotExposeClearAfterExecutionSwitch()
    {
        Assert.Null(typeof(SqlOptions).GetProperty("IsClearAfterExecution"));
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
    /// 测试目的：根查询应公开语义化实体来源和原生文本查询入口，避免无类型描述绕开结果映射约束。
    /// </summary>
    [Fact]
    public void FromAndSql_WhenPublicApiInspected_ShouldExposeTypedInstanceQueryEntryPoints()
    {
        // Arrange
        var methods = typeof(ISqlQuery).GetMethods();

        // Act
        var typed = methods.Single(method => method.Name == "From" &&
                            method.IsGenericMethodDefinition &&
                            method.GetGenericArguments().Length == 1 &&
                            method.GetParameters().Length == 1 &&
                            method.GetParameters()[0].ParameterType == typeof(string));
        var raw = methods.Single(method => method.Name == "Sql" &&
                                          method.IsGenericMethodDefinition &&
                                          method.GetParameters().Length == 2);

        // Assert
        Assert.Equal(typeof(SqlLambdaQuery<>), typed.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlTextQuery<>), raw.ReturnType.GetGenericTypeDefinition());
        Assert.True(typeof(SqlQuery<>).IsPublic);
        Assert.True(typeof(SqlTextQuery<>).IsPublic);
        Assert.Contains(methods, method => method.Name == "SqlInterpolated");
        Assert.DoesNotContain(methods, method => method.Name is "Text" or "TextInterpolated");
        Assert.Null(typeof(ISqlQuery).GetMethod("Lambda"));
    }

    /// <summary>
    /// 测试目的：多表根查询必须完整公开一至十表入口，并使用对应 arity 的强类型查询描述。
    /// </summary>
    [Fact]
    public void From_WhenMultipleEntitySourcesSupported_ShouldExposeArityOneThroughTen()
    {
        // Arrange
        var methods = typeof(ISqlQuery).GetMethods().Where(method => method.Name == "From" &&
            method.IsGenericMethodDefinition &&
            (method.GetGenericArguments().Length == 1
                ? method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string)
                : method.GetParameters().Length == 0)).ToList();

        // Act
        var arities = methods.Select(method => method.GetGenericArguments().Length).OrderBy(count => count).ToArray();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, arities);
        foreach (var method in methods.Where(method => method.GetGenericArguments().Length > 1))
        {
            Assert.Equal("SqlLambdaQuery", method.ReturnType.GetGenericTypeDefinition().Name.Split('`')[0]);
            Assert.Equal(method.GetGenericArguments().Length, method.ReturnType.GetGenericArguments().Length);
        }
    }

        /// <summary>
        /// 未实现运行时绑定控制器的查询桩。
        /// </summary>
        private sealed class UnsupportedRuntimeBindingQuery : ISqlQuery
        {
            /// <inheritdoc />
            public SqlFluentQuery<TResult> Query<TResult>() => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null) => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlTextQuery<TResult> SqlInterpolated<TResult>(FormattableString sql) => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlProcedureQuery<TResult> Procedure<TResult>(string procedure, object parameters = null) =>
                throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TEntity> From<TEntity>(string alias = null) where TEntity : class => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlSubqueryLambdaQuery<TProjection> From<TProjection>(SqlSubquery<TProjection> subquery)
                where TProjection : class => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond> From<TFirst, TSecond>() where TFirst : class where TSecond : class =>
                throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird> From<TFirst, TSecond, TThird>() where TFirst : class
                where TSecond : class where TThird : class => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> From<TFirst, TSecond, TThird, TFourth>()
                where TFirst : class where TSecond : class where TThird : class where TFourth : class =>
                throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> From<TFirst, TSecond, TThird, TFourth, TFifth>()
                where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class =>
                throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> From<TFirst, TSecond, TThird, TFourth,
                TFifth, TSixth>() where TFirst : class where TSecond : class where TThird : class where TFourth : class
                where TFifth : class where TSixth : class => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> From<TFirst, TSecond, TThird,
                TFourth, TFifth, TSixth, TSeventh>() where TFirst : class where TSecond : class where TThird : class
                where TFourth : class where TFifth : class where TSixth : class where TSeventh : class =>
                throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> From<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>()
                where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
                where TSixth : class where TSeventh : class where TEighth : class => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> From<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>()
                where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
                where TSixth : class where TSeventh : class where TEighth : class where TNinth : class => throw new NotSupportedException();

            /// <inheritdoc />
            public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> From<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth>()
                where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
                where TSixth : class where TSeventh : class where TEighth : class where TNinth : class where TTenth : class => throw new NotSupportedException();

            /// <inheritdoc />
            public void Dispose()
            {
            }

            /// <inheritdoc />
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
    /// 测试目的：原始 Builder DSL 只存在于 Fluent 查询类型，类型化 Lambda 查询不得继承原始操作 marker 或多映射入口。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicApiInspected_ShouldBeIsolatedFromRawBuilderDsl()
    {
        // Arrange
        var lambdaMethods = typeof(SqlLambdaQuery<>).GetMethods();
        var multiLambdaMethods = typeof(SqlLambdaQuery<,>).GetMethods();

        // Assert
        Assert.True(typeof(ISqlQueryOperation).IsAssignableFrom(typeof(SqlFluentQuery<>)));
        Assert.False(typeof(ISqlQueryOperation).IsAssignableFrom(typeof(SqlQuery<>)));
        Assert.False(typeof(ISqlQueryOperation).IsAssignableFrom(typeof(SqlLambdaQuery<>)));
        Assert.False(typeof(ISqlQueryOperation).IsAssignableFrom(typeof(SqlLambdaQuery<,>)));
        Assert.DoesNotContain(lambdaMethods, method => method.Name is "From" or "SplitOn");
        Assert.DoesNotContain(multiLambdaMethods, method => method.Name is "From" or "SplitOn");
        Assert.DoesNotContain(lambdaMethods, method => method.Name is "SelectFrom" or "AppendSelectFrom" or "WhereFrom");
        Assert.DoesNotContain(lambdaMethods, method => method.Name == "On" && method.IsGenericMethodDefinition);
        Assert.DoesNotContain(lambdaMethods, method => (method.Name is "ToList" or "ToListAsync") &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length > 1);
        Assert.DoesNotContain(multiLambdaMethods, method => (method.Name is "ToList" or "ToListAsync") &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length > 1);
        Assert.Contains(typeof(SqlFluentQuery<>).GetMethods(), method => method.Name == "ToList" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 3);
        Assert.Contains(typeof(SqlFluentQuery<>).GetMethods(), method => method.Name == "ToListAsync" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 3);
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
        Assert.Null(typeof(ISqlQuery).GetMethod("TextInterpolated"));
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
        Assert.False(typeof(SqlTextQuery<>).IsAssignableFrom(typeof(SqlProcedureQuery<>)));
    }

    /// <summary>
    /// 测试目的：过程描述只允许过程结果终结入口，不能继承文本查询、流式或多映射终结方法绕过输出参数快照。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenPublicApiInspected_ShouldNotExposeTextQueryTerminals()
    {
        // Arrange
        var type = typeof(SqlProcedureQuery<>);
        var forbiddenMethods = new[]
        {
            "ToList", "First", "FirstOrDefault", "Single", "SingleOrDefault", "Scalar", "AsEnumerable",
            "ToListAsync", "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
            "ScalarAsync", "AsAsyncEnumerable", "SplitOn"
        };

        // Act
        var publicMethods = type.GetMethods().Select(method => method.Name);

        // Assert
        Assert.DoesNotContain(publicMethods, method => forbiddenMethods.Contains(method));
        Assert.NotNull(type.GetProperty(nameof(SqlProcedureQuery<int>.Procedure)));
        Assert.NotNull(type.GetProperty(nameof(SqlProcedureQuery<int>.Parameters)));
    }

    /// <summary>
    /// 测试目的：根查询应公开使用实体映射初始化的 From 查询入口。
    /// </summary>
    [Fact]
    public void From_WhenPublicApiInspected_ShouldExposeTypedEntryPoint()
    {
        var method = typeof(ISqlQuery).GetMethods().Single(candidate => candidate.Name == "From" &&
            candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1 &&
            candidate.GetParameters().Length == 1 &&
            candidate.GetParameters()[0].ParameterType == typeof(string));

        Assert.NotNull(method);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(SqlLambdaQuery<>), method.ReturnType.GetGenericTypeDefinition());
        Assert.True(method.GetGenericArguments().Single().GenericParameterAttributes
            .HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint));
    }

    /// <summary>
    /// 测试目的：根查询应公开类型化派生表入口，并由受限的派生根查询描述承接后续 Lambda 组合。
    /// </summary>
    [Fact]
    public void From_WhenPublicApiInspected_ShouldExposeTypedSubqueryRootEntryPoint()
    {
        // Arrange and Act
        var method = typeof(ISqlQuery).GetMethods().Single(candidate => candidate.Name == "From" &&
            candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1 &&
            candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType.IsGenericType &&
            candidate.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(SqlSubquery<>));
        var type = typeof(SqlSubqueryLambdaQuery<>);

        // Assert
        Assert.Equal(type, method.ReturnType.GetGenericTypeDefinition());
        var methods = type.GetMethods();
        Assert.Contains(methods, candidate => candidate.Name == "Where");
        Assert.Contains(methods, candidate => candidate.Name == "Select");
        Assert.Contains(methods, candidate => candidate.Name == "OrderBy");
        Assert.DoesNotContain(methods, candidate => candidate.Name == "On");
        Assert.DoesNotContain(methods, candidate => candidate.Name == "Select" &&
            candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == 1 &&
            candidate.GetParameters()[0].ParameterType.ToString().Contains("System.Object[]", StringComparison.Ordinal));

        var joinNames = new[] { "Join", "LeftJoin", "RightJoin", "FullJoin", "CrossJoin" };
        foreach (var joinName in joinNames)
        {
            var entityJoin = methods.Single(candidate => candidate.Name == joinName &&
                candidate.IsGenericMethodDefinition && candidate.GetParameters().Length ==
                (joinName == "CrossJoin" ? 2 : 3));
            Assert.Equal(typeof(SqlLambdaQuery<,>), entityJoin.ReturnType.GetGenericTypeDefinition());

            var derivedJoin = methods.Single(candidate => candidate.Name == joinName &&
                candidate.IsGenericMethodDefinition && candidate.GetParameters().Length ==
                (joinName == "CrossJoin" ? 1 : 2) &&
                candidate.GetParameters()[0].ParameterType.IsGenericType &&
                candidate.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(SqlSubquery<>));
            Assert.Equal(typeof(SqlLambdaQuery<,>), derivedJoin.ReturnType.GetGenericTypeDefinition());
            if (joinName != "CrossJoin")
                Assert.Equal(typeof(System.Linq.Expressions.Expression<>),
                    derivedJoin.GetParameters()[1].ParameterType.GetGenericTypeDefinition());
        }
    }

    /// <summary>
    /// 测试目的：多表查询应以逐步扩展泛型元数的原子 Join 谓词扩展来源，且不保留后置 On 入口。
    /// </summary>
    [Fact]
    public void MultiLambdaQuery_WhenPublicApiInspected_ShouldExposeAtomicTypedJoinChain()
    {
        // Arrange
        var types = new[]
        {
            typeof(SqlLambdaQuery<,>),
            typeof(SqlLambdaQuery<,,>),
            typeof(SqlLambdaQuery<,,,>),
            typeof(SqlLambdaQuery<,,,,>),
            typeof(SqlLambdaQuery<,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,,,,>)
        };

        // Act and Assert
        var joinMethodNames = new[] { "Join", "LeftJoin", "RightJoin", "FullJoin", "CrossJoin" };
        for (var index = 0; index < types.Length - 1; index++)
        {
            foreach (var methodName in joinMethodNames)
            {
                var expectedParameterCount = methodName == "CrossJoin" ? 2 : 3;
                var join = types[index].GetMethods().Single(method => method.Name == methodName &&
                    method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == expectedParameterCount);
                Assert.Equal(types[index + 1], join.ReturnType.GetGenericTypeDefinition());

                var derivedParameterCount = methodName == "CrossJoin" ? 1 : 2;
                var derivedJoin = types[index].GetMethods().Single(method => method.Name == methodName &&
                    method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == derivedParameterCount &&
                    method.GetParameters()[0].ParameterType.IsGenericType &&
                    method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(SqlSubquery<>));
                Assert.Equal(types[index + 1], derivedJoin.ReturnType.GetGenericTypeDefinition());
                if (methodName != "CrossJoin")
                    Assert.Equal(typeof(System.Linq.Expressions.Expression<>),
                        derivedJoin.GetParameters()[1].ParameterType.GetGenericTypeDefinition());
            }
        }

        foreach (var type in types)
            Assert.DoesNotContain(type.GetMethods(), method => method.Name == "On");
    }

    /// <summary>
    /// 测试目的：多表查询的 DTO Select 应保留当前 Lambda 元数，结果映射由终结方法显式指定。
    /// </summary>
    [Fact]
    public void MultiLambdaQuery_WhenPublicApiInspected_ShouldKeepLambdaArityAfterProjection()
    {
        // Arrange
        var types = new[]
        {
            typeof(SqlLambdaQuery<,>),
            typeof(SqlLambdaQuery<,,>),
            typeof(SqlLambdaQuery<,,,>),
            typeof(SqlLambdaQuery<,,,,>),
            typeof(SqlLambdaQuery<,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,,,>),
            typeof(SqlLambdaQuery<,,,,,,,,,>)
        };

        // Act and Assert
        foreach (var type in types)
        {
            var projections = type.GetMethods().Where(method => method.Name == "Select" &&
                method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                method.GetParameters().Length == 1).ToArray();
            Assert.Single(projections);
            Assert.All(projections, method => Assert.Equal(type, method.ReturnType.GetGenericTypeDefinition()));

            var selectSubquery = type.GetMethods().Single(method => method.Name == "SelectSubquery" &&
                method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                method.GetParameters().Length == 2);
            Assert.Equal(typeof(SqlSubquery<>), selectSubquery.ReturnType.GetGenericTypeDefinition());

            Assert.DoesNotContain(type.GetMethods(), method => method.Name == "SelectDto");
            Assert.DoesNotContain(type.GetMethods(), method => method.Name == "As");
        }
    }

    /// <summary>
    /// 测试目的：单根 Lambda 查询应保持类型化组合，投影不切换描述类型，标量映射由终结方法指定。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicApiInspected_ShouldKeepCompositionAndMaterializationSeparate()
    {
        // Arrange
        var lambdaType = typeof(SqlLambdaQuery<>);

        // Act
        var join = lambdaType.GetMethods().Single(method => method.Name == "Join" &&
            method.IsGenericMethodDefinition && method.GetParameters().Length == 3);
        var leftJoin = lambdaType.GetMethods().Single(method => method.Name == "LeftJoin" &&
            method.IsGenericMethodDefinition && method.GetParameters().Length == 3);
        var rightJoin = lambdaType.GetMethods().Single(method => method.Name == "RightJoin" &&
            method.IsGenericMethodDefinition && method.GetParameters().Length == 3);
        var fullJoin = lambdaType.GetMethods().Single(method => method.Name == "FullJoin" &&
            method.IsGenericMethodDefinition && method.GetParameters().Length == 3);
        var crossJoin = lambdaType.GetMethods().Single(method => method.Name == "CrossJoin" &&
            method.IsGenericMethodDefinition && method.GetParameters().Length == 2);
        var projection = lambdaType.GetMethods().Single(method => method.Name == "Select" && method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 1 && method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(SqlLambdaQuery<>));
        var aggregate = lambdaType.GetMethods().Single(method => method.Name == "Aggregate" && !method.IsGenericMethodDefinition);

        // Assert
        Assert.Equal(typeof(SqlLambdaQuery<,>), join.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<,>), leftJoin.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<,>), rightJoin.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<,>), fullJoin.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<,>), crossJoin.ReturnType.GetGenericTypeDefinition());
        Assert.DoesNotContain(lambdaType.GetMethods(), method => method.Name == "On");
        Assert.Equal(typeof(SqlLambdaQuery<>), projection.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<>), aggregate.ReturnType.GetGenericTypeDefinition());
        Assert.DoesNotContain(lambdaType.GetMethods(), method => method.Name == "Aggregate" &&
            method.IsGenericMethodDefinition);
        Assert.DoesNotContain(lambdaType.GetMethods(), method => method.Name == "OrderBy" &&
            method.IsGenericMethodDefinition);
        Assert.DoesNotContain(lambdaType.GetMethods(), method => (method.Name is "WhereIf" or "WhereIfNotEmpty") &&
            method.IsGenericMethodDefinition);
        Assert.Equal(2, lambdaType.GetMethods().Count(method => method.Name == "GroupBy" && method.DeclaringType == lambdaType));

        var selectSubquery = lambdaType.GetMethods().Single(method => method.Name == "SelectSubquery" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
            method.GetParameters().Length == 2);
        var derivedJoin = lambdaType.GetMethods().Single(method => method.Name == "Join" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
            method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType.IsGenericType &&
            method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(SqlSubquery<>));
        var derivedLeftJoin = lambdaType.GetMethods().Single(method => method.Name == "LeftJoin" &&
            method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
            method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType.IsGenericType &&
            method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(SqlSubquery<>));
        Assert.Equal(typeof(SqlSubquery<>), selectSubquery.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SqlLambdaQuery<,>), derivedJoin.ReturnType.GetGenericTypeDefinition());
        var derivedJoinNames = new[] { "LeftJoin", "RightJoin", "FullJoin", "CrossJoin" };
        foreach (var methodName in derivedJoinNames)
        {
            var derivedJoinMethod = methodName == "LeftJoin" ? derivedLeftJoin : lambdaType.GetMethods().Single(method =>
                method.Name == methodName && method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 1 && method.GetParameters().Length ==
                (methodName == "CrossJoin" ? 1 : 2) &&
                method.GetParameters()[0].ParameterType.IsGenericType &&
                method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(SqlSubquery<>));
            Assert.Equal(typeof(SqlLambdaQuery<,>), derivedJoinMethod.ReturnType.GetGenericTypeDefinition());
        }
        Assert.Equal(typeof(System.Linq.Expressions.Expression<>),
            derivedJoin.GetParameters()[1].ParameterType.GetGenericTypeDefinition());
    }

    /// <summary>
    /// 测试目的：字符串 Count 与 GroupBy API 必须使用明确的统计和 Having 入口，不得重新公开语义混合重载。
    /// </summary>
    [Fact]
    public void QueryClauses_WhenPublicApiInspected_ShouldExposeExplicitCountAndHavingContracts()
    {
        // Arrange
        var selectMethods = typeof(Bing.Data.Sql.Builders.ISelectClause).GetMethods();
        var groupMethods = typeof(Bing.Data.Sql.Builders.IGroupByClause).GetMethods();

        // Assert
        Assert.NotNull(typeof(Bing.Data.Sql.Builders.ISelectClause).GetMethod("CountAll", new[] { typeof(string) }));
        Assert.NotNull(typeof(Bing.Data.Sql.Builders.ISelectClause).GetMethod("CountColumn",
            new[] { typeof(string), typeof(string), typeof(bool) }));
        Assert.DoesNotContain(selectMethods, method => method.Name == "Count" && method.IsGenericMethod == false);
        Assert.DoesNotContain(groupMethods, method => method.Name == "GroupBy" && method.GetParameters().Length == 2);
        Assert.Null(typeof(SqlLambdaQuery<>).GetMethod("HavingRaw", new[] { typeof(string) }));
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
        // Assert
        Assert.NotNull(clearSelect);
        Assert.Equal(typeof(SqlLambdaQuery<>), clearSelect.ReturnType.GetGenericTypeDefinition());
        Assert.DoesNotContain(lambdaType.GetMethods(), method => method.Name is "SelectFrom" or "AppendSelectFrom" or "WhereFrom");
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
        var fluent = typeof(SqlQuery<>).GetMethods().Single(method => method.Name == "ScalarAsync" &&
            method.IsGenericMethod == false);
        var text = typeof(SqlTextQuery<>).GetMethods().Single(method => method.Name == "ScalarAsync" &&
            method.IsGenericMethod == false);

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
        var sync = typeof(SqlQuery<>).GetMethods().Single(method => method.Name == "ToPage" &&
            method.IsGenericMethod == false);
        var async = typeof(SqlQuery<>).GetMethods().Single(method => method.Name == "ToPageAsync" &&
            method.IsGenericMethod == false);
        var lambdaSync = typeof(SqlLambdaQuery<>).GetMethods().Single(method => method.Name == "ToPage" &&
            method.IsGenericMethod == false);
        var lambdaAsync = typeof(SqlLambdaQuery<>).GetMethods().Single(method => method.Name == "ToPageAsync" &&
            method.IsGenericMethod == false);

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
        var fluent = typeof(SqlQuery<>).GetMethods().Single(method => method.Name == "AsEnumerable" &&
            method.IsGenericMethod == false);
        var text = typeof(SqlTextQuery<>).GetMethods().Single(method => method.Name == "AsEnumerable" &&
            method.IsGenericMethod == false);

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
        var fluentSync = GetTwoTypeMappingMethod(typeof(SqlFluentQuery<>), "ToList");
        var textSync = GetTwoTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetTwoTypeMappingMethod(typeof(SqlFluentQuery<>), "ToListAsync");
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
        var fluent = typeof(SqlFluentQuery<>).GetMethod("SplitOn");
        var text = typeof(SqlTextQuery<>).GetMethod("SplitOn");

        // Assert
        Assert.NotNull(fluent);
        Assert.NotNull(text);
        Assert.Equal(typeof(SqlFluentQuery<>), fluent.ReturnType.GetGenericTypeDefinition());
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
        var fluentSync = GetThreeTypeMappingMethod(typeof(SqlFluentQuery<>), "ToList");
        var textSync = GetThreeTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetThreeTypeMappingMethod(typeof(SqlFluentQuery<>), "ToListAsync");
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
        var fluentSync = GetFourTypeMappingMethod(typeof(SqlFluentQuery<>), "ToList");
        var textSync = GetFourTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetFourTypeMappingMethod(typeof(SqlFluentQuery<>), "ToListAsync");
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
        var fluentSync = GetFiveTypeMappingMethod(typeof(SqlFluentQuery<>), "ToList");
        var textSync = GetFiveTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetFiveTypeMappingMethod(typeof(SqlFluentQuery<>), "ToListAsync");
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
        var fluentSync = GetSixTypeMappingMethod(typeof(SqlFluentQuery<>), "ToList");
        var textSync = GetSixTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetSixTypeMappingMethod(typeof(SqlFluentQuery<>), "ToListAsync");
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
        var fluentSync = GetSevenTypeMappingMethod(typeof(SqlFluentQuery<>), "ToList");
        var textSync = GetSevenTypeMappingMethod(typeof(SqlTextQuery<>), "ToList");
        var fluentAsync = GetSevenTypeMappingMethod(typeof(SqlFluentQuery<>), "ToListAsync");
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
    /// 测试目的：根服务工厂和事务 Scope 必须使用固定返回契约，不能重新开放任意实现类型创建入口。
    /// </summary>
    [Fact]
    public void FactoriesAndTransactionScope_WhenPublicApiInspected_ShouldUseFixedCreationContracts()
    {
        // Arrange
        var factories = new[]
        {
            (typeof(ISqlQueryFactory), typeof(ISqlQuery)),
            (typeof(ISqlExecutorFactory), typeof(ISqlExecutor)),
            (typeof(ISqlMultipleQueryExecutorFactory), typeof(ISqlMultipleQueryExecutor))
        };

        // Act and Assert
        foreach (var (factoryType, resultType) in factories)
        {
            var create = Assert.Single(factoryType.GetMethods().Where(method => method.Name == "Create"));
            Assert.False(create.IsGenericMethodDefinition);
            Assert.Equal(resultType, create.ReturnType);
            Assert.Equal(new[] { typeof(string) }, create.GetParameters().Select(parameter => parameter.ParameterType));
        }

        var scopeMethods = typeof(ISqlTransactionScope).GetMethods();
        var createQuery = Assert.Single(scopeMethods.Where(method => method.Name == "CreateQuery"));
        var createExecutor = Assert.Single(scopeMethods.Where(method => method.Name == "CreateExecutor"));
        Assert.False(createQuery.IsGenericMethodDefinition);
        Assert.False(createExecutor.IsGenericMethodDefinition);
        Assert.Equal(typeof(ISqlQuery), createQuery.ReturnType);
        Assert.Equal(typeof(ISqlExecutor), createExecutor.ReturnType);
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