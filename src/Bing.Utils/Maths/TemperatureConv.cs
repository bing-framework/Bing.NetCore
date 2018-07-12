namespace Bing.Utils.Maths
{
    /// <summary>
    /// 温度转换
    /// </summary>
    public static class TemperatureConv
    {
        /// <summary>
        /// 摄氏度转换为华氏度
        /// </summary>
        /// <param name="value">摄氏度</param>
        /// <returns></returns>
        public static decimal DegreesCelsiusToFahrenheit(decimal value)
        {
            return (decimal)1.8 * value + 32;
        }

        /// <summary>
        /// 摄氏度转换为开氏度(热力学温度)
        /// </summary>
        /// <param name="value">摄氏度</param>
        /// <returns></returns>
        public static decimal DegreesCelsiusToThermodynamicTemperature(decimal value)
        {
            return value + (decimal)273.16;
        }

        /// <summary>
        /// 华氏度转换为摄氏度
        /// </summary>
        /// <param name="value">华氏度</param>
        /// <returns></returns>
        public static decimal FahrenheitToDegreesCelsius(decimal value)
        {
            return (value - 32) / (decimal)1.8;
        }

        /// <summary>
        /// 华氏度转换为开氏度
        /// </summary>
        /// <param name="value">华氏度</param>
        /// <returns></returns>
        public static decimal FahrenheitToThermodynamicTemperature(decimal value)
        {
            return (value - 32) / (decimal)1.8 + (decimal)273.16;
        }

        /// <summary>
        /// 开氏度转换为摄氏度
        /// </summary>
        /// <param name="value">开氏度</param>
        /// <returns></returns>
        public static decimal ThermodynamicTemperatureToDegreesCelsius(decimal value)
        {
            return value - (decimal)273.16;
        }

        /// <summary>
        /// 开氏度转换为华氏度
        /// </summary>
        /// <param name="value">开氏度</param>
        /// <returns></returns>
        public static decimal ThermodynamicTemperatureToFahrenheit(decimal value)
        {
            return (value - (decimal)273.16) * (decimal)1.8 + 32;
        }
    }
}
