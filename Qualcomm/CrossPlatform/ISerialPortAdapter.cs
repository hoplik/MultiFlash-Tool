using System;
using System.Threading;
using System.Threading.Tasks;

namespace OPFlashTool.Qualcomm.CrossPlatform
{
    /// <summary>
    /// 串口适配器接口 - 跨平台抽象层
    /// 为未来迁移到 .NET Core/6+ 做准备
    /// </summary>
    public interface ISerialPortAdapter : IDisposable
    {
        /// <summary>
        /// 端口名称 (Windows: COM3, Linux: /dev/ttyUSB0, macOS: /dev/cu.usbserial)
        /// </summary>
        string PortName { get; }
        
        /// <summary>
        /// 是否已打开
        /// </summary>
        bool IsOpen { get; }
        
        /// <summary>
        /// 读取超时 (毫秒)
        /// </summary>
        int ReadTimeout { get; set; }
        
        /// <summary>
        /// 写入超时 (毫秒)
        /// </summary>
        int WriteTimeout { get; set; }
        
        /// <summary>
        /// 可读取字节数
        /// </summary>
        int BytesToRead { get; }
        
        /// <summary>
        /// 打开端口
        /// </summary>
        void Open();
        
        /// <summary>
        /// 关闭端口
        /// </summary>
        void Close();
        
        /// <summary>
        /// 读取数据
        /// </summary>
        int Read(byte[] buffer, int offset, int count);
        
        /// <summary>
        /// 写入数据
        /// </summary>
        void Write(byte[] buffer, int offset, int count);
        
        /// <summary>
        /// 清空输入缓冲区
        /// </summary>
        void DiscardInBuffer();
        
        /// <summary>
        /// 清空输出缓冲区
        /// </summary>
        void DiscardOutBuffer();
        
        /// <summary>
        /// 读取单个字节
        /// </summary>
        int ReadByte();
    }

    /// <summary>
    /// 串口工厂 - 根据平台创建适配器
    /// </summary>
    public static class SerialPortFactory
    {
        /// <summary>
        /// 获取当前操作系统
        /// </summary>
        public static PlatformOS CurrentPlatform
        {
            get
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    return PlatformOS.Windows;
                
                // .NET Core 方式检测 (留作未来使用)
                // if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                //     return PlatformOS.Linux;
                // if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                //     return PlatformOS.MacOS;
                
                return PlatformOS.Unknown;
            }
        }
        
        /// <summary>
        /// 创建串口适配器
        /// </summary>
        public static ISerialPortAdapter Create(string portName, int baudRate = 115200)
        {
            switch (CurrentPlatform)
            {
                case PlatformOS.Windows:
                    return new WindowsSerialPortAdapter(portName, baudRate);
                    
                case PlatformOS.Linux:
                case PlatformOS.MacOS:
                    // 未来实现: 使用 libserialport 或 termios
                    throw new PlatformNotSupportedException($"平台 {CurrentPlatform} 暂不支持");
                    
                default:
                    throw new PlatformNotSupportedException("未知平台");
            }
        }
        
        /// <summary>
        /// 列出可用串口
        /// </summary>
        public static string[] GetPortNames()
        {
            switch (CurrentPlatform)
            {
                case PlatformOS.Windows:
                    return System.IO.Ports.SerialPort.GetPortNames();
                    
                case PlatformOS.Linux:
                    // 未来实现: 扫描 /dev/ttyUSB*, /dev/ttyACM*
                    return Array.Empty<string>();
                    
                case PlatformOS.MacOS:
                    // 未来实现: 扫描 /dev/cu.usbserial*, /dev/tty.usbserial*
                    return Array.Empty<string>();
                    
                default:
                    return Array.Empty<string>();
            }
        }
    }
    
    /// <summary>
    /// 平台枚举
    /// </summary>
    public enum PlatformOS
    {
        Unknown,
        Windows,
        Linux,
        MacOS
    }
}
