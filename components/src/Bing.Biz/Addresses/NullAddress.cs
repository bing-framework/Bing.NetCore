using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Biz.Addresses
{
    /// <summary>
    /// 空地址
    /// </summary>
    [NotMapped]
    public class NullAddress : Address
    {
    }
}
