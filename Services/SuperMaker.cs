using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

namespace OPFlashTool.Services
{
    // [新增] 定义单个分区的刷写动作
    public class SuperFlashAction
    {
        public string PartitionName { get; set; }        // 分区名 (如 system_a)
        public string FilePath { get; set; }             // 本地文件绝对路径
        public long RelativeSectorOffset { get; set; }   // 相对于 Super 分区头部的扇区偏移
        public long SizeInBytes { get; set; }            // 文件大小
        public string DebugInfo { get; set; }            // 调试信息 (方便日志输出)
    }

    public class SuperMaker
    {
        private readonly Action<string> _log;
        private readonly string _lpmakePath;

        public SuperMaker(string binDir, Action<string> logCallback)
        {
            _log = logCallback;
            // [修改] 使用 C:\edltool 目录下的 lpmake.exe
            _lpmakePath = Path.Combine(DependencyManager.InstallDir, "lpmake.exe");

            // 移除旧的提取逻辑，现在由 DependencyManager 统一管理
        }

        // [新增] 智能入口：从固件根目录构建 super.img
        public async Task<bool> MakeSuperFromDirectoryAsync(string rootDirectory, string outputDir)
        {
            _log($"[SuperMaker] 启动智能构建模式 (根目录: {Path.GetFileName(rootDirectory)})");

            if (!Directory.Exists(rootDirectory))
            {
                _log("[Error] 目录不存在。");
                return false;
            }

            // 1. 自动寻找配置文件 (META/*.json)
            string metaDir = Path.Combine(rootDirectory, "META");
            string jsonPath = "";

            if (Directory.Exists(metaDir))
            {
                var jsonFiles = Directory.GetFiles(metaDir, "*.json");
                jsonPath = jsonFiles.FirstOrDefault(f => Path.GetFileName(f).StartsWith("super_def", StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(jsonPath))
                {
                    jsonPath = jsonFiles.FirstOrDefault(f =>
                        !Path.GetFileName(f).Equals("config.json", StringComparison.OrdinalIgnoreCase) &&
                        !Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase)
                    );
                }
            }

            if (string.IsNullOrEmpty(jsonPath))
            {
                var rootJsons = Directory.GetFiles(rootDirectory, "*.json");
                jsonPath = rootJsons.FirstOrDefault(f => Path.GetFileName(f).StartsWith("super_def", StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                _log("[Error] 无法找到有效的 super 分区定义文件 (.json)。请确保 META 文件夹中包含该文件。");
                return false;
            }

            _log($"[锁定] 配置文件: {Path.GetFileName(jsonPath)}");

            // 2. 验证 IMAGES 目录 (仅做提示)
            string imagesDir = Path.Combine(rootDirectory, "IMAGES");
            if (!Directory.Exists(imagesDir))
            {
                _log("[警告] 根目录下未找到 IMAGES 文件夹，正在尝试继续...");
            }

            // 3. 调用核心构建方法
            return await MakeSuperImgAsync(jsonPath, outputDir, rootDirectory);
        }

        public async Task<bool> MakeSuperImgAsync(string jsonPath, string outputDir, string imageRootDir = null)
        {
            _log("[Info] SuperMaker v20 (Smart Path) Initialized.");
            if (!File.Exists(_lpmakePath))
            {
                _log($"[Error] 找不到 lpmake.exe ({_lpmakePath})，请检查依赖是否正确解压。");
                return false;
            }

            string tempRawDir = Path.Combine(Path.GetTempPath(), "OPFlashTool_Raw_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempRawDir);
                _log($"[Info] 正在解析配置文件: {Path.GetFileName(jsonPath)}");

                string jsonContent;
                using (var reader = File.OpenText(jsonPath))
                {
                    jsonContent = await reader.ReadToEndAsync();
                }
                var def = JsonSerializer.Deserialize(jsonContent, AppJsonContext.Default.SuperDef);

                if (def?.BlockDevices == null || def.BlockDevices.Count == 0)
                {
                    _log("[Error] JSON 格式无效: 缺少 block_devices 定义");
                    return false;
                }

                string baseDir = imageRootDir;
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = Path.GetDirectoryName(jsonPath) ?? "";
                }
                _log($"[Info] 镜像搜索根目录: {baseDir}");

                var device = def.BlockDevices[0];
                long deviceSize = 0;
                if (!string.IsNullOrEmpty(device.Size))
                {
                    string sizeStr = device.Size.Replace(",", "").Replace("_", "").Trim();
                    if (sizeStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        try { deviceSize = Convert.ToInt64(sizeStr, 16); } catch { /* 无效数字格式，使用默认值 */ }
                    }
                    else
                    {
                        long.TryParse(sizeStr, out deviceSize);
                    }
                }

                if (deviceSize == 0) _log("[Warn] Device Size 解析结果为 0! 请检查 JSON。");
                else _log($"[Info] 解析后的 Device Size: {deviceSize}");

                long alignment = 0;
                if (!string.IsNullOrEmpty(device.Alignment))
                {
                    string alignStr = device.Alignment.Replace(",", "").Replace("_", "").Trim();
                    if (alignStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        try { alignment = Convert.ToInt64(alignStr, 16); } catch { /* 无效数字格式，使用默认值 */ }
                    }
                    else
                    {
                        long.TryParse(alignStr, out alignment);
                    }
                }

                var args = new StringBuilder();
                args.Append($"--device-size {deviceSize} ");
                args.Append("--super-name super ");
                args.Append("--metadata-size 65536 ");
                args.Append("--metadata-slots 2 ");
                args.Append($"--alignment {alignment} ");
                args.Append("--force-full-image ");

                // 输出路径：优先用户指定目录，其次 imageRootDir/IMAGES，其次 imageRootDir，其次 JSON 目录
                string finalOutputDir = outputDir;
                if (string.IsNullOrEmpty(finalOutputDir))
                {
                    if (!string.IsNullOrEmpty(imageRootDir))
                    {
                        if (imageRootDir.EndsWith("IMAGES", StringComparison.OrdinalIgnoreCase))
                        {
                            finalOutputDir = imageRootDir;
                        }
                        else
                        {
                            string imagesSubDir = Path.Combine(imageRootDir, "IMAGES");
                            finalOutputDir = Directory.Exists(imagesSubDir) ? imagesSubDir : imageRootDir;
                        }
                    }
                    else
                    {
                        finalOutputDir = Path.GetDirectoryName(jsonPath) ?? "";
                    }
                }

                string outputPath = Path.Combine(finalOutputDir, "super.img");

                if (def.Groups != null)
                {
                    foreach (var group in def.Groups)
                    {
                        if (group.Name == "default") continue;
                        if (long.TryParse(group.MaximumSize, out long maxSize))
                        {
                             args.Append($"--group {group.Name}:{maxSize} ");
                        }
                    }
                }

                if (def.Partitions != null)
                {
                    foreach (var part in def.Partitions)
                    {
                        if (!long.TryParse(part.Size, out long size)) size = 0;
                        string imgPath = null;

                        if (!string.IsNullOrEmpty(part.Path))
                        {
                            string relativePath = part.Path.Replace("/", "\\");
                            string tempPath = Path.Combine(baseDir, relativePath);
                            if (!File.Exists(tempPath) && relativePath.StartsWith("IMAGES\\", StringComparison.OrdinalIgnoreCase))
                            {
                                string altPath = Path.Combine(baseDir, relativePath.Substring(7));
                                if (File.Exists(altPath))
                                {
                                    tempPath = altPath;
                                    _log($"[Info] 路径自动修正: {part.Path} -> {tempPath}");
                                }
                            }

                            if (File.Exists(tempPath))
                            {
                                imgPath = tempPath;
                                if (SparseImageHandler.IsSparseImage(imgPath))
                                {
                                    // 简化 Sparse 转换日志，屏蔽内部细节
                                    _log($"[解压] {part.Name} (Sparse -> Raw)...");
                                    string rawPath = Path.Combine(tempRawDir, part.Name + ".raw");

                                    // 传入空日志委托以屏蔽 SparseImageHandler 内部的详细输出
                                    if (await SparseImageHandler.ConvertToRawAsync(imgPath, rawPath, _ => { }))
                                    {
                                        imgPath = rawPath;
                                        _log("ok"); // 发送完成信号，消除误判的错误日志
                                    }
                                    else
                                    {
                                        _log("失败");
                                    }
                                }

                                try 
                                {
                                    using (var fs = new FileStream(imgPath, FileMode.Open, FileAccess.ReadWrite))
                                    {
                                        long imgSize = fs.Length;
                                        if (imgSize % 4096 != 0)
                                        {
                                            long padding = 4096 - (imgSize % 4096);
                                            fs.Seek(0, SeekOrigin.End);
                                            fs.Write(new byte[padding], 0, (int)padding);
                                            imgSize += padding;
                                        }
                                        if (imgSize > size) size = imgSize;
                                    }
                                }
                                catch (Exception ex) { _log($"[Warn] 镜像检查失败: {ex.Message}"); }
                            }
                            else
                            {
                                _log($"[Warn] 文件缺失: {part.Name} ({part.Path}) -> 生成空分区");
                            }
                        }

                        string groupName = !string.IsNullOrEmpty(part.GroupName) ? part.GroupName : part.Group;
                        if (string.IsNullOrEmpty(groupName)) groupName = "default";

                        args.Append($"--partition {part.Name}:readonly:{size}:{groupName} ");
                        if (imgPath != null) args.Append($"--image {part.Name}=\"{imgPath}\" ");
                    }
                }

                args.Append($"--output \"{outputPath}\" ");

                _log("[打包] 开始构建 super.img (这可能需要几分钟)...");
                var outputDirPath = Path.GetDirectoryName(outputPath);
                Directory.CreateDirectory(string.IsNullOrEmpty(outputDirPath) ? "." : outputDirPath);
                if (File.Exists(outputPath)) File.Delete(outputPath);

                var psi = new ProcessStartInfo
                {
                    FileName = _lpmakePath,
                    Arguments = args.ToString(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var outputBuffer = new StringBuilder();
                using (var proc = new Process { StartInfo = psi })
                {
                    // 过滤 lpmake 的冗余输出，仅保留关键信息
                    DataReceivedEventHandler handler = (s, e) => {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            if (e.Data.Contains("will resize")) return;
                            if (e.Data.Contains("Invalid sparse")) return;
                            if (e.Data.Contains("I lpmake")) return;
                            if (e.Data.StartsWith(" ")) return; // 过滤缩进信息

                            _log($"[信息] {e.Data}");
                            outputBuffer.AppendLine(e.Data);
                        }
                    };
                    proc.OutputDataReceived += handler;
                    proc.ErrorDataReceived += handler;

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    await Task.Run(() => proc.WaitForExit());

                    if (proc.ExitCode == 0)
                    {
                        _log($"[成功] super.img 构建完毕");

                        // 等待文件句柄释放
                        for (int retries = 0; retries < 5; retries++)
                        {
                            try
                            {
                                using (var fs = File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                                break;
                            }
                            catch
                            {
                                await Task.Delay(800);
                            }
                        }

                        try
                        {
                            var fileInfo = new FileInfo(outputPath);
                            bool isSparse = false;
                            uint magic = 0;

                            try
                            {
                                using (var fs = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (var br = new BinaryReader(fs))
                                {
                                    if (fs.Length >= 4) magic = br.ReadUInt32();
                                }
                            }
                            catch (Exception ex)
                            {
                                _log($"[Warn] 读取文件头失败: {ex.Message}");
                            }

                            if (magic == 0xED26FF3A) isSparse = true;

                            if (!isSparse && deviceSize > 0 && fileInfo.Length < deviceSize)
                            {
                                _log($"[Info] Raw 镜像小于声明大小，填充至 {deviceSize}");
                                try
                                {
                                    using (var fs = new FileStream(outputPath, FileMode.Open, FileAccess.Write))
                                    {
                                        fs.SetLength(deviceSize);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _log($"[Error] 填充失败: {ex.Message}");
                                }
                            }

                            if (isSparse)
                            {
                                _log("[Info] 检测到 Sparse，转换为 Raw...");
                                string sparsePath = outputPath + ".sparse";
                                try
                                {
                                    if (File.Exists(sparsePath)) File.Delete(sparsePath);
                                    File.Move(outputPath, sparsePath);
                                    bool converted = await SparseImageHandler.ConvertToRawAsync(sparsePath, outputPath, _log);
                                    if (converted) try { File.Delete(sparsePath); } catch { /* 清理失败不影响主流程 */ }
                                    else
                                    {
                                        _log("[Error] Sparse 转换失败，恢复原文件。");
                                        if (File.Exists(outputPath)) File.Delete(outputPath);
                                        File.Move(sparsePath, outputPath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _log($"[Error] Sparse 处理失败: {ex.Message}");
                                    if (!File.Exists(outputPath) && File.Exists(sparsePath))
                                    {
                                        File.Move(sparsePath, outputPath);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log($"[Error] 输出检查失败: {ex.Message}");
                        }

                        _log($"[路径] {outputPath}");
                        try { Process.Start("explorer.exe", $"/select,\"{outputPath}\""); } catch { /* 打开资源管理器失败不影响主流程 */ }
                        return true;
                    }
                    else
                    {
                        _log($"[失败] lpmake 错误码: {proc.ExitCode}");
                        if (outputBuffer.Length > 0) _log($"[失败] 详细错误:\n{outputBuffer}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"[Exception] {ex.Message}");
                return false;
            }
            finally
            {
                if (Directory.Exists(tempRawDir)) try { Directory.Delete(tempRawDir, true); } catch { /* 清理临时目录失败不影响主流程 */ }
            }
        }

        // =========================================================
        // [新增功能] 9008 模式不打包直接刷写布局计算
        // =========================================================
        public async Task<List<SuperFlashAction>> PrepareDirectFlashActionsAsync(string jsonPath, string imageRootDir = null)
        {
            var actions = new List<SuperFlashAction>();
            _log("[Info] 正在计算 Super 分区直刷布局 (不合并模式)...");

            // 1. 确定根目录
            string baseDir = imageRootDir;
            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = Path.GetDirectoryName(jsonPath) ?? "";
            }

            // 2. 读取并反序列化 JSON
            string jsonContent;
            using (var reader = File.OpenText(jsonPath))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;
                
                // --- A. 获取对齐参数 ---
                long alignment = 1048576; // 默认 1MB
                if (root.TryGetProperty("block_devices", out var devices) && devices.GetArrayLength() > 0)
                {
                    var dev = devices[0];
                    if (dev.TryGetProperty("alignment", out var alignProp))
                    {
                        string alignStr = alignProp.GetString()?.Replace("_", "") ?? "1048576";
                        long.TryParse(alignStr, out alignment);
                    }
                }
                _log($"[Layout] 对齐大小: {alignment} bytes");

                long currentByteOffset = 0;
                const int SECTOR_SIZE = 512;

                // 创建临时目录用于存放转换后的 Raw
                string tempRawDir = Path.Combine(Path.GetTempPath(), "OPFlashTool_DirectRaw_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRawDir);

                // --- B. 处理 Super Metadata (必须在 Offset 0) ---
                if (root.TryGetProperty("super_meta", out var meta))
                {
                    string metaPathRel = meta.GetProperty("path").GetString().Replace("/", "\\");
                    string metaSizeStr = meta.GetProperty("size").GetString();
                    long.TryParse(metaSizeStr, out long metaSize);

                    string fullMetaPath = Path.Combine(baseDir, metaPathRel);

                    if (!File.Exists(fullMetaPath))
                    {
                        if (metaPathRel.StartsWith("IMAGES\\", StringComparison.OrdinalIgnoreCase))
                        {
                            string altPath = Path.Combine(baseDir, metaPathRel.Substring(7));
                            if (File.Exists(altPath))
                            {
                                fullMetaPath = altPath;
                                _log($"[Info] Meta路径修正: {metaPathRel} -> {fullMetaPath}");
                            }
                        }
                    }

                    if (File.Exists(fullMetaPath))
                    {
                        actions.Add(new SuperFlashAction
                        {
                            PartitionName = "super_meta",
                            FilePath = fullMetaPath,
                            RelativeSectorOffset = 0,
                            SizeInBytes = metaSize,
                            DebugInfo = "Metadata (Offset 0)"
                        });
                        currentByteOffset += metaSize; // 更新偏移
                    }
                    else
                    {
                        _log($"[Error] 找不到 super_meta 文件: {fullMetaPath}");
                        return null; // Metadata 缺失是致命错误
                    }
                }
                else
                {
                    _log("[Error] JSON 中缺少 'super_meta' 定义，无法进行直刷。");
                    return null;
                }

                // --- C. 处理各个分区 ---
                if (root.TryGetProperty("partitions", out var partitions))
                {
                    foreach (var part in partitions.EnumerateArray())
                    {
                        // 只需要处理包含 'path' (即有镜像文件) 的分区
                        if (part.TryGetProperty("path", out var pathProp))
                        {
                            string partName = part.GetProperty("name").GetString();
                            string relPath = pathProp.GetString().Replace("/", "\\");
                            string fullPath = Path.Combine(baseDir, relPath);

                            if (!File.Exists(fullPath))
                            {
                                if (relPath.StartsWith("IMAGES\\", StringComparison.OrdinalIgnoreCase))
                                {
                                    string altPath = Path.Combine(baseDir, relPath.Substring(7));
                                    if (File.Exists(altPath))
                                    {
                                        fullPath = altPath;
                                        _log($"[Info] 路径自动修正: {relPath} -> {fullPath}");
                                    }
                                }
                            }

                            // 1. 对齐计算: 下一个写入点必须是 alignment 的整数倍
                            long alignedStartOffset = AlignOffset(currentByteOffset, alignment);
                            
                            if (File.Exists(fullPath))
                            {
                                string finalPath = fullPath;
                                
                                // 2. 检查 Sparse -> Raw 转换
                                // Firehose Offset 写入必须使用 Raw 格式
                                if (SparseImageHandler.IsSparseImage(fullPath))
                                {
                                    _log($"[Convert] 检测到 Sparse: {partName} -> 转换为 Raw...");
                                    string rawPath = Path.Combine(tempRawDir, partName + ".raw");
                                    
                                    // 复用你现有的 SparseHandler
                                    bool converted = await SparseImageHandler.ConvertToRawAsync(fullPath, rawPath, _log);
                                    if (converted)
                                    {
                                        finalPath = rawPath;
                                    }
                                    else
                                    {
                                        _log($"[Error] 转换 {partName} 失败，无法继续。");
                                        return null;
                                    }
                                }

                                // 3. 获取实际 Raw 大小
                                long fileSize = new FileInfo(finalPath).Length;

                                // 4. 生成动作
                                actions.Add(new SuperFlashAction
                                {
                                    PartitionName = partName,
                                    FilePath = finalPath,
                                    RelativeSectorOffset = alignedStartOffset / SECTOR_SIZE,
                                    SizeInBytes = fileSize,
                                    DebugInfo = $"Offset: {alignedStartOffset} (Sector {alignedStartOffset / SECTOR_SIZE})"
                                });

                                // 5. 更新偏移量 (当前起点 + 文件实际大小)
                                currentByteOffset = alignedStartOffset + fileSize;
                            }
                            else
                            {
                                _log($"[Warn] 文件缺失: {relPath} (分区 {partName}) - 跳过写入");
                            }
                        }
                    }
                }

                _log($"[Success] 布局计算完成，共 {actions.Count} 个步骤。");
                return actions;
            }
        }

        private long AlignOffset(long current, long alignment)
        {
            if (alignment == 0) return current;
            long remainder = current % alignment;
            if (remainder == 0) return current;
            return current + (alignment - remainder);
        }
    }

    // --- JSON 数据模型 ---

    public class SuperDef
    {
        [JsonPropertyName("super_meta")]
        public SuperMeta? Meta { get; set; }

        [JsonPropertyName("block_devices")]
        public List<BlockDevice>? BlockDevices { get; set; }

        [JsonPropertyName("groups")]
        public List<Group>? Groups { get; set; }

        [JsonPropertyName("partitions")]
        public List<Partition>? Partitions { get; set; }
    }

    public class SuperMeta
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; }
    }

    public class BlockDevice
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("size")]
        public string Size { get; set; } = "0";

        [JsonPropertyName("alignment")]
        public string Alignment { get; set; } = "0";
    }

    public class Group
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("maximum_size")]
        public string MaximumSize { get; set; } = "0";
    }

    public class Partition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("group_name")]
        public string GroupName { get; set; } = "";

        [JsonPropertyName("group")]
        public string Group { get; set; } = "";

        [JsonPropertyName("size")]
        public string Size { get; set; } = "0";

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }
}