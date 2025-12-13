// 云端功能已移除 - 此文件保留空实现以保持编译兼容性
using System;
using System.Drawing;
using System.Threading.Tasks;
using AntdUI;

namespace OPFlashTool
{
    public static class Cloud
    {
        public class CloudDownloadContext
        {
            public Input? StatusInput { get; set; }
            public Progress? ProgressPrimary { get; set; }
            public Progress? ProgressSecondary { get; set; }
            public AntdUI.Label? SpeedLabel { get; set; }
            public Action<string, Color, bool>? AppendLog { get; set; }
            public Action<string>? ShowWarnMessage { get; set; }
            public Action<string>? ShowErrorMessage { get; set; }
        }

        public static void InitializeHttpClient() { }
        
        public static void UpdateProgress(CloudDownloadContext? context, float value) { }
        
        public static void UpdateDownloadSpeed(CloudDownloadContext? context, long bytes, double seconds) { }
        
        public static void 安卓驱动ToolStripMenuItem_Click(object? sender, EventArgs e) { }
        
        public static void 高通驱动ToolStripMenuItem_Click(object? sender, EventArgs e) { }
    }
}
