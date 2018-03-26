using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Newtonsoft.Json;

namespace Bing.Encryption.Core.Internals.Extensions
{
    /// <summary>
    /// RSA 密钥扩展
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal static class RSAKeyExtensions
    {
        /// <summary>
        /// 获取RSA key序列化Json
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="includePrivateParameters">是否包含私钥</param>
        internal static string ToJsonString(this RSA rsa, bool includePrivateParameters)
        {
            var parameters = rsa.ExportParameters(includePrivateParameters);

            var parasJson=new RSAParametersJson()
            {
                Modulus = parameters.Modulus!=null?Convert.ToBase64String(parameters.Modulus):null,
                Exponent = parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null,
                P = parameters.P != null ? Convert.ToBase64String(parameters.P) : null,
                Q = parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null,
                DP = parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null,
                DQ = parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null,
                InverseQ = parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null,
                D = parameters.D != null ? Convert.ToBase64String(parameters.D) : null,
            };

            return JsonConvert.SerializeObject(parasJson);
        }

        /// <summary>
        /// 从Json字符串中获取RSA
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="jsonString">Json字符串</param>
        internal static void FromJsonString(this RSA rsa, string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                throw new ArgumentNullException(nameof(jsonString));
            }

            var parameters=new RSAParameters();

            try
            {
                var paramsJson = JsonConvert.DeserializeObject<RSAParametersJson>(jsonString);
                parameters.Modulus = paramsJson.Modulus != null ? Convert.FromBase64String(paramsJson.Modulus) : null;
                parameters.Exponent = paramsJson.Exponent != null ? Convert.FromBase64String(paramsJson.Exponent) : null;
                parameters.P = paramsJson.P != null ? Convert.FromBase64String(paramsJson.P) : null;
                parameters.Q = paramsJson.Q != null ? Convert.FromBase64String(paramsJson.Q) : null;
                parameters.DP = paramsJson.DP != null ? Convert.FromBase64String(paramsJson.DP) : null;
                parameters.DQ = paramsJson.DQ != null ? Convert.FromBase64String(paramsJson.DQ) : null;
                parameters.InverseQ = paramsJson.InverseQ != null ? Convert.FromBase64String(paramsJson.InverseQ) : null;
                parameters.D = paramsJson.D != null ? Convert.FromBase64String(paramsJson.D) : null;
            }
            catch
            {
                throw new Exception("Invalid Json RSA key.");
            }
            rsa.ImportParameters(parameters);
        }

        /// <summary>
        /// 从Xml字符串中获取RSA
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="xmlString">Xml字符串</param>
        public static void FromExtXmlString(this RSA rsa, string xmlString)
        {
            RSAParameters parameters = new RSAParameters();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlString);
            if (doc.DocumentElement.Name.Equals("RSAKeyValue"))
            {
                foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "Modulus":
                            parameters.Modulus = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "Exponent":
                            parameters.Exponent = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "P":
                            parameters.P = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "Q":
                            parameters.Q = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "DP":
                            parameters.DP = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "DQ":
                            parameters.DQ = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "InverseQ":
                            parameters.InverseQ = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "D":
                            parameters.D = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                    }
                }
            }
            else
            {
                throw new Exception("Invalid Xml RSA Key");
            }
            rsa.ImportParameters(parameters);
        }

        /// <summary>
        /// 获取RSA key序列化Xml
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="includePrivateParameters">是否包含私钥</param>
        /// <returns></returns>
        public static string ToExtXmlString(this RSA rsa, bool includePrivateParameters)
        {
            RSAParameters parameters = rsa.ExportParameters(includePrivateParameters);

            if (includePrivateParameters)
            {
                return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent><P>{2}</P><Q>{3}</Q><DP>{4}</DP><DQ>{5}</DQ><InverseQ>{6}</InverseQ><D>{7}</D></RSAKeyValue>",
                    parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
                    parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null,
                    parameters.P != null ? Convert.ToBase64String(parameters.P) : null,
                    parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null,
                    parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null,
                    parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null,
                    parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null,
                    parameters.D != null ? Convert.ToBase64String(parameters.D) : null);
            }
            return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent></RSAKeyValue>",
                parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
                parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null);
        }
    }
}
