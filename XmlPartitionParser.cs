using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace OPFlashTool
{
    public class PartitionXmlInfo
    {
        public string Label { get; set; } = "";
        public string FileName { get; set; } = "";
        
        // 【修改】改为 string，以支持 "NUM_DISK_SECTORS-34." 等公式
        public string StartSector { get; set; } = "0"; 
        
        public long NumSectors { get; set; }
        public string Lun { get; set; } = "0";
        public int SectorSize { get; set; } = 0; // 由 XML 或调用方提供
        
        // [新增] 文件读取偏移量 (单位: 扇区)
        public long FileSectorOffset { get; set; } = 0; 

        public double SizeMb => (NumSectors * SectorSize) / 1024.0 / 1024.0;
    }

    public class XmlPartitionParser
    {
        public static List<PartitionXmlInfo> Parse(string xmlPath, int? deviceSectorSize = null)
        {
            var list = new List<PartitionXmlInfo>();
            if (!File.Exists(xmlPath)) return list;

            try
            {
                XDocument doc = XDocument.Load(xmlPath);
                foreach (var elem in doc.Descendants("program"))
                {
                    var info = new PartitionXmlInfo();

                    int.TryParse(elem.Attribute("SECTOR_SIZE_IN_BYTES")?.Value, out int ss);
                    if (ss > 0) info.SectorSize = ss;
                    else if (deviceSectorSize.HasValue && deviceSectorSize.Value > 0) info.SectorSize = deviceSectorSize.Value;
                    else info.SectorSize = 4096; // 最终兜底，保持向后兼容但风险需知悉

                    // 解析基础属性
                    info.Label = elem.Attribute("label")?.Value ?? "";
                    info.FileName = elem.Attribute("filename")?.Value ?? "";
                    info.Lun = elem.Attribute("physical_partition_number")?.Value ?? "0";

                    // 【修改】增强解析逻辑：支持 start_byte_hex 和 size_in_KB
                    var startSectorAttr = elem.Attribute("start_sector");
                    if (startSectorAttr != null)
                    {
                        info.StartSector = startSectorAttr.Value;
                    }
                    else
                    {
                        // 尝试从 start_byte_hex 解析
                        var startByteHexAttr = elem.Attribute("start_byte_hex");
                        if (startByteHexAttr != null)
                        {
                            try
                            {
                                string hex = startByteHexAttr.Value.Trim().Replace("0x", "").Replace("0X", "");
                                long bytes = Convert.ToInt64(hex, 16);
                                info.StartSector = (bytes / info.SectorSize).ToString();
                            }
                            catch { info.StartSector = "0"; }
                        }
                    }

                    var numSectorsAttr = elem.Attribute("num_partition_sectors");
                    if (numSectorsAttr != null)
                    {
                        long.TryParse(numSectorsAttr.Value, out long num);
                        info.NumSectors = num;
                    }
                    else
                    {
                        // 尝试从 size_in_KB 解析
                        var sizeKbAttr = elem.Attribute("size_in_KB");
                        if (sizeKbAttr != null)
                        {
                            if (double.TryParse(sizeKbAttr.Value, out double kb))
                            {
                                info.NumSectors = (long)((kb * 1024) / info.SectorSize);
                            }
                        }
                    }

                    // [新增] 解析 file_sector_offset
                    // 官方 XML 中，如果该值为 "0" 或不存在，默认为从头读取
                    // 如果为 "1000"，表示从文件的 (1000 * SectorSize) 字节处开始读取
                    if (long.TryParse(elem.Attribute("file_sector_offset")?.Value, out long fileOffset))
                    {
                        info.FileSectorOffset = fileOffset;
                    }

                    // [Modified] Allow entries without filename (e.g. partition table definitions)
                    // if (!string.IsNullOrEmpty(info.FileName))
                    // {
                        list.Add(info);
                    // }
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"XML Parse Error: {ex.Message}");
            }
            return list;
        }

        public static void GenerateXml(List<PartitionInfo> partitions, string outputPath)
        {
            var doc = new XDocument(new XElement("data"));
            foreach (var p in partitions)
            {
                var elem = new XElement("program");
                elem.SetAttributeValue("SECTOR_SIZE_IN_BYTES", p.SectorSize);
                elem.SetAttributeValue("file_sector_offset", "0");
                elem.SetAttributeValue("filename", p.FileName ?? "");
                elem.SetAttributeValue("label", p.Name);
                elem.SetAttributeValue("num_partition_sectors", p.Sectors);
                elem.SetAttributeValue("physical_partition_number", p.Lun);
                elem.SetAttributeValue("size_in_KB", (p.Sectors * (ulong)p.SectorSize) / 1024.0);
                elem.SetAttributeValue("sparse", "false");
                elem.SetAttributeValue("start_byte_hex", $"0x{(p.StartLba * (ulong)p.SectorSize):X}");
                elem.SetAttributeValue("start_sector", !string.IsNullOrEmpty(p.StartLbaStr) ? p.StartLbaStr : p.StartLba.ToString());
                
                doc.Root.Add(elem);
            }
            doc.Save(outputPath);
        }
    }
}
