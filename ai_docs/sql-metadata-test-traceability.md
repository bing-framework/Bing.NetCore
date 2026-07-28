# SQL Metadata Test Traceability

| Production symbol | Key behavior | Verification |
| --- | --- | --- |
| `EntityAliasRegister.RegisterAlias` | Rejects aliases case-insensitively within one query scope. | `EntityAliasRegisterTest.RegisterAlias_WhenAliasAlreadyExistsIgnoringCase_ShouldThrowInvalidOperationException` |
| `EntityAliasRegister.Register` | Shares alias validation between entity and string/raw table sources. | `EntityAliasRegisterTest.Register_WhenAliasWasRegisteredByStringSource_ShouldThrowInvalidOperationException` |
| `EntityAliasRegister.Replace` | Replaces the current From entity alias and releases the previous alias. | `EntityAliasRegisterTest.Replace_WhenFromAliasChanges_ShouldReleasePreviousAlias` |
| `IFromClause.From` / `IJoinClause.Join` / `Extensions.From` / `Extensions.Join` | String table APIs accept only `table` and `alias`; generic entity APIs retain the schema override. | `RawApiContractTest.StringTableApis_ShouldExposeOnlyTableAndAlias`; `RawApiContractTest.FluentStringTableApis_ShouldExposeOnlySupportedParameters` |
| `FromClause.AppendSql` / `Extensions.AppendFrom` | Raw From text is not parsed, formatted, or alias-registered. The first raw Append replaces a structured From; later raw text is concatenated without an implicit separator; blank input is ignored. | `AppendRawSqlContractTest.AppendRawSql_ShouldPreserveTextAcrossFromAndJoinVariants`; `AppendRawSqlContractTest.AppendRawSql_WhenConditionIsFalse_ShouldLeaveBuilderUnchanged`; `AppendRawSqlContractTest.AppendFrom_WhenRawTextIsBlankOrStructuredFromExists_ShouldApplyDocumentedBehavior`; `AppendRawSqlContractTest.AppendFrom_WhenCalledRepeatedly_ShouldNotInsertSeparator` |
| `JoinClause.AppendJoin` / `Extensions.AppendJoin` / `Extensions.AppendLeftJoin` / `Extensions.AppendRightJoin` / `JoinItem.GetOn` | Each raw Append creates an independent Join item without parsing, formatting, or alias registration. `AppendOn` applies to the last Join; an existing raw `On` receives subsequent conditions with `And`; a nonblank condition without a Join throws before parameter or alias side effects. | `AppendJoinAndOnCompositionTest.AppendJoin_WhenFollowedByAppendOn_ShouldComposeSql`; `AppendJoinAndOnCompositionTest.AppendLeftJoin_WhenFollowedByAppendOn_ShouldComposeSql`; `AppendJoinAndOnCompositionTest.AppendRightJoin_WhenFollowedByAppendOn_ShouldComposeSql`; `AppendJoinAndOnCompositionTest.AppendJoin_WhenMultipleJoinsExist_ShouldBindOnToLatestJoin`; `AppendJoinAndOnCompositionTest.AppendJoin_WhenJoinAlreadyContainsOn_ShouldAppendAdditionalCondition`; `AppendJoinAndOnCompositionTest.AppendOn_WhenNoJoinExists_ShouldThrowWithoutChangingFollowingJoin`; `AppendJoinAndOnCompositionTest.OnValue_WhenNoJoinExists_ShouldThrowWithoutAddingParameter`; `AppendJoinAndOnCompositionTest.OnExpression_WhenNoJoinExists_ShouldThrowWithoutAddingParameter`; `JoinClauseTest.Test_On_1`; `OracleJoinClauseTest.Test_On_WithoutJoin_ShouldThrowWithoutAddingParameter` |
| `SqlItem.Clone` / `JoinItem.Clone` / `SqlBuilderBase.ClearFrom` / `SqlBuilderBase.ClearJoin` | Raw clause state remains stable through repeated rendering and is independent after Clone/New/Clear; raw joins without explicit On do not gain an empty `On` clause after cloning. | `AppendRawSqlContractTest.AppendRawSql_ShouldRemainStableAcrossCloneNewClearAndRepeatedRendering`; `MySqlBuilderTest.Clone_WhenJoinUsesDottedPhysicalTable_ShouldPreserveMySqlStringTableStrategy` |
| `EntityAliasRegister.RegisterAlias` through structured From/Join | Raw aliases do not enter the structured alias registry, while structured duplicate aliases remain rejected. | `AppendAliasRegistrationBoundaryTest.AppendRawSql_WhenTextContainsRawSourceAlias_ShouldNotRegisterAlias`; `AppendAliasRegistrationBoundaryTest.AppendRawSql_WhenTextContainsMultipleAliases_ShouldNotRegisterAliases`; `AppendAliasRegistrationBoundaryTest.StructuredSql_WhenAliasDuplicates_ShouldThrowInvalidOperationException` |
| `MySqlTableNameParser.Parse` | Supports atomic dotted MySQL physical names, escaped backticks, quoted schema plus atomic physical table names, alias reconciliation, and malformed identifier rejection. Qualified schema/table components must use backticks consistently; mixed quote forms are rejected. | `MySqlTableNameParserTest.Parse_WhenTableNameIsValid_ShouldReturnStructuredReference`; `MySqlTableNameParserTest.Parse_WhenAliasesConflict_ShouldThrowInvalidOperationException`; `MySqlTableNameParserTest.Parse_WhenTableNameIsInvalid_ShouldThrowArgumentException` |
| `MySqlFromClause.ParseTableName` / `MySqlJoinClause.ParseTableName` | Applies MySQL quote-aware parsing only for `DatabaseType.MySql`; mixed quote forms are rejected consistently by From and Join; Doris retains segmented string-name behavior. | `MySqlBuilderTest.From_WhenQuotedTableNameIsInvalid_ShouldThrowArgumentException`; `MySqlBuilderTest.Join_WhenTableNameUsesMixedQuotes_ShouldThrowArgumentException`; `MySqlBuilderTest.Join_WhenUsingQuotedSchemaAndDottedPhysicalTable_ShouldRenderAllJoinTypes`; `MySqlRoutingAndMappingTest.Doris_StructuredFrom_ShouldKeepSegmentedName` |
| `IJoinClause.LeftJoin(SqlTableReference)` / `JoinClause.LeftJoin(SqlTableReference)` | Provides a structured left-join path with the same object-name formatting and cross-database validation model as structured inner Join. | `StructuredTableReferenceBuilderTest.LeftJoin_WhenUsingStructuredReference_ShouldRenderCompleteSql`; `StructuredTableReferenceBuilderTest.Join_WhenUsingStructuredReference_ShouldRenderCompleteSql` |
| `MySqlBuilder` with `DatabaseType.Doris` | Doris reuses the MySQL provider builder: structured string tables stay segmented, while raw Append remains byte-for-byte caller text. | `MySqlRoutingAndMappingTest.Doris_StructuredFrom_ShouldKeepSegmentedName`; `MySqlRoutingAndMappingTest.Doris_AppendFrom_ShouldPreserveRawSql`; `MySqlRoutingAndMappingTest.Doris_AppendJoin_ShouldPreserveRawSql` |
| `MySqlJoinClause.Clone` | Preserves the MySQL string table-name split strategy after cloning. | `MySqlBuilderTest.Clone_WhenJoinUsesDottedPhysicalTable_ShouldPreserveMySqlStringTableStrategy` |
| `StructuredSqlItem.ToSql` / `DefaultSqlObjectNameFormatter.Format` | Keeps ORM `TableName` atomic while formatting optional schema, aliases, lambda joins, parameters, paging, and repeated rendering. | `MySqlRoutingAndMappingTest.TypedTables_WhenPhysicalTableNamesContainDots_ShouldRemainAtomicAcrossRenderings` |
| `SqlItem.ToSql` / `JoinItem.ToSql` | Keeps non-MySQL string qualified-name segmentation unchanged. | `SqlServerBuilderTest.From_WhenTableNameHasThreeSegments_ShouldFormatEachSegmentAndPage`; `PostgreSqlBuilderTest.StringQualifiedTables_ShouldKeepPostgreSqlSegmentedFormatting`; `FromClauseTest.From_WhenSchemaAndTableAreProvided_ShouldFormatEachIdentifier`; `SqliteRoutingAndMappingTest.StringQualifiedTables_ShouldKeepSqliteSegmentedFormatting`; `MySqlRoutingAndMappingTest.Doris_StructuredFrom_ShouldKeepSegmentedName` |
| `SelectClause.Aggregate(string, string, string)` / `ColumnCollection.AddAggregationColumn(string, string, string)` / `ColumnItem.Clone` / `SelectClause.Clone` | String Count, Sum, Avg, Max and Min parse qualified identifier segments before dialect quoting; unquoted, double-quoted and backtick-quoted inputs produce the provider's correct quoted alias and column. Cloned builders retain aggregation and Distinct state. Raw Select expressions remain on their existing raw path. | `SqlBuilderTest.Clone_WhenDistinctIsConfigured_ShouldPreserveDistinct`; `PostgreSqlBuilderTest.Count_WithQualifiedColumn_ShouldFormatEachIdentifierSegment`; `PostgreSqlBuilderTest.Sum_WithQualifiedColumn_ShouldFormatEachIdentifierSegment`; `PostgreSqlBuilderTest.Clone_WhenCountUsesQualifiedColumn_ShouldPreserveAggregation`; `PostgreSqlBuilderTest.Clone_WhenDistinctIsConfigured_ShouldPreserveDistinct`; `MySqlBuilderTest.Count_WithQualifiedColumn_ShouldFormatEachIdentifierSegment`; `MySqlBuilderTest.Sum_WithQualifiedColumn_ShouldFormatEachIdentifierSegment`; `MySqlBuilderTest.Clone_WhenCountUsesQualifiedColumn_ShouldPreserveAggregation`; `PostgreSqlQueryTest.PostgreSql_CountQualifiedColumn_ShouldExecuteSuccessfully`; `PostgreSqlQueryTest.PostgreSql_SumQualifiedColumn_ShouldExecuteSuccessfully`; `MySqlQueryTest.MySql_CountQualifiedColumn_ShouldExecuteSuccessfully`; `MySqlQueryTest.MySql_SumQualifiedColumn_ShouldExecuteSuccessfully` |
| `MySqlBuilder` / `SqlServerBuilder` / `PostgreSqlBuilder` / `OracleBuilder` / `SqliteBuilder` raw Append paths | Provider dialects may format structured columns but must not convert raw From or three Join variants, including hints and foreign parameter placeholders. | Each Provider `AppendRawSqlTest.AppendFrom_ShouldPreserveRawSql`; `AppendJoin_ShouldPreserveRawSql`; `AppendLeftJoin_ShouldPreserveRawSql`; `AppendRightJoin_ShouldPreserveRawSql` |
| `MySqlIntegrationDatabaseFixture.InitializeAsync` / `AddMySqlIntegrationTestServices` | Owns one gated root service provider and centralizes `AddSqlCore()` plus MySQL query/executor registrations; the registration path must resolve factories without opening a database. | `MySqlIntegrationDatabaseFixtureTest.Fixture_WhenIntegrationEnabled_ShouldResolveSqlFactoriesFromSingleRootProvider`; `MySqlIntegrationDatabaseFixtureTest.Fixture_WhenIntegrationEnabled_ShouldResolveSqlCoreServices`; `MySqlIntegrationDatabaseFixtureTest.ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection` |
| `MySqlIntegrationDatabaseFixture.DisposeAsync` | Disposes the unique root service provider and clears MySQL connection pools after gated integration execution. | Gated MySQL integration suite; execution requires `RUN_MYSQL_INTEGRATION_TESTS=true`, `ConnectionStrings__MySqlConnection`, and `ALLOW_DATABASE_RESET_FOR_TESTS=true`. |
| `DatabaseScript.InitializeAsync` / `DatabaseScript.ResetAsync` / `MySqlFromClause.ParseTableName` | Creates and clears the `` `Merchants.Company` `` and `` `Merchants.Merchant` `` physical tables; structured, typed and raw Join paths execute against atomic dotted identifiers. | `MySqlQueryTest.ExecuteScalar_WhenStructuredFromUsesDottedPhysicalTable_ShouldReturnRowCount`; `MySqlQueryTest.ExecuteScalar_WhenAppendFromUsesDottedPhysicalTable_ShouldReturnRowCount`; `MySqlQueryTest.LeftJoin_WithDottedPhysicalTables_ShouldExecuteSuccessfully`; `MySqlQueryTest.LeftJoin_WithTypedDottedPhysicalTables_ShouldExecuteSuccessfully`; `MySqlQueryTest.AppendLeftJoin_WithDottedPhysicalTables_ShouldExecuteSuccessfully`; `MySqlQueryTest.LeftJoin_WhenDottedMerchantDoesNotMatch_ShouldReturnCompanyWithNullMerchant` (gated MySQL integration) |
| `SqlBuilderBase.RenderSubquery` | Merges external subquery parameters and renames collisions without overwriting outer parameters. | `SqlBuilderTest.Join_WhenExternalSubqueryParameterConflicts_ShouldRenameSubqueryParameter` |
| `MySqlBuilder` / `MySqlFromClause` / `MySqlJoinClause` / `SqlBuilderBase.ToDebugSql` / `SqlBuilderBase.Clone` / `SqlBuilderBase.Clear` | Normal MySQL table sources, value-bound Inner Join, empty In/NotIn, aggregate grouping with paging, debug literal output, and cloned/cleared state retain dialect SQL and parameter contracts. | `MySqlBuilderTest.From_WhenTableAndAliasAreConfigured_ShouldRenderQuotedColumns`; `MySqlBuilderTest.Join_WhenNormalTableValueOnConfigured_ShouldRenderSqlAndParameter`; `MySqlBuilderTest.InAndNotIn_WhenValuesAreEmpty_ShouldOmitConditionsAndKeepParametersEmpty`; `MySqlBuilderTest.GroupBy_WhenAggregateOrderAndPageAreCombined_ShouldKeepClauseAndParameterOrder`; `MySqlBuilderTest.ToDebugSql_WhenParametersExist_ShouldRenderMySqlLiteralsAndPreserveParameters`; `MySqlBuilderTest.CloneAndClear_WhenNormalTableStateExists_ShouldKeepInstancesIsolated` |
| `DatabaseScript.InitializeAsync` / `DatabaseScript.ResetAsync` / `ISqlQuery.ExecuteSingleAsync` / `ISqlExecutor.ExecuteSqlAsync` | The gated MySQL test database creates and clears ordinary parent/child tables in child-first order; normal Inner/Left Join and insert/update/delete/no-match affected-row execution are verified against real MySQL. | `MySqlQueryTest.MySql_InnerJoin_ShouldReturnMatchingProductItem`; `MySqlQueryTest.MySql_LeftJoin_ShouldPreserveProductWithoutItem`; `MySqlExecutorTest.ExecuteSqlAsync_ShouldReturnAffectedRowsForInsertUpdateAndDelete` |
| `SelectClause` / `FromClause` / `JoinClause` / `SqlBuilderBase.RenderSubquery` | PostgreSQL structured select, aggregate, qualified source, raw join condition and subquery parameter propagation retain PostgreSQL identifier formatting and parameter order. | `PostgreSqlBuilderTest.Select_WhenColumnsHaveTableAlias_ShouldRenderQuotedColumns`; `Select_WhenDistinctAndAggregateAreConfigured_ShouldRenderExpectedSql`; `Select_WhenSubqueryColumnIsConfigured_ShouldMergeParameters`; `From_WhenSchemaAndAliasAreConfigured_ShouldRenderQualifiedTable`; `From_WhenSubqueryIsConfigured_ShouldRenderSubqueryAndParameters`; `Join_WhenRawOnConditionIsConfigured_ShouldRenderExpectedSql` |
| `WhereClause` / `GroupByClause` / `OrderByClause` / `SqlBuilderBase.Skip` / `SqlBuilderBase.Take` / `Extensions.UnionAll` | PostgreSQL and MySQL retain dialect-specific identifiers while rendering composed conditions, grouping, parameterized limit/offset pagination, and collision-safe union parameters. | `PostgreSqlBuilderTest.Where_WhenMultipleConditionTypesAreConfigured_ShouldRenderExpectedSql`; `GroupBy_WhenHavingAndOrderByAreConfigured_ShouldRenderExpectedSql`; `Page_WhenSkipAndTakeAreConfigured_ShouldRenderLimitOffset`; `UnionAll_WhenBothQueriesHaveParameters_ShouldRenderAllParameters`; `MySqlBuilderTest.Where_WhenMultipleConditionTypesAreConfigured_ShouldRenderExpectedSql`; `GroupBy_WhenHavingAndOrderByAreConfigured_ShouldRenderExpectedSql`; `Page_WhenSkipAndTakeAreConfigured_ShouldRenderLimitOffset`; `UnionAll_WhenBothQueriesHaveParameters_ShouldRenderAllParameters` |
| `Bing.Datas.EntityFramework.Core.UnitOfWorkBase.GetTableName` / `GetSchema` / `GetColumnName` | Preserves EF Core's physical dotted table name and column metadata when supplied to `MySqlBuilder`. | `EfCoreSqlQueryFactoryTest.MetadataProvider_WhenEfTableNameContainsDot_ShouldKeepAtomicNameForMySqlBuilder` |
| `Bing.Uow.UnitOfWorkBase.GetTable` / `GetSchema` / `GetTableName` / `GetColumnName` | Treats FreeSQL `DbName` as an atomic table name, does not infer schema from dots, and supplies the same metadata to `MySqlBuilder`. | `FreeSqlEntityModelMetadataProviderTest.MetadataProvider_WhenTableNameContainsDot_ShouldKeepAtomicNameForMySqlBuilder` |

## 统一聚合与 AppendTo 追溯

| 生产代码 | 测试项目 | 测试类 | 测试方法 | 测试类型 |
| --- | --- | --- | --- | --- |
| `SqlAggregateFunction` / `SqlAggregateArgumentValidator.ValidateFunction` | `Bing.Data.Sql.Tests` | `SqlBuilderTest` | `Aggregate_WhenFunctionIsUndefined_ShouldThrowArgumentOutOfRangeException`; `AggregateRaw_WhenFunctionIsUndefined_ShouldThrowArgumentOutOfRangeException`; `AggregateExpression_WhenFunctionIsUndefined_ShouldThrowArgumentOutOfRangeException` | Unit |
| `SqlIdentifierPathParser` / `SqlAggregateArgumentValidator.ParseStructuredColumn` / `ColumnCollection.AddStructuredAggregationColumn` | `Bing.Data.Sql.Tests`; all five Dapper Provider unit projects | `SqlBuilderTest`; each Provider `*BuilderAggregateTest` | `Aggregate_WhenColumnIsStructuredIdentifier_ShouldRenderColumn`; `Aggregate_WhenColumnContainsQuotedSpacesAndEscapedClosingQuote_ShouldRenderStructuredIdentifier`; `Aggregate_WhenColumnIsNotSingleStructuredIdentifier_ShouldThrowWithoutChangingBuilder`; `AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRender*Sql` | Unit; supports one to three segments, quoted spaces and doubled closing delimiters, then applies target dialect escaping. |
| `SqlAggregateArgumentValidator.ValidateWildcard` / `SelectClause.AggregateRaw` / `SelectClause.AggregateExpression` | `Bing.Data.Sql.Tests` | `SqlBuilderTest` | `AggregateRawAndExpression_WhenNonCountFunctionUsesWildcard_ShouldThrowWithoutChangingBuilder`; `Count_WhenDistinctWildcardIsRequested_ShouldThrowArgumentException` | Unit |
| `SelectClause.AggregateRaw` / `Extensions.AggregateRaw` | `Bing.Data.Sql.Tests`; `Bing.Dapper.MySql.Tests` | `SqlBuilderTest`; `MySqlBuilderTest` | `AggregateRaw_WhenSqlContainsJsonPath_ShouldPreserveBrackets`; `AggregateRaw_WhenSqlContainsStringBrackets_ShouldPreserveText`; `AggregateRaw_WhenCountWildcardContainsWhitespace_ShouldPreserveText`; `AggregateRaw_WhenJsonPathAndStringBracketsAreConfigured_ShouldPreserveText` | Unit |
| `SqlExpressionIdentifierResolver` / `SelectClause.AggregateExpression` / `Extensions.AggregateExpression` | `Bing.Data.Sql.Tests`; all five Dapper Provider unit projects; Doris unit tests | `SqlBuilderTest`; each Provider `*BuilderAggregateTest`; `MySqlRoutingAndMappingTest` | `AggregateExpression_WhenSqlContainsStringsAndComments_ShouldPreserveTheirBrackets`; `AggregateExpression_WhenSqlContainsLineComment_ShouldPreserveCommentBrackets`; `AggregateExpression_WhenSqlContextIsUnclosed_ShouldThrowWithoutChangingBuilder`; each Provider `AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRender*Sql`; `Doris_AggregateExpression_ShouldProtectStringAndCommentContexts` | Unit; only normal SQL context converts `[]`; JSON Path, quoted text, escaped quotes and comments remain byte-for-byte text. |
| `SelectClause.CountAll` / `SelectClause.CountColumn` / `Extensions.CountAll` / `Extensions.CountColumn` | `Bing.Data.Sql.Tests`; `Bing.Dapper.MySql.Tests` | `SqlBuilderTest`; `MySqlBuilderTest`; `MySqlRoutingAndMappingTest` | `CountAll_WhenAliasIsConfigured_ShouldRenderCountWildcard`; `CountColumn_WhenDistinctIsConfigured_ShouldRenderDistinctColumnCount`; `LegacyCount_WhenSingleStringIsConfigured_ShouldTreatStringAsAlias`; `CountAllAndCountColumn_WhenConfigured_ShouldRenderMySqlSql`; `Doris_AggregateApis_ShouldPreserveRawAndRenderQualifiedExpressions` | Unit |
| `SelectClause.AggregateLegacy` / `SelectClause.Aggregate` / `ColumnCollection.AddStructuredAggregationColumn` | `Bing.Data.Sql.Tests` | `SqlBuilderTest` | `AggregateApis_WhenAliasIsNotConfigured_ShouldNotRenderAlias`; `LegacyAggregateApis_WhenAliasIsNotConfigured_ShouldUseLeafColumnAlias` | Unit; legacy `Count`/`Sum`/`Avg`/`Max`/`Min` retain a leaf-column Alias, while `CountAll`/`CountColumn`/`Aggregate`/`AggregateRaw`/`AggregateExpression` omit `As` when Alias is null. |
| `SqlBuilderBase.RenderSubquery` / explicit aggregate Raw and Expression parameters | `Bing.Data.Sql.Tests` | `SqlBuilderTest` | `AggregateRawAndExpression_WhenParametersAreExplicit_ShouldBindAndRenderStably`; `Cte_WhenAggregateExpressionParametersConflict_ShouldRenameCteParameter`; `Union_WhenAggregateExpressionParametersHavePrefixNames_ShouldKeepTokenBoundaries`; `Subquery_WhenAggregateExpressionParametersConflict_ShouldRenameEachExactToken` | Unit; verifies exact SQL, parameter order/value, Debug SQL, repeat rendering, `@p/@p1/@p10` and `@Min/@MinAmount` token boundaries. |
| `ColumnItem.AggregateFunction` / `ColumnItem.Clone` / `SelectClause.Clone` / `SqlBuilderBase.New` / `SqlBuilderBase.ClearSelect` / `SqlBuilderBase.CreateCte` / `SqlBuilderBase.CreateSqlByUnion` | `Bing.Data.Sql.Tests` | `SqlBuilderTest` | `Clone_WhenRawAndExpressionAggregatesExist_ShouldPreserveAndIsolateState`; `New_WhenSourceHasAggregate_ShouldNotShareAggregateState`; `Cte_WhenDistinctAggregateIsConfigured_ShouldRenderExpectedSql`; `Union_WhenDistinctAggregateIsConfigured_ShouldMergeCorrectly`; `Subquery_WhenAggregateExpressionHasParameters_ShouldMergeParametersAndRemainStable`; `AppendTo_WhenAggregateExpressionIsConfigured_ShouldRemainStableAcrossRepeatedCalls`; `ClearSelect_WhenDistinctAggregateExists_ShouldRemoveAllSelectState` | Unit |
| `ColumnItem.AggregationFunc` / `ColumnItem.IsAggregation` / `SqlItem.AggregationFunc` | `Bing.Data.Sql.Tests` | `SqlItemTest` | `LegacyAggregationFunction_WhenCloned_ShouldPreserveRendering` | Unit; public compatibility members are obsolete, while standard aggregation uses `SqlAggregateFunction` only |
| `SqlBuilderBase.AppendTo` / `SqlBuilderBase.ToSql` | `Bing.Data.Sql.Tests` | `SqlBuilderAppendToTest` | `AppendTo_WhenBuilderIsEmpty_ShouldRenderSameSqlAsToSql`; `AppendTo_WhenBuilderContainsPrefix_ShouldAppendWithoutClearing`; `AppendTo_WhenCalledTwice_ShouldAppendTwice`; `AppendTo_WhenArgumentIsNull_ShouldThrowArgumentNullException`; `AppendTo_WhenSubqueryHasParameters_ShouldMergeParametersAndRenderExpectedSql` | Unit |
| `DatabaseScript` aggregate test schema and unified aggregate execution path | `Bing.Dapper.MySql.Tests.Integration`; `Bing.Dapper.PostgreSql.Tests.Integration`; `Bing.Dapper.Sqlite.Tests.Integration` | `MySqlQueryTest`; `PostgreSqlQueryTest`; `SqliteExecutionIntegrationTest` | `Count_WhenAggregateDataContainsNull_ShouldReturnNonNullCount`; `CountDistinct_WhenAggregateDataContainsDuplicates_ShouldReturnDistinctCount`; `SumAndAvg_WhenAggregateDataContainsDuplicates_ShouldReturnExpectedValues`; `MaxAndMin_WhenDistinctIsConfigured_ShouldReturnExtremes`; `QualifiedDistinctAggregate_WhenUserIdsRepeat_ShouldExecuteSuccessfully`; `AggregateExpression_WhenCaseAndArithmeticAreConfigured_ShouldExecuteSuccessfully`; `Aggregate_WhenAliasesAreConfigured_ShouldMapToDto`; `Aggregate_WhenDuplicateAndNullValuesExist_ShouldReturnExpectedCountsAndExtremes`; `AggregateRawAndExpression_WhenConfigured_ShouldExecuteAndMapAliases` | Gated MySQL/PostgreSQL integration; local SQLite integration |
| `SqlServerIntegrationDatabaseFixture` / SQL Server aggregate `DatabaseScript` | `Bing.Dapper.SqlServer.Tests.Integration` | `SqlServerQueryAggregateTest` | `AggregateApis_WhenAggregateDataIsSeeded_ShouldExecuteExpectedValues` | Opt-in integration; creates and clears only `dbo.BingSqlAggregateIntegration`, requires `RUN_SQLSERVER_INTEGRATION_TESTS=true`, a safe test database connection and `ALLOW_DATABASE_RESET_FOR_TESTS=true`. Current environment: safely skipped, not counted as execution pass. |
| `IntegrationTestGate` Oracle branch | `Bing.Test.Shared` | `IntegrationTestGateTest` | `GetSkipReason_ShouldEnableOnlyOracleWhenOracleSwitchIsEnabled`; `GetSkipReason_ShouldDescribeOracleSwitchWhenOracleIsDisabled` | Unit; Oracle keeps independent Skip evidence. No Oracle DDL/DML aggregate fixture exists because a safe schema/user/table-prefix reset contract has not been established. |

### 最终功能验收

- 统一模型支持 `Count(*)`、`CountAll`、`CountColumn`、`Count(Distinct column)`、五种标准聚合及其单参数 `Distinct`、实体 Lambda、Raw 参数、可转换 Expression 以及别名映射。
- `Select Distinct Count(...)` 由 `SelectClause.Distinct()` 管理，`Select Count(Distinct ...)` 由聚合参数 `distinct: true` 管理；两种状态可同时存在，且测试分别断言完整 SQL。
- `Count(string columnAlias = null)` 保留既有兼容语义，即 `Count(*) As alias`；新调用应使用 `CountAll` 或 `CountColumn` 消除歧义。`Count(Distinct *)` 与非 Count 的 `*` 明确拒绝，避免生成不可移植 SQL。
- 结构化聚合只接受一个一至三段标识符路径，支持引用段中的空格和双写结束符；函数、Case、运算、注释、分号和多列输入均被拒绝。`AggregateRaw` 对受信任 SQL 完全原样，不转换 JSON Path 或字符串中的方括号；`AggregateExpression` 只转换普通 SQL 上下文中的 `[]`，字符串、转义引用与注释保持原文，未闭合上下文在状态写入前抛出 `ArgumentException`。
- 旧便捷 `Count`/`Sum`/`Avg`/`Max`/`Min` 未指定 Alias 时保留逻辑叶子名称；新统一 `CountAll`/`CountColumn`/`Aggregate`/`AggregateRaw`/`AggregateExpression` 未指定 Alias 时不输出 `As`。Raw 与 Expression 参数必须通过 `AddParam` 显式绑定。
- MySQL、PostgreSQL 与 SQLite 真实集成覆盖重复值、null、限定列、Raw、Expression、Case、Max/Min Distinct 和 DTO 映射；Avg 精度按 Provider 实际数值类型结果断言。
- `AppendTo(StringBuilder)` 现在执行完整初始化、验证和 SQL 生成流程，只去除本次追加片段尾部空白，不清空调用方内容；`ToSql()` 复用此路径并保持历史输出。

### 最终性能验收

环境：Windows 10、Intel Core Ultra 7 270K Plus、.NET 8.0.27、BenchmarkDotNet 0.14.0、`FormalHost`（3 launch、6 warmup、15 iteration）。基准源为 `framework/tests/Bing.Data.Sql.Benchmarks/SqlAggregateAndAppendToBenchmarks.cs`。

| 场景 | Mean | Allocated | 相对十常规聚合 | 结论 |
| --- | ---: | ---: | ---: | --- |
| 十个普通/Distinct 聚合 `RenderTenAggregates` | 3.680 us | 24.08 KB | 1.00x / 1.00x | 三启动正式结果；相对旧 4.042 us / 25.17 KB 分别改善约 9.0% / 4.3%。 |
| Expression JSON Path 字符串解析 `AggregateExpression_JsonPathString` | 929.1 ns | 6.49 KB | 不适用 | 三启动正式结果；验证字符串感知扫描，无 Gen2。 |
| 十个普通/Distinct 聚合 `AppendTenAggregates` | 3.983 us | 24.16 KB | 0.99x / 0.96x | `AppendTo` 仍保留约 4% 分配收益。 |
| Raw JSON Path `AggregateRaw_JsonPath` | 446.4 ns | 3.56 KB | 0.11x / 0.14x | Raw 原样语义无额外转换成本。 |
| Expression 算术 `AggregateExpression_Arithmetic` | 464.8 ns | 3.55 KB | 0.12x / 0.14x | 方括号标识符转换由构建期完成。 |
| Expression Case `AggregateExpression_Case` | 482.6 ns | 4.05 KB | 0.12x / 0.16x | 覆盖 Case 与参数级 Distinct。 |
| `CountColumn_Distinct` | 627.3 ns | 4.48 KB | 0.16x / 0.18x | 明确列 Count API 的渲染成本。 |
| 十个 Expression 聚合 `TenAggregateExpressions` | 2.170 us | 15.66 KB | 0.54x / 0.62x | 预转换表达式减少重复的列结构化解析。 |
| `Clone_TenAggregateExpressions` | 483.4 ns | 4.82 KB | 0.12x / 0.19x | 覆盖完整结构化聚合状态复制。 |

曾试验 Select/Column Clause 直接写入最终 `StringBuilder`，但未达到“复杂查询与十聚合分配均下降至少 20%，且 Mean 不回退超过 10%”的保留条件，已撤回该实验路径。本轮未重启该改造；最终仅保留 `SqlBuilderBase.AppendTo` 合同修复，不在 Clause 中长期持有可变 `StringBuilder`。本轮报告为 `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlAggregateRenderingBenchmarks-report-github.md`。

### 最终执行结果

| 验收项 | 结果 |
| --- | --- |
| `dotnet build .\Bing.All.sln -c Release -nologo -v minimal` | 成功，84 条既有警告：68 条 `NU1902`/`NU1903`/`NU1904` 依赖包漏洞、8 条 `NETSDK1138` .NET 6 EOL、7 条 `CS0108`/`CS0114` 成员隐藏、1 条 `CS0618` 过时 API；无 nullable、XML 或 analyzer 警告，未发现聚合改动引入的警告。 |
| `Bing.Data.Sql.Tests` | 1350 passed，0 failed，0 skipped。 |
| MySQL、PostgreSQL、SQL Server、Oracle、SQLite Provider 单元测试 | 1042 passed，0 failed，0 skipped。 |
| SQLite、本地 MySQL、PostgreSQL 集成测试 | 264 passed，0 failed，0 skipped。 |
| `Bing.Test.Shared` Gate 单元测试 | 78 passed，0 failed，0 skipped。 |
| 本轮已执行范围合计 | 2734 passed，0 failed，8 skipped。 |
| SQL Server 集成测试 | 6 skipped，0 failed；新受控 fixture 已编译并验证门控关闭原因，未配置安全外部数据库，未计入通过。 |
| Oracle 集成测试 | 仅 Gate/Skip 合同已单测；本轮不建立 DDL/DML reset，未计入真实聚合执行通过。 |
| Git 操作 | 未执行 commit 或 push。 |

依赖包漏洞告警位于未修改的 `Bing.TextTemplating.Scriban`、`Bing.AutoMapper`、`Bing.MailKit` 和 `Bing.Tests.Samples` 依赖链。本轮未进行跨模块依赖升级；这些告警不来自聚合实现，仍应单独规划兼容性验证后的升级。

## Append SQL 本轮追溯

| 生产代码 | 测试项目 | 测试类 | 测试方法 | 测试类型 |
| --- | --- | --- | --- | --- |
| `FromClause.AppendSql` / `Extensions.AppendFrom` | `Bing.Data.Sql.Tests` | `AppendRawSqlContractTest` | `AppendFrom_WhenRawTextIsBlankOrStructuredFromExists_ShouldApplyDocumentedBehavior`; `AppendFrom_WhenCalledRepeatedly_ShouldNotInsertSeparator` | Unit |
| `JoinClause.AppendJoin` / `JoinClause.AppendOn` / `JoinClause.On` / `JoinItem.GetOn` | `Bing.Data.Sql.Tests`; `Bing.Dapper.Oracle.Tests` | `AppendJoinAndOnCompositionTest`; `JoinClauseTest`; `OracleJoinClauseTest` | `AppendJoin_WhenFollowedByAppendOn_ShouldComposeSql`; `AppendLeftJoin_WhenFollowedByAppendOn_ShouldComposeSql`; `AppendRightJoin_WhenFollowedByAppendOn_ShouldComposeSql`; `AppendJoin_WhenMultipleJoinsExist_ShouldBindOnToLatestJoin`; `AppendJoin_WhenPreviousJoinContainsOn_ShouldBindAppendOnToLatestJoin`; `AppendJoin_WhenJoinAlreadyContainsOn_ShouldAppendAdditionalCondition`; `AppendOn_WhenNoJoinExists_ShouldThrowWithoutChangingFollowingJoin`; `AppendOn_WhenNoJoinExistsAndSqlIsWhitespace_ShouldDoNothing`; `OnValue_WhenNoJoinExists_ShouldThrowWithoutAddingParameter`; `OnExpression_WhenNoJoinExists_ShouldThrowWithoutAddingParameter`; `Test_On_1`; `Test_On_WithoutJoin_ShouldThrowWithoutAddingParameter` | Unit |
| `SqlParameterExtensions.AddParam` / `ParameterManager` / `SqlBuilderBase.Clone` | `Bing.Data.Sql.Tests` | `AppendRawParameterBindingTest` | `AppendFrom_WithParameter_ShouldPreserveSqlAndBindValue`; `AppendJoin_WithParameter_ShouldPreserveSqlAndBindValue`; `AppendOn_WithParameter_ShouldPreserveSqlAndKeepParameterStableAcrossRepeatedRendering`; `AppendFrom_WithMultipleParameters_ShouldBindAllValuesInOrder`; `AppendFrom_WithRawAndStructuredParameters_ShouldNotConflict`; `AppendRawSql_WhenToSqlCalledRepeatedly_ShouldNotDuplicateParameters`; `AppendRawSql_WhenCloned_ShouldKeepParameterChangesIsolated`; `AppendRawSql_WhenParameterNameIsRepeated_ShouldReplaceValueWithoutDuplicate`; `AppendRawSql_WithMultipleStructuredParameters_ShouldKeepAllParameters` | Unit |
| `SelectClause.AppendSql` / `WhereClause.AppendSql` / `GroupByClause.AppendSql` / `OrderByClause.AppendSql` / `JoinItem.AppendOn` / `Helper.ResolveSql` | `Bing.Data.Sql.Tests` | `AppendSqlSemanticTest` | `AppendSelect_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers`; `AppendWhere_WhenSqlContainsParameter_ShouldResolveIdentifiersAndKeepExplicitParameter`; `AppendGroupBy_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers`; `AppendOrderBy_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers`; `AppendOn_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers` | Unit |
| SQLite query/executor parameter binding | `Bing.Dapper.Sqlite.Tests.Integration` | `SqliteExecutionIntegrationTest` | `AppendFrom_WithRawParameter_ShouldExecuteSuccessfully`; `AppendFrom_WithRawAndStructuredParameters_ShouldExecuteSuccessfully` | SQLite Integration |
| MySQL dotted physical-table schema, metadata and query binding | `Bing.Dapper.MySql.Tests.Integration` | `MySqlQueryTest` | `LeftJoin_WithDottedPhysicalTables_ShouldExecuteSuccessfully`; `LeftJoin_WithTypedDottedPhysicalTables_ShouldExecuteSuccessfully`; `AppendLeftJoin_WithDottedPhysicalTables_ShouldExecuteSuccessfully`; `LeftJoin_WhenDottedMerchantDoesNotMatch_ShouldReturnCompanyWithNullMerchant`; `AppendFrom_WithDottedPhysicalTableAndParameter_ShouldExecuteSuccessfully`; `AppendJoin_WithDottedPhysicalTableAndParameter_ShouldExecuteSuccessfully` | Gated MySQL Integration |
| `MySqlCrossDatabaseFactAttribute` / `IntegrationDatabaseSafetyValidator` / structured and raw MySQL qualified table rendering | `Bing.Dapper.MySql.Tests.Integration` | `MySqlCrossDatabaseQueryTest` | `AppendFrom_WhenCrossDatabaseIsEnabled_ShouldExecuteDottedPhysicalTableQuery`; `From_WhenUsingStructuredCrossDatabaseReference_ShouldExecuteDottedPhysicalTableQuery`; `LeftJoin_WhenUsingStructuredCrossDatabaseReference_ShouldExecuteAndPreserveUnmatchedRows` | Opt-in MySQL Integration: requires a precreated dedicated cross-database; creates, inserts, queries and drops only ``Merchants.Company`` and ``Merchants.Merchant`` tables in `finally`, never creates or drops a database |
| MySQL shared Doris builder behavior | `Bing.Dapper.MySql.Tests` | `MySqlRoutingAndMappingTest` | `Doris_StructuredFrom_ShouldKeepSegmentedName`; `Doris_AppendFrom_ShouldPreserveRawSql`; `Doris_AppendJoin_ShouldPreserveRawSql` | Unit |
| Provider raw Append paths | `Bing.Dapper.MySql.Tests`; `Bing.Dapper.SqlServer.Tests`; `Bing.Dapper.PostgreSql.Tests`; `Bing.Dapper.Oracle.Tests`; `Bing.Dapper.Sqlite.Tests` | Each Provider `AppendRawSqlTest` | `AppendFrom_ShouldPreserveRawSql`; `AppendJoin_ShouldPreserveRawSql`; `AppendLeftJoin_ShouldPreserveRawSql`; `AppendRightJoin_ShouldPreserveRawSql` | Unit |

## 其他 Append API 语义矩阵

| API | 文本是否字节原样 | 方言处理 | 参数行为 | Alias 注册 |
| --- | --- | --- | --- | --- |
| `AppendFrom` | 是 | 否 | 调用方通过 `AddParam` 显式绑定 | 否 |
| `AppendJoin` / `AppendLeftJoin` / `AppendRightJoin` | 是 | 否 | 调用方通过 `AddParam` 显式绑定 | 否 |
| `AppendSelect` | 否 | `[]` 经 `Helper.ResolveSql` 转为当前方言标识符 | 调用方通过 `AddParam` 显式绑定 | 否 |
| `AppendWhere` | 否 | `[]` 经 `Helper.ResolveSql` 转为当前方言标识符 | 支持显式参数；不自动解析占位符 | 否 |
| `AppendGroupBy` | 否 | `[]` 经 `Helper.ResolveSql` 转为当前方言标识符 | 调用方负责参数 | 否 |
| `AppendOrderBy` | 否 | `[]` 经 `Helper.ResolveSql` 转为当前方言标识符 | 调用方负责参数 | 否 |
| `AppendOn` | 否 | `[]` 经 `Helper.ResolveSql` 转为当前方言标识符 | 空白文本无操作；非空文本仅追加到最后一个 Join，无 Join 时抛出 `InvalidOperationException` | 否 |

## MySQL Provider 性能基线

基准工程直接引用真实 `Bing.Dapper.MySql` Provider，并通过 `InternalsVisibleTo` 测量 `MySqlTableNameParser`。本次基准使用 `.NET 8.0.27`、BenchmarkDotNet `0.14.0` 的 `FormalHost`（1 launch、6 warmup、15 iteration），为首次同源基线；未将历史自定义方言报告用于对比。

| 场景 | 基线结论 | 报告 |
| --- | --- | --- |
| Parser simple/dotted/quoted/qualified/alias | 86.53 ns 至 297.24 ns，分配 328 B 至 944 B。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.MySqlTableNameParserBenchmarks-report-github.md` |
| Builder simple/dotted/qualified/complex/clone | 448.8 ns 至 1.828 us，复杂 Builder Clone 为 703.6 ns。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.MySqlBuilderBenchmarks-report-github.md` |
| Repeated `ToSql` | 1/10/100 次分别为 1.828 us、18.058 us、181.137 us，线性增长。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.MySqlRepeatedRenderBenchmarks-report-github.md` |
| Raw Append Join 1/5/20 | 853.7 ns、1.805 us、4.753 us，未观察到平方级增长。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.MySqlAppendBenchmarks-report-github.md` |

本轮未修改生产热路径：基线没有显示超过验收门槛的异常增长或明显平方级分配；“优化后”结果不适用。所有基准场景由 `MySqlTableNameParserBenchmarks`、`MySqlBuilderBenchmarks`、`MySqlRepeatedRenderBenchmarks` 和 `MySqlAppendBenchmarks` 提供，源文件为 `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`。

本次修复影响字符串聚合列在配置时的限定标识符拆分，并使 `ColumnItem.Clone` 复制已有聚合函数状态；不涉及映射缓存或重复渲染热路径。现有基准场景不覆盖该一次性配置和状态复制分支，因此基准更新不适用；两种目标方言已由直接 Clone 单元测试覆盖。

## SQL Builder 执行准备与跨库闭环追溯

| 生产代码 | 测试项目 | 测试类 | 测试方法 | 测试类型 |
| --- | --- | --- | --- | --- |
| `ISqlBuilder.ToDebugSql(string)` / `SqlBuilderBase.ToDebugSql(string)` | `Bing.Data.Sql.Tests` | `SqlBuilderTest` | `ToDebugSql_WhenSqlIsProvided_ShouldReuseSqlAndPreserveOutput`; `ToDebugSql_WhenParameterNameOrValueContainsRegexCharacters_ShouldPreserveLiteralValue`; `ToDebugSql_WhenParametersHavePrefixes_ShouldReplaceOnlyStandaloneParameters`; `ToDebugSql_WhenSqlIsNull_ShouldThrowArgumentNullException` | Unit; verifies `@p/@p1/@p10` and `@Tenant/@TenantId` prefix isolation and prevents replacement inside `x@p`. |
| `ISqlQuery` / `SqlQueryBase` | N/A | N/A | XML `<remarks>` declares that mutable Builder, connection and transaction state makes an instance unsuitable for concurrent sharing. | Documentation-only public contract; no runtime behavior changed, so no executable test applies. |
| `SqlQueryBase.WriteTraceLog(ISqlBuilder, string)` / `InternalQuery` / `InternalQueryAsync` / `StreamQueryIterator` / `StreamQueryAsync` / `GetCount` / `GetCountAsync` / `PagerQuery` / `PagerQueryAsync` | `Bing.Dapper.SqlServer.Tests` | `SqlServerRoutingAndExecutionTest` | `ExecuteScalar_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`; `ExecuteScalarAsync_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`; `StreamQuery_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`; `StreamQueryAsync_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`; `GetCount_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`; `ExecuteScalar_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql`; `ExecuteQueryAsync_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql`; `GetCount_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql`; `StreamQuery_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql`; `PagerQuery_WhenTraceIsEnabled_ShouldReuseExecutedSqlForCountAndData`; `ExecuteScalar_WhenDebugLogIsDisabled_ShouldNotRenderDebugSql`; `ExecuteScalar_WhenExecutionFails_ShouldRenderSqlOnceAndPublishError`; `ExecuteScalarAsync_WhenExecutionFails_ShouldRenderSqlOnceAndPublishError`; `StreamQueryAsync_WhenExecutionFails_ShouldRenderSqlOnceAndPublishError` | Unit with capture connection; verifies the expected `ToSql` count per execution (two independent executions for paging), zero or one `ToDebugSql(string)` per execution, matching executed/debug SQL, Error diagnostics and command-failure resource behavior. |
| `MySqlFromClause.ParseTableName` / `MySqlJoinClause.ParseTableName` through public string APIs | `Bing.Dapper.MySql.Tests.Integration` | `MySqlCrossDatabaseQueryTest` | `From_WhenUsingQualifiedDottedPhysicalTable_ShouldExecuteQuery`; `Join_WhenUsingQualifiedDottedPhysicalTable_ShouldExecuteQuery`; `LeftJoin_WhenUsingQualifiedDottedPhysicalTable_ShouldPreserveUnmatchedRow` | Opt-in MySQL Integration: uses only the precreated dedicated cross database, creates/drops tables in `finally`, and deletes only generated primary-database rows |

### 最终执行准备性能验收

本次正式基准使用 BenchmarkDotNet `0.14.0` 的固定 `FormalHost` Job（1 launch、6 warmup、15 iteration）。主机环境为 Windows 10、Intel Core Ultra 7 270K Plus、.NET SDK `10.0.300`、Host runtime `.NET 8.0.27`。由于机器未安装 .NET 8 SDK，Job 使用 HostProcess 固定实际运行时，避免把跨 SDK 的结果误作可比较数据。

| 场景 | 二次渲染基线 | SQL 复用结果 | 结论 | 报告 |
| --- | ---: | ---: | --- | --- |
| `SqlDebugRenderingBenchmarks` | 1.595 us，8.15 KB | 690.2 ns，2.17 KB，Ratio 0.43 | Debug SQL 复用减少约 56.7% 时间和 73.4% 分配。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlDebugRenderingBenchmarks-report-github.md` |
| `SqlQueryExecutionPreparationBenchmarks` Trace 开启 | 3.463 us，23.26 KB | 2.022 us，12.51 KB，Ratio 0.58 | 复用执行 SQL 减少约 41.6% 时间和 46.2% 分配。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlQueryExecutionPreparationBenchmarks-report-github.md` |
| `SqlQueryExecutionPreparationBenchmarks` Trace 关闭 | 不适用 | 1.424 us，10.75 KB，Ratio 0.41（相对 Trace 二次渲染） | Trace 关闭不再生成 Debug SQL。 | `BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlQueryExecutionPreparationBenchmarks-report-github.md` |

`MySqlBuilderConstructionBenchmarks` 和 `MySqlBuilderCloneBenchmarks` 已纳入相同 `FormalHost` Job，覆盖仅 From、1/5/20 Join、10/50 参数、raw/structured 混合场景。它们用于后续识别 Builder 构建或 Clone 热点；本轮数据只证明执行准备重复渲染是明确热点，因此未对 parser、Join 渲染或 Clone 增加无数据支撑的微优化。

### Debug SQL 大参数性能验收

`SqlDebugRenderingLargeParameterBenchmarks` 使用真实 `MySqlBuilder` 生成 `@_p_0` 至 `@_p_n` 的参数前缀场景，并与无参 `ToDebugSql()` 的二次 SQL 渲染做同 Job 对比。本轮正式运行仍使用 `.NET 8.0.27`、BenchmarkDotNet `0.14.0`、`FormalHost`（1 launch、6 warmup、15 iteration）。

| 参数数量 | 二次渲染基线 | 复用已生成 SQL | 时间 Ratio | 分配 Ratio | 结论 |
| ---: | ---: | ---: | ---: | ---: | --- |
| 10 | 3.555 us，18.91 KB | 2.406 us，10.13 KB | 0.68 | 0.54 | SQL 复用减少约 32% 时间和 46% 分配。 |
| 50 | 65.229 us，360.71 KB | 62.866 us，336.07 KB | 0.96 | 0.93 | 参数字面量替换成为主要成本，复用仍减少约 4% 时间和 7% 分配。 |
| 100 | 142.501 us，980.74 KB | 137.672 us，936.27 KB | 0.97 | 0.95 | 参数字面量替换成为主要成本，复用仍减少约 3% 时间和 5% 分配。 |

报告：`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlDebugRenderingLargeParameterBenchmarks-report-github.md`。50/100 参数的收益低于引入新替换算法的行为风险阈值，因此本轮不做无数据支撑的热路径重构。

### 最终验收执行证据

2026-07-23 在 Windows 10、.NET SDK `10.0.300`、.NET 6.0.36/.NET 8.0.27 环境执行：

| 验收项 | 命令范围 | 结果 |
| --- | --- | --- |
| Release 编译 | `dotnet build .\Bing.All.sln -c Release -nologo -v minimal` | 成功；125 条警告。 |
| SQL/Provider 单元回归 | `Bing.Data.Sql.Tests`、MySql、SqlServer、PostgreSql、Oracle、Sqlite、EntityFrameworkCore、FreeSQL | 2142 项通过，0 失败。 |
| SQLite 集成 | `Bing.Dapper.Sqlite.Tests.Integration` | 82 项通过，0 失败。 |
| MySQL 真实集成 | 项目现有受控 runsettings，双 TFM 串行 | 86 项通过，0 失败；涵盖公开字符串 From/Join/LeftJoin 跨库带点物理表名。 |
| 大参数性能 | `SqlDebugRenderingLargeParameterBenchmarks`，固定 `FormalHost` Job | 6 个正式基准完成；产物路径见上。 |

## SQL Builder 生命周期、Context、Provider Clone 与 Item 模型追溯

| 生产代码 | 测试项目 | 测试类 | 测试方法 | 测试类型 |
| --- | --- | --- | --- | --- |
| `SqlBuilderBase.New` / `SqlBuilderBase.Clone` / `SqlBuilderBase.Clear` / `SqlBuilderBase.ClearSelect` / `SqlBuilderBase.ClearFrom` / `SqlBuilderBase.ClearJoin` | `Bing.Data.Sql.Tests`; `Bing.Dapper.MySql.Tests` | `SqlBuilderTest`; `AppendRawSqlContractTest`; `MySqlBuilderTest` | `New_WhenSourceHasAggregate_ShouldNotShareAggregateState`; `Clone_WhenRawAndExpressionAggregatesExist_ShouldPreserveAndIsolateState`; `ClearSelect_WhenDistinctAggregateExists_ShouldRemoveAllSelectState`; `AppendRawSql_ShouldRemainStableAcrossCloneNewClearAndRepeatedRendering`; `CloneAndClear_WhenNormalTableStateExists_ShouldKeepInstancesIsolated` | Unit; verifies New and Clone isolation, Clear lifecycle reset, repeated rendering stability, and retention of documented provider state. |
| `SqlBuilderDependencies` / `SqlClauseContext` / `SqlBuilderBase.CreateClauseContext` | `Bing.Data.Sql.Tests` | `SqlClauseContextTest`; `BuilderNewLifecycleTest` | `Rebind_ShouldReplaceRuntimeStateAndPreserveSharedDependencies`; `New_ShouldShareDependenciesAndUseIndependentRuntimeState`; `New_WhenSourceContainsParameters_ShouldReturnEmptyParameters`; `New_ShouldReturnSameBuilderType` | Unit; immutable共享服务依赖可复用，Builder、别名、实体解析器和参数管理器等运行状态在 Rebind/New 后独立。 |
| `MySqlBuilder.Clone` / `MySqlFromClause.Clone` / `MySqlJoinClause.Clone` | `Bing.Dapper.MySql.Tests` | `MySqlBuilderTest` | `Clone_WhenJoinUsesDottedPhysicalTable_ShouldPreserveMySqlStringTableStrategy`; `CloneAndClear_WhenNormalTableStateExists_ShouldKeepInstancesIsolated`; `Clone_WhenCountUsesQualifiedColumn_ShouldPreserveAggregation` | Unit; preserves MySQL quote-aware string-table parsing strategy, dotted physical-table behavior, aggregate state and independent mutable state after cloning. |
| `SqlServerBuilder.Clone` / `PostgreSqlBuilder.Clone` / `OracleBuilder.Clone` / `SqliteBuilder.Clone` | `Bing.Dapper.SqlServer.Tests`; `Bing.Dapper.PostgreSql.Tests`; `Bing.Dapper.Oracle.Tests`; `Bing.Dapper.Sqlite.Tests` | Provider `*BuilderTest` | Provider Clone, qualified-name rendering, aggregate and repeated-render regression cases | Unit; provider builders retain their dialect formatter, clause model and parameter behavior after Clone without sharing source-builder mutations. |
| `SqlItem.Clone` / `ColumnItem.Clone` / `JoinItem.Clone` / `StructuredSqlItem.Clone` | `Bing.Data.Sql.Tests`; `Bing.Dapper.MySql.Tests`; `Bing.Dapper.PostgreSql.Tests` | `SqlItemTest`; `SqlBuilderTest`; `MySqlBuilderTest`; `PostgreSqlBuilderTest` | `LegacyAggregationFunction_WhenCloned_ShouldPreserveRendering`; `Clone_WhenRawAndExpressionAggregatesExist_ShouldPreserveAndIsolateState`; `Clone_WhenJoinUsesDottedPhysicalTable_ShouldPreserveMySqlStringTableStrategy`; `Clone_WhenCountUsesQualifiedColumn_ShouldPreserveAggregation` | Unit; item models copy structured/raw SQL, aggregation metadata, aliases, join conditions and provider-specific table-reference state without retaining shared mutable collections. |
| `ColumnItem.AggregateFunction` / `ColumnItem.AggregationFunc` / `ColumnItem.IsAggregation` / `SqlItem.AggregationFunc` | `Bing.Data.Sql.Tests` | `SqlItemTest`; `SqlBuilderTest` | `LegacyAggregationFunction_WhenCloned_ShouldPreserveRendering`; `Clone_WhenRawAndExpressionAggregatesExist_ShouldPreserveAndIsolateState` | Unit; obsolete compatibility members retain rendering behavior, while new aggregation state is represented by `SqlAggregateFunction`. |
| `ColumnItem.CreateColumn` / `CreateAggregate` / `CreateAggregateExpression` / `CreateAggregateRaw` / `ColumnItem.Clone` | `Bing.Data.Sql.Tests` | `ColumnItemFactoryTest` | `CreateColumn_ShouldCreateNormalColumn`; `CreateAggregate_WhenCloned_ShouldPreserveStructuredDescriptor`; `CreateAggregateExpressionAndRaw_ShouldPreserveArgumentText` | Unit; structured normal、聚合、表达式与 raw 列构造路径保留完整 SQL 语义和 Clone 描述符。 |
| `ParameterManager.GenerateName` / `SqlBuilderBase.MergeSubqueryParameters` / `WhereClause.And` / `WhereClause.Or` | `Bing.Data.Sql.Tests` | `ParameterManagerTest`; `SqlBuilderTest` | `GenerateName_WhenGeneratedNameAlreadyExists_ShouldSkipExistingName`; `TestAnd_2`; `TestOr_3`; `TestIn_3`; `Test_Union_1` | Unit; independent New Builder 的参数会在组合时完整合并，名称冲突稳定重命名，重复渲染不覆盖已有参数。 |

### 生命周期与 Clone 性能基准

## Provider SPI 收敛追溯

| 生产符号 | 关键行为 | 验证 |
| --- | --- | --- |
| `ISqlProvider.Key` / `SqlBuilderFactory.Create(string)` | Provider Key 是大小写不敏感、去除首尾空白的正式查找键；未知 Key 明确失败。 | `CustomProviderBuilderTest.Factory_WhenProviderKeyUsesDifferentCaseAndWhitespace_ShouldCreateExpectedBuilder`; `Factory_WhenProviderKeyIsUnknownOrDuplicated_ShouldThrowWithKey` |
| `SqlBuilderFactory` / `SqlBuilderFactoryRegistration` | 不同 Key 可共享同一 `DatabaseType`；兼容的 `DatabaseType` 查找保留首个注册项。 | `CustomProviderBuilderTest.Factory_WhenDifferentProviderKeysShareDatabaseType_ShouldAllowRegistration` |
| `SqlBuilderFactory.Create(ISqlProvider, SqlBuilderServices)` | Factory 原样传递查询级服务，避免把 Query 选项或数据库上下文提升为静态状态。 | `CustomProviderBuilderTest.Factory_WhenQueryServicesAreProvided_ShouldPassSameInstanceToBuilder` |
| `SqlQueryBase.CreateSqlBuilder` / `AddSqlBuilderProvider` | 五个 Dapper Provider 的 Query/Executor 通过 DI Factory 创建 Builder，便捷注册和多 Provider 注册均注册映射。 | `Bing.Dapper.SqlServer.Tests`、`Bing.Dapper.MySql.Tests`、`Bing.Dapper.PostgreSql.Tests`、`Bing.Dapper.Oracle.Tests`、`Bing.Dapper.Sqlite.Tests`、`Bing.EntityFrameworkCore.Tests` Release 回归 |
| `SqlServerSqlProvider.MaxParameterCount` | SQL Server 参数数上限固定为 2100；其他官方 Provider 显式声明无上限。 | `OfficialProviderInstanceTest.Provider_WhenParameterLimitIsRequested_ShouldReturnOfficialContract`; `CustomProviderBuilderTest.ParameterLimit_WhenLimitReached_ShouldRejectNewParameterAndAllowReplacement` |
| `OraclePaginationRenderer` | Oracle 目标版本为 12c+，分页渲染为 `Offset ... Rows Fetch Next ... Rows Only`，Clone/New 保持参数隔离。 | `OracleBuilderTest.Page_WhenSkipAndTakeAreSet_ShouldRenderOracleOffsetFetchSyntax`; `Clone_WhenPageIsConfigured_ShouldKeepOracleOffsetFetchSyntaxAndParameters`; `New_WhenPageIsConfigured_ShouldUseNewOraclePaginationParameters` |
| `SqlIdentifierPathParser` / `SqlAggregateArgumentValidator` / `SqlTableNameParser` | 严格解析结构化标识符，拒绝不完整路径与语句分隔符；聚合通配符仅允许 `Count(*)`。 | `SqlInternalParserTest.IdentifierPathParser_WhenQuotedThreePartPathIsProvided_ShouldReturnLogicalSegments`; `IdentifierPathParser_WhenPathIsInvalid_ShouldReturnFalse`; `AggregateArgumentValidator_WhenWildcardContractIsViolated_ShouldThrow`; `TableNameParser_WhenValidAliasOrUnsafeInputIsProvided_ShouldParseOrReject` |

已删除兼容符号：`LegacySqlProvider`、`SqlClauseContext.Create(...)`、`SqlItem.AggregationFunc`、`ColumnItem.AggregationFunc`、`ColumnItem.IsAggregation`。聚合状态仅由 `SqlAggregateFunction` 与 `SqlAggregateDescriptor` 表示。

### Provider SPI 发布前性能验收

2026-07-27 在 Windows 10、Intel Core Ultra 7 270K Plus、.NET SDK `10.0.300`、.NET runtime `8.0.27` 上完成 90 个场景的 BenchmarkDotNet `0.14.0` 正式运行。所有基准统一使用 `FormalHost`：3 次启动、6 次预热、15 次迭代。

| 场景 | Mean | Allocated | 结论 |
| --- | ---: | ---: | --- |
| MySQL 复杂 Builder 渲染 | 1.723 us | 12.27 KB | Provider Clause、分页和参数渲染维持稳定。 |
| MySQL 复杂 Builder Clone | 482.2 ns | 3.72 KB | Provider Builder 的 Clone 保持低于 0.5 us。 |
| `NewEmptyBuilder` | 335.3 ns | 4.62 KB | New 生命周期创建独立状态，不共享参数集合。 |
| 20 Join Clone | 684.3 ns | 5.38 KB | Join 状态复制随规模线性增长，无异常退化。 |
| 50 参数 Clone | 2.086 us | 18.42 KB | 参数复制为预期主要成本。 |
| 100 映射配置缓存命中 | 293.0 ns | 64 B | 映射缓存命中未随 Provider SPI 变更失效。 |
| Trace 关闭执行准备 | 1.301 us | 10.04 KB | 不生成 Debug SQL 的路径保持最低开销。 |
| Trace 使用已生成 SQL | 1.944 us | 11.86 KB | 相比二次渲染 3.355 us / 21.90 KB，时间约降低 42%，分配约降低 46%。 |

全部产物位于 `BenchmarkDotNet.Artifacts/pre-release-final/results/`，包含 MySQL 表名解析、Builder 渲染/构造/Clone、重复渲染、Append、聚合、Debug SQL、执行准备和元数据缓存报告。

`MySqlBuilderConstructionBenchmarks` 与 `MySqlBuilderCloneBenchmarks` 使用与执行准备基准相同的 BenchmarkDotNet `0.14.0` `FormalHost` Job（1 launch、6 warmup、15 iteration），覆盖仅 From、1/5/20 Join、10/50 参数以及 raw/structured 混合场景。基准源为 `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`。

| 场景 | 覆盖范围 | 基线结论 |
| --- | --- | --- |
| `MySqlBuilderConstructionBenchmarks` | Builder 创建、结构化 From、raw/structured Join 组合及参数注册。 | 用于识别 Context、Clause 和 Item 组装成本；本轮未观察到需要改变生产热路径的异常增长。 |
| `MySqlBuilderCloneBenchmarks` | Clone 仅 From、1/5/20 Join、10/50 参数及 raw/structured 混合 Builder。 | 用于验证 Provider Clone 与 Item 状态复制成本；本轮未引入无数据支撑的 Clone 微优化。 |
| `MySqlRepeatedRenderBenchmarks` | 同一 Builder 的 1/10/100 次 `ToSql`。 | 1.828 us、18.058 us、181.137 us，保持线性增长，未观察到 Context 或 Item 状态导致的平方级增长。 |
| `SqlDebugRenderingBenchmarks` / `SqlQueryExecutionPreparationBenchmarks` | 已生成 SQL 的 Debug/Trace 复用。 | 复用执行 SQL 降低重复渲染成本；生命周期调整不改变已验证的 SQL、参数和 Debug 输出合同。 |
| `NewEmptyBuilder` | 空 MySQL Builder 的 New 生命周期。 | Mean 326.9 ns，Allocated 4.64 KB，Gen0 0.2522，Gen1 0.0033，无 Gen2。 |
| `CloneTenAggregates` | 十个结构化聚合列的 Provider Builder Clone。 | Mean 496.2 ns，Allocated 5.25 KB，Gen0 0.2851，Gen1 0.0038，无 Gen2。 |
| `RebindClauseContext` | Context 对新 Builder、AliasRegister、ParameterManager 的重绑定。 | Mean 260.3 ns，Allocated 3.75 KB，Gen0 0.2036，Gen1 0.0024，无 Gen2。 |

## 参数管理器合同与快照追溯

| 生产代码 | 测试项目 | 测试类 | 测试方法 | 测试类型 |
| --- | --- | --- | --- | --- |
| `IParameterManager` / `IAdvancedParameterManager` / `ISqlBuilder` / `ISqlQuery` / `SqlQueryBase` | `Bing.Data.Sql.Tests` | `BuilderNewLifecycleTest`; `BuilderCloneIsolationTest` | `New_WhenIndependentBuildersRunConcurrently_ShouldKeepSqlAndParametersIsolated`; `Clone_WhenIndependentBuildersRunConcurrently_ShouldKeepSourceAndClonesIsolated` | Documentation and Unit; 单个 Builder、Query 和 ParameterManager 是可变且非线程安全实例。受支持的并发模式是每个操作使用独立 New/Clone 实例；不对共享实例并发读写作出成功承诺。 |
| `ParameterManager.NormalizeName` / `Add` / `Contains` / `GetValue` / `GenerateName` | `Bing.Data.Sql.Tests` | `ParameterManagerTest` | `Add_WhenNamesUseKnownPrefixesOrDifferentCasing_ShouldReplaceSingleNormalizedParameter`; `NormalizeName_WhenDialectOrInputNameChanges_ShouldUseDialectPrefixAndIgnoreInvalidNames`; `GenerateName_WhenGeneratedNameAlreadyExists_ShouldSkipExistingName` | Unit; 剥离一个已知 `@`/`:`/`?` 前缀，再应用当前方言前缀并以 `OrdinalIgnoreCase` 比较；无效名称不写入。 |
| `ParameterManager.GetParams` / `ExportValues` / `GetSqlParams` / `CloneSqlParam` / `Clone` | `Bing.Data.Sql.Tests`; `Bing.Dapper.SqlServer.Tests` | `ParameterManagerTest`; `DefaultSqlParameterBinderTest` | `GetParamsAndExportValues_WhenManagerChangesLater_ShouldKeepOriginalSnapshots`; `GetSqlParamsAndClone_WhenParameterContainsMetadata_ShouldPreserveMetadataWithoutSharingContainer`; `GetSqlParams_WhenBuilderUsesEnhancedParameterSnapshot_ShouldPreserveMetadata` | Unit; 导出集合是调用时刻的独立快照，增强参数复制 `SqlParam` 容器、`OriginalValue` 和全部绑定元数据。任意 `Value`/`OriginalValue` 业务对象仅保留引用，不递归复制。 |
| `ParameterLimitManagerBase.EnsureCanAdd` / `ParameterLimitManager` / `AdvancedParameterLimitManager` | `Bing.Data.Sql.Tests` | `ParameterLimitManagerTest`; `AdvancedParameterLimitManagerTest` | `Add_WhenWithinLimitOrReplacingExisting_ShouldPreserveCountAndAllowClear`; `Add_WhenLimitExceeded_ShouldThrowWithProviderAndCountsWithoutMutatingState`; `CloneAndCreateEmpty_ShouldRetainLimitAndKeepParameterStateIsolated`; `AddSqlParam_WhenLimitExceeded_ShouldThrowWithoutMutatingMetadata` | Unit; 标准化后的同名替换不增加计数；无效名称不触发满额异常；快照、Clone 和 CreateEmpty 保留上限与隔离合同。 |
| `SqlBuilderBase.ApplyParameterLimit` / `CreateParameterManager` / 构造函数注入路径 | `Bing.Data.Sql.CustomProvider.Tests`; `Bing.Dapper.SqlServer.Tests` | `CustomProviderBuilderTest`; `OfficialProviderInstanceTest` | `ParameterLimit_WhenExplicitManagerIsInjected_ShouldRejectParametersBeyondProviderLimit`; `ParameterLimit_WhenBuilderIsNewOrCloned_ShouldPreserveLimitAndIsolateParameters`; `Provider_WhenParameterLimitIsRequested_ShouldReturnOfficialContract` | Unit; 工厂和显式注入的普通/增强管理器均执行 Provider 上限，已包装实例不重复装饰，Clone/New 保留限制。 |

`ParameterManagerSnapshotBenchmarks` 位于 `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`。2026-07-28 在 Windows 10、Intel Core Ultra 7 270K Plus、.NET 8.0.27、BenchmarkDotNet 0.14.0 的 `FormalHost`（3 launch、6 warmup、15 iteration）完成首次正式基线。

| 参数数量 | `GetParams` Mean / Allocated | `ExportValues` Mean / Allocated | `GetSqlParams` Mean / Allocated |
| ---: | ---: | ---: | ---: |
| 10 | 61.89 ns / 480 B | 56.89 ns / 480 B | 264.16 ns / 1,896 B |
| 100 | 479.98 ns / 3,168 B | 481.62 ns / 3,168 B | 2.608 us / 16,824 B |
| 1000 | 4.283 us / 31,056 B | 4.275 us / 31,056 B | 28.431 us / 167,112 B |

基础值快照随参数规模线性增长；增强快照额外复制 `SqlParam` 容器与元数据，时间和分配成本相应更高。完整报告：`BenchmarkDotNet.Artifacts/parameter-manager-snapshot-20260728/results/Bing.Data.Sql.Benchmarks.ParameterManagerSnapshotBenchmarks-report-github.md`。
