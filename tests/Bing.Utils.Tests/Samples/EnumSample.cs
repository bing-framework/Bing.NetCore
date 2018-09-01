using System.ComponentModel;

namespace Bing.Utils.Tests.Samples
{
    /// <summary>
    /// 枚举测试样例
    /// </summary>
    public enum EnumSample
    {
        A = 1,
        [Description("B2")]
        B = 2,
        [Description("C2")]
        C =3,
        [Description("D2")]
        D =4,
        [Description("E2")]
        E =5
    }
}
