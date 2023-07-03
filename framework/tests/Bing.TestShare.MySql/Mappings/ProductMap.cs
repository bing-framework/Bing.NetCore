//using Bing.Tests.Configs;
//using Bing.Tests.Models;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Metadata.Builders;

//namespace Bing.Tests.Mappings;

//public class ProductMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<Product>
//{
//    /// <summary>
//    /// 映射表
//    /// </summary>
//    /// <param name="builder">实体类型生成器</param>
//    protected override void MapTable(EntityTypeBuilder<Product> builder)
//    {
//        builder.ToTable("Product").HasComment("产品");
//    }

//    /// <summary>
//    /// 映射属性
//    /// </summary>
//    /// <param name="builder">实体类型生成器</param>
//    protected override void MapProperties(EntityTypeBuilder<Product> builder)
//    {
//        builder.Property(t => t.Id)
//            .HasColumnName("ProductId")
//            .HasComment("产品标识");
//        builder.Property(t => t.Code)
//            .HasColumnName("Code")
//            .HasComment("产品编码");
//        builder.Property(t => t.Name)
//            .HasColumnName("Name")
//            .HasComment("产品名称");
//        builder.Property(t => t.Price)
//            .HasColumnName("Price")
//            .HasComment("价格")
//            .HasDefaultValue(0)
//            .HasPrecision(12, 2);
//        builder.Property(t => t.IntPrice)
//            .HasColumnName("IntPrice")
//            .HasComment("价格")
//            .HasDefaultValue(0);
//        builder.Property(t => t.LongPrice)
//            .HasColumnName("LongPrice")
//            .HasComment("价格")
//            .HasDefaultValue(0);
//        builder.Property(t => t.FloatPrice)
//            .HasColumnName("FloatPrice")
//            .HasComment("价格")
//            .HasDefaultValue(0);
//        builder.Property(t => t.Description)
//            .HasColumnName("Description")
//            .HasComment("描述");
//        builder.Property(t => t.Enabled)
//            .HasColumnName("Enabled")
//            .HasDefaultValue(TestConfig.BoolValue)
//            .HasComment("启用");
//        builder.Property(t => t.CreationTime)
//            .HasColumnName("CreationTime")
//            .HasComment("创建时间");
//        builder.Property(t => t.CreatorId)
//            .HasColumnName("CreatorId")
//            .HasComment("创建人");
//        builder.Property(t => t.LastModificationTime)
//            .HasColumnName("LastModificationTime")
//            .HasComment("最后修改时间");
//        builder.Property(t => t.LastModifierId)
//            .HasColumnName("LastModifierId")
//            .HasComment("最后修改人");
//        builder.Property(t => t.IsDeleted)
//            .HasDefaultValue(false);

//        InitData(builder);
//    }

//    /// <summary>
//    /// 初始化数据
//    /// </summary>
//    private void InitData(EntityTypeBuilder<Product> builder)
//    {
//        var product = new Product(TestConfig.Id)
//        {
//            Code = TestConfig.Value,
//            Price = TestConfig.DecimalValue,
//            IntPrice = TestConfig.IntValue,
//            LongPrice = TestConfig.LongValue,
//            FloatPrice = TestConfig.FloatValue
//        };
//        builder.HasData(product);
//    }
//}
