using System;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Auditing;
using Bing.Data;
using Bing.Data.Persistence;

namespace Bing.Admin.Data.Pos.Systems
{
    /// <summary>
    /// 资源持久化对象
    /// </summary>
    public class ResourcePo : TreePersistentObjectBase, ISoftDelete, IAuditedObjectWithName
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>  
        public Guid ApplicationId { get; set; }
        /// <summary>
        /// 资源识别号
        /// </summary>  
        public string Uri { get; set; }
        /// <summary>
        /// 资源名称
        /// </summary>  
        public string Name { get; set; }
        /// <summary>
        /// 资源类型
        /// </summary>  
        public ResourceType Type { get; set; }
        /// <summary>
        /// 备注
        /// </summary>  
        public string Remark { get; set; }
        /// <summary>
        /// 拼音简码
        /// </summary>  
        public string PinYin { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>  
        public bool IsDeleted { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>  
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人标识
        /// </summary>  
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>  
        public string Creator { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>  
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人标识
        /// </summary>  
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>  
        public string LastModifier { get; set; }
        /// <summary>
        /// 扩展
        /// </summary>  
        public string Extend { get; set; }
    }
}
