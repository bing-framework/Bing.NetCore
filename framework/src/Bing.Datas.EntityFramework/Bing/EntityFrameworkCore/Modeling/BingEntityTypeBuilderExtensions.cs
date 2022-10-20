using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;
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
        if (builder.Metadata.ClrType.IsBaseOn<IVersion>())
        {
            builder.Property(nameof(IVersion.Version))
                .IsConcurrencyToken()
                .HasColumnName(nameof(IVersion.Version));
        }
    }

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
        if (builder.Metadata.ClrType.IsBaseOn<ISoftDelete>())
        {
            builder.Property(nameof(ISoftDelete.IsDeleted))
                .IsRequired()
                .HasDefaultValue(false)
                .HasColumnName(nameof(ISoftDelete.IsDeleted));
        }
    }

    /// <summary>
    /// 配置删除时间
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureDeletionTime<T>(this EntityTypeBuilder<T> builder) where T : class, IHasDeletionTime
    {
        builder.As<EntityTypeBuilder>().TryConfigureDeletionTime();
    }

    /// <summary>
    /// 尝试配置删除时间
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureDeletionTime(this EntityTypeBuilder builder)
    {
        if (builder.Metadata.ClrType.IsBaseOn<IHasDeletionTime>())
        {
            builder.TryConfigureSoftDelete();
            builder.Property(nameof(IHasDeletionTime.DeletionTime))
                .IsRequired(false)
                .HasColumnName(nameof(IHasDeletionTime.DeletionTime));
        }
    }

    /// <summary>
    /// 尝试配置创建时间
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="builder">实体类型生成器</param>
    public static void ConfigureCreationTime<T>(this EntityTypeBuilder<T> builder) where T : class, IHasCreationTime
    {
        builder.As<EntityTypeBuilder>().TryConfigureCreationTime();
    }

    /// <summary>
    /// 尝试配置创建时间
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    public static void TryConfigureCreationTime(this EntityTypeBuilder builder)
    {
        if (builder.Metadata.ClrType.IsBaseOn<IHasCreationTime>())
        {
            builder.Property(nameof(IHasCreationTime.CreationTime))
                .IsRequired(false)
                .HasColumnName(nameof(IHasCreationTime.CreationTime));
        }
    }
}
