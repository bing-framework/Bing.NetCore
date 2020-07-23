namespace Bing.Auditing
{
    /// <summary>
    /// 实体属性审计信息
    /// </summary>
    public class AuditPropertyEntry
    {
        /// <summary>
        /// 初始化一个<see cref="AuditPropertyEntry"/>类型的实例
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="propertyType">属性类型</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        /// <param name="description">描述</param>
        public AuditPropertyEntry(string propertyName, string propertyType, string oldValue, string newValue, string description = "")
        {
            PropertyName = propertyName;
            Type = propertyType;
            OriginalValue = oldValue;
            NewValue = newValue;
            Description = description;
        }

        /// <summary>
        /// 字段
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 旧值
        /// </summary>
        public string OriginalValue { get; set; }

        /// <summary>
        /// 新值
        /// </summary>
        public string NewValue { get; set; }

        /// <summary>
        /// 输出变更信息
        /// </summary>
        public override string ToString() => $"[ENTITY: {GetType().Name}]; {{ 'PropertyName': {PropertyName}({Description}), 'PropertyType': {Type}, 'OriginalValue': {OriginalValue}, 'NewValue': {NewValue}}}";
    }
}
