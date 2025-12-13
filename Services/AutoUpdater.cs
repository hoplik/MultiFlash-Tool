using System.Threading.Tasks;

namespace OPFlashTool.Services
{
    // 云端功能已移除 - 自动更新功能已禁用
    public static class AutoUpdater
    {
        public static Task CheckAndPerformUpdateAsync()
        {
            // 自动更新功能已禁用
            return Task.CompletedTask;
        }
    }
}
