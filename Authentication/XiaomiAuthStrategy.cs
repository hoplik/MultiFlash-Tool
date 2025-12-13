using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OPFlashTool.Qualcomm;

namespace OPFlashTool.Authentication
{
    public class XiaomiAuthStrategy : IAuthStrategy
    {
        private Action<string> _log;

        public XiaomiAuthStrategy(Action<string> logger)
        {
            _log = logger;
        }

        public string Name => "Xiaomi Bypass (MiAuth)";

        // 完整的 5 组免授权签名列表
        private readonly string[] _authSigns = new[]
        {
            // Sig 1: 通用 
            "BF35D6013A39D6166BE0387E6B9B00FD0E096283F811EDE81594866CF676B41B1A32EA67FBAB4F6D90E45C944B53302A1DA32D94F30A68E1EB116672B02920089AA938F91464D6926C42A93D0EAE88E549A49C00FCF9B1B89EF68A7CD23DEBEB88C01D850ACD52A832BB80134C4B0E2A7A1422E2530C19B309EBA1FF7E123A34DD3B83DCFACDCE45F303D135FE58899E531E1CF7155D48BFF18AB3E5FC1A2E85FBB015DE2A3CFC8EE51AA453F7DEBC4A095861DA1637C8DF4D9CF64EC4A5F45486AD73FB036965B94E1EE8F4077FFB54E90AF0AB52BF02E499517FB7D1028ABCBA1B98951843B2A8C964B4D94801BAF630C6179FA6F86371830A484F2792D491",
            // Sig 2
            "600000010800936E3A8E573CAD07C167644B61217835D85AD4FDDB7D840A2B7225432FCDA13A7C192CFA979ED16517E6970B1B07DF6C516FEC81F6968FCF7FFDDBC397A162C2CA3E5D76124AA1769F1B2164B39B76930B4CC67519F7F339877677F4E8AF25828682BCBF4E593A57E7E30532699253E0B1CC5D9D0D554AF2BD46D56F18D6E5290BA4A0CAC2431F9F19C4C1A39D7664FFAB48A9E11A559386819835B84DF5675E70D25FDB5123E7B040FE21108F0AE6D7D9D267F2C9C61AD054C68493DC4D33F74D0CF2D4AADCD430152DB67C22A181AD6D7761637F70CBDA884CDC11337203837790E6845CA5A8767930B9C26FDA71272564CA34763D352F5FE4",
            // Sig 3
            "936E3A8E573CAD07C167644B61217835D85AD4FDDB7D840A2B7225432FCDA13A7C192CFA979ED16517E6970B1B07DF6C516FEC81F6968FCF7FFDDBC397A162C2CA3E5D76124AA1769F1B2164B39B76930B4CC67519F7F339877677F4E8AF25828682BCBF4E59600000110532699253E0B1CC5D9D0D554AF2BD46D56F18D6E5290BA4A0CAC2431F9F19C4C1A39D7664FFAB48A9E11A559386819835B84DF5675E70D25FDB5123E7B040FE21108F0AE6D7D9D267F2C9C61AD054C68493DC4D33F74D0CF2D4AADCD430152DB67C22A181AD6D7761637F70CBDA884CDC11337203837790E6845CA5A8767930B9C26FDA71272564CA34763D352F5FE42AB738FB38A5",
            // Sig 4
            "936E3A8E573CAD07C167644B61217835D85AD4FDDB7D840A2B7225432FCDA13A7C192CFA979ED16517E6970B1B07DF6C516FEC81F6968FCF7FFDDBC397A162C2CA3E5D76124AA1769F1B2164B39B76930B4CC67519F7F339877677F4E8AF25828682BCBF4E593A57E7E30532699253E0B1CC5D9D0D554AF2BD46D56F18D6E5290BA4A0CAC2431F9F19C4C1A39D7664FFAB48A9E11A559386819835B84DF5675E70D25FDB5123E7B040FE21108F0AE6D7D9D267F2C9C61AD054C68493DC4D33F74D0CF2D4AADCD430152DB67C22A181AD6D7761637F70CBDA884CDC11337203837790E6845CA5A8767930B9C26FDA71272564CA34763D352F5FE42AB738FB38A5",
            // Sig 5
            "936E3A8E573CAD07C167644B61217835D85AD4FDDB7D840A2B7225432FCDA13A7C192CFA979ED16517E6970B1B07DF6C516FEC81F6968FCF7FFDDBC397A162C2CA3E5D76124AA1769F1B2164B39B76930B4CC67519F7F339877677F4E8AF25828682BCBF4E59600000020532699253E0B1CC5D9D0D554AF2BD46D56F18D6E5290BA4A0CAC2431F9F19C4C1A39D7664FFAB48A9E11A559386819835B84DF5675E70D25FDB5123E7B040FE21108F0AE6D7D9D267F2C9C61AD054C68493DC4D33F74D0CF2D4AADCD430152DB67C22A181AD6D7761637F70CBDA884CDC11337203837790E6845CA5A8767930B9C26FDA71272564CA34763D352F5FE42AB738FB38A5"
        };

        public bool PerformAuth(FirehoseClient firehose, string programmerPath, Func<string, string> manualSignCallback = null)
        {
            _log("[MiAuth] 正在尝试免授权认证 (优先盲试签名)...");

            try
            {
                // [重要修复]
                // 1. 先 Ping 设备，确保连接活跃
                firehose.Ping();
                Thread.Sleep(100);

                // 2. 【优先】尝试所有内置签名 (盲试)
                // 这匹配了高通工具箱的成功逻辑：直接发签名，不问 Blob
                int index = 1;
                foreach (var hexSign in _authSigns)
                {
                    try 
                    {
                        // _log($"[MiAuth] 尝试签名 #{index}...");
                        if (firehose.SendSignature(HexStringToBytes(hexSign)))
                        {
                            _log($"[MiAuth] 内置签名 #{index} 验证成功！设备已解锁。");
                            return true; // 成功直接返回
                        }
                    } 
                    catch {}
                    index++;
                }

                _log("[MiAuth] 内置签名尝试失败，尝试获取 Blob 进行手动签名...");

                // 3. 【后备】如果盲试失败，再尝试获取 Blob (Challenge)
                // 这用于那些必须依赖 Blob 计算签名的机型
                string blob = firehose.SendXmlCommandWithAttributeResponse(
                    "<?xml version=\"1.0\" ?><data><sig TargetName=\"req\" /></data>", 
                    "value",
                    10 // 50秒超时
                );

                if (string.IsNullOrEmpty(blob))
                {
                    // 如果连 Blob 都拿不到，说明设备不支持交互式验证，且前面的盲试也失败了
                    // 这时返回 false，因为设备肯定处于锁定状态
                    _log("[MiAuth] 无法获取 Blob，且内置签名无效。认证失败。");
                    return false; 
                }

                // Base64 转 Hex
                string displayBlob = blob;
                if (IsBase64String(blob))
                {
                    try 
                    {
                        byte[] blobBytes = Convert.FromBase64String(PadBase64(blob));
                        displayBlob = BitConverter.ToString(blobBytes).Replace("-", "");
                    }
                    catch { }
                }
                _log($"[MiAuth] 获取到 Blob: {displayBlob.Substring(0, Math.Min(displayBlob.Length, 20))}...");

                // 4. 弹窗请求手动输入
                if (manualSignCallback != null)
                {
                    string userSignHex = manualSignCallback(displayBlob); 
                    
                    if (!string.IsNullOrEmpty(userSignHex))
                    {
                        try 
                        {
                            if (firehose.SendSignature(HexStringToBytes(userSignHex.Trim())))
                            {
                                _log("[MiAuth] 手动签名验证成功！");
                                return true;
                            }
                            _log("[MiAuth] 手动签名验证失败。");
                        }
                        catch (Exception ex)
                        {
                            _log($"[MiAuth] 签名格式错误: {ex.Message}");
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _log($"[MiAuth] 异常: {ex.Message}");
                return false;
            }
        }

        public bool PerformAuth(FirehoseClient firehose, string programmerPath)
        {
            return PerformAuth(firehose, programmerPath, null);
        }

        // ... 辅助方法保持不变 ...
        private bool IsBase64String(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '='))
                    return false;
            }
            return (s.Length % 4 == 0) || s.Length > 20; 
        }

        private string PadBase64(string s)
        {
            int pad = 4 - (s.Length % 4);
            if (pad < 4) return s + new string('=', pad);
            return s;
        }

        private byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            hex = hex.Replace(" ", "").Replace("\r", "").Replace("\n", "").Trim();
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex string length must be even.");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
