using OPFlashTool.Qualcomm;

namespace OPFlashTool.Authentication
{
    public interface IAuthStrategy
    {
        string Name { get; }
        bool PerformAuth(FirehoseClient firehose, string programmerPath);
    }
}
