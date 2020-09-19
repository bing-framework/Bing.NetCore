namespace Bing.Auditing
{
    /// <summary>
    /// 创建人
    /// </summary>
    public interface IHasCreator : IHasCreator<string> { }

    /// <summary>
    /// 创建人
    /// </summary>
    public interface IHasCreator<TCreator>
    {
        /// <summary>
        /// 创建人
        /// </summary>
        TCreator Creator { get; set; }
    }
}
