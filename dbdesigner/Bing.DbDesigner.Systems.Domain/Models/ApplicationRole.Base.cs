using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Entities.Tenants;

namespace Bing.DbDesigner.Systems.Domain.Models {
    /// <summary>
    /// 应用程序角色
    /// </summary>
    [DisplayName( "应用程序角色" )]
    public partial class ApplicationRole : AggregateRoot<ApplicationRole> {
        /// <summary>
        /// 初始化应用程序角色
        /// </summary>
        public ApplicationRole() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化应用程序角色
        /// </summary>
        /// <param name="id">应用程序角色标识</param>
        public ApplicationRole( Guid id ) : base( id ) {
        }

        
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions() {
            AddDescription( t => t.Id );
            AddDescription( t => t.Id );
        }
        
        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( ApplicationRole other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Id, other.Id );
        }
    }
}