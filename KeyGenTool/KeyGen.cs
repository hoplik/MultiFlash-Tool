using System;
using System.Security.Cryptography;
using System.IO;

namespace KeyGen
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var rsa = RSA.Create(2048))
            {
                // Generate Private Key (PEM)
                var privateKey = rsa.ExportRSAPrivateKey();
                var privateKeyPem = "-----BEGIN PRIVATE KEY-----\n" + 
                                    Convert.ToBase64String(privateKey, Base64FormattingOptions.InsertLineBreaks) + 
                                    "\n-----END PRIVATE KEY-----";
                
                File.WriteAllText(@"C:\Users\Administrator\Downloads\5_6145399405101982018 (2)\OPFlashTool\Server\includes\private.pem", privateKeyPem);
                
                // Generate Public Key (XML)
                var publicKeyXml = rsa.ToXmlString(false);
                Console.WriteLine("NEW_PUBLIC_KEY_XML_START");
                Console.WriteLine(publicKeyXml);
                Console.WriteLine("NEW_PUBLIC_KEY_XML_END");
            }
        }
    }
}
