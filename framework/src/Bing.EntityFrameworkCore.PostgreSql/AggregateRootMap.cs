﻿using Bing.Datas.EntityFramework.Core;

namespace Bing.Datas.EntityFramework.PgSql;

/// <summary>
/// 聚合根映射配置
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class AggregateRootMap<TEntity> : MapBase<TEntity>, IMap where TEntity : class
{
}
