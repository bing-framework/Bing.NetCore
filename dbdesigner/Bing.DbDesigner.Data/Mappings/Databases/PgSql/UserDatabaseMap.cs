using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Databases.PgSql {
    /// <summary>
    /// 用户数据库映射配置
    /// </summary>
    public class UserDatabaseMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<UserDatabase> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserDatabase> builder ) {
            builder.ToTable( "UserDatabase", "Databases" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserDatabase> builder ) {
            //用户数据库标识
            builder.Property(t => t.Id)
                .HasColumnName("UserDatabaseId");
        }
    }
}
