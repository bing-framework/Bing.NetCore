using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Encryption.Core.Internals
{
    /// <summary>
    /// RSA 参数Json
    /// </summary>
    internal class RSAParametersJson
    {
        /// <summary>
        /// 表示 RSA 算法的 Modulus 参数
        /// </summary>
        public string Modulus { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 Exponent 参数
        /// </summary>
        public string Exponent { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 P 参数
        /// </summary>
        public string P { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 Q 参数
        /// </summary>
        public string Q { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 DP 参数
        /// </summary>
        public string DP { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 DQ 参数
        /// </summary>
        public string DQ { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 InverseQ 参数
        /// </summary>
        public string InverseQ { get; set; }

        /// <summary>
        /// 表示 RSA 算法的 D 参数
        /// </summary>
        public string D { get; set; }
    }
}
