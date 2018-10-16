using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing.Utils;
using Bing;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Trees;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Entities.Tenants;
using Bing.Security.Identity.Models;

namespace Bing.DbDesigner.Systems.Domain.Models {
    /// <summary>
    /// 角色
    /// </summary>
    [Description( "角色" )]
    public partial class Role : Role<Role,Guid,Guid?> {
        /// <summary>
        /// 初始化角色
        /// </summary>
        public Role()
            : this( Guid.Empty, "", 0 ) {
        }

        /// <summary>
        /// 初始化角色
        /// </summary>
        /// <param name="id">角色标识</param>
        /// <param name="path">路径</param>
        /// <param name="level">级数</param>
        public Role( Guid id, string path, int level )
            : base( id, path, level ) {
        }       
    }
}