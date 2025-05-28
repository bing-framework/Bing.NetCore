using Bing.Auditing;
using Bing.Data;
using Bing.Data.ObjectExtending;
using Bing.Domain.Entities;
using Bing.EntityFrameworkCore.ValueComparers;
using Bing.EntityFrameworkCore.ValueConverters;
using Bing.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.EntityFrameworkCore.Modeling;

/// <summary>
/// 实体类型生成器(<see cref="EntityTypeBuilder{TEntity}"/>) 扩展
/// </summary>
public static class BingEntityTypeBuilderExtensions
{
    /// <summary>
    /// 按照约定进行配置
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureByConvention(this EntityTypeBuilder builder)
    {
        builder.TryConfigureConcurrencyStamp();
        builder.TryConfigureExtraProperties();
        builder.TryConfigureSoftDelete();
        builder.TryConfigureCreationTime();
        builder.TryConfigureDeletionTime();
        builder.TryConfigureLastModificationTime();
    }

    #region ConfigureConcurrencyStamp(配置乐观锁)

    /// <summary>
    /// 配置并发锁
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureConcurrencyStamp<T>(this EntityTypeBuilder<T> builder)
        where T : class, IVersion
    {
        builder.As<EntityTypeBuilder>().TryConfigureConcurrencyStamp();
    }

    /// <summary>
    /// 尝试配置并发锁
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureConcurrencyStamp(this EntityTypeBuilder builder)
    {
        if (!builder.Metadata.ClrType.IsBaseOn<IVersion>())
            return;
        builder.Property(nameof(IVersion.Version))
            .IsConcurrencyToken()
            .HasColumnName(nameof(IVersion.Version));
    }

    #endregion

    #region ConfigureExtraProperties(配置扩展属性)

    /// <summary>
    /// 尝试配置扩展属性
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureExtraProperties<T>(this EntityTypeBuilder<T> builder) 
        where T : class, IHasExtraProperties
    {
        builder.As<EntityTypeBuilder>().TryConfigureExtraProperties();
    }

    /// <summary>
    /// 尝试配置扩展属性
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureExtraProperties(this EntityTypeBuilder builder)
    {
        if (!builder.Metadata.ClrType.IsBaseOn<IHasExtraProperties>())
            return;
        builder.Property("ExtraProperties")
            .HasColumnName("ExtraProperties")
            .HasConversion(new ExtraPropertiesValueConverter())
            .Metadata.SetValueComparer(new ExtraPropertyDictionaryValueComparer());
    }

    #endregion

    #region ConfigureSoftDelete(配置逻辑删除)

    /// <summary>
    /// 配置逻辑删除
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureSoftDelete<T>(this EntityTypeBuilder<T> builder)
        where T : class, ISoftDelete
    {
        builder.As<EntityTypeBuilder>().TryConfigureSoftDelete();
    }

    /// <summary>
    /// 尝试配置逻辑删除
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureSoftDelete(this EntityTypeBuilder builder)
    {
        if (!builder.Metadata.ClrType.IsBaseOn<ISoftDelete>())
            return;
        builder.Property(nameof(ISoftDelete.IsDeleted))
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName(nameof(ISoftDelete.IsDeleted));
    }

    #endregion

    #region ConfigureDeletionTime(配置删除时间)

    /// <summary>
    /// 配置删除时间
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureDeletionTime<T>(this EntityTypeBuilder<T> builder) 
        where T : class, IHasDeletionTime
    {
        builder.As<EntityTypeBuilder>().TryConfigureDeletionTime();
    }

    /// <summary>
    /// 尝试配置删除时间
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureDeletionTime(this EntityTypeBuilder builder)
    {
        if (!builder.Metadata.ClrType.IsBaseOn<IHasDeletionTime>())
            return;
        builder.TryConfigureSoftDelete();
        builder.Property(nameof(IHasDeletionTime.DeletionTime))
            .IsRequired(false)
            .HasColumnName(nameof(IHasDeletionTime.DeletionTime));
    }

    #endregion

    #region ConfigureCreationTime(配置创建时间)

    /// <summary>
    /// 配置创建时间
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureCreationTime<T>(this EntityTypeBuilder<T> builder) 
        where T : class, IHasCreationTime
    {
        builder.As<EntityTypeBuilder>().TryConfigureCreationTime();
    }

    /// <summary>
    /// 尝试配置创建时间
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureCreationTime(this EntityTypeBuilder builder)
    {
        if (!builder.Metadata.ClrType.IsBaseOn<IHasCreationTime>())
            return;
        builder.Property(nameof(IHasCreationTime.CreationTime))
            .IsRequired(false)
            .HasColumnName(nameof(IHasCreationTime.CreationTime));
    }

    #endregion

    #region ConfigureLastModificationTime(配置最后修改时间)

    /// <summary>
    /// 配置最后修改时间
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureLastModificationTime<T>(this EntityTypeBuilder<T> builder)
        where T : class, IHasModificationTime
    {
        builder.As<EntityTypeBuilder>().TryConfigureLastModificationTime();
    }

    /// <summary>
    /// 尝试配置最后修改时间
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureLastModificationTime(this EntityTypeBuilder builder)
    {
        if (!builder.Metadata.ClrType.IsBaseOn<IHasModificationTime>())
            return;
        builder.Property(nameof(IHasModificationTime.LastModificationTime))
            .IsRequired(false)
            .HasColumnName(nameof(IHasModificationTime.LastModificationTime));
    }

    #endregion
}
