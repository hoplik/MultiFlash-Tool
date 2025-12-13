using System;
using System.Security.Cryptography;
using System.Text;

namespace OPFlashTool.Services
{
    /// <summary>
    /// 加密服务（从 LoginService 迁移）
    /// </summary>
    public static class CryptoService
    {
        // RSA 公钥现在从配置文件读取
        private static string PublicKeyXml => AppSettings.Instance.RsaPublicKey;

        /// <summary>
        /// 加密请求数据类
        /// </summary>
        public class EncryptedRequest
        {
            public string encrypted_data { get; set; } = "";
        }

        /// <summary>
        /// RSA 加密
        /// </summary>
        public static string EncryptRsa(string plainText)
        {
            using (RSA rsa = RSA.Create())
            {
                try
                {
                    rsa.FromXmlString(PublicKeyXml);
                }
                catch (Exception ex)
                {
                    throw new Exception($"公钥格式错误: {ex.Message}");
                }

                byte[] data = Encoding.UTF8.GetBytes(plainText);
                
                // Use OAEP SHA1 padding for better compatibility with standard PHP OpenSSL
                byte[] encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA1);
                
                return Convert.ToBase64String(encrypted);
            }
        }
    }
}
