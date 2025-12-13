using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace OPFlashTool.Qualcomm
{
    /// <summary>
    /// Qualcomm Streaming Protocol (DMSS Download Protocol)
    /// 用于老旧高通设备 (MSM8960 及更早)
    /// 参考: https://github.com/bkerler/edl
    /// </summary>
    public class StreamingClient
    {
        private SerialPort _port;
        private Action<string> _log;
        
        // Streaming 协议命令
        private const byte CMD_HELLO = 0x01;
        private const byte CMD_HELLO_RSP = 0x02;
        private const byte CMD_READ = 0x03;
        private const byte CMD_READ_RSP = 0x04;
        private const byte CMD_WRITE = 0x07;
        private const byte CMD_WRITE_RSP = 0x08;
        private const byte CMD_NOP = 0x06;
        private const byte CMD_RESET = 0x0B;
        private const byte CMD_POWERDOWN = 0x0E;
        private const byte CMD_OPEN = 0x1B;
        private const byte CMD_OPEN_RSP = 0x1C;
        private const byte CMD_CLOSE = 0x15;
        private const byte CMD_CLOSE_RSP = 0x16;
        private const byte CMD_SECURITY_MODE = 0x17;
        private const byte CMD_PARTITION_TBL = 0x19;
        private const byte CMD_PARTITION_TBL_RSP = 0x1A;
        private const byte CMD_OPEN_MULTI = 0x1B;
        private const byte CMD_ERASE = 0x1D;
        private const byte CMD_ERASE_RSP = 0x1E;
        private const byte CMD_GET_ECC_STATE = 0x20;
        private const byte CMD_SET_ECC = 0x21;
        
        // HDLC 帧标记
        private const byte HDLC_FLAG = 0x7E;
        private const byte HDLC_ESC = 0x7D;
        private const byte HDLC_ESC_MASK = 0x20;
        
        // CRC16 查找表
        private static readonly ushort[] CrcTable = GenerateCrcTable();

        public StreamingClient(SerialPort port, Action<string> logger)
        {
            _port = port;
            _log = logger ?? Console.WriteLine;
            
            try
            {
                _port.ReadTimeout = 5000;
                _port.WriteTimeout = 5000;
            }
            catch { }
        }

        #region 公开方法

        /// <summary>
        /// 握手连接
        /// </summary>
        public bool Connect()
        {
            try
            {
                _log("[Streaming] 发送 Hello...");
                
                // Hello 包: magic="QCOM fast download protocol host"
                byte[] helloData = System.Text.Encoding.ASCII.GetBytes("QCOM fast download protocol host");
                byte[] packet = BuildPacket(CMD_HELLO, helloData);
                
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                if (response == null || response.Length < 1 || response[0] != CMD_HELLO_RSP)
                {
                    _log("[Streaming] Hello 响应失败");
                    return false;
                }
                
                _log("[Streaming] 连接成功");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Streaming] 连接失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 打开分区进行读写
        /// </summary>
        public bool OpenPartition(byte mode)
        {
            try
            {
                // mode: 0=NAND, 1=MMC
                byte[] data = new byte[] { mode };
                byte[] packet = BuildPacket(CMD_OPEN, data);
                
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                return response != null && response.Length > 0 && response[0] == CMD_OPEN_RSP;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 关闭分区
        /// </summary>
        public bool ClosePartition()
        {
            try
            {
                byte[] packet = BuildPacket(CMD_CLOSE, Array.Empty<byte>());
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                return response != null && response.Length > 0 && response[0] == CMD_CLOSE_RSP;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        public byte[]? Read(uint address, uint length)
        {
            try
            {
                _log($"[Streaming] 读取 @ 0x{address:X} ({length} bytes)");
                
                byte[] data = new byte[8];
                BitConverter.GetBytes(address).CopyTo(data, 0);
                BitConverter.GetBytes(length).CopyTo(data, 4);
                
                byte[] packet = BuildPacket(CMD_READ, data);
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                if (response == null || response.Length < 5 || response[0] != CMD_READ_RSP)
                {
                    return null;
                }
                
                // 跳过命令字节和地址，返回数据
                byte[] result = new byte[response.Length - 5];
                Array.Copy(response, 5, result, 0, result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _log($"[Streaming] 读取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        public bool Write(uint address, byte[] writeData)
        {
            try
            {
                _log($"[Streaming] 写入 @ 0x{address:X} ({writeData.Length} bytes)");
                
                byte[] data = new byte[4 + writeData.Length];
                BitConverter.GetBytes(address).CopyTo(data, 0);
                writeData.CopyTo(data, 4);
                
                byte[] packet = BuildPacket(CMD_WRITE, data);
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                return response != null && response.Length > 0 && response[0] == CMD_WRITE_RSP;
            }
            catch (Exception ex)
            {
                _log($"[Streaming] 写入失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 擦除扇区
        /// </summary>
        public bool Erase(uint address, uint length)
        {
            try
            {
                _log($"[Streaming] 擦除 @ 0x{address:X} ({length} bytes)");
                
                byte[] data = new byte[8];
                BitConverter.GetBytes(address).CopyTo(data, 0);
                BitConverter.GetBytes(length).CopyTo(data, 4);
                
                byte[] packet = BuildPacket(CMD_ERASE, data);
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                return response != null && response.Length > 0 && response[0] == CMD_ERASE_RSP;
            }
            catch (Exception ex)
            {
                _log($"[Streaming] 擦除失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取分区表
        /// </summary>
        public byte[]? GetPartitionTable()
        {
            try
            {
                _log("[Streaming] 获取分区表...");
                
                byte[] packet = BuildPacket(CMD_PARTITION_TBL, Array.Empty<byte>());
                SendPacket(packet);
                
                byte[] response = ReceivePacket();
                if (response == null || response.Length < 2 || response[0] != CMD_PARTITION_TBL_RSP)
                {
                    return null;
                }
                
                byte[] result = new byte[response.Length - 1];
                Array.Copy(response, 1, result, 0, result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _log($"[Streaming] 获取分区表失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重启设备
        /// </summary>
        public bool Reset()
        {
            try
            {
                _log("[Streaming] 重启设备...");
                byte[] packet = BuildPacket(CMD_RESET, Array.Empty<byte>());
                SendPacket(packet);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 关机
        /// </summary>
        public bool PowerDown()
        {
            try
            {
                _log("[Streaming] 关机...");
                byte[] packet = BuildPacket(CMD_POWERDOWN, Array.Empty<byte>());
                SendPacket(packet);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 刷写文件到指定地址
        /// </summary>
        public async Task<bool> FlashFileAsync(string filePath, uint startAddress, Action<long, long>? progress = null, CancellationToken ct = default)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _log($"[Streaming] 文件不存在: {filePath}");
                    return false;
                }

                long fileSize = new FileInfo(filePath).Length;
                _log($"[Streaming] 刷写 {Path.GetFileName(filePath)} ({fileSize} bytes)");

                const int chunkSize = 4096;
                uint currentAddr = startAddress;
                long totalWritten = 0;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[chunkSize];
                    int bytesRead;

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, chunkSize, ct)) > 0)
                    {
                        if (ct.IsCancellationRequested) return false;

                        byte[] chunk;
                        if (bytesRead == chunkSize)
                        {
                            chunk = buffer;
                        }
                        else
                        {
                            chunk = new byte[bytesRead];
                            Array.Copy(buffer, 0, chunk, 0, bytesRead);
                        }
                        
                        if (!Write(currentAddr, chunk))
                        {
                            _log($"[Streaming] 写入失败 @ 0x{currentAddr:X}");
                            return false;
                        }

                        currentAddr += (uint)bytesRead;
                        totalWritten += bytesRead;
                        progress?.Invoke(totalWritten, fileSize);
                    }
                }

                _log("[Streaming] 刷写完成");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Streaming] 刷写失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region HDLC 帧处理

        private byte[] BuildPacket(byte command, byte[] data)
        {
            // 构建负载: command + data
            byte[] payload = new byte[1 + data.Length];
            payload[0] = command;
            data.CopyTo(payload, 1);
            
            // 计算 CRC16
            ushort crc = CalculateCrc16(payload);
            
            // 添加 CRC (小端)
            byte[] withCrc = new byte[payload.Length + 2];
            payload.CopyTo(withCrc, 0);
            withCrc[payload.Length] = (byte)(crc & 0xFF);
            withCrc[payload.Length + 1] = (byte)((crc >> 8) & 0xFF);
            
            // HDLC 转义
            var escaped = new List<byte> { HDLC_FLAG };
            foreach (byte b in withCrc)
            {
                if (b == HDLC_FLAG || b == HDLC_ESC)
                {
                    escaped.Add(HDLC_ESC);
                    escaped.Add((byte)(b ^ HDLC_ESC_MASK));
                }
                else
                {
                    escaped.Add(b);
                }
            }
            escaped.Add(HDLC_FLAG);
            
            return escaped.ToArray();
        }

        private void SendPacket(byte[] packet)
        {
            _port.Write(packet, 0, packet.Length);
        }

        private byte[]? ReceivePacket()
        {
            var buffer = new List<byte>();
            bool inFrame = false;
            bool escaped = false;
            int timeout = 5000;
            int oldTimeout = _port.ReadTimeout;
            _port.ReadTimeout = timeout;

            try
            {
                DateTime start = DateTime.Now;
                while ((DateTime.Now - start).TotalMilliseconds < timeout)
                {
                    if (_port.BytesToRead == 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    int b = _port.ReadByte();
                    if (b < 0) break;

                    byte data = (byte)b;

                    if (data == HDLC_FLAG)
                    {
                        if (inFrame && buffer.Count > 0)
                        {
                            // 帧结束
                            break;
                        }
                        inFrame = true;
                        continue;
                    }

                    if (!inFrame) continue;

                    if (data == HDLC_ESC)
                    {
                        escaped = true;
                        continue;
                    }

                    if (escaped)
                    {
                        data ^= HDLC_ESC_MASK;
                        escaped = false;
                    }

                    buffer.Add(data);
                }
            }
            finally
            {
                _port.ReadTimeout = oldTimeout;
            }

            if (buffer.Count < 3) return null; // 至少 1 字节命令 + 2 字节 CRC

            // 验证 CRC
            byte[] withCrc = buffer.ToArray();
            byte[] payload = new byte[withCrc.Length - 2];
            Array.Copy(withCrc, payload, payload.Length);
            
            ushort expectedCrc = (ushort)(withCrc[withCrc.Length - 2] | (withCrc[withCrc.Length - 1] << 8));
            ushort actualCrc = CalculateCrc16(payload);
            
            if (expectedCrc != actualCrc)
            {
                _log($"[Streaming] CRC 校验失败: 期望 0x{expectedCrc:X4}, 实际 0x{actualCrc:X4}");
                return null;
            }

            return payload;
        }

        #endregion

        #region CRC16

        private static ushort[] GenerateCrcTable()
        {
            ushort[] table = new ushort[256];
            const ushort polynomial = 0x8408; // CRC-16-CCITT (反转)

            for (int i = 0; i < 256; i++)
            {
                ushort crc = (ushort)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ polynomial);
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }

        private static ushort CalculateCrc16(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc = (ushort)((crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF]);
            }
            return (ushort)(crc ^ 0xFFFF);
        }

        #endregion
    }
}
