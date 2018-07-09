using System;
using System.IO;
using System.Text.RegularExpressions;
using Bing.Utils.Helpers;
using Bing.Utils.Randoms;

namespace Bing.Utils.Files.Paths
{
    /// <summary>
    /// 路径生成器基类
    /// </summary>
    public abstract class PathGeneratorBase:IPathGenerator
    {
        /// <summary>
        /// 随机数生成器
        /// </summary>
        private readonly IRandomGenerator _randomGenerator;

        /// <summary>
        /// 初始化一个<see cref="PathGeneratorBase"/>类型的实例
        /// </summary>
        /// <param name="randomGenerator">随机数生成器</param>
        protected PathGeneratorBase(IRandomGenerator randomGenerator)
        {
            _randomGenerator = randomGenerator ?? GuidRandomGenerator.Instance;
        }

        /// <summary>
        /// 生成路径
        /// </summary>
        /// <param name="fileName">文件名，必须包含扩展名，如果仅传入扩展名则生成随机文件名</param>
        /// <returns></returns>
        public string Generate(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return GeneratePath(GetFileName(fileName));
        }

        /// <summary>
        /// 创建完整路径
        /// </summary>
        /// <param name="fileName">被处理过的安全有效的文件名</param>
        /// <returns></returns>
        protected abstract string GeneratePath(string fileName);

        /// <summary>
        /// 获取文件名
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string GetFileName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName)?.TrimStart('.');
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = fileName;
                name = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = _randomGenerator.Generate();
            }

            name = FilterFileName(name);
            return $"{name}-{Time.GetDateTime():HHmmss}.{extension}";
        }

        /// <summary>
        /// 过滤文件名
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns></returns>
        private static string FilterFileName(string fileName)
        {
            fileName = Regex.Replace(fileName, "\\W", "");
            return Str.PinYin(fileName);
        }
    }
}
