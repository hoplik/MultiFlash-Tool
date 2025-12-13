using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace OPFlashTool
{
    // 保持 UI 兼容的视图模型
    public class PartitionInfo
    {
        public int Lun { get; set; }
        public string Name { get; set; } = "";
        public ulong StartLba { get; set; }
        public string StartLbaStr { get; set; } // [新增] 支持 XML 中的字符串/公式
        public ulong Sectors { get; set; }
        public int SectorSize { get; set; } = 4096;
        public string FileName { get; set; } = ""; // 关联的镜像文件名

        public ulong EndLba => StartLba + Sectors - 1;
        public double SizeKb => (Sectors * (ulong)SectorSize) / 1024.0;
        public double SizeMb => SizeKb / 1024.0;
        public double SizeGb => SizeMb / 1024.0;
    }

    // --- 以下参照 gpttool 定义的底层结构 ---

    public class GPT_Header
    {
        public ulong Signature;      // "EFI PART" (8 bytes)
        public uint Revision;        // 0x00010000
        public uint HeaderSize;      // usually 92
        public uint HeaderCRC32;
        public uint Reserved;
        public ulong MyLBA;
        public ulong AlternateLBA;
        public ulong FirstUsableLBA;
        public ulong LastUsableLBA;
        public byte[] DiskGUID = new byte[16];
        public ulong PartitionEntryLBA;
        public uint NumberOfPartitionEntries;
        public uint SizeOfPartitionEntry;
        public uint PartitionEntryArrayCRC32;
        // Remaining bytes are reserved/padding
    }

    public class GPT_Entry
    {
        public byte[] PartitionTypeGUID = new byte[16];
        public byte[] UniquePartitionGUID = new byte[16];
        public ulong StartingLBA;
        public ulong EndingLBA;
        public ulong Attributes;
        public string PartitionName = ""; // 72 bytes (36 UTF-16 chars)
    }

    public static class GptParser
    {
        // 签名: "EFI PART"
        private const ulong GPT_SIGNATURE = 0x5452415020494645; 

        public static List<PartitionInfo> ParseGptFile(string filePath, int lunId)
        {
            if (!File.Exists(filePath)) return new List<PartitionInfo>();
            byte[] data = File.ReadAllBytes(filePath);
            return ParseGptBytes(data, lunId);
        }

        public static List<PartitionInfo> ParseGptBytes(byte[] data, int lunId)
        {
            var partitions = new List<PartitionInfo>();
            int sectorSize = 0;
            int headerOffset = 0;

            // 1. 自动探测扇区大小 (UFS:4096 / eMMC:512)
            // GPT Header 始终位于 LBA 1
            if (CheckSignature(data, 4096)) 
            {
                sectorSize = 4096;
                headerOffset = 4096;
            }
            else if (CheckSignature(data, 512))
            {
                sectorSize = 512;
                headerOffset = 512;
            }
            else
            {
                // 无效 GPT
                return partitions;
            }

            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                // 2. 解析 Header
                ms.Seek(headerOffset, SeekOrigin.Begin);
                GPT_Header header = ReadHeader(br);

                // (可选) 可以在这里校验 header.HeaderCRC32

                // 3. 定位分区表数组
                // 通常 EntryArray 在 LBA 2，即 headerOffset + sectorSize
                // 但最严谨的做法是使用 Header 中的 PartitionEntryLBA * SectorSize
                long entryStartPos = (long)(header.PartitionEntryLBA * (ulong)sectorSize);
                
                // 如果计算出的偏移量超出了文件范围，尝试回退到标准位置 (LBA 2)
                if (entryStartPos >= data.Length) 
                {
                    entryStartPos = headerOffset + sectorSize;
                }

                ms.Seek(entryStartPos, SeekOrigin.Begin);

                // 4. 遍历分区条目
                for (int i = 0; i < header.NumberOfPartitionEntries; i++)
                {
                    // 记录当前流位置，确保读取固定大小
                    long currentEntryPos = ms.Position;

                    // 读取单个条目
                    byte[] typeGuid = br.ReadBytes(16);
                    byte[] uniqueGuid = br.ReadBytes(16);
                    
                    // [修改] 检查前 32 字节 (TypeGUID + UniqueGUID) 是否全为 0
                    // 如果全为 0，说明是未使用条目
                    if (IsZeroBytes(typeGuid) && IsZeroBytes(uniqueGuid))
                    {
                        // 跳过这 128 字节 (或 SizeOfPartitionEntry 字节)
                        ms.Seek(currentEntryPos + header.SizeOfPartitionEntry, SeekOrigin.Begin);
                        continue;
                    }

                    ulong firstLba = br.ReadUInt64();
                    ulong lastLba = br.ReadUInt64();
                    ulong attributes = br.ReadUInt64();
                    byte[] nameBytes = br.ReadBytes(72); // 36 chars * 2

                    // 解析名称 (去除尾部 \0)
                    string name = Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');

                    // 添加到列表
                    partitions.Add(new PartitionInfo
                    {
                        Lun = lunId,
                        Name = name,
                        StartLba = firstLba,
                        StartLbaStr = firstLba.ToString(), // [新增] 填充字符串形式
                        Sectors = lastLba - firstLba + 1,
                        SectorSize = sectorSize,
                        FileName = "" // 默认不关联文件名，避免误报缺失
                    });

                    // 确保指针移动到下一个条目的正确位置
                    ms.Seek(currentEntryPos + header.SizeOfPartitionEntry, SeekOrigin.Begin);
                }
            }

            return partitions;
        }

        private static GPT_Header ReadHeader(BinaryReader br)
        {
            var h = new GPT_Header();
            h.Signature = br.ReadUInt64();
            h.Revision = br.ReadUInt32();
            h.HeaderSize = br.ReadUInt32();
            h.HeaderCRC32 = br.ReadUInt32();
            h.Reserved = br.ReadUInt32();
            h.MyLBA = br.ReadUInt64();
            h.AlternateLBA = br.ReadUInt64();
            h.FirstUsableLBA = br.ReadUInt64();
            h.LastUsableLBA = br.ReadUInt64();
            h.DiskGUID = br.ReadBytes(16);
            h.PartitionEntryLBA = br.ReadUInt64();
            h.NumberOfPartitionEntries = br.ReadUInt32();
            h.SizeOfPartitionEntry = br.ReadUInt32();
            h.PartitionEntryArrayCRC32 = br.ReadUInt32();
            return h;
        }

        private static bool CheckSignature(byte[] data, int offset)
        {
            if (data.Length < offset + 8) return false;
            // "EFI PART" 
            return BitConverter.ToUInt64(data, offset) == GPT_SIGNATURE;
        }

        private static bool IsZeroBytes(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0) return false;
            }
            return true;
        }
    }

    // --- 移植 gpttool 的 CRC32 算法 (为未来修改 GPT 做准备) ---
    public class Crc32
    {
        private static readonly uint[] Table;

        static Crc32()
        {
            uint poly = 0x04c11db7;
            Table = new uint[256];
            uint temp = 0;
            for (uint i = 0; i < Table.Length; ++i)
            {
                temp = i;
                for (int j = 8; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                        temp = (uint)((temp >> 1) ^ poly);
                    else
                        temp >>= 1;
                }
                Table[i] = temp;
            }
        }

        public static uint ComputeChecksum(byte[] bytes)
        {
            uint crc = 0xffffffff;
            for (int i = 0; i < bytes.Length; ++i)
            {
                byte index = (byte)((crc & 0xff) ^ bytes[i]);
                crc = (uint)((crc >> 8) ^ Table[index]);
            }
            return ~crc;
        }
    }
}
