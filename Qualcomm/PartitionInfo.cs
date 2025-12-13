using System;

namespace OPFlashTool.Qualcomm
{
    public class FlashPartitionInfo
    {
        public string Lun { get; set; } = "0";
        public string Name { get; set; } = "";
        
        // [修改] 改为 string 类型，支持 "NUM_DISK_SECTORS-33." 等公式
        public string StartSector { get; set; } = "0"; 
        
        public long NumSectors { get; set; }
        public string Filename { get; set; } = "";
        public long FileOffset { get; set; } = 0;
        public bool IsSparse { get; set; } = false;

        public FlashPartitionInfo() { }

        // [修改] 构造函数参数类型也改为 string
        public FlashPartitionInfo(string lun, string name, string start, long sectors, string filename = "", long offset = 0)
        {
            Lun = lun;
            Name = name;
            StartSector = start;
            NumSectors = sectors;
            Filename = filename;
            FileOffset = offset;
        }
    }
}
