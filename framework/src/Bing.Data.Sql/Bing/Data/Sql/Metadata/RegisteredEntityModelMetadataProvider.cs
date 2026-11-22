using System.Collections.ObjectModel;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 读取由宿主显式提供的实体模型元数据。
/// </summary>
public sealed class RegisteredEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    private readonly IReadOnlyDictionary<Type, EntityModelMetadata> _models;

    /// <summary>
    /// 使用宿主提供的实体模型元数据初始化只读索引。
    /// </summary>
    /// <param name="models">实体模型元数据集合。</param>
    public RegisteredEntityModelMetadataProvider(params EntityModelMetadata[] models)
    {
        if (models == null)
            throw new ArgumentNullException(nameof(models));

        var indexedModels = new Dictionary<Type, EntityModelMetadata>();
        foreach (var model in models)
        {
            if (model == null)
                throw new ArgumentException("实体模型集合不能包含空项。", nameof(models));
            if (indexedModels.ContainsKey(model.EntityType))
                throw new ArgumentException($"实体类型 '{model.EntityType.FullName}' 重复登记。", nameof(models));

            indexedModels.Add(model.EntityType, model);
        }

        _models = new ReadOnlyDictionary<Type, EntityModelMetadata>(indexedModels);
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata(Type entityType)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        return _models.TryGetValue(entityType, out var model) ? model : null;
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
}
