using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OPFlashTool
{
    internal static class Program
    {
        private static Mutex _mutex = null;
        private static EventWaitHandle _duplicateInstanceEvent;
        private static RegisteredWaitHandle _duplicateInstanceWaitHandle;
        private const string AppMutexName = "MultiFlashTool_SingleInstanceMutex";
        private const string DuplicateEventName = "MultiFlashTool_ShowDuplicateWarningEvent";
        private const string DuplicateWarningMessage = "MultiFlash Tool已打开，请勿重复打开！！！";
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool ownsMutex = false;

            try
            {
                // 1. 单实例检测
                _mutex = new Mutex(true, AppMutexName, out bool isNewInstance);
                if (!isNewInstance)
                {
                    NotifyExistingInstanceOrShowWarning();
                    _mutex?.Dispose();
                    return;
                }

                ownsMutex = true;
                SetupDuplicateInstanceListener();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // Enable TLS 1.2, TLS 1.1, and TLS 1.0 to ensure compatibility
                // Also try to enable TLS 1.3 (12288) if available on the OS
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;

                // Global settings for connection stability
                System.Net.ServicePointManager.DefaultConnectionLimit = 100;
                System.Net.ServicePointManager.Expect100Continue = false;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 直接启动主窗体（无需登录）
                Application.Run(new Form1());
            }
            finally
            {
                CleanupDuplicateInstanceListener();
                if (ownsMutex && _mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
        }

        private static void SetupDuplicateInstanceListener()
        {
            _duplicateInstanceEvent = new EventWaitHandle(false, EventResetMode.AutoReset, DuplicateEventName);
            _duplicateInstanceWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _duplicateInstanceEvent,
                (_, timedOut) =>
                {
                    if (!timedOut)
                    {
                        ShowDuplicateWarningAsync();
                    }
                },
                null,
                Timeout.Infinite,
                false);
        }

        private static void CleanupDuplicateInstanceListener()
        {
            _duplicateInstanceWaitHandle?.Unregister(null);
            _duplicateInstanceWaitHandle = null;
            _duplicateInstanceEvent?.Dispose();
            _duplicateInstanceEvent = null;
        }

        private static void NotifyExistingInstanceOrShowWarning()
        {
            try
            {
                using (var existingEvent = EventWaitHandle.OpenExisting(DuplicateEventName))
                {
                    existingEvent.Set();
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                ShowDuplicateWarningAsync();
            }
        }

        private static void ShowDuplicateWarningAsync()
        {
            var thread = new Thread(() => ShowForm3WithMessage(DuplicateWarningMessage))
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        /// <summary>
        /// 显示 Form3 并设置提示信息（仅用于重复打开提示）
        /// </summary>
        private static void ShowForm3WithMessage(string message)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form3 form3 = new Form3();
            form3.Input3Text = message;
            Application.Run(form3);
        }
    }
}
