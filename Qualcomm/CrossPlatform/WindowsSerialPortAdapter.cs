using System;
using System.IO.Ports;

namespace OPFlashTool.Qualcomm.CrossPlatform
{
    /// <summary>
    /// Windows 串口适配器 - 封装 System.IO.Ports.SerialPort
    /// </summary>
    public class WindowsSerialPortAdapter : ISerialPortAdapter
    {
        private readonly SerialPort _port;
        private bool _disposed;

        public WindowsSerialPortAdapter(string portName, int baudRate = 115200)
        {
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 5000,
                WriteTimeout = 5000,
                DtrEnable = true,
                RtsEnable = true
            };
            
            // 尽量放大缓冲区
            try
            {
                _port.ReadBufferSize = 1024 * 1024;
                _port.WriteBufferSize = 1024 * 1024;
            }
            catch { /* 某些驱动不支持 */ }
        }

        public string PortName => _port.PortName;
        
        public bool IsOpen => _port.IsOpen;
        
        public int ReadTimeout
        {
            get => _port.ReadTimeout;
            set => _port.ReadTimeout = value;
        }
        
        public int WriteTimeout
        {
            get => _port.WriteTimeout;
            set => _port.WriteTimeout = value;
        }
        
        public int BytesToRead => _port.BytesToRead;

        public void Open()
        {
            if (!_port.IsOpen)
            {
                _port.Open();
            }
        }

        public void Close()
        {
            if (_port.IsOpen)
            {
                try
                {
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();
                }
                catch { }
                
                _port.Close();
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _port.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _port.Write(buffer, offset, count);
        }

        public void DiscardInBuffer()
        {
            if (_port.IsOpen)
            {
                _port.DiscardInBuffer();
            }
        }

        public void DiscardOutBuffer()
        {
            if (_port.IsOpen)
            {
                _port.DiscardOutBuffer();
            }
        }

        public int ReadByte()
        {
            return _port.ReadByte();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (disposing)
            {
                Close();
                _port?.Dispose();
            }
            
            _disposed = true;
        }

        ~WindowsSerialPortAdapter()
        {
            Dispose(false);
        }

        /// <summary>
        /// 获取底层 SerialPort (用于兼容现有代码)
        /// </summary>
        public SerialPort UnderlyingPort => _port;
    }
}
