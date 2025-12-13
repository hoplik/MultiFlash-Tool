using System.Text;

namespace OPFlashTool;

public static class GptConfig
{
    public static string GetGptReadXml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" ?>");
        sb.AppendLine("<data>");
        
        // Generate entries for LUN 0 to 5
        for (int i = 0; i <= 5; i++)
        {
            // Primary GPT (Head)
            sb.AppendLine($"  <program SECTOR_SIZE_IN_BYTES=\"4096\" file_sector_offset=\"0\" filename=\"gpt_main{i}.bin\" label=\"PrimaryGPT\" num_partition_sectors=\"6\" partofsingleimage=\"true\" physical_partition_number=\"{i}\" readbackverify=\"false\" size_in_KB=\"24.0\" sparse=\"false\" start_byte_hex=\"0x0\" start_sector=\"0\"/>");
            
            // Backup GPT (Tail)
            sb.AppendLine($"  <program SECTOR_SIZE_IN_BYTES=\"4096\" file_sector_offset=\"0\" filename=\"gpt_backup{i}.bin\" label=\"BackupGPT\" num_partition_sectors=\"5\" partofsingleimage=\"true\" physical_partition_number=\"{i}\" readbackverify=\"false\" size_in_KB=\"20.0\" sparse=\"false\" start_byte_hex=\"(4096*NUM_DISK_SECTORS)-20480.\" start_sector=\"NUM_DISK_SECTORS-5.\"/>");
        }
        
        sb.AppendLine("</data>");
        return sb.ToString();
    }
}
