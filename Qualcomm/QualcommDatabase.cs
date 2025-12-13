using System.Collections.Generic;

namespace OPFlashTool.Qualcomm
{
    public enum MemoryType
    {
        Unknown = -1,
        Nand = 0,
        Emmc = 1,
        Ufs = 2,
        Spinor = 3
    }

    /// <summary>
    /// 高通芯片数据库 (移植自 qualcomm_config.py)
    /// </summary>
    public static class QualcommDatabase
    {
        // HWID -> 芯片名称映射 (扩展版，参考 bkerler/edl)
        public static readonly Dictionary<uint, string> MsmIds = new Dictionary<uint, string>
        {
            // === 旗舰 8 系列 ===
            { 0x009470E1, "MSM8996" },   // Snapdragon 820/821
            { 0x0005E0E1, "MSM8998" },   // Snapdragon 835
            { 0x0008B0E1, "SDM845" },    // Snapdragon 845
            { 0x000A50E1, "SM8150" },    // Snapdragon 855/855+
            { 0x000C30E1, "SM8250" },    // Snapdragon 865/865+
            { 0x001350E1, "SM8350" },    // Snapdragon 888/888+
            { 0x001620E1, "SM8450" },    // Snapdragon 8 Gen 1
            { 0x001900E1, "SM8475" },    // Snapdragon 8+ Gen 1
            { 0x001CA0E1, "SM8550" },    // Snapdragon 8 Gen 2
            { 0x0022A0E1, "SM8650" },    // Snapdragon 8 Gen 3
            { 0x002480E1, "SM8750" },    // Snapdragon 8 Elite
            
            // === 中端 7 系列 ===
            { 0x000910E1, "SDM670" },    // Snapdragon 670
            { 0x000DB0E1, "SDM710" },    // Snapdragon 710
            { 0x000E70E1, "SM7150" },    // Snapdragon 730/730G
            { 0x0011E0E1, "SM7250" },    // Snapdragon 765/765G
            { 0x001920E1, "SM7325" },    // Snapdragon 778G/778G+
            { 0x001B40E1, "SM7450" },    // Snapdragon 7 Gen 1
            { 0x001F80E1, "SM7475" },    // Snapdragon 7+ Gen 2
            { 0x002120E1, "SM7550" },    // Snapdragon 7 Gen 3
            
            // === 中端 6 系列 ===
            { 0x000460E1, "MSM8953" },   // Snapdragon 625/626
            { 0x0008C0E1, "SDM660" },    // Snapdragon 660
            { 0x000CC0E1, "SDM636" },    // Snapdragon 636
            { 0x000DA0E1, "SDM632" },    // Snapdragon 632
            { 0x000F50E1, "SM6150" },    // Snapdragon 675
            { 0x00100E01, "SM6125" },    // Snapdragon 665
            { 0x001260E1, "SM6350" },    // Snapdragon 690
            { 0x001410E1, "SM6375" },    // Snapdragon 695
            { 0x001A10E1, "SM6450" },    // Snapdragon 6 Gen 1
            
            // === 入门 4 系列 ===
            { 0x007050E1, "MSM8916" },   // Snapdragon 410/412
            { 0x007B00E1, "MSM8917" },   // Snapdragon 425
            { 0x009600E1, "MSM8909" },   // Snapdragon 210
            { 0x009900E1, "MSM8937" },   // Snapdragon 430
            { 0x009A00E1, "MSM8940" },   // Snapdragon 435
            { 0x000E00E1, "SDM439" },    // Snapdragon 439
            { 0x000F70E1, "SM4250" },    // Snapdragon 460
            { 0x001700E1, "SM4350" },    // Snapdragon 480
            { 0x001D30E1, "SM4450" },    // Snapdragon 4 Gen 1
            { 0x002000E1, "SM4550" },    // Snapdragon 4 Gen 2
            
            // === 老旗舰 ===
            { 0x009400E1, "MSM8994" },   // Snapdragon 810
            { 0x009500E1, "MSM8992" },   // Snapdragon 808
            { 0x009100E1, "MSM8974" },   // Snapdragon 800/801
            { 0x009200E1, "APQ8084" },   // Snapdragon 805
            { 0x008110E1, "MSM8960" },   // Snapdragon S4 Plus
            
            // === PC 平台 ===
            { 0x0014A0E1, "SC8280X" },   // Snapdragon 8cx Gen 3
            { 0x001230E1, "SC8180X" },   // Snapdragon 8cx
            { 0x000F30E1, "SC7180" },    // Snapdragon 7c
            { 0x001550E1, "SC7280" },    // Snapdragon 7c+ Gen 3
            
            // === MTK 伪装高通 ID (部分设备) ===
            { 0x000000E1, "Unknown_QC" },
        };

        // 芯片名称 -> 推荐存储类型映射 (扩展版)
        public static readonly Dictionary<string, MemoryType> PreferredMemory = new Dictionary<string, MemoryType>
        {
            // === UFS 芯片 (现代旗舰/中端) ===
            { "MSM8996", MemoryType.Ufs },
            { "MSM8998", MemoryType.Ufs },
            { "SDM845", MemoryType.Ufs },
            { "SM8150", MemoryType.Ufs },
            { "SM8250", MemoryType.Ufs },
            { "SM8350", MemoryType.Ufs },
            { "SM8450", MemoryType.Ufs },
            { "SM8475", MemoryType.Ufs },
            { "SM8550", MemoryType.Ufs },
            { "SM8650", MemoryType.Ufs },
            { "SM8750", MemoryType.Ufs },
            { "SM7150", MemoryType.Ufs },
            { "SM7250", MemoryType.Ufs },
            { "SM7325", MemoryType.Ufs },
            { "SM7450", MemoryType.Ufs },
            { "SM7475", MemoryType.Ufs },
            { "SM7550", MemoryType.Ufs },
            { "SM6350", MemoryType.Ufs },
            { "SM6375", MemoryType.Ufs },
            { "SM6450", MemoryType.Ufs },
            { "SC8280X", MemoryType.Ufs },
            { "SC8180X", MemoryType.Ufs },
            
            // === eMMC 芯片 (入门/老旧) ===
            { "MSM8953", MemoryType.Emmc },
            { "MSM8974", MemoryType.Emmc },
            { "MSM8916", MemoryType.Emmc },
            { "MSM8917", MemoryType.Emmc },
            { "MSM8909", MemoryType.Emmc },
            { "MSM8937", MemoryType.Emmc },
            { "MSM8940", MemoryType.Emmc },
            { "MSM8994", MemoryType.Emmc },
            { "MSM8992", MemoryType.Emmc },
            { "MSM8960", MemoryType.Emmc },
            { "APQ8084", MemoryType.Emmc },
            { "SDM439", MemoryType.Emmc },
            { "SDM632", MemoryType.Emmc },
            { "SM4250", MemoryType.Emmc },
            { "SM4350", MemoryType.Emmc },
            { "SM4450", MemoryType.Emmc },
            { "SM4550", MemoryType.Emmc },
            
            // === 混合 (需根据具体设备判断) ===
            { "SDM660", MemoryType.Emmc },  // 常用 eMMC，部分支持 UFS
            { "SDM636", MemoryType.Emmc },
            { "SDM670", MemoryType.Ufs },   // 大多数是 UFS
            { "SDM710", MemoryType.Ufs },
            { "SM6150", MemoryType.Ufs },   // 部分 eMMC
            { "SM6125", MemoryType.Emmc },
            { "SC7180", MemoryType.Ufs },
            { "SC7280", MemoryType.Ufs },
        };
        
        // Sahara 协议版本信息
        public static readonly Dictionary<string, int> SaharaVersion = new Dictionary<string, int>
        {
            // V3 芯片 (需要手动指定 loader)
            { "SM8450", 3 },
            { "SM8475", 3 },
            { "SM8550", 3 },
            { "SM8650", 3 },
            { "SM8750", 3 },
            { "SM7450", 3 },
            { "SM7475", 3 },
            { "SM7550", 3 },
            { "SM6450", 3 },
            // V2 芯片 (支持自动检测)
            { "SM8350", 2 },
            { "SM8250", 2 },
            { "SM8150", 2 },
            { "SDM845", 2 },
        };
        
        public static int GetSaharaVersion(string chipName)
        {
            if (SaharaVersion.ContainsKey(chipName)) return SaharaVersion[chipName];
            return 2; // 默认 V2
        }

        public static string GetChipName(uint hwId)
        {
            if (MsmIds.ContainsKey(hwId)) return MsmIds[hwId];
            return "Unknown";
        }

        public static MemoryType GetMemoryType(string chipName)
        {
            if (PreferredMemory.ContainsKey(chipName)) return PreferredMemory[chipName];
            return MemoryType.Ufs; // Default to UFS for modern chips
        }
    }
}
