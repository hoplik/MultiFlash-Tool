using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OPFlashTool.Qualcomm;

namespace OPFlashTool.Strategies
{
    public class StandardDeviceStrategy : IDeviceStrategy
    {
        public virtual string Name => "Standard Qualcomm";

        public virtual Task<bool> AuthenticateAsync(FirehoseClient client, string programmerPath, Action<string> log, Func<string, string> inputCallback = null, string digestPath = null, string signaturePath = null)
        {
            return Task.FromResult(true);
        }

        // [标准策略] 读取 PrimaryGPT
        public virtual async Task<List<PartitionInfo>> ReadGptAsync(FirehoseClient client, CancellationToken ct, Action<string> log)
        {
            var allPartitions = new List<PartitionInfo>();
            int maxLun = (client.SectorSize == 4096) ? 5 : 0;
            int sectorsToRead = (client.SectorSize == 4096) ? 6 : 34;
            int lunRead = 0;

            for (int lun = 0; lun <= maxLun; lun++)
            {
                if (ct.IsCancellationRequested) break;

                // 标准设备：直接读取 PrimaryGPT
                // Filename: gpt_main{lun}.bin
                // Label: PrimaryGPT
                // Sector: 0
                var data = await client.ReadGptPacketAsync(
                    lun.ToString(), 
                    0, 
                    sectorsToRead, 
                    "PrimaryGPT", 
                    $"gpt_main{lun}.bin", 
                    ct
                );

                if (data != null)
                {
                    var parts = GptParser.ParseGptBytes(data, lun);
                    if (parts != null && parts.Count > 0)
                    {
                        allPartitions.AddRange(parts);
                        lunRead++;
                    }
                }

                await Task.Delay(50);
            }

            log($"[GPT] 共读取到 {lunRead} 个 LUN，解析出 {allPartitions.Count} 个分区");
            if (allPartitions.Count == 0) throw new Exception("未读取到任何有效分区信息");
            return allPartitions;
        }

        public virtual async Task<bool> ReadPartitionAsync(FirehoseClient client, PartitionInfo part, string savePath, Action<long, long> progress, CancellationToken ct, Action<string> log)
        {
            // 标准读取，无需 Token
            return await client.ReadPartitionAsync(savePath, part.StartLba.ToString(), (long)part.Sectors, part.Lun.ToString(), progress, ct, part.Name);
        }

        public virtual async Task<bool> WritePartitionAsync(FirehoseClient client, PartitionInfo part, string imagePath, Action<long, long> progress, CancellationToken ct, Action<string> log)
        {
            // 标准写入，无需 Token
            return await client.FlashPartitionAsync(imagePath, part.StartLba.ToString(), (long)part.Sectors, part.Lun.ToString(), progress, ct, part.Name);
        }

        public virtual async Task<bool> ErasePartitionAsync(FirehoseClient client, PartitionInfo part, CancellationToken ct, Action<string> log)
        {
            return client.ErasePartition(part.StartLba.ToString(), (long)part.Sectors, part.Lun.ToString());
        }

        public virtual Task<bool> ResetAsync(FirehoseClient client, string mode, Action<string> log)
        {
            return Task.FromResult(client.Reset(mode));
        }
    }
}
