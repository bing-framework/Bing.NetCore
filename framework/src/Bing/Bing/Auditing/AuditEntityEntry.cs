using System.Collections.Generic;

namespace Bing.Auditing
{
    /// <summary>
    /// 实体审计信息
    /// </summary>
    public class AuditEntityEntry
    {
        /// <summary>
        /// 类型名称
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 实体标识
        /// </summary>
        public string EntityId { get; set; }
        
        /// <summary>
        /// 操作类型
        /// </summary>
        public OperationType OperationType { get; set; }

        /// <summary>
        /// 操作实体属性集合
        /// </summary>
        public ICollection<AuditPropertyEntry> Properties { get; set; }

        /// <summary>
        /// 添加操作实体属性集合
        /// </summary>
        /// <param name="properties">操作实体属性集合</param>
        public void AddProperties(IEnumerable<AuditPropertyEntry> properties)
        {
            foreach (var property in properties) 
                Properties.Add(property);
        }

        /// <summary>
        /// 输出变更信息
        /// </summary>
        public override string ToString() => $"[ENTITY: {GetType().Name}]; {{ 'TypeName': {TypeName}({Description}), 'EntityId': {EntityId}, 'OperateType': {OperationType}";
    }
}
