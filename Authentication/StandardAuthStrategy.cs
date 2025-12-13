using OPFlashTool.Qualcomm;

namespace OPFlashTool.Authentication
{
    public class StandardAuthStrategy : IAuthStrategy
    {
        public string Name => "Standard (No Auth)";

        public bool PerformAuth(FirehoseClient firehose, string programmerPath)
        {
            // 标准设备无需额外验证，直接返回成功
            return true;
        }
    }
}
