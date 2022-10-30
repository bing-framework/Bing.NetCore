using System.Text;
using Bing.Extensions;

namespace Bing.Data;

/// <summary>
/// 分页参数
/// </summary>
public class Pager : IPager
{
    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="Pager"/>类型的实例
    /// </summary>
    public Pager() : this(1)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Pager"/>类型的实例
    /// </summary>
    /// <param name="page">页索引</param>
    /// <param name="pageSize">每页显示行数，默认20</param>
    /// <param name="order">排序条件</param>
    public Pager(int page, int pageSize, string order) : this(page, pageSize, 0, order)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Pager"/>类型的实例
    /// </summary>
    /// <param name="page">页索引</param>
    /// <param name="pageSize">每页显示行数，默认20</param>
    /// <param name="totalCount">总行数</param>
    /// <param name="order">排序条件</param>
    public Pager(int page, int pageSize = 20, int totalCount = 0, string order = "")
    {
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        Order = order;
    }

    #endregion

    /// <summary>
    /// 页索引，级第几页，从1开始
    /// </summary>
    private int _pageIndex;

    /// <summary>
    /// 描述
    /// </summary>
    private StringBuilder _description;

    /// <summary>
    /// 页索引，即第几页，从1开始
    /// </summary>
    public int Page
    {
        get
        {
            if (_pageIndex <= 0)
                _pageIndex = 1;
            return _pageIndex;
        }
        set => _pageIndex = value;
    }

    /// <summary>
    /// 每页显示行数
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总行数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 排序条件
    /// </summary>
    public string Order { get; set; }

    /// <summary>
    /// 获取总页数
    /// </summary>
    public int GetPageCount()
    {
        if (TotalCount % PageSize == 0)
            return TotalCount / PageSize;
        return (TotalCount / PageSize) + 1;
    }

    /// <summary>
    /// 获取跳过的行数
    /// </summary>
    public int GetSkipCount() => PageSize * (Page - 1);

    /// <summary>
    /// 获取起始行数
    /// </summary>
    public int GetStartNumber() => (Page - 1) * PageSize + 1;

    /// <summary>
    /// 获取结束行数
    /// </summary>
    public int GetEndNumber() => Page * PageSize;

    /// <summary>
    /// 重写 生成字符串
    /// </summary>
    public override string ToString()
    {
        _description = new StringBuilder();
        AddDescriptions();
        return _description.ToString().TrimEnd().TrimEnd(',');
    }

    /// <summary>
    /// 添加描述
    /// </summary>
    protected virtual void AddDescriptions()
    {
        AddDescription("Page", Page);
        AddDescription("PageSize", PageSize);
        AddDescription("Order", Order);
    }

    /// <summary>
    /// 添加描述
    /// </summary>
    /// <param name="description">描述</param>
    protected void AddDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;
        _description.Append(description);
    }

    /// <summary>
    /// 添加描述
    /// </summary>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    protected void AddDescription<TValue>(string name, TValue value)
    {
        if (string.IsNullOrWhiteSpace(value.SafeString()))
            return;
        _description.AppendFormat("{0}:{1},", name, value);
    }
}