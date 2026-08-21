from pathlib import Path

TYPE_NAMES = [
    "First", "Second", "Third", "Fourth", "Fifth",
    "Sixth", "Seventh", "Eighth", "Ninth", "Tenth",
]

ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries"


def type_name(index):
    return f"T{TYPE_NAMES[index]}"


def generic_types(count):
    return ", ".join(type_name(index) for index in range(count))


def query_type(count):
    return f"SqlLambdaQuery<{generic_types(count)}>"


def expression_type(count, result):
    return f"Expression<Func<{generic_types(count)}, {result}>>"


def constraints(count):
    return "\n".join(f"    where {type_name(index)} : class" for index in range(count))


def parameter_types(count, include_next=False):
    names = [type_name(index) for index in range(count)]
    if include_next:
        names.append(type_name(count))
    return ", ".join(names)


def expression_with_next(count):
    return f"Expression<Func<{parameter_types(count, True)}, bool>>"


def next_query_type(count):
    return f"SqlLambdaQuery<{generic_types(count)}, {type_name(count)}>"


def generate_class(count):
    current = query_type(count)
    next_type = next_query_type(count) if count < len(TYPE_NAMES) else None
    source_types = generic_types(count)
    class_name = "SqlLambdaQuery"
    lines = [
        "/// <summary>",
        f"/// 使用{count}个实体来源构建的强类型 Lambda 查询描述。",
        "/// </summary>",
        f"public sealed class {current} : SqlMultiLambdaQuery",
        constraints(count),
        "{",
        f"    internal {class_name}(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)",
        "    {",
        "    }",
        "",
        f"    internal {class_name}(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)",
        "    {",
        "        if (initializeRoots)",
        "        {",
        "            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[]",
        "            {",
    ]
    for index in range(count):
        comma = "," if index < count - 1 else ""
        lines.append(f"                typeof({type_name(index)}){comma}")
    lines += [
        "            });",
        "            GetBuilder().Select<TFirst>();",
        "        }",
        "    }",
        "",
        f"    public {current} Where({expression_type(count, 'bool')} predicate)",
        "    {",
        "        WhereCore(predicate);",
        "        return this;",
        "    }",
        "",
        f"    public {current} Select({expression_type(count, 'object[]')} columns)",
        "    {",
        "        SelectCore(columns);",
        "        return this;",
        "    }",
        "",
        f"    public {current} Select<TProjection>(",
        f"        Expression<Func<{source_types}, TProjection>> projection)",
        "    {",
        "        SelectTypedCore(projection);",
        "        return this;",
        "    }",
        "",
        f"    public SqlSubquery<TProjection> SelectSubquery<TProjection>(",
        f"        Expression<Func<{source_types}, TProjection>> projection, string alias) where TProjection : class =>",
        "        SelectSubqueryCore<TProjection>(projection, alias);",
        "",
        f"    public {current} GroupBy({expression_type(count, 'object[]')} columns)",
        "    {",
        "        GroupByCore(columns);",
        "        return this;",
        "    }",
        "",
        f"    public {current} Having({expression_type(count, 'bool')} predicate)",
        "    {",
        "        HavingCore(predicate);",
        "        return this;",
        "    }",
        "",
        f"    public {current} Skip(int count)",
        "    {",
        "        SkipCore(count);",
        "        return this;",
        "    }",
        "",
        f"    public {current} Take(int count)",
        "    {",
        "        TakeCore(count);",
        "        return this;",
        "    }",
        "",
        f"    public {current} OrderBy({expression_type(count, 'object[]')} columns, bool desc = false)",
        "    {",
        "        OrderByCore(columns, desc);",
        "        return this;",
        "    }",
    ]

    if count < 10:
        next_type_name = type_name(count)
        for method, call in (
            ("Join", f"JoinCore<{next_type_name}>(predicate, alias, schema);"),
            ("LeftJoin", f"LeftJoinCore<{next_type_name}>(predicate, alias, schema);"),
            ("RightJoin", f"RightJoinCore<{next_type_name}>(predicate, alias, schema);"),
            ("FullJoin", f"FullJoinCore<{next_type_name}>(predicate, alias, schema);"),
        ):
            lines += [
                "",
                f"    public {next_type} {method}<{next_type_name}>({expression_with_next(count)} predicate,",
                f"        string alias = null, string schema = null) where {next_type_name} : class",
                "    {",
                f"        {call}",
                f"        return new {next_type}(Executor, GetBuilder(), false);",
                "    }",
            ]
        lines += [
            "",
            f"    public {next_type} CrossJoin<{next_type_name}>(string alias = null, string schema = null) where {next_type_name} : class",
            "    {",
            f"        CrossJoinCore<{next_type_name}>(alias, schema);",
            f"        return new {next_type}(Executor, GetBuilder(), false);",
            "    }",
        ]
        for method, call in (
            ("Join", "JoinCore(subquery, predicate);"),
            ("LeftJoin", "LeftJoinCore(subquery, predicate);"),
            ("RightJoin", "RightJoinCore(subquery, predicate);"),
            ("FullJoin", "FullJoinCore(subquery, predicate);"),
        ):
            lines += [
                "",
                f"    public {next_type} {method}<{next_type_name}>(SqlSubquery<{next_type_name}> subquery,",
                f"        {expression_with_next(count)} predicate) where {next_type_name} : class",
                "    {",
                f"        {call}",
                f"        return new {next_type}(Executor, GetBuilder(), false);",
                "    }",
            ]
        lines += [
            "",
            f"    public {next_type} CrossJoin<{next_type_name}>(SqlSubquery<{next_type_name}> subquery) where {next_type_name} : class",
            "    {",
            "        CrossJoinCore(subquery);",
            f"        return new {next_type}(Executor, GetBuilder(), false);",
            "    }",
        ]

    lines.append("}")
    return "\n".join(lines)


def generate():
    header = "using System.Linq.Expressions;\nusing Bing.Data.Sql.Builders;\n\nnamespace Bing.Data.Sql;\n\n"
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for count in range(2, 11):
        output = OUTPUT_DIR / f"SqlMultiLambdaQuery.Arity{count:02d}.cs"
        output.write_text(header + generate_class(count) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    generate()
    print(OUTPUT_DIR)
