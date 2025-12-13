using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;

namespace OPFlashTool.Qualcomm
{
    /// <summary>
    /// XML 刷机配置解析器 (兼容 QFIL rawprogram*.xml 格式)
    /// </summary>
    public class XmlFlashConfig
    {
        public class ProgramEntry
        {
            public string FileName { get; set; } = "";
            public string Label { get; set; } = "";
            public long StartSector { get; set; }
            public long NumSectors { get; set; }
            public int PhysicalPartitionNumber { get; set; }
            public bool Sparse { get; set; }
            public bool ReadBackVerify { get; set; }
            public string FilePath { get; set; } = ""; // 解析后的完整路径
        }

        public class PatchEntry
        {
            public string FileName { get; set; } = "";
            public long ByteOffset { get; set; }
            public long SizeInBytes { get; set; }
            public string Value { get; set; } = "";
            public int PhysicalPartitionNumber { get; set; }
        }

        public List<ProgramEntry> Programs { get; } = new List<ProgramEntry>();
        public List<PatchEntry> Patches { get; } = new List<PatchEntry>();
        public string StorageType { get; set; } = "ufs";
        public int SectorSize { get; set; } = 4096;

        /// <summary>
        /// 从 rawprogram*.xml 文件加载刷机配置
        /// </summary>
        public static XmlFlashConfig LoadFromFile(string xmlPath, string imageBaseDir = null)
        {
            var config = new XmlFlashConfig();
            
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"XML 文件不存在: {xmlPath}");

            imageBaseDir ??= Path.GetDirectoryName(xmlPath);
            
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;

            if (root == null)
                throw new Exception("无效的 XML 文件");

            // 解析 <program> 节点
            foreach (var elem in root.Elements("program"))
            {
                var entry = new ProgramEntry
                {
                    FileName = elem.Attribute("filename")?.Value ?? elem.Attribute("FILENAME")?.Value ?? "",
                    Label = elem.Attribute("label")?.Value ?? elem.Attribute("LABEL")?.Value ?? "",
                    Sparse = ParseBool(elem.Attribute("sparse")?.Value ?? elem.Attribute("SPARSE")?.Value),
                    ReadBackVerify = ParseBool(elem.Attribute("read_back_verify")?.Value),
                };

                // 解析数字属性
                entry.StartSector = ParseLong(elem.Attribute("start_sector")?.Value ?? elem.Attribute("START_SECTOR")?.Value);
                entry.NumSectors = ParseLong(elem.Attribute("num_partition_sectors")?.Value ?? elem.Attribute("NUM_PARTITION_SECTORS")?.Value);
                entry.PhysicalPartitionNumber = (int)ParseLong(elem.Attribute("physical_partition_number")?.Value ?? elem.Attribute("PHYSICAL_PARTITION_NUMBER")?.Value);

                // 解析文件路径
                if (!string.IsNullOrEmpty(entry.FileName) && entry.FileName != "")
                {
                    string fullPath = Path.Combine(imageBaseDir, entry.FileName);
                    if (File.Exists(fullPath))
                    {
                        entry.FilePath = fullPath;
                    }
                    else
                    {
                        // 尝试在子目录查找
                        var found = Directory.GetFiles(imageBaseDir, entry.FileName, SearchOption.AllDirectories).FirstOrDefault();
                        entry.FilePath = found ?? "";
                    }
                }

                // 只添加有效的条目 (有文件名且扇区数 > 0)
                if (!string.IsNullOrEmpty(entry.FileName) && entry.NumSectors > 0)
                {
                    config.Programs.Add(entry);
                }
            }

            // 解析 <patch> 节点
            foreach (var elem in root.Elements("patch"))
            {
                var entry = new PatchEntry
                {
                    FileName = elem.Attribute("filename")?.Value ?? elem.Attribute("FILENAME")?.Value ?? "",
                    Value = elem.Attribute("value")?.Value ?? elem.Attribute("VALUE")?.Value ?? "",
                };

                entry.ByteOffset = ParseLong(elem.Attribute("byte_offset")?.Value ?? elem.Attribute("BYTE_OFFSET")?.Value);
                entry.SizeInBytes = ParseLong(elem.Attribute("size_in_bytes")?.Value ?? elem.Attribute("SIZE_IN_BYTES")?.Value);
                entry.PhysicalPartitionNumber = (int)ParseLong(elem.Attribute("physical_partition_number")?.Value ?? elem.Attribute("PHYSICAL_PARTITION_NUMBER")?.Value);

                if (entry.SizeInBytes > 0)
                {
                    config.Patches.Add(entry);
                }
            }

            return config;
        }

        /// <summary>
        /// 从目录加载所有 rawprogram*.xml 文件
        /// </summary>
        public static XmlFlashConfig LoadFromDirectory(string directory)
        {
            var config = new XmlFlashConfig();

            // 查找所有 rawprogram*.xml 文件
            var xmlFiles = Directory.GetFiles(directory, "rawprogram*.xml", SearchOption.TopDirectoryOnly)
                                   .OrderBy(f => f)
                                   .ToList();

            if (xmlFiles.Count == 0)
            {
                // 尝试其他常见名称
                xmlFiles = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly)
                                   .Where(f => Path.GetFileName(f).StartsWith("rawprogram", StringComparison.OrdinalIgnoreCase))
                                   .OrderBy(f => f)
                                   .ToList();
            }

            foreach (var xmlFile in xmlFiles)
            {
                try
                {
                    var subConfig = LoadFromFile(xmlFile, directory);
                    config.Programs.AddRange(subConfig.Programs);
                    config.Patches.AddRange(subConfig.Patches);
                }
                catch { /* 忽略解析失败的文件 */ }
            }

            // 加载 patch*.xml
            var patchFiles = Directory.GetFiles(directory, "patch*.xml", SearchOption.TopDirectoryOnly);
            foreach (var patchFile in patchFiles)
            {
                try
                {
                    var patchConfig = LoadFromFile(patchFile, directory);
                    config.Patches.AddRange(patchConfig.Patches);
                }
                catch { /* 忽略解析失败的文件 */ }
            }

            return config;
        }

        /// <summary>
        /// 获取按 LUN 分组的刷写任务
        /// </summary>
        public Dictionary<int, List<ProgramEntry>> GetProgramsByLun()
        {
            return Programs
                .GroupBy(p => p.PhysicalPartitionNumber)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.StartSector).ToList());
        }

        /// <summary>
        /// 验证所有文件是否存在
        /// </summary>
        public List<string> ValidateFiles()
        {
            var missing = new List<string>();
            foreach (var prog in Programs)
            {
                if (!string.IsNullOrEmpty(prog.FileName) && string.IsNullOrEmpty(prog.FilePath))
                {
                    missing.Add(prog.FileName);
                }
            }
            return missing;
        }

        private static long ParseLong(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            value = value.Trim();
            
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(value, 16);
            }
            
            if (long.TryParse(value, out long result))
            {
                return result;
            }
            
            return 0;
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.Ordinal);
        }
    }
}
