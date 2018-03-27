using System;
using System.Collections.Generic;
using System.Text;
using Bing.Encryption.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Encryption.Tests.Asymmetric
{
    public class RSAEncryptionProviderTest:TestBase
    {
        public RSAEncryptionProviderTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_Encrypt()
        {
            var key = RSAEncryptionProvider.CreateKey();
            var source = "装逼打脸2018";
            var cliphertext = RSAEncryptionProvider.Encrypt(source, key.PublickKey,outType:OutType.Hex);
            var plaintext = RSAEncryptionProvider.Decrypt(cliphertext, key.PrivateKey, outType: OutType.Hex);
            var signData = RSAEncryptionProvider.SignData(source, key.PrivateKey);
            var verifyData = RSAEncryptionProvider.VerifyData(source, signData, key.PublickKey);
            Output.WriteLine("密文:\n"+cliphertext);
            Output.WriteLine("明文:\n" + plaintext);
            Output.WriteLine("签名:\n" + signData);
            Output.WriteLine("验签:\n" + verifyData);
        }

        [Fact]
        public void Test_Encrypt_1()
        {
            var privateKey =
                "MIIEpQIBAAKCAQEAwye3PozLsZJTAN/roZo16POjfG2jAY8OSXfzAUsxte7desdq1U+CIkEgx50SXeFMqVdnLTmm/x+1i7162F0Cky2EU3dyb859aUmhG3SdY+DOOQ5TK/0Nfjcof+0E+WPQlghsXK/gficCEqbivrYisBVQ9rDrlvxuE2svKkeAP8iy/CEssxWidm8odlZCxIIlCgbvhuwnD06oQUOEZ6iOZid2kyETmZ3WxlGWnhYW+q9XoXynml1/kZ16DHEJqFESUSWL3OyRSxMV74tAmfy0f9OcBDkT1GTDAkznauE2PM1fl/u98YEwTkRupbhYC5tXF9BQh6wRI4jes3NPe2dWuQIDAQABAoIBAQCilkwMSLDLV+TfLa7aC+guFA14dL8BZXW5r708rrDTqhXLXKic3ojEkQ4GP841eKatzque+hEvK/PMYCggahzjEWDVSQaGL7o8JaObhCQ8OeaVkmGonELJjJqpOYaTX50/4fSlo8GcWFNZxr/Rs1xi5t91JyCfwd7TPtEkoD0w5UgBaoNRlungobKES2w4L6CSNIeeq427N1uxGPhphsZMB6qslzpNnENGofab+jxhby/CqQ/iAVubraltd0lat4lXtyEaMmh0bGUI628uAH5wnWQiUdErEkwmzifBCL+0XA54jMHG1ujvUaVJIf/ATj2YBwgHRDuZgwV4e3n7bCQBAoGBAOJaUKmYGfWm1Edlm3qwkd64Hl3LNG1iViecqbqFGrl7bmWiW5WdN3A+0UF4IFLkZ8kdjqK1Ve3yG21YwVbTbBNrCkEgskkVwfaIqo0OEV9C3cOD9KnuJRcsuucc15tpuYNi+YxcLTztf1rTOJCedoLH8/2fwCvRa4geUsaLoWDpAoGBANy3VTwebGlL7lwa1EkOEbEZlVPw0NX2nWXb+trwn6/YPY354JQ0yHH+V9+HetjGXkgaHNGRVz/d9G25MmLQsHa+fqZdKbpkQW/fIUEP1ytynjO5hAzkK2HmBqlTLIW4ba5snpiRDHWUogW0rsYaS6lCcrw9WOvlO2NIHckB5iVRAoGAYfNDRKCVW0A+TEcj6QvPk9mJCn2Mymjrb2jT6er/jZRkSYbgqvXFr8T/OJ2LH1PHtbgcqTxfWwCR7deikrga5KxFW7mSbR4FOXIam7+itN5yqNDJZ1+unUC2AJzykEZICRsjciHRUbRUkDEnIS3xitaMNwySVGPjbJvypgh2ZUECgYEA017XR62zENvgt2ASMKxCkSH4+dxDgsScU7HpeMa7hsFFobPNOOGbnF+Bc9Xg9bxzCgXH14Ki1c0PigyzjiJg/DbOPzA1CAV/DU3YIOC0pS0tCEf/iADy8txBQOMgXicTEtl+wIkYL3pZ97DjN2BzWPMDwfXWNILg7uFfdOJWryECgYEA0ZNb5fgbNh5tXue/wUPr2LJ1nV/j1gf0vx40+D64Zv08OfpoSTwwnmEejz3GZBdJJ/PLhbghGL+PNug2M8sEHtYuz7CNQahknEcPwch/dToK4jjcjMnuAv+pbpYieZV1I/MvWo5MvgSvk+eWl/WsmeBBGfrDuAbAScJxXLMBoWA=";
            var publicKey =
                "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwye3PozLsZJTAN/roZo16POjfG2jAY8OSXfzAUsxte7desdq1U+CIkEgx50SXeFMqVdnLTmm/x+1i7162F0Cky2EU3dyb859aUmhG3SdY+DOOQ5TK/0Nfjcof+0E+WPQlghsXK/gficCEqbivrYisBVQ9rDrlvxuE2svKkeAP8iy/CEssxWidm8odlZCxIIlCgbvhuwnD06oQUOEZ6iOZid2kyETmZ3WxlGWnhYW+q9XoXynml1/kZ16DHEJqFESUSWL3OyRSxMV74tAmfy0f9OcBDkT1GTDAkznauE2PM1fl/u98YEwTkRupbhYC5tXF9BQh6wRI4jes3NPe2dWuQIDAQAB";
            var source= "sign_type=RSA2&t=1024";
            var signData =
                "FGGaXIw/BEUflRZOhDuKrBqRiSMx7tbhFEzQPWKyCErghs9KoSTVd9IqpINqAfL+wXZTYaaW+2f+/6z08uMQOG3NY9mPNvUkAEY4DCfrOHN7cVC0UcHByP/VQlJ0YHh/sNLhq+vZRI59wOlhbUxoKFA55uLelky07bK84erqFGGNzDNZnYrSghUXMEE+WPKjjk/r0n0cvnBinWF0SgyXZtnisEt9J1vFDWT6Lku01huNp7VGWwMNlL9dRnEtBiZ5vGfp/pBongosYe/ndWjPW+dRtAnNoo1xf66YYipRfYaQBlJ5lQnFGhC2pkFoko6cloc9b8mZ3Cy0EuCoAdNDCA==";
            var signResult = RSAEncryptionProvider.SignData(source, privateKey, keyType: RSAKeyType.Base64,rsaType:RSAType.RSA2);
            var verifyData = RSAEncryptionProvider.VerifyData(source, signData, publicKey,keyType:RSAKeyType.Base64, rsaType: RSAType.RSA2);
            Output.WriteLine("签名:\n" + signResult);
            Output.WriteLine("验签:\n" + verifyData);
        }
    }
}
