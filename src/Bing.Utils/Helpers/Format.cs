namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 格式化操作
    /// </summary>
    public static class Format
    {
        /// <summary>
        /// 加密手机号码
        /// </summary>
        /// <param name="phone">手机号码</param>
        public static string EncryptPhoneOfChina(string phone) =>
            string.IsNullOrWhiteSpace(phone)
                ? string.Empty
                : $"{phone.Substring(0, 3)}******{phone.Substring(phone.Length - 2, 2)}";

        /// <summary>
        /// 加密车牌号
        /// </summary>
        /// <param name="plateNumber">车牌号</param>
        public static string EncryptPlateNumberOfChina(string plateNumber) =>
            string.IsNullOrWhiteSpace(plateNumber)
                ? string.Empty
                : $"{plateNumber.Substring(0, 2)}***{plateNumber.Substring(plateNumber.Length - 2, 2)}";

        /// <summary>
        /// 加密汽车VIN
        /// </summary>
        /// <param name="vinCode">汽车VIN</param>
        public static string EncryptVinCode(string vinCode) =>
            string.IsNullOrWhiteSpace(vinCode)
                ? string.Empty
                : $"{vinCode.Substring(0, 3)}***********{vinCode.Substring(vinCode.Length - 3, 3)}";

        /// <summary>
        /// 格式化金额
        /// </summary>
        /// <param name="money">金额</param>
        /// <param name="isEncrypt">是否加密。默认：false</param>
        public static string FormatMoney(decimal money, bool isEncrypt = false) => isEncrypt ? "***" : $"{money:N2}";
    }
}
