using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 颜色转换器
    /// </summary>
    public static class ColorConverter
    {
        /// <summary>
        /// 转换为16进制颜色
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        /// <summary>
        /// 转换为RGB颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public static string ToRgb(Color color) => $"RGB({color.R},{color.G},{color.B})";

        /// <summary>
        /// RGB格式转换为16进制颜色
        /// </summary>
        /// <param name="r">红色</param>
        /// <param name="g">绿色</param>
        /// <param name="b">蓝色</param>
        public static string RgbToHex(int r, int g, int b) => ToHex(Color.FromArgb(r, g, b));

        /// <summary>
        /// 从样式颜色中获取系统颜色
        /// </summary>
        /// <param name="cssColour">样式颜色</param>
        public static Color GetColorFromCssString(string cssColour)
        {
            if (string.IsNullOrWhiteSpace(cssColour))
            {
                throw new ArgumentNullException(nameof(cssColour));
            }

            var m1 = Regex.Match(cssColour, @"^#?([A-F\d]{2})([A-F\d]{2})([A-F\d]{2})([A-F\d]{2})?",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);// #FFFFFF
            if (m1.Success && m1.Groups.Count == 5)
            {
                if (m1.Groups[4].Value.Length > 0)// 判断是否包含透明度
                {
                    return Color.FromArgb(byte.Parse(m1.Groups[1].Value, NumberStyles.HexNumber),
                        byte.Parse(m1.Groups[2].Value, NumberStyles.HexNumber),
                        byte.Parse(m1.Groups[3].Value, NumberStyles.HexNumber),
                        byte.Parse(m1.Groups[4].Value, NumberStyles.HexNumber));
                }

                return Color.FromArgb(0xFF, 
                    byte.Parse(m1.Groups[1].Value, NumberStyles.HexNumber),
                    byte.Parse(m1.Groups[2].Value, NumberStyles.HexNumber),
                    byte.Parse(m1.Groups[3].Value, NumberStyles.HexNumber));
            }
            else
            {
                Match m2 = Regex.Match(cssColour, @"^#?([A-F\d])([A-F\d])([A-F\d])$",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);// #FFF
                if (m2.Success && m2.Groups.Count == 4)
                {
                    var r = byte.Parse(m2.Groups[1].Value, NumberStyles.HexNumber);
                    r += (byte) (r << 4);
                    var g = byte.Parse(m2.Groups[2].Value, NumberStyles.HexNumber);
                    g += (byte)(g << 4);
                    var b = byte.Parse(m2.Groups[3].Value, NumberStyles.HexNumber);
                    b += (byte)(b << 4);
                    return Color.FromArgb(0xFF, r, g, b);
                }

                if (cssColour.StartsWith("rgb(") && cssColour.EndsWith(")"))
                {
                    string[] rgbTemp = cssColour.Remove(cssColour.Length - 1).Remove(0, "rgb(".Length).Split(',');

                    if (rgbTemp.Length == 3)
                    {
                        byte r = ParseRgb(rgbTemp[0]);
                        byte g = ParseRgb(rgbTemp[1]);
                        byte b = ParseRgb(rgbTemp[2]);
                        return Color.FromArgb(0xFF, r, g, b);
                    }
                }

                if (cssColour.StartsWith("rgba(") && cssColour.EndsWith(")"))
                {
                    string[] rgbaTemp = cssColour.Remove(cssColour.Length - 1).Remove(0, "rgba(".Length).Split(',');

                    if (rgbaTemp.Length == 3)
                    {
                        byte r = ParseRgb(rgbaTemp[0]);
                        byte g = ParseRgb(rgbaTemp[1]);
                        byte b = ParseRgb(rgbaTemp[2]);
                        byte a = ParseFloat(rgbaTemp[3]);
                        return Color.FromArgb(a, r, g, b);
                    }
                }

                if (cssColour.StartsWith("hsl(") && cssColour.EndsWith(")"))
                {
                    string[] hslTemp = cssColour.Remove(cssColour.Length - 1).Remove(0, "hsl(".Length).Split(',');

                    if (hslTemp.Length == 3)
                    {
                        short h = ParseHue(hslTemp[0]);
                        byte s = ParseFloat(hslTemp[1]);
                        byte l = ParseFloat(hslTemp[2]);
                        return HslaToRgba(h, s, l);
                    }
                }

                if (cssColour.StartsWith("hsla(") && cssColour.EndsWith(")"))
                {
                    string[] hslaTemp = cssColour.Remove(cssColour.Length - 1).Remove(0, "hsla(".Length).Split(',');

                    if (hslaTemp.Length == 4)
                    {
                        short h = ParseHue(hslaTemp[0]);
                        byte s = ParseFloat(hslaTemp[1]);
                        byte l = ParseFloat(hslaTemp[2]);
                        byte a = ParseFloat(hslaTemp[3]);
                        return HslaToRgba(h, s, l, a);
                    }
                }

                switch (cssColour.ToLower())
                {
                    case "white":
                    case "silver":
                    case "gray":
                    case "black":
                    case "red":
                    case "maroon":
                    case "yellow":
                    case "olive":
                    case "lime":
                    case "green":
                    case "aqua":
                    case "teal":
                    case "blue":
                    case "navy":
                    case "fuschia":
                    case "purple":
                        return Color.FromName(cssColour);
                    default:
                        throw new ArgumentException("无效颜色");
                }
            }
        }

        /// <summary>
        /// Hsla格式转换为RGBA格式
        /// </summary>
        /// <param name="hue"></param>
        /// <param name="saturation"></param>
        /// <param name="lightness"></param>
        /// <param name="alpha"></param>
        public static Color HslaToRgba(short hue, byte saturation, byte lightness, byte alpha = 255)
        {
            double h = hue / 360.0f;
            double sl = saturation / 255.0f;
            double l = lightness / 255.0f;

            var r = l;
            var g = l;
            var b = l;

            var v = (l <= 0.5) ? (1 * (1.0 + sl)) : (l + sl - l * sl);

            if (v > 0)
            {
                var m = l + l - v;
                var sv = (v - m) / v;
                h *= 6.0;
                var sextant = (int) h;
                var fract = h - sextant;
                var vsf = v * sv * fract;
                var mid1 = m + vsf;
                var mid2 = v - vsf;

                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;
                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;
                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;
                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;
                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }

            return Color.FromArgb(alpha, Convert.ToByte(r * 255.0f), Convert.ToByte(g * 255.0f),
                Convert.ToByte(b * 255.0f));
        }

        /// <summary>
        /// 格式化RGB
        /// </summary>
        /// <param name="input"></param>
        private static byte ParseRgb(string input)
        {
            string parseString = input.Trim();
            if (parseString.EndsWith("%"))
            {
                return (byte) (ParseClamp(parseString.Remove(parseString.Length - 1), 100) * 2.55);
            }

            return (byte) (ParseClamp(parseString, 255));
        }

        /// <summary>
        /// 格式化范围值
        /// </summary>
        /// <param name="input"></param>
        /// <param name="maxValue"></param>
        /// <param name="minValue"></param>
        private static double ParseClamp(string input, double maxValue, double minValue = 0)
        {
            if (double.TryParse(input, out var parsedValue))
            {
                if (parsedValue > maxValue)
                {
                    return maxValue;
                }

                if (parsedValue < minValue)
                {
                    return minValue;
                }

                return parsedValue;
            }
            throw new ArgumentException($"无效数字 \"{input}\"");
        }

        /// <summary>
        /// 格式化Float
        /// </summary>
        /// <param name="input"></param>
        private static byte ParseFloat(string input)
        {
            string parseString = input.Trim();
            if (parseString.EndsWith("%"))
            {
                return (byte)(ParseClamp(parseString.Remove(parseString.Length - 1), 100) * 2.55);
            }

            return (byte)(ParseClamp(parseString, 1)*255);
        }

        /// <summary>
        /// 格式化Hue
        /// </summary>
        /// <param name="input"></param>
        private static short ParseHue(string input)
        {
            string parseString = input.Trim();
            if (double.TryParse(input, out var parsedValue))
            {
                return (short) (((parsedValue % 360) + 360) % 360);
            }
            throw new ArgumentException($"无效数字 \"{input}\"");
        }
    }
}
