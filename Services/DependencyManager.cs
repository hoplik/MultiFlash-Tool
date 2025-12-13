using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace OPFlashTool.Services
{
    public static class DependencyManager
    {
        // 目标解压目录
        public static readonly string InstallDir = @"C:\edltool";
        
        // 资源前缀 (根据项目命名空间和文件夹结构调整)
        // 假设文件放在 Resources/SuperTools 目录下
        private const string ResourcePrefix = "OPFlashTool.Resources.SuperTools.";

        public static async Task ExtractDependenciesAsync(Action<string> log = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(InstallDir))
                    {
                        Directory.CreateDirectory(InstallDir);
                    }

                    var assembly = Assembly.GetExecutingAssembly();
                    var allResources = assembly.GetManifestResourceNames();

                    // 筛选出我们的工具资源
                    var toolResources = allResources.Where(r => r.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase));

                    foreach (var resourceName in toolResources)
                    {
                        // 解析文件名
                        // ResourcePrefix = "OPFlashTool.Resources.SuperTools."
                        // resourceName = "OPFlashTool.Resources.SuperTools.lpmake.exe"
                        // fileName = "lpmake.exe"
                        string fileName = resourceName.Substring(ResourcePrefix.Length);
                        string destPath = Path.Combine(InstallDir, fileName);

                        // 提取文件 (始终覆盖，确保版本最新)
                        using (var stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream == null) continue;

                            // 检查文件是否存在且大小一致，避免重复写入 (可选优化)
                            if (File.Exists(destPath))
                            {
                                var info = new FileInfo(destPath);
                                if (info.Length == stream.Length)
                                {
                                    // log?.Invoke($"[Dependency] {fileName} 已存在且大小一致，跳过。");
                                    continue; 
                                }
                            }

                            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                            {
                                stream.CopyTo(fileStream);
                            }
                            // log?.Invoke($"[Dependency] 已解压: {fileName} -> {InstallDir}");
                        }
                    }
                    
                    // 配置环境变量，将 InstallDir 添加到 PATH
                    ConfigureEnvironment();
                }
                catch (Exception ex)
                {
                    log?.Invoke($"依赖释放失败: {ex.Message}");
                }
            });
        }

        private static void ConfigureEnvironment()
        {
            try
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                if (pathEnv != null && !pathEnv.Contains(InstallDir))
                {
                    Environment.SetEnvironmentVariable("PATH", pathEnv + ";" + InstallDir, EnvironmentVariableTarget.Process);
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[DependencyManager] ConfigureEnvironment failed: {ex.Message}");
            }
        }
    }
}
