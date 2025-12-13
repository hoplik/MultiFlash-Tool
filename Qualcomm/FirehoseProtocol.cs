using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using OPFlashTool;

namespace OPFlashTool.Qualcomm
{
    public class FirehoseClient
    {
        private SerialPort _port;
        private Action<string> _log;
        private int _sectorSize = 4096;
        private int _maxPayloadSize = 1048576; // Default 1MB
        private readonly StringBuilder _rxBuffer = new StringBuilder(); // 累积未处理的串口数据
        
        // [新增] 公开存储类型供策略使用
        public string StorageType { get; private set; } = "ufs";

        // Sparse Image Constants
        private const uint SPARSE_HEADER_MAGIC = 0xED26FF3A;
        private const ushort CHUNK_TYPE_RAW = 0xCAC1;
        private const ushort CHUNK_TYPE_FILL = 0xCAC2;
        private const ushort CHUNK_TYPE_DONT_CARE = 0xCAC3;
        private const ushort CHUNK_TYPE_CRC32 = 0xCAC4;

        public FirehoseClient(SerialPort port, Action<string> logger)
        {
            _port = port;
            _log = logger;

            // 尽量放大缓冲区，降低高速传输时的溢出风险
            try
            {
                if (_port.ReadBufferSize < 1024 * 1024) _port.ReadBufferSize = 1024 * 1024;
                if (_port.WriteBufferSize < 1024 * 1024) _port.WriteBufferSize = 1024 * 1024;
                _port.DtrEnable = true;
                _port.RtsEnable = true;
            }
            catch { /* 某些驱动可能不支持调整，忽略 */ }
        }

        // --- 1. VIP 验证 ---
        public bool PerformVipAuth(string digestPath, string signaturePath, Action<long, long>? progressCallback = null)
        {
            if (!File.Exists(digestPath) || !File.Exists(signaturePath))
            {
                _log("[VIP] 错误: 缺少验证文件");
                return false;
            }

            _log("[VIP] 开始安全验证...");
            
            // [Fix] 1. 清理缓冲区，防止残留数据
            PurgeBuffer();

            // [Fix] 2. 尝试重置 SHA 引擎 (解决 Too many concurrent open SHA handles)
            // SendXmlCommand("<data><sha256init Verbose=\"1\"/></data>", true);
            Thread.Sleep(100);
            PurgeBuffer();
            
            _log("[VIP] 发送 Digest...");
            SendRawFile(digestPath, true, progressCallback);
            Thread.Sleep(200);

            _log("[VIP] 发送 Verify 指令...");
            SendXmlCommand("<data><verify value=\"ping\" EnableVip=\"1\"/></data>", true);
            Thread.Sleep(200);

            _log("[VIP] 发送 Signature...");
            SendRawFile(signaturePath, true, progressCallback);
            Thread.Sleep(200);

            _log("[VIP] 发送 SHA256Init...");
            SendXmlCommand("<data><sha256init Verbose=\"1\"/></data>", true);

            // 强制清理缓冲区，防止残留数据影响后续流程
            PurgeBuffer();

            _log("[VIP] 验证流程结束 (已忽略错误)");
            return true;
        }

        // --- 2. 基础配置 ---
        public bool Configure(string storageType = "ufs")
        {
            StorageType = storageType.ToLower();
            _sectorSize = (StorageType == "emmc") ? 512 : 4096;
            
            // [修复] 增加 EnableFlash="1" 和 ZlpAwareHost="1" 以解决 "Mode= Invalid value" 和 "EnableFlash not found" 错误
            // 设置 ZlpAwareHost="0" 避免设备等待主机 ZLP（SerialPort 无法发送 ZLP）
            string xml = $"<?xml version=\"1.0\" ?><data><configure MemoryName=\"{storageType}\" Verbose=\"0\" AlwaysValidate=\"0\" MaxPayloadSizeToTargetInBytes=\"{_maxPayloadSize}\" ZlpAwareHost=\"0\" SkipStorageInit=\"0\" CheckDevinfo=\"0\" EnableFlash=\"1\" /></data>";
            
            byte[] data = Encoding.UTF8.GetBytes(xml);
            _port.Write(data, 0, data.Length);
            
            int maxRetries = 50;
            while (maxRetries-- > 0)
            {
                XElement? resp = ProcessXmlResponse();
                if (resp != null)
                {
                    string val = resp.Attribute("value")?.Value ?? "";
                    if (val.Equals("ACK", StringComparison.OrdinalIgnoreCase) ||
                        val.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        string ss = resp.Attribute("SectorSizeInBytes")?.Value;
                        if (int.TryParse(ss, out int size)) _sectorSize = size;
                        string mp = resp.Attribute("MaxPayloadSizeToTargetInBytes")?.Value;
                        if (int.TryParse(mp, out int maxPayload)) _maxPayloadSize = maxPayload;
                        
                        _log($"[Config] SectorSize:{_sectorSize}, MaxPayload:{_maxPayloadSize}");
                        PurgeBuffer();
                        Thread.Sleep(100);
                        return true;
                    }
                }
            }
            return false;
        }

        // [新增] 设置激活 LUN (SetBootableStorageDrive)
        public bool SetBootLun(int lun)
        {
            string xml = $"<?xml version=\"1.0\" ?><data><setbootablestoragedrive value=\"{lun}\" /></data>";
            return SendXmlCommand(xml, true);
        }

        public string GetStorageInfo(int lun = 0)
        {
            try
            {
                // [Fix] Use snake_case for attribute to ensure device respects the LUN parameter
                string xml = $"<?xml version=\"1.0\" ?><data><getstorageinfo physical_partition_number=\"{lun}\"/></data>";
                byte[] data = Encoding.UTF8.GetBytes(xml);
                _port.Write(data, 0, data.Length);

                StringBuilder sb = new StringBuilder();
                List<string> logs = new List<string>();
                
                int maxRetries = 50; 
                while (maxRetries-- > 0)
                {
                    // [Fix] Capture logs using the new parameter
                    XElement? resp = ProcessXmlResponse(logs);
                    
                    if (resp != null)
                    {
                        string val = resp.Attribute("value")?.Value;
                        // Check for final response (ACK/NAK/true/false)
                        if (!string.IsNullOrEmpty(val))
                        {
                            // Append all captured logs to the result
                            foreach (var log in logs) sb.AppendLine(log);
                            
                            // Also append attributes from the response tag itself (e.g. rawmode, etc.)
                            foreach (var attr in resp.Attributes())
                            {
                                if (attr.Name != "value") sb.AppendLine($"{attr.Name}: {attr.Value}");
                            }
                            
                            return sb.ToString();
                        }
                    }
                    else
                    {
                        // If null, maybe timeout or no more data, but we loop until timeout in ProcessXmlResponse
                        // If ProcessXmlResponse returns null, it means no valid XML found in 5 seconds.
                        break;
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting storage info: {ex.Message}";
            }
        }

        public void SetSectorSize(int size) => _sectorSize = size;
        public int SectorSize => _sectorSize;

        // --- 4. 小米/通用 扩展功能 ---

        /// <summary>
        /// 发送 XML 并获取指定属性的值 (用于小米获取 Blob)
        /// [修复] 引入智能重试逻辑：收到 ACK 后自动延长等待，防止过早退出
        /// </summary>
        public string? SendXmlCommandWithAttributeResponse(string xml, string attributeName, int maxRetries = 50)
        {
            try
            {
                PurgeBuffer();
                byte[] data = Encoding.UTF8.GetBytes(xml);
                _port.Write(data, 0, data.Length);

                int currentTry = 0;
                bool hasReceivedAck = false; // 标记是否收到过 ACK

                while (currentTry < maxRetries)
                {
                    currentTry++;
                    XElement? resp = ProcessXmlResponse();

                    if (resp != null)
                    {
                        // 1. 检查是否存在目标属性 (例如 "value")
                        var attr = resp.Attribute(attributeName);
                        if (attr != null)
                        {
                            string val = attr.Value;
                            
                            // 2. 过滤无效响应 (ACK/true 只是确认收到，不是数据)
                            if (val.Equals("ACK", StringComparison.OrdinalIgnoreCase) ||
                                val.Equals("true", StringComparison.OrdinalIgnoreCase))
                            {
                                // [关键策略] 既然设备回了 ACK，说明它活着且正在处理
                                // 我们绝对不能现在放弃。如果剩余重试次数不足，强制加时！
                                hasReceivedAck = true;
                                if ((maxRetries - currentTry) < 10) 
                                {
                                    maxRetries += 5; // 自动续命 5 次
                                }
                                // _log("[Debug] 收到 ACK，继续等待数据...");
                                continue; 
                            }

                            // 3. 拿到真正的数据，返回
                            return val;
                        }

                        // 检查是否明确拒绝
                        string statusVal = resp.Attribute("value")?.Value ?? "";
                        if (statusVal.Contains("NAK")) return null;
                    }
                    else
                    {
                        // 没读到数据。如果之前收到过 ACK，说明设备可能在计算，多等一会
                        if (hasReceivedAck)
                        {
                            Thread.Sleep(200); 
                        }
                        else
                        {
                            Thread.Sleep(50);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"[Error] XML Response Error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 发送签名数据 (用于小米验证)
        /// </summary>
        public bool SendSignature(byte[] signatureData)
        {
            try 
            {
                // 1. 发送 Header
                // 小米验证通常先发一个带 size 的 XML
                string xml = $"<?xml version=\"1.0\" ?><data><sig TargetName=\"sig\" verbose=\"1\" size_in_bytes=\"{signatureData.Length}\" /></data>";
                if (!SendXmlCommand(xml)) return false;

                // 2. 发送 Raw Data
                _port.Write(signatureData, 0, signatureData.Length);
                
                // 3. 等待确认
                return WaitForAck();
            } 
            catch 
            { 
                return false; 
            }
        }

        // --- 3. 核心读写功能 (异步 + 流式) ---

        private async Task<bool> SendDataStreamUnifiedAsync(string path, bool isSparse, Action<long, long>? progress = null, CancellationToken ct = default, long offset = 0, long length = -1)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
                {
                    Stream inputStream = fs;
                    if (isSparse) inputStream = new SparseStream(fs);
                    else if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);

                    byte[] buffer = new byte[_maxPayloadSize]; 
                    long totalSent = 0;
                    long lastReported = 0;
                    long expectedTotal = length; 
                    if (isSparse && expectedTotal == -1) expectedTotal = inputStream.Length;

                    while (true)
                    {
                        if (ct.IsCancellationRequested) return false;

                        long remainingBytes = (expectedTotal >= 0) ? (expectedTotal - totalSent) : buffer.Length;
                        if (remainingBytes <= 0) break;

                        int bytesToRequest = (int)Math.Min(buffer.Length, remainingBytes);
                        int read = await inputStream.ReadAsync(buffer, 0, bytesToRequest, ct);

                        // 补零对齐
                        if (read < bytesToRequest)
                        {
                            int padding = bytesToRequest - read;
                            if (padding > 0)
                            {
                                Array.Clear(buffer, read, padding);
                                read += padding;
                                remainingBytes = 0; 
                            }
                        }

                        if (read == 0) break;

                        _port.Write(buffer, 0, read);
                        
                        totalSent += read;

                        if ((totalSent - lastReported) >= 5 * 1024 * 1024 || totalSent >= expectedTotal) 
                        {
                            progress?.Invoke(Math.Min(totalSent, expectedTotal), expectedTotal);
                            lastReported = totalSent;
                        }
                        
                        if (expectedTotal >= 0 && totalSent >= expectedTotal) break;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Error] 数据发送中断: {ex.Message}");
                return false;
            }
        }

        // 普通写入 (单次)
        public async Task<bool> FlashPartitionAsync(string filePath, string startSector, long numSectors, string lun = "0", Action<long, long>? progress = null, CancellationToken ct = default, string label = "image", string? overrideFilename = null, long fileOffsetBytes = 0)
        {
            if (!File.Exists(filePath)) return false;

            bool isSparse = false;
            long finalNumSectors = numSectors;
            long fileSize = new FileInfo(filePath).Length;

            if (fileOffsetBytes == 0 && !SparseImageHandler.IsSparseImage(filePath))
            {
                finalNumSectors = (fileSize + _sectorSize - 1) / _sectorSize;
            }
            else if (fileOffsetBytes == 0)
            {
                 try {
                    using (var fsTest = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (var brTest = new BinaryReader(fsTest)) {
                        if (fsTest.Length > 28 && brTest.ReadUInt32() == SPARSE_HEADER_MAGIC) {
                            isSparse = true;
                            long expandedSize = GetSparseExpandedSize(filePath);
                            finalNumSectors = expandedSize / _sectorSize;
                            if (expandedSize % _sectorSize != 0) finalNumSectors++;
                            // Sparse image detected; keep quiet to reduce log noise
                        }
                    }
                } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Sparse Check] {ex.Message}"); }
            }

            string filenameAttr = overrideFilename ?? Path.GetFileName(filePath);
            _log($"[Write] 写入 {filenameAttr} @ LUN{lun} Sec {startSector} (Sectors: {finalNumSectors})");
            string xml = $"<?xml version=\"1.0\" ?><data><program SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"{filenameAttr}\" label=\"{label}\" num_partition_sectors=\"{finalNumSectors}\" physical_partition_number=\"{lun}\" sparse=\"false\" start_sector=\"{startSector}\" /></data>";
            
            PurgeBuffer();
            await Task.Delay(50); 

            byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
            _port.Write(xmlBytes, 0, xmlBytes.Length);

            if (!WaitForAck()) 
            {
                _log("[Error] 写入握手失败");
                return false;
            }

            long totalBytesToSend = finalNumSectors * _sectorSize;
            bool sendResult = await SendDataStreamUnifiedAsync(filePath, isSparse, progress, ct, fileOffsetBytes, totalBytesToSend);
            if (!sendResult) return false;

            return WaitForAck();
        }

        // 3.2 分块写入 (针对 Super 等超大分区)
        public async Task<bool> FlashPartitionChunkedAsync(string filePath, long startSector, string lun, Action<long, long>? progress = null, CancellationToken ct = default, string label = "BackupGPT", string? overrideFilename = null)
        {
            if (!File.Exists(filePath)) return false;
            long totalFileSize = new FileInfo(filePath).Length;
            int chunkSizeInSectors = 16384; // 64MB
            long chunkSizeInBytes = chunkSizeInSectors * 4096;
            long currentFileOffset = 0;
            long currentTargetSector = startSector;

            string filenameAttr = overrideFilename ?? $"gpt_backup{lun}.bin";
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (currentFileOffset < totalFileSize)
                {
                    if (ct.IsCancellationRequested) return false;
                    long bytesRemaining = totalFileSize - currentFileOffset;
                    long bytesToWrite = Math.Min(bytesRemaining, chunkSizeInBytes);
                    long sectorsToWrite = (bytesToWrite + 4095) / 4096;

                    string xml = $"<?xml version=\"1.0\" ?><data><program SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" num_partition_sectors=\"{sectorsToWrite}\" start_sector=\"{currentTargetSector}\" physical_partition_number=\"{lun}\" label=\"{label}\" filename=\"{filenameAttr}\" sparse=\"false\" /></data>";

                    PurgeBuffer();
                    byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                    _port.Write(xmlBytes, 0, xmlBytes.Length);

                    if (!WaitForAck()) return false;
                    if (!await SendRawDataStreamChunkAsync(fs, currentFileOffset, bytesToWrite, progress, totalFileSize, ct)) return false;
                    if (!WaitForAck()) return false;

                    // [建议] 增加延时，让设备喘口气，减少 "Failed to read XML"
                    await Task.Delay(200); 

                    currentFileOffset += bytesToWrite;
                    currentTargetSector += sectorsToWrite;
                }
            }
            return true;
        }

        // 3.3 读取分区
        // 增加 overrideFilename 参数，支持文件名伪装
        // 增加 append 参数，支持追加模式
        public async Task<bool> ReadPartitionAsync(string savePath, string startSector, long numSectors, string lun = "0", Action<long, long>? progress = null, CancellationToken ct = default, string label = "dump", string? overrideFilename = null, bool append = false, bool suppressError = false)
        {
            long totalBytes = numSectors * _sectorSize;
            
            // 如果有伪装文件名则使用，否则使用保存路径的文件名
            string filenameAttr = overrideFilename ?? Path.GetFileName(savePath);
            _log($"[Read] 读取 {filenameAttr} @ LUN{lun} Sec {startSector} (Sectors: {numSectors})");

            string xml = $"<?xml version=\"1.0\" ?><data><read SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"{filenameAttr}\" label=\"{label}\" num_partition_sectors=\"{numSectors}\" physical_partition_number=\"{lun}\" sparse=\"false\" start_sector=\"{startSector}\" /></data>";
            
            PurgeBuffer();
            
            byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
            _port.Write(xmlBytes, 0, xmlBytes.Length);

            if (!await ReceiveRawDataToFileAsync(savePath, totalBytes, progress, ct, label, append, suppressError)) return false;
            
            return WaitForAck();
        }

        // 3.4 分块读取 (针对某些限制单次读取大小的设备)
        // 增加 append 参数，支持追加模式
        public async Task<bool> ReadPartitionChunkedAsync(string savePath, string startSector, long numSectors, string lun = "0", Action<long, long>? progress = null, CancellationToken ct = default, string label = "dump", string? forceFilename = null, bool append = false, bool suppressError = false)
        {
            long totalBytes = numSectors * _sectorSize;
            // 尝试使用更小的分块: 32MB (32 * 1024 * 1024 / 4096 = 8192 sectors)
            // 某些设备对 128MB 仍然敏感
            long sectorsPerChunk = 8192; 
            long currentSector = long.Parse(startSector);
            long remainingSectors = numSectors;
            long totalReadBytes = 0;

            // 创建空文件 (如果不是追加模式)
            if (!append)
            {
                using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None)) { }
            }

            while (remainingSectors > 0)
            {
                if (ct.IsCancellationRequested) return false;

                long chunkSectors = Math.Min(remainingSectors, sectorsPerChunk);
                long chunkBytes = chunkSectors * _sectorSize;
                
                // [关键修改] 使用传入的强制文件名，或者默认伪装
                string filenameAttr = forceFilename ?? "gpt_backup0.bin"; 
                
                string xml = $"<?xml version=\"1.0\" ?><data><read SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"{filenameAttr}\" label=\"{label}\" num_partition_sectors=\"{chunkSectors}\" physical_partition_number=\"{lun}\" sparse=\"false\" start_sector=\"{currentSector}\" /></data>";
                
                PurgeBuffer();
                // _log($"[Read] Reading Chunk @ {currentSector} ({chunkSectors} sectors)...");
                
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                // 接收并追加到文件 (始终为 true，因为分块本身就是追加)
                if (!await ReceiveRawDataToFileAsync(savePath, chunkBytes, (c, t) => {
                    progress?.Invoke(totalReadBytes + c, totalBytes);
                }, ct, label, true, suppressError)) 
                {
                    if (!suppressError) _log($"[Error] 分块读取失败 @ Sector {currentSector}");
                    return false;
                }

                if (!WaitForAck()) return false;

                remainingSectors -= chunkSectors;
                currentSector += chunkSectors;
                totalReadBytes += chunkBytes;
            }
            return true;
        }

        // --- 4. 核心传输逻辑 ---

        // [核心] 零临时文件 Sparse 流式发送
        private async Task<bool> SendSparseDataStreamAsync(string path, Action<long, long>? progress = null, CancellationToken ct = default)
        {
            try
            {
                // 移除 BinaryReader，只用 FileStream
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
                {
                    byte[] headerBuf = new byte[32]; // 用于读取头部的临时 buffer
                    
                    // 1. 读取文件头 (28 bytes)
                    if (await fs.ReadAsync(headerBuf, 0, 28, ct) != 28) throw new Exception("File too short");
                    
                    uint magic = BitConverter.ToUInt32(headerBuf, 0);
                    if (magic != SPARSE_HEADER_MAGIC) throw new Exception("Not a sparse image");

                    // 解析头部 (使用 BitConverter 代替 BinaryReader)
                    ushort fileHdrSz = BitConverter.ToUInt16(headerBuf, 8);
                    ushort chunkHdrSz = BitConverter.ToUInt16(headerBuf, 10);
                    uint blkSz = BitConverter.ToUInt32(headerBuf, 12);
                    uint totalBlks = BitConverter.ToUInt32(headerBuf, 16);
                    uint totalChunks = BitConverter.ToUInt32(headerBuf, 20);

                    // 跳转到第一个 Chunk
                    fs.Seek(fileHdrSz, SeekOrigin.Begin);

                    long totalExpandedBytes = (long)totalBlks * blkSz;
                    long currentExpandedSent = 0;
                    long lastReported = 0;
                    
                    byte[] transferBuffer = new byte[1024 * 1024]; // 1MB

                    // 2. 遍历 Chunks
                    for (int i = 0; i < totalChunks; i++)
                    {
                        if (ct.IsCancellationRequested) throw new OperationCanceledException();

                        // 读取 Chunk Header (12 bytes)
                        if (await fs.ReadAsync(headerBuf, 0, 12, ct) != 12) break;
                        
                        ushort chunkType = BitConverter.ToUInt16(headerBuf, 0);
                        // ushort reserved1 = BitConverter.ToUInt16(headerBuf, 2);
                        uint chunkSz = BitConverter.ToUInt32(headerBuf, 4);
                        uint totalSz = BitConverter.ToUInt32(headerBuf, 8);

                        long chunkSizeBytes = (long)chunkSz * blkSz;

                        switch (chunkType)
                        {
                            case CHUNK_TYPE_RAW:
                                long bytesToRead = totalSz - chunkHdrSz;
                                long bytesSentForChunk = 0;
                                while (bytesSentForChunk < bytesToRead)
                                {
                                    int toRead = (int)Math.Min(transferBuffer.Length, bytesToRead - bytesSentForChunk);
                                    // 此时 fs.ReadAsync 是安全的，因为没有 BinaryReader 的干扰
                                    int read = await fs.ReadAsync(transferBuffer, 0, toRead, ct);
                                    if (read == 0) break;

                                    _port.Write(transferBuffer, 0, read);
                                    bytesSentForChunk += read;
                                    currentExpandedSent += read;
                                    
                                    // 进度节流
                                    if (currentExpandedSent - lastReported >= 5 * 1024 * 1024) {
                                        progress?.Invoke(currentExpandedSent, totalExpandedBytes);
                                        lastReported = currentExpandedSent;
                                    }
                                }
                                break;

                            case CHUNK_TYPE_FILL:
                                if (await fs.ReadAsync(headerBuf, 0, 4, ct) != 4) break;
                                uint fillVal = BitConverter.ToUInt32(headerBuf, 0);
                                byte[] fillBytes = BitConverter.GetBytes(fillVal);
                                
                                // 填充 buffer
                                for (int k = 0; k < transferBuffer.Length; k += 4)
                                    Array.Copy(fillBytes, 0, transferBuffer, k, 4);

                                long remainingFill = chunkSizeBytes;
                                while (remainingFill > 0)
                                {
                                    int toWrite = (int)Math.Min(remainingFill, transferBuffer.Length);
                                    _port.Write(transferBuffer, 0, toWrite);
                                    remainingFill -= toWrite;
                                    currentExpandedSent += toWrite;
                                    if (currentExpandedSent - lastReported >= 5 * 1024 * 1024) {
                                        progress?.Invoke(currentExpandedSent, totalExpandedBytes);
                                        lastReported = currentExpandedSent;
                                    }
                                }
                                break;

                            case CHUNK_TYPE_DONT_CARE:
                                Array.Clear(transferBuffer, 0, transferBuffer.Length);
                                long remainingSkip = chunkSizeBytes;
                                while (remainingSkip > 0)
                                {
                                    int toWrite = (int)Math.Min(remainingSkip, transferBuffer.Length);
                                    _port.Write(transferBuffer, 0, toWrite);
                                    remainingSkip -= toWrite;
                                    currentExpandedSent += toWrite;
                                    if (currentExpandedSent - lastReported >= 5 * 1024 * 1024) {
                                        progress?.Invoke(currentExpandedSent, totalExpandedBytes);
                                        lastReported = currentExpandedSent;
                                    }
                                }
                                break;

                            case CHUNK_TYPE_CRC32:
                                fs.Seek(4, SeekOrigin.Current); // 跳过 CRC
                                break;
                        }
                    }
                    
                    // 对齐
                    if (currentExpandedSent % _sectorSize != 0)
                    {
                        int pad = _sectorSize - (int)(currentExpandedSent % _sectorSize);
                        _port.Write(new byte[pad], 0, pad);
                    }
                }
                return true;
            }
            catch (Exception ex) { _log($"[Error] Sparse 流式发送失败: {ex.Message}"); return false; }
        }

        private async Task<bool> SendRawDataStreamAsync(string path, Action<long, long>? progress = null, CancellationToken ct = default, long offset = 0, long length = -1)
        {
            try {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) {
                    if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);
                    
                    long bytesRemaining = (length >= 0) ? length : (fs.Length - offset);
                    long totalBytesToSend = bytesRemaining;
                    
                    byte[] buffer = new byte[1024 * 1024]; // 回归 1MB 高效 Buffer
                    int read;
                    long totalSent = 0;
                    long lastReported = 0;

                    while (bytesRemaining > 0) {
                        if (ct.IsCancellationRequested) return false;
                        
                        int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                        read = await fs.ReadAsync(buffer, 0, bytesToRead, ct);
                        if (read == 0) break;

                        // 1. 补零对齐 (合并发送，确保是对齐的)
                        bool isLastChunk = (read < buffer.Length) || (read == bytesRemaining);
                        if (isLastChunk && (totalSent + read) % _sectorSize != 0)
                        {
                            int padSize = _sectorSize - (int)((totalSent + read) % _sectorSize);
                            if (read + padSize <= buffer.Length)
                            {
                                Array.Clear(buffer, read, padSize);
                                read += padSize; // 延伸长度
                            }
                            else
                            {
                                // 分开发送 (极罕见)
                                _port.Write(buffer, 0, read);
                                _port.Write(new byte[padSize], 0, padSize); 
                                totalSent += read;
                                bytesRemaining -= read;
                                continue; 
                            }
                        }

                        // 2. 正常发送 (整包)
                        _port.Write(buffer, 0, read);
                        
                        // 3. [新尝试] 手动触发 ZLP
                        // 如果本次发送长度是 4096 的倍数，且是最后一块，设备可能会等 ZLP
                        // 我们尝试发一个空包
                        // NOTE: SerialPort.Write(byte[0],0,0) 不会真正发送 USB ZLP，保留注释避免误导

                        totalSent += read;
                        bytesRemaining -= read;

                        if ((totalSent - lastReported) >= 1024 * 1024 || bytesRemaining <= 0) {
                            progress?.Invoke(Math.Min(totalSent, totalBytesToSend), totalBytesToSend);
                            lastReported = totalSent;
                        }
                    }
                }
                return true;
            } 
            catch (Exception ex) { _log($"[Error] 发送数据失败: {ex.Message}"); return false; }
        }

        // 分块发送辅助
        private async Task<bool> SendRawDataStreamChunkAsync(FileStream fs, long offset, long length, Action<long, long>? progress, long totalSize, CancellationToken ct)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            byte[] buffer = new byte[1024 * 1024];
            long sent = 0;
            while (sent < length)
            {
                int toRead = (int)Math.Min(buffer.Length, length - sent);
                int read = await fs.ReadAsync(buffer, 0, toRead, ct);
                if (read == 0) break;
                _port.Write(buffer, 0, read);
                sent += read;
                if (progress != null) progress(offset + sent, totalSize);
            }
            if (sent % 4096 != 0) {
                int pad = 4096 - (int)(sent % 4096);
                _port.Write(new byte[pad], 0, pad);
            }
            return true;
        }

        // 接收数据 (支持粘包处理)
        private async Task<bool> ReceiveRawDataToFileAsync(string path, long totalBytes, Action<long, long>? progress = null, CancellationToken ct = default, string label = "dump", bool append = false, bool suppressError = false)
        {
            try
            {
                FileMode mode = append ? FileMode.Append : FileMode.Create;
                using (FileStream fs = new FileStream(path, mode, FileAccess.Write, FileShare.None, 4096, true))
                {
                    byte[] buffer = new byte[1024 * 1024]; 
                    long received = 0;
                    long lastReported = 0;
                    bool headerFound = false;
                    MemoryStream? headerBuffer = new MemoryStream();

                    while (received < totalBytes)
                    {
                        if (ct.IsCancellationRequested) return false;

                        int requestSize = headerFound ? (int)Math.Min(buffer.Length, totalBytes - received) : 4096;
                        int read = _port.Read(buffer, 0, requestSize);
                        if (read == 0) throw new TimeoutException("Read Timeout");

                        if (!headerFound)
                        {
                            // 累积数据，确保跨包也能找到 rawmode 标记
                            headerBuffer!.Write(buffer, 0, read);
                            string currentContent = Encoding.UTF8.GetString(headerBuffer.ToArray());

                            int ackIndex = currentContent.IndexOf("rawmode=\"true\"", StringComparison.OrdinalIgnoreCase);
                            if (ackIndex == -1) ackIndex = currentContent.IndexOf("rawmode='true'", StringComparison.OrdinalIgnoreCase);

                            if (ackIndex >= 0)
                            {
                                int xmlEndIndex = currentContent.IndexOf("</data>", ackIndex, StringComparison.OrdinalIgnoreCase);
                                if (xmlEndIndex >= 0)
                                {
                                    int dataStartOffset = xmlEndIndex + 7;
                                    byte[] hdrBytes = headerBuffer.ToArray();
                                    int remaining = hdrBytes.Length - dataStartOffset;
                                    if (remaining > 0)
                                    {
                                        await fs.WriteAsync(hdrBytes, dataStartOffset, remaining, ct);
                                        received += remaining;
                                        if ((received - lastReported) >= 5 * 1024 * 1024 || received == totalBytes)
                                        {
                                            progress?.Invoke(received, totalBytes);
                                            lastReported = received;
                                        }
                                    }

                                    headerFound = true;
                                    headerBuffer.Dispose();
                                    headerBuffer = null;
                                    continue;
                                }
                            }

                            // 检查 NAK 或错误
                            if (currentContent.IndexOf("value=\"NAK\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                currentContent.IndexOf("Failed to run the last command", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (!suppressError) _log($"[Error] 导出被拒绝或出错: {currentContent}");
                                return false;
                            }

                            // 防止无界增长
                            if (headerBuffer.Length > 64 * 1024)
                            {
                                throw new Exception("Invalid or missing Firehose header");
                            }

                            continue; // 继续读取直到找到头
                        }

                        // 已找到头，直接写入数据
                        await fs.WriteAsync(buffer, 0, read, ct);
                        received += read;

                        if ((received - lastReported) >= 5 * 1024 * 1024 || received == totalBytes)
                        {
                            progress?.Invoke(received, totalBytes);
                            lastReported = received;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex) { if (!suppressError) _log($"[Error] File Write: {ex.Message}"); return false; }
        }

        // 辅助: 获取 Sparse 大小
        private long GetSparseExpandedSize(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 28) return 0;
                fs.Seek(16, SeekOrigin.Begin);
                uint blkSz = br.ReadUInt32();
                uint totalBlks = br.ReadUInt32();
                return (long)totalBlks * blkSz;
            }
        }

        private void PurgeBuffer()
        {
            try
            {
                // 1. 先调用系统 API
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();

                // 同步清空缓存的 rx 累积
                _rxBuffer.Clear();

                // 2. 手动吸干剩余数据：短超时循环读取直到超时
                int oldTimeout = _port.ReadTimeout;
                _port.ReadTimeout = 10;
                byte[] trash = new byte[4096];
                try
                {
                    while (true)
                    {
                        try
                        {
                            if (_port.Read(trash, 0, trash.Length) <= 0) break;
                        }
                        catch (TimeoutException)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PurgeBuffer Inner] {ex.Message}"); }
                finally
                {
                    _port.ReadTimeout = oldTimeout;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PurgeBuffer] {ex.Message}"); }
        }

        // XML 响应处理
        private XElement? ProcessXmlResponse(List<string>? captureLogs = null)
        {
            byte[] buf = new byte[4096];
            XElement? foundResponse = null;
            DateTime start = DateTime.Now;
            int oldTimeout = _port.ReadTimeout;
            _port.ReadTimeout = 50; // 短阻塞等待，避免依赖 BytesToRead 轮询

            try
            {
                while ((DateTime.Now - start).TotalMilliseconds < 5000)
                {
                    // 1) 先尝试从缓存中提取完整包
                    if (TryExtractPacket(out string packetStr))
                    {
                        foundResponse = ParsePacket(packetStr, captureLogs) ?? foundResponse;
                        if (foundResponse != null) return foundResponse;
                        continue;
                    }

                    // 2) 阻塞读取一小段数据，超时则继续循环
                    try
                    {
                        int read = _port.Read(buf, 0, buf.Length);
                        if (read > 0)
                        {
                            _rxBuffer.Append(Encoding.UTF8.GetString(buf, 0, read));
                        }
                    }
                    catch (TimeoutException)
                    {
                        // 无数据，继续等待直到超时窗口结束
                    }
                }
            }
            finally
            {
                _port.ReadTimeout = oldTimeout;
            }

            // 超时前再尝试一次提取
            if (TryExtractPacket(out string finalPacket))
            {
                foundResponse = ParsePacket(finalPacket, captureLogs) ?? foundResponse;
            }
            return foundResponse;
        }

        private bool TryExtractPacket(out string packet)
        {
            packet = string.Empty;
            int endIdx = _rxBuffer.ToString().IndexOf("</data>", StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0) return false;

            int packetLen = endIdx + "</data>".Length;
            string raw = _rxBuffer.ToString(0, packetLen);
            _rxBuffer.Remove(0, packetLen);
            packet = raw;
            return true;
        }

        private XElement? ParsePacket(string packet, List<string>? captureLogs)
        {
            string xmlStr = packet.Trim();
            if (!xmlStr.EndsWith("</data>", StringComparison.OrdinalIgnoreCase)) xmlStr += "</data>";
            int dataIndex = xmlStr.IndexOf("<data", StringComparison.OrdinalIgnoreCase);
            if (dataIndex >= 0) xmlStr = xmlStr.Substring(dataIndex); else return null;

            try
            {
                XDocument doc = XDocument.Parse(xmlStr);
                foreach (var log in doc.Descendants("log"))
                {
                    string msg = log.Attribute("value")?.Value ?? "";
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        _log($"[Device] {msg}");
                        captureLogs?.Add(msg);
                    }
                }
                return doc.Descendants("response").FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
        
        // 发送 XML 并等待
        public bool SendXmlCommand(string xml, bool ignoreResponse = false)
        {
            byte[] data = Encoding.UTF8.GetBytes(xml);
            _port.Write(data, 0, data.Length);
            if (ignoreResponse)
            {
                // 快速读取一下，避免缓冲区堆积，但不等待 ACK
                var resp = ProcessXmlResponse(); 
                if (resp != null)
                {
                     string val = resp.Attribute("value")?.Value ?? "";
                     if (val.Contains("NAK") || val.Contains("false"))
                     {
                         _log($"[Warn] Ignored response: {val}");
                     }
                }
                return true;
            }
            return WaitForAck();
        }

        private bool WaitForAck()
        {
            int maxRetries = 50; 
            while (maxRetries-- > 0)
            {
                XElement? resp = ProcessXmlResponse();
                if (resp != null)
                {
                    string val = resp.Attribute("value")?.Value ?? "";
                    if (val.Equals("ACK", StringComparison.OrdinalIgnoreCase) || 
                        val.Equals("true", StringComparison.OrdinalIgnoreCase)) 
                    {
                        return true;
                    }
                    if (val.Equals("NAK", StringComparison.OrdinalIgnoreCase))
                    {
                        string rawMode = resp.Attribute("rawmode")?.Value ?? "false";
                        if (rawMode.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                        _log($"[Error] Device returned NAK: {resp}");
                        return false;
                    }
                }
            }
            _log("[Error] 等待 ACK 超时");
            return false;
        }
        
        private bool SendRawFile(string path, bool ignoreResponse = false, Action<long, long>? progressCallback = null)
        {
            try {
                byte[] data = File.ReadAllBytes(path);
                _port.Write(data, 0, data.Length);
                progressCallback?.Invoke(data.Length, data.Length);
                if (ignoreResponse)
                {
                    ProcessXmlResponse();
                    return true;
                }
                return WaitForAck();
            } catch { return false; }
        }

        public bool Power(string mode)
        {
            _log($"[Power] {mode}...");
            return SendXmlCommand($"<?xml version=\"1.0\" ?><data><power value=\"{mode}\" /></data>");
        }

        public bool Ping()
        {
            return SendXmlCommand("<?xml version=\"1.0\" ?><data><nop /></data>");
        }

        // [新增] 读取 Backup GPT (使用 NUM_DISK_SECTORS 语法)
        public async Task<byte[]?> ReadBackupGptAsync(string lun = "0", int numSectors = 6, CancellationToken ct = default)
        {
            PurgeBuffer();
            // 注意：NUM_DISK_SECTORS-X. 最后的点是必须的，表示从末尾倒数
            string start = $"NUM_DISK_SECTORS-{numSectors}.";
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            // 使用 ssd 伪装，因为 BackupGPT 通常没有特定白名单
            string filename = "ssd"; 
            string label = "ssd";

            string xml = $"<?xml version=\"1.0\" ?><data><read SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"{filename}\" label=\"{label}\" num_partition_sectors=\"{numSectors}\" physical_partition_number=\"{lun}\" sparse=\"false\" start_sector=\"{start}\" /></data>";
            
            _log($"[Read] 读取 Backup GPT LUN{lun} (Start: {start})...");
            byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
            _port.Write(xmlBytes, 0, xmlBytes.Length);
            
            byte[] buffer = new byte[numSectors * _sectorSize];
            if (!await ReceiveRawDataToMemoryAsync(buffer, ct)) return null;
            if (!WaitForAck()) return null;
            return buffer;
        }
        
        // 3.1 [修复版] 异步读取 GPT
        // [重构] 移除内置的重试策略，改为纯净的单一读取方法
        // 由上层策略决定传什么 label 和 filename
        public async Task<byte[]?> ReadGptPacketAsync(
            string lun, 
            long startSector, 
            int numSectors, 
            string label, 
            string filename, 
            CancellationToken ct = default)
        {
            try
            {
                // 构造指令
                string xml = $"<?xml version=\"1.0\" ?><data>\n" +
                             $"<read SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"{filename}\" label=\"{label}\" " +
                             $"num_partition_sectors=\"{numSectors}\" physical_partition_number=\"{lun}\" " +
                             $"sparse=\"false\" start_sector=\"{startSector}\" />\n" +
                             "</data>\n";

                PurgeBuffer();

                _log($"[Read] 读取 GPT LUN{lun} ({label})...");
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                byte[] buffer = new byte[numSectors * _sectorSize];

                // 接收数据
                if (await ReceiveRawDataToMemoryAsync(buffer, ct))
                {
                    // 等待 ACK
                    if (WaitForAck())
                    {
                        return buffer;
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"[Error] GPT 读取异常: {ex.Message}");
            }

            return null;
        }



        // 3.2 设置启动分区 (SetBootableStorageDrive)
        public bool SetBootableStorageDrive(int value)
        {
            _log($"[Config] Setting Bootable Storage Drive to {value}...");
            string xml = $"<?xml version=\"1.0\" ?><data><setbootablestoragedrive value=\"{value}\" /></data>";
            return SendXmlCommand(xml);
        }
        




        private async Task<bool> ReceiveRawDataToMemoryAsync(byte[] buffer, CancellationToken ct)
        {
            return await Task.Run(() => 
            {
                try
                {
                    int totalBytes = buffer.Length;
                    int received = 0;
                    bool headerFound = false;
                    byte[] tempBuf = new byte[65536]; 

                    while (received < totalBytes)
                    {
                        if (ct.IsCancellationRequested) return false;

                        // 未找到头时，读小块
                        int requestSize = headerFound ? (int)Math.Min(tempBuf.Length, totalBytes - received) : 4096;
                        
                        int read = _port.Read(tempBuf, 0, requestSize);
                        if (read == 0) throw new TimeoutException("Read Timeout");

                        int dataStartOffset = 0;

                        if (!headerFound)
                        {
                            string content = Encoding.UTF8.GetString(tempBuf, 0, read);
                            
                            // [修复] 无论开头是什么，都搜索 rawmode 标记
                            int ackIndex = content.IndexOf("rawmode=\"true\"", StringComparison.OrdinalIgnoreCase);
                            if (ackIndex == -1) ackIndex = content.IndexOf("rawmode='true'", StringComparison.OrdinalIgnoreCase);

                            if (ackIndex >= 0)
                            {
                                int xmlEndIndex = content.IndexOf("</data>", ackIndex);
                                if (xmlEndIndex >= 0)
                                {
                                    headerFound = true;
                                    dataStartOffset = xmlEndIndex + 7; // + len(</data>)
                                    
                                    // 如果本包全是 XML，没数据，继续
                                    if (dataStartOffset >= read) continue;
                                }
                            }
                            else if (content.Contains("value=\"NAK\""))
                            {
                                _log($"[Error] 读取被拒绝: {content}");
                                return false;
                            }
                            else
                            {
                                // [关键] 既没找到头，也不是 NAK，说明全是垃圾数据 (Logs/ZLP)
                                // 直接丢弃，继续读下一包！绝对不要 headerFound=true
                                // _log("[Debug] 丢弃残留数据..."); 
                                continue; 
                            }
                        }

                        int dataLength = read - dataStartOffset;
                        if (dataLength > 0)
                        {
                            if (received + dataLength > buffer.Length) dataLength = buffer.Length - received;
                            Array.Copy(tempBuf, dataStartOffset, buffer, received, dataLength);
                            received += dataLength;
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _log($"[Error] 接收数据异常: {ex.Message}");
                    return false;
                }
            }, ct);
        }


        
        public bool SetActiveSlot(int slot)
        {
            return SendXmlCommand($"<?xml version=\"1.0\" ?><data><setactivepartition value=\"{slot}\" /></data>");
        }

        public bool Reset(string mode = "reset")
        {
            // modes: reset, off, reset_to_edl, reset_to_fastboot, reset_to_recovery
            return SendXmlCommand($"<?xml version=\"1.0\" ?><data><power value=\"{mode}\" /></data>");
        }

        // --- 3.4 擦除分区
        public bool ErasePartition(string startSector, long numSectors, string lun = "0")
        {
            string xml = $"<?xml version=\"1.0\" ?><data><erase SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" num_partition_sectors=\"{numSectors}\" physical_partition_number=\"{lun}\" start_sector=\"{startSector}\" /></data>";
            _log($"[Erase] 擦除 @ LUN{lun} Sec {startSector} (Sectors: {numSectors})");
            return SendXmlCommand(xml);
        }

        // [修复] 构造 <patch> 指令
        // 修改点：sector 参数类型由 long 改为 string，以支持 "NUM_DISK_SECTORS-1."
        public bool ApplyPatch(string sector, long byteOffset, string value, int sizeInBytes, string lun = "0")
        {
            string xml = $"<?xml version=\"1.0\" ?><data><patch SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" " +
                         $"byte_offset=\"{byteOffset}\" physical_partition_number=\"{lun}\" size_in_bytes=\"{sizeInBytes}\" " +
                         $"start_sector=\"{sector}\" value=\"{value}\" filename=\"DISK\" /></data>";
            
            _log($"[Patch] LUN{lun} Sec:{sector} Off:{byteOffset} Val:{value}");
            return SendXmlCommand(xml);
        }

        // [修复] 解析并应用 Patch XML 内容
        public bool ApplyPatch(string xmlContent)
        {
            try
            {
                // 简单的 XML 清洗/包裹逻辑
                string xmlToParse = xmlContent.Trim();

                // 移除 XML 声明 (如果存在)，防止被包裹在根节点内部导致解析错误
                if (xmlToParse.StartsWith("<?xml"))
                {
                    int endDecl = xmlToParse.IndexOf("?>");
                    if (endDecl > 0)
                    {
                        xmlToParse = xmlToParse.Substring(endDecl + 2).Trim();
                    }
                }

                if (!xmlToParse.StartsWith("<root>") && !xmlToParse.StartsWith("<data>") && !xmlToParse.StartsWith("<patches>"))
                {
                    // 如果只有 <patch> 标签堆在一起，包裹一个根节点
                    xmlToParse = $"<patches>{xmlToParse}</patches>";
                }

                XDocument doc = XDocument.Parse(xmlToParse);
                bool allSuccess = true;
                
                foreach (var patch in doc.Descendants("patch"))
                {
                    // 【关键修改】直接读取字符串，绝对不要 Parse 为 long (防止公式解析失败)
                    string sector = patch.Attribute("start_sector")?.Value ?? "0";
                    
                    long byteOffset = long.Parse(patch.Attribute("byte_offset")?.Value ?? "0");
                    // 【关键修改】Value 保持字符串，绝对不要转换
                    string value = patch.Attribute("value")?.Value ?? "";
                    int size = int.Parse(patch.Attribute("size_in_bytes")?.Value ?? "4");
                    string lun = patch.Attribute("physical_partition_number")?.Value ?? "0";

                    if (!ApplyPatch(sector, byteOffset, value, size, lun))
                    {
                        allSuccess = false;
                        _log($"[Error] Patch failed at sector {sector}");
                    }
                }
                return allSuccess;
            }
            catch (Exception ex)
            {
                _log($"[Error] Failed to parse patch XML: {ex.Message}");
                return false;
            }
        }

        // ==========================================
        // 高级功能：GPT 备份/恢复、内存读写、设备信息
        // ==========================================

        /// <summary>
        /// 备份 GPT 分区表
        /// </summary>
        public async Task<bool> BackupGptAsync(string savePath, int lun = 0, CancellationToken ct = default)
        {
            try
            {
                _log($"[GPT] 备份 LUN{lun} 分区表...");
                int sectorsToRead = (_sectorSize == 4096) ? 6 : 34; // UFS=6, eMMC=34
                
                string xml = $"<?xml version=\"1.0\" ?><data><read SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"gpt_backup{lun}.bin\" label=\"PrimaryGPT\" num_partition_sectors=\"{sectorsToRead}\" physical_partition_number=\"{lun}\" sparse=\"false\" start_sector=\"0\" /></data>";
                
                PurgeBuffer();
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                long totalBytes = sectorsToRead * _sectorSize;
                if (!await ReceiveRawDataToFileAsync(savePath, totalBytes, null, ct, "GPT", false, false))
                {
                    _log("[GPT] 备份失败");
                    return false;
                }

                if (!WaitForAck())
                {
                    _log("[GPT] 备份确认失败");
                    return false;
                }

                _log($"[GPT] 备份成功: {savePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[GPT] 备份异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复 GPT 分区表
        /// </summary>
        public async Task<bool> RestoreGptAsync(string gptPath, int lun = 0, CancellationToken ct = default)
        {
            try
            {
                if (!File.Exists(gptPath))
                {
                    _log($"[GPT] 文件不存在: {gptPath}");
                    return false;
                }

                _log($"[GPT] 恢复 LUN{lun} 分区表...");
                long fileSize = new FileInfo(gptPath).Length;
                long sectorsToWrite = fileSize / _sectorSize;

                string xml = $"<?xml version=\"1.0\" ?><data><program SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" filename=\"gpt_restore.bin\" label=\"PrimaryGPT\" num_partition_sectors=\"{sectorsToWrite}\" physical_partition_number=\"{lun}\" sparse=\"false\" start_sector=\"0\" /></data>";

                PurgeBuffer();
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                if (!WaitForAck())
                {
                    _log("[GPT] 恢复握手失败");
                    return false;
                }

                if (!await SendRawDataStreamAsync(gptPath, null, ct))
                {
                    _log("[GPT] 恢复数据发送失败");
                    return false;
                }

                if (!WaitForAck())
                {
                    _log("[GPT] 恢复确认失败");
                    return false;
                }

                _log("[GPT] 恢复成功");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[GPT] 恢复异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 内存读取 (Peek) - 读取指定地址的内存
        /// </summary>
        public byte[]? PeekMemory(ulong address, int size)
        {
            try
            {
                _log($"[Peek] 读取内存 @ 0x{address:X} ({size} bytes)");
                
                string xml = $"<?xml version=\"1.0\" ?><data><peek address64=\"{address}\" size_in_bytes=\"{size}\" /></data>";
                
                PurgeBuffer();
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                // 读取响应数据
                byte[] result = new byte[size];
                int totalRead = 0;
                int timeout = 5000;
                int oldTimeout = _port.ReadTimeout;
                _port.ReadTimeout = timeout;

                try
                {
                    while (totalRead < size)
                    {
                        int bytesRead = _port.Read(result, totalRead, size - totalRead);
                        if (bytesRead <= 0) break;
                        totalRead += bytesRead;
                    }
                }
                finally
                {
                    _port.ReadTimeout = oldTimeout;
                }

                if (totalRead < size)
                {
                    _log($"[Peek] 只读取了 {totalRead}/{size} 字节");
                    return null;
                }

                if (!WaitForAck())
                {
                    _log("[Peek] 确认失败");
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                _log($"[Peek] 读取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 内存写入 (Poke) - 写入数据到指定地址
        /// </summary>
        public bool PokeMemory(ulong address, byte[] data)
        {
            try
            {
                _log($"[Poke] 写入内存 @ 0x{address:X} ({data.Length} bytes)");
                
                string xml = $"<?xml version=\"1.0\" ?><data><poke address64=\"{address}\" size_in_bytes=\"{data.Length}\" value=\"{BitConverter.ToString(data).Replace("-", "")}\" /></data>";
                
                return SendXmlCommand(xml, true);
            }
            catch (Exception ex)
            {
                _log($"[Poke] 写入失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 内存转储 (Memory Dump) - 转储内存范围到文件
        /// </summary>
        public async Task<bool> DumpMemoryAsync(string savePath, ulong startAddress, ulong size, Action<long, long>? progress = null, CancellationToken ct = default)
        {
            try
            {
                _log($"[Dump] 转储内存 0x{startAddress:X} - 0x{startAddress + size:X}");
                
                const int chunkSize = 1024 * 1024; // 1MB 分块
                ulong remaining = size;
                ulong currentAddr = startAddress;
                long totalWritten = 0;

                using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    while (remaining > 0 && !ct.IsCancellationRequested)
                    {
                        int toRead = (int)Math.Min(remaining, chunkSize);
                        byte[]? chunk = PeekMemory(currentAddr, toRead);
                        
                        if (chunk == null)
                        {
                            _log($"[Dump] 读取失败 @ 0x{currentAddr:X}");
                            return false;
                        }

                        await fs.WriteAsync(chunk, 0, chunk.Length, ct);
                        totalWritten += chunk.Length;
                        remaining -= (ulong)chunk.Length;
                        currentAddr += (ulong)chunk.Length;

                        progress?.Invoke(totalWritten, (long)size);
                    }
                }

                _log($"[Dump] 完成，保存到: {savePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Dump] 转储失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取设备信息 (Device Info)
        /// </summary>
        public Dictionary<string, string> GetDeviceInfo()
        {
            var info = new Dictionary<string, string>();
            
            try
            {
                string xml = "<?xml version=\"1.0\" ?><data><getdevinfo /></data>";
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                
                PurgeBuffer();
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                List<string> logs = new List<string>();
                int maxRetries = 50;
                
                while (maxRetries-- > 0)
                {
                    XElement? resp = ProcessXmlResponse(logs);
                    if (resp != null)
                    {
                        // 解析属性
                        foreach (var attr in resp.Attributes())
                        {
                            if (attr.Name != "value")
                            {
                                info[attr.Name.LocalName] = attr.Value;
                            }
                        }
                        
                        string val = resp.Attribute("value")?.Value ?? "";
                        if (val.Equals("ACK", StringComparison.OrdinalIgnoreCase) || 
                            val.Equals("NAK", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }

                // 解析 log 中的信息
                foreach (var log in logs)
                {
                    if (log.Contains(":"))
                    {
                        var parts = log.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            info[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"[DevInfo] 获取失败: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// 获取设备树 (Device Tree / FDT)
        /// </summary>
        public async Task<bool> ExtractDeviceTreeAsync(string savePath, CancellationToken ct = default)
        {
            try
            {
                _log("[FDT] 提取设备树...");
                
                // 设备树通常在 dtbo/dtb 分区
                // 首先尝试读取 dtbo 分区
                string xml = "<?xml version=\"1.0\" ?><data><getstorageinfo physical_partition_number=\"0\" /></data>";
                
                // 这是简化实现，实际需要先读取 GPT 找到 dtbo 分区位置
                _log("[FDT] 注意: 完整实现需要先解析 GPT 定位 dtbo 分区");
                _log("[FDT] 当前为简化实现，请使用分区读取功能手动读取 dtbo/dtb");
                
                return false; // 返回 false 表示需要手动操作
            }
            catch (Exception ex)
            {
                _log($"[FDT] 提取失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Firehose 电源控制
        /// </summary>
        public bool PowerCommand(string mode)
        {
            // mode: reset, reset_to_edl, reset_to_recovery, reset_to_fastboot, shutdown
            string xml = $"<?xml version=\"1.0\" ?><data><power value=\"{mode}\" /></data>";
            _log($"[Power] 执行: {mode}");
            return SendXmlCommand(xml, true);
        }

        /// <summary>
        /// 获取 SHA256 哈希
        /// </summary>
        public string? GetSha256(int lun, long startSector, long numSectors)
        {
            try
            {
                string xml = $"<?xml version=\"1.0\" ?><data><getsha256digest SECTOR_SIZE_IN_BYTES=\"{_sectorSize}\" num_partition_sectors=\"{numSectors}\" physical_partition_number=\"{lun}\" start_sector=\"{startSector}\" /></data>";
                
                byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
                PurgeBuffer();
                _port.Write(xmlBytes, 0, xmlBytes.Length);

                int maxRetries = 50;
                while (maxRetries-- > 0)
                {
                    XElement? resp = ProcessXmlResponse();
                    if (resp != null)
                    {
                        var digestAttr = resp.Attribute("Digest");
                        if (digestAttr != null)
                        {
                            return digestAttr.Value;
                        }

                        string val = resp.Attribute("value")?.Value ?? "";
                        if (val.Equals("NAK", StringComparison.OrdinalIgnoreCase))
                        {
                            _log("[SHA256] 设备不支持");
                            return null;
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _log($"[SHA256] 获取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 设置传输窗口大小 (优化传输速度)
        /// </summary>
        public bool SetTransferWindow(int size)
        {
            string xml = $"<?xml version=\"1.0\" ?><data><configure MemoryName=\"{StorageType}\" MaxPayloadSizeToTargetInBytes=\"{size}\" /></data>";
            if (SendXmlCommand(xml, true))
            {
                _maxPayloadSize = size;
                _log($"[Config] 传输窗口设置为: {size / 1024}KB");
                return true;
            }
            return false;
        }
    }
}
