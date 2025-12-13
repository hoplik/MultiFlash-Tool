using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OPFlashTool.Services
{
    public class AppSettings
    {
        private static AppSettings? _instance;
        public static AppSettings Instance => _instance ??= Load();

        [JsonPropertyName("UpdateApiUrl")]
        public string UpdateApiUrl { get; set; } = "https://xiriacg.top/api/update.php";

        [JsonPropertyName("DefaultStorage")]
        public string DefaultStorage { get; set; } = "ufs";

        [JsonPropertyName("FileHashes")]
        public Dictionary<string, string> FileHashes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("TrustedThumbprints")]
        public List<string> TrustedThumbprints { get; set; } = new List<string>();

        [JsonPropertyName("CloudApiUrl")]
        public string CloudApiUrl { get; set; } = "https://xiriacg.top/api/cloud.php";

        [JsonPropertyName("RsaPublicKey")]
        public string RsaPublicKey { get; set; } = "<RSAKeyValue><Modulus>qkDjTvasKBmk6Q2ncR5KJl8h/p/Rv/X77HjE2Iw1pEqq/r1RppIZiXZdaHK61nPWDK22K6NDn+0dCOVaGH5FRM9OCuBgON8ksjDW8b8pXRm491Q5HobwRKGlCe2sBNuN+nvmcAl27HL8PFUxcSwNrksep6HzbuSf6/TnwjmNKwNoMzW3a1xkXAjC0Vq1M2jM18/FWfNuwiWPXQRfiootZ76S6EuUKPVyddPZWUKVevheHJ3Ts5A/vUVJrDrJi/n/bfdmgLemNXjHTrcTwJunNTV6JQ0+BCgojSqTcPvyGSmbsB5wq7L6GhjGaHRoczDiVMf7/6QeO65fGMCWd42a7Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, AppJsonContext.Default.AppSettings);
                File.WriteAllText("config.json", json);
            }
            catch { }
        }

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists("config.json"))
                {
                    var json = File.ReadAllText("config.json");
                    // Using the source generator context if possible, otherwise fallback to reflection
                    // Since we are adding it to AppJsonContext, we should use it.
                    // But we need to update AppJsonContext first. 
                    // For now, let's assume we will update AppJsonContext.
                    // However, circular dependency in my plan (Update Context -> Create Settings -> Use Context).
                    // I'll use reflection here for simplicity as trimming is disabled, 
                    // OR I can update AppJsonContext first.
                    
                    // Let's just use JsonSerializer.Deserialize, it will use reflection by default which is fine.
                    var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                    return settings ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }
    }
}
