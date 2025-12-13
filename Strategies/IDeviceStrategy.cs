using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OPFlashTool.Qualcomm;

namespace OPFlashTool.Strategies
{
    public interface IDeviceStrategy
    {
        string Name { get; }

        // [修改] 增加了 digestPath 和 signaturePath 参数
        Task<bool> AuthenticateAsync(
            FirehoseClient client, 
            string programmerPath, 
            Action<string> log, 
            Func<string, string> inputCallback = null,
            string digestPath = null,     // 新增
            string signaturePath = null   // 新增
        );

        // 2. 核心操作阶段 (隔离点)
        Task<List<PartitionInfo>> ReadGptAsync(FirehoseClient client, CancellationToken ct, Action<string> log);
        Task<bool> ReadPartitionAsync(FirehoseClient client, PartitionInfo part, string savePath, Action<long, long> progress, CancellationToken ct, Action<string> log);
        Task<bool> WritePartitionAsync(FirehoseClient client, PartitionInfo part, string imagePath, Action<long, long> progress, CancellationToken ct, Action<string> log);
        Task<bool> ErasePartitionAsync(FirehoseClient client, PartitionInfo part, CancellationToken ct, Action<string> log);
        Task<bool> ResetAsync(FirehoseClient client, string mode, Action<string> log);
    }
}
