namespace Bing.Auditing
{
    /// <summary>
    /// 审计属性常量
    /// </summary>
    public static class AuditedPropertyConst
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public static string Creator { get; set; } = "Creator";

        /// <summary>
        /// 创建人标识
        /// </summary>
        public static string CreatorId { get; set; } = "CreatorId";

        /// <summary>
        /// 创建时间
        /// </summary>
        public static string CreationTime { get; set; } = "CreationTime";

        /// <summary>
        /// 修改人
        /// </summary>
        public static string Modifier { get; set; } = "LastModifier";

        /// <summary>
        /// 修改人标识
        /// </summary>
        public static string ModifierId { get; set; } = "LastModifierId";

        /// <summary>
        /// 修改时间
        /// </summary>
        public static string ModificationTime { get; set; } = "LastModificationTime";

        /// <summary>
        /// 乐观锁
        /// </summary>
        public static string Version { get; set; } = "Version";
    }
}
