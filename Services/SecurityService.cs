using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OPFlashTool.Security
{
    public static class SecurityService
    {
        // --- Native Methods ---
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // --- RSA Encryption/Decryption ---
        // 注意：在实际生产环境中，私钥不应硬编码在客户端代码中，否则反编译后即可获取。
        // 建议：
        // 1. 使用服务器下发密钥
        // 2. 或者使用非对称加密仅用于验证签名，数据加密使用 AES
        // 这里演示如何使用 RSA 进行数据加解密
        
        private static RSACryptoServiceProvider _rsa;

        static SecurityService()
        {
            _rsa = new RSACryptoServiceProvider(2048);
            // 这里可以加载预设的 XML 密钥
            // _rsa.FromXmlString("<RSAKeyValue>...</RSAKeyValue>");
        }

        public static string EncryptString(string plainText, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKeyXml);
                var data = Encoding.UTF8.GetBytes(plainText);
                var encrypted = rsa.Encrypt(data, false);
                return Convert.ToBase64String(encrypted);
            }
        }

        public static string DecryptString(string encryptedBase64, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);
                var data = Convert.FromBase64String(encryptedBase64);
                var decrypted = rsa.Decrypt(data, false);
                return Encoding.UTF8.GetString(decrypted);
            }
        }

        // --- Anti-Debugging & Anti-Tamper ---

        public static void EnsureSafeEnvironment()
        {
            // 1. 基础调试器检查
            if (Debugger.IsAttached)
            {
                CrashAndBurn();
            }

            // 2. Native 调试器检查
            if (IsDebuggerPresent())
            {
                CrashAndBurn();
            }

            // 3. 远程调试器检查
            bool isRemoteDebuggerPresent = false;
            CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemoteDebuggerPresent);
            if (isRemoteDebuggerPresent)
            {
                CrashAndBurn();
            }

            // 4. 检查黑名单进程 (常见的逆向工具)
            CheckBlacklistedProcesses();

            // 5. 启动后台监控线程
            Thread monitorThread = new Thread(MonitorLoop);
            monitorThread.IsBackground = true;
            monitorThread.Start();
        }

        private static void MonitorLoop()
        {
            while (true)
            {
                try
                {
                    if (Debugger.IsAttached || IsDebuggerPresent())
                    {
                        Environment.FailFast("Security Violation");
                    }
                    
                    CheckBlacklistedProcesses();
                    
                    Thread.Sleep(2000);
                }
                catch 
                { 
                    // 忽略错误，保持运行
                }
            }
        }

        private static void CheckBlacklistedProcesses()
        {
            string[] badProcessNames = new string[]
            {
                "dnspy",
                "ilspy",
                "de4dot",
                "dotpeek",
                "fiddler",
                "wireshark",
                "charles",
                "x64dbg",
                "ollydbg",
                "processhacker",
                "cheatengine",
                "httpdebugger"
            };

            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    string pName = p.ProcessName.ToLower();
                    foreach (var bad in badProcessNames)
                    {
                        if (pName.Contains(bad))
                        {
                            // 发现恶意工具，强制退出
                            Environment.FailFast($"Detected prohibited tool: {bad}");
                        }
                    }
                    
                    // 检查窗口标题
                    if (!string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        string title = p.MainWindowTitle.ToLower();
                        if (title.Contains("debug") || title.Contains("disassembly") || title.Contains("fiddler"))
                        {
                             Environment.FailFast("Detected debugging environment");
                        }
                    }
                }
                catch { }
            }
        }

        private static void CrashAndBurn()
        {
            // 抛出致命错误或直接退出
            // 使用 FailFast 比 Exit 更难被拦截
            Environment.FailFast("Security Check Failed");
        }
    }
}
