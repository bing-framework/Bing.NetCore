using System.Collections.Generic;
using System.Data;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// SqlParam 集合诊断快照测试。
/// </summary>
public sealed class SqlQueryParameterDiagnosticTest
{
    /// <summary>
    /// 验证空 SqlParam 集合不会把集合的 Count 属性当成参数。
    /// </summary>
    [Fact]
    public void CreateParameterDiagnostics_WhenSqlParamCollectionIsEmpty_ShouldReturnNoItems()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var query = new DiagnosticQuery(serviceProvider);

        var snapshot = query.CreateSnapshot(new List<SqlParam>());

        Assert.Empty(snapshot.Items);
        Assert.False(snapshot.IsMetadataBound);
    }

    /// <summary>
    /// 验证 SqlParam 集合会保留增强元数据并对敏感值脱敏。
    /// </summary>
    [Fact]
    public void CreateParameterDiagnostics_WhenSqlParamCollectionHasValues_ShouldPreserveMetadataAndRedactSensitiveValues()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var query = new DiagnosticQuery(serviceProvider);
        var parameter = new SqlParam("apiToken", "token-value", DbType.String, ParameterDirection.Input,
            size: 128, precision: 9, scale: 3)
        {
            OriginalValue = "original-token-value",
            EntityType = typeof(DiagnosticEntity),
            PropertyName = "Token",
            ColumnName = "api_token",
            ProviderTypeName = "varchar",
            Source = SqlParameterSource.Lambda,
            MetadataLevel = SqlParameterMetadataLevel.Full
        };

        var snapshot = query.CreateSnapshot(new List<SqlParam>
        {
            parameter,
            new("pageSize", 25, DbType.Int32)
            {
                Source = SqlParameterSource.Manual,
                MetadataLevel = SqlParameterMetadataLevel.Partial
            }
        });

        Assert.Equal(2, snapshot.Items.Count);
        var item = snapshot.Items[0];
        Assert.Equal("apiToken", item.Name);
        Assert.Null(item.Value);
        Assert.Null(item.OriginalValue);
        Assert.True(item.IsSensitive);
        Assert.Equal(DbType.String, item.DbType);
        Assert.Equal(ParameterDirection.Input, item.Direction);
        Assert.Equal(128, item.Size);
        Assert.Equal((byte)9, item.Precision);
        Assert.Equal((byte)3, item.Scale);
        Assert.Equal(typeof(DiagnosticEntity).FullName, item.EntityType);
        Assert.Equal("Token", item.PropertyName);
        Assert.Equal("api_token", item.ColumnName);
        Assert.Equal("varchar", item.ProviderTypeName);
        Assert.Equal(SqlParameterSource.Lambda, item.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, item.MetadataLevel);
        var ordinaryItem = snapshot.Items[1];
        Assert.Equal("pageSize", ordinaryItem.Name);
        Assert.Equal(25, ordinaryItem.Value);
        Assert.False(ordinaryItem.IsSensitive);
        Assert.Equal(DbType.Int32, ordinaryItem.DbType);
        Assert.Equal(SqlParameterSource.Manual, ordinaryItem.Source);
        Assert.Equal(SqlParameterMetadataLevel.Partial, ordinaryItem.MetadataLevel);
        Assert.False(snapshot.IsMetadataBound);
    }

    /// <summary>
    /// 用于暴露 SqlQueryBase 参数快照工厂的最小查询实现。
    /// </summary>
    private sealed class DiagnosticQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化一个 <see cref="DiagnosticQuery" /> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <remarks>
        /// 初始化测试查询。
        /// </remarks>
        public DiagnosticQuery(IServiceProvider serviceProvider) : base(serviceProvider, new SqlOptions())
        {
        }

        /// <summary>
        /// 创建指定参数对象的诊断快照。
        /// </summary>
        /// <param name="parameters">原始参数对象。</param>
        /// <returns>参数诊断快照。</returns>
        public SqlParameterDiagnosticSnapshot CreateSnapshot(object parameters) =>
            CreateParameterDiagnostics(parameters, Array.Empty<SqlParameterDiagnosticInfo>());

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 用于验证 SqlParam 实体元数据的测试实体。
    /// </summary>
    private sealed class DiagnosticEntity
    {
    }
}
