namespace Bing.Domains.Entities
{
    /// <summary>
    /// 删除操作类型
    /// </summary>
    public enum DeleteOperationTypes
    {
        /// <summary>
        /// 逻辑删除，也称为软删除
        /// </summary>
        LogicDelete,
        /// <summary>
        /// 还原。用于逻辑删除实体
        /// </summary>
        Restore,
        /// <summary>
        /// 物理删除，也称为硬删除
        /// </summary>
        PhysicalDelete,
    }
}
