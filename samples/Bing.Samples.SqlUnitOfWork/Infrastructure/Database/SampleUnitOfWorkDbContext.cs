using Bing.Datas.EntityFramework.Core;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Database;

/// <summary>
/// orders 数据源映射到的 scoped EF 工作单元。
/// </summary>
public sealed class SampleUnitOfWorkDbContext : UnitOfWorkBase
{
    public SampleUnitOfWorkDbContext(DbContextOptions<SampleUnitOfWorkDbContext> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    public DbSet<OrderRecord> OrderRecords => Set<OrderRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderRecord>(builder =>
        {
            builder.ToTable("OrderRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).IsRequired();
            builder.Property(record => record.OperationId).IsRequired();
            builder.Property(record => record.Layer).IsRequired();
            builder.Property(record => record.Value).IsRequired();
        });
    }

    protected override void SaveChangesBefore()
    {
    }
}
