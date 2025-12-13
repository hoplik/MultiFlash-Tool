using System.IO;
using OPFlashTool.Qualcomm;

namespace OPFlashTool.Authentication
{
    public class DefaultVipStrategy : IAuthStrategy
    {
        public string Name => "Generic VIP (digest/signature)";

        public bool PerformAuth(FirehoseClient firehose, string programmerPath)
        {
            string dir = Path.GetDirectoryName(programmerPath);
            if (string.IsNullOrEmpty(dir)) return true;

            // 常见文件名
            string digestPath = Path.Combine(dir, "digest.bin");
            string sigPath = Path.Combine(dir, "signature.bin");

            // 也可以尝试查找其他扩展名，如 .mbn
            if (!File.Exists(digestPath)) digestPath = Path.Combine(dir, "digest.mbn");
            if (!File.Exists(sigPath)) sigPath = Path.Combine(dir, "signature.mbn");

            if (File.Exists(digestPath) && File.Exists(sigPath))
            {
                return firehose.PerformVipAuth(digestPath, sigPath);
            }

            // 如果找不到文件，视为无需验证 (或者由具体业务逻辑决定是否报错)
            // 这里返回 true 表示 "没有阻碍继续流程的错误"
            return true;
        }
    }
}
