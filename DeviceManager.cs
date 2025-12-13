using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OPFlashTool
{
    internal class DeviceManager
    {
        private readonly List<string> _lastDeviceKeys = new List<string>();
        private string _activeEdlPort = string.Empty;
        private string _lastMode = "none";
        private readonly object _detectionLock = new object();

        public class DeviceInfo
        {
            public string Serial { get; set; } = "";
            public string Port { get; set; } = "";
            public string Mode { get; set; } = "";
            public string Status { get; set; } = "";
            public string DeviceType { get; set; } = "";
            public bool IsFirstDevice { get; set; }
            public string DisplayText
            {
                get
                {
                    if (DeviceType == "EDL")
                        return $"EDL设备: {Port}";
                    else if (DeviceType == "Fastboot")
                        return $"Fastboot: {Serial} ({Mode})";
                    else if (DeviceType == "Unauthorized")
                        return $"未授权: {Serial}";
                    else if (DeviceType == "ADB")
                        return $"已授权: {Serial} ({Mode}模式)";
                    else
                        return $"{DeviceType}: {Serial} ({Mode})";
                }
            }
        }

        public class SlotSwitchResult
        {
            public bool Success { get; set; }
            public string CurrentSlot { get; set; } = "";
            public string TargetSlot { get; set; } = "";
        }

        public class DeviceLogEvent
        {
            public string Message { get; set; } = "";
            public Color Color { get; set; }
        }

        public class DeviceDetectionResult
        {
            public List<DeviceInfo> Devices { get; } = new List<DeviceInfo>();
            public string DisplayInfo { get; set; } = "未连接任何设备";
            public string Mode { get; set; } = "none";
            public List<DeviceLogEvent> LogEvents { get; } = new List<DeviceLogEvent>();
        }

        private sealed class SerialPortInfo
        {
            public string DeviceId { get; set; } = string.Empty;
            public string PnpId { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private static readonly Regex ComPortRegex = new Regex(@"COM\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly string[] EdlDescriptorKeywords = { "9008", "QDLOADER", "QHSUSB", "EDL" };
        private static readonly string[] KnownEdlPids = { "PID_9008", "PID_900E", "PID_900C", "PID_901D", "PID_F000", "PID_FFF0" };

        /// <summary>
        /// 执行命令并返回输出
        /// </summary>
        public string RunCommand(string command)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {command}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit(1500);
                    return (output + "\n" + error).Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 异步执行命令并返回输出
        /// </summary>
        public async Task<string> RunCommandAsync(string command)
        {
            return await Task.Run(() => RunCommand(command));
        }

        /// <summary>
        /// 获取所有Fastboot设备
        /// </summary>
        public List<Tuple<bool, string>> GetFastbootDevices()
        {
            var devices = new List<Tuple<bool, string>>();
            try
            {
                string devicesOutput = RunCommand("fastboot devices");
                if (!string.IsNullOrWhiteSpace(devicesOutput))
                {
                    string[] lines = devicesOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string serial = parts[0];
                            string deviceType = parts[1];
                            bool isFastbootD = deviceType == "fastbootd";

                            // 使用getvar is-userspace进行权威判断
                            string getvarOutput = RunCommand($"fastboot -s {serial} getvar is-userspace 2>&1");
                            if (getvarOutput.Contains("is-userspace: yes"))
                            {
                                isFastbootD = true;
                            }
                            else if (getvarOutput.Contains("is-userspace: no"))
                            {
                                isFastbootD = false;
                            }

                            devices.Add(Tuple.Create(isFastbootD, serial));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 异常将在调用处处理
                throw new Exception($"Fastboot设备检测异常: {ex.Message}");
            }
            return devices;
        }

        public List<string> GetEdlDevices()
        {
            try
            {
                var detectedPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var serialPorts = BuildSerialPortSnapshot();

                foreach (var info in serialPorts)
                {
                    if (IsEdlPortCandidate(info))
                    {
                        string portName = NormalizeComPort(info.DeviceId);
                        if (!string.IsNullOrEmpty(portName))
                        {
                            detectedPorts.Add(portName);
                        }
                    }
                }

                if (detectedPorts.Count == 0)
                {
                    foreach (var port in QueryPnPEntitiesForEdl(serialPorts))
                    {
                        if (!string.IsNullOrEmpty(port))
                        {
                            detectedPorts.Add(port);
                        }
                    }
                }

                return detectedPorts.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"EDL设备检测异常: {ex.Message}");
            }
        }

        public bool Is901DModeConnected()
        {
            try
            {
                ManagementObjectSearcher searcher = null;
                ManagementObjectCollection collection = null;
                try
                {
                    searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
                    collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        try
                        {
                            string name = obj["Name"] != null ? obj["Name"].ToString() : "";
                            if (name.Contains("901D")) return true;
                        }
                        finally
                        {
                            obj?.Dispose();
                            ReleaseComObject(obj);
                        }
                    }
                }
                finally
                {
                    collection?.Dispose();
                    ReleaseComObject(collection);
                    searcher?.Dispose();
                    ReleaseComObject(searcher);
                }
            }
            catch
            {
            }
            return false;
        }

        public List<DeviceInfo> ConvertToDeviceInfos(List<Tuple<string, string, string>> devices, string deviceType)
        {
            var deviceInfos = new List<DeviceInfo>();
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                deviceInfos.Add(new DeviceInfo
                {
                    Serial = device.Item1,
                    Mode = device.Item3,
                    Status = device.Item2,
                    DeviceType = deviceType,
                    IsFirstDevice = i == 0
                });
            }
            return deviceInfos;
        }

        public List<DeviceInfo> ConvertToDeviceInfos(List<Tuple<bool, string>> devices)
        {
            var deviceInfos = new List<DeviceInfo>();
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                deviceInfos.Add(new DeviceInfo
                {
                    Serial = device.Item2,
                    Mode = device.Item1 ? "FastbootD" : "Fastboot",
                    Status = "fastboot",
                    DeviceType = "Fastboot",
                    IsFirstDevice = i == 0
                });
            }
            return deviceInfos;
        }

        public List<DeviceInfo> ConvertToDeviceInfos(List<string> ports)
        {
            var deviceInfos = new List<DeviceInfo>();
            for (int i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                deviceInfos.Add(new DeviceInfo
                {
                    Port = port,
                    Mode = "9008",
                    Status = "edl",
                    DeviceType = "EDL",
                    IsFirstDevice = i == 0
                });
            }
            return deviceInfos;
        }

        public DeviceDetectionResult DetectDeviceStatus()
        {
            lock (_detectionLock)
            {
                var result = new DeviceDetectionResult();
                var allDeviceInfos = new List<DeviceInfo>();
                bool edlDetectedThisRound = false;
                string displayInfo = "未连接任何设备";
                string currentMode = "none";
                string previousMode = _lastMode;
                string previousEdlPort = _activeEdlPort;

                var adbDevices = new List<Tuple<string, string, string>>();
                var fastbootDevices = new List<Tuple<bool, string>>();
                var edlDevices = new List<string>();

                try
                {
                    adbDevices = GetAdbDevices();
                }
                catch (Exception ex)
                {
                    result.LogEvents.Add(new DeviceLogEvent { Message = ex.Message, Color = Color.Orange });
                }

                try
                {
                    fastbootDevices = GetFastbootDevices();
                }
                catch (Exception ex)
                {
                    result.LogEvents.Add(new DeviceLogEvent { Message = ex.Message, Color = Color.Orange });
                }

                try
                {
                    edlDevices = GetEdlDevices();
                }
                catch (Exception ex)
                {
                    result.LogEvents.Add(new DeviceLogEvent { Message = ex.Message, Color = Color.Orange });
                }

                bool is901DConnected = Is901DModeConnected();

                var unauthorizedDevices = adbDevices.Where(d => d.Item2 == "unauthorized").ToList();
                if (unauthorizedDevices.Any())
                {
                    allDeviceInfos.AddRange(ConvertToDeviceInfos(unauthorizedDevices, "Unauthorized"));
                    string serials = string.Join(", ", unauthorizedDevices.Select(d => d.Item1));
                    string unauthorizedKey = $"unauthorized:{serials}";
                    if (!_lastDeviceKeys.Contains(unauthorizedKey))
                    {
                        result.LogEvents.Add(new DeviceLogEvent
                        {
                            Message = $"已连接的设备({serials})未授权，请在设备上勾选一律允许！",
                            Color = Color.Red
                        });
                        _lastDeviceKeys.RemoveAll(k => k.StartsWith("unauthorized:"));
                        _lastDeviceKeys.Add(unauthorizedKey);
                    }
                }

                var connectedAdbDevices = adbDevices.Where(d => d.Item2 != "unauthorized").ToList();
                if (connectedAdbDevices.Any())
                {
                    allDeviceInfos.AddRange(ConvertToDeviceInfos(connectedAdbDevices, "ADB"));
                    if (connectedAdbDevices.Count > 1)
                    {
                        string serials = string.Join(", ", connectedAdbDevices.Select(d => d.Item1));
                        string adbMultiKey = $"adb_multi:{serials}";
                        if (!_lastDeviceKeys.Contains(adbMultiKey))
                        {
                            result.LogEvents.Add(new DeviceLogEvent
                            {
                                Message = $"检测到多个已授权设备: {serials}",
                                Color = Color.Blue
                            });
                            _lastDeviceKeys.RemoveAll(k => k.StartsWith("adb_multi:"));
                            _lastDeviceKeys.Add(adbMultiKey);
                        }
                    }
                }

                if (fastbootDevices.Count > 0)
                {
                    allDeviceInfos.AddRange(ConvertToDeviceInfos(fastbootDevices));
                    if (fastbootDevices.Count > 1)
                    {
                        string serials = string.Join(", ", fastbootDevices.Select(d => d.Item2));
                        string fastbootMultiKey = $"fastboot_multi:{serials}";
                        if (!_lastDeviceKeys.Contains(fastbootMultiKey))
                        {
                            result.LogEvents.Add(new DeviceLogEvent
                            {
                                Message = $"检测到多个Fastboot设备: {serials}",
                                Color = Color.Blue
                            });
                            _lastDeviceKeys.RemoveAll(k => k.StartsWith("fastboot_multi:"));
                            _lastDeviceKeys.Add(fastbootMultiKey);
                        }
                    }
                }

                if (edlDevices.Count > 0)
                {
                    allDeviceInfos.AddRange(ConvertToDeviceInfos(edlDevices));
                    edlDetectedThisRound = true;

                    if (edlDevices.Count > 1)
                    {
                        string ports = string.Join(", ", edlDevices);
                        string edlMultiKey = $"edl_multi:{ports}";
                        if (!_lastDeviceKeys.Contains(edlMultiKey))
                        {
                            result.LogEvents.Add(new DeviceLogEvent
                            {
                                Message = $"检测到多个EDL设备连接: {ports}",
                                Color = Color.Green
                            });
                            _lastDeviceKeys.RemoveAll(k => k.StartsWith("edl_multi:"));
                            _lastDeviceKeys.Add(edlMultiKey);
                        }
                    }
                    else
                    {
                        string port = edlDevices.First();
                        string edlSingleKey = $"edl_single:{port}";
                        if (!_lastDeviceKeys.Contains(edlSingleKey))
                        {
                            result.LogEvents.Add(new DeviceLogEvent
                            {
                                Message = $"检测到9008设备: {port}",
                                Color = Color.Blue
                            });
                            _lastDeviceKeys.RemoveAll(k => k.StartsWith("edl_single:"));
                            _lastDeviceKeys.Add(edlSingleKey);
                        }
                        _activeEdlPort = port;
                    }
                }

                if (is901DConnected)
                {
                    allDeviceInfos.Add(new DeviceInfo
                    {
                        Serial = "901D",
                        Port = "901D",
                        Mode = "901D",
                        Status = "Connected",
                        DeviceType = "901D"
                    });
                }

                if (allDeviceInfos.Count == 0)
                {
                    displayInfo = "未连接任何设备";
                    currentMode = "none";
                }
                else if (allDeviceInfos.Count == 1)
                {
                    var device = allDeviceInfos.First();
                    displayInfo = device.DisplayText;
                    currentMode = device.DeviceType?.ToLowerInvariant() ?? "unknown";
                }
                else
                {
                    int adbCount = allDeviceInfos.Count(d => d.DeviceType == "ADB");
                    int unauthorizedCount = allDeviceInfos.Count(d => d.DeviceType == "Unauthorized");
                    int fastbootCount = allDeviceInfos.Count(d => d.DeviceType == "Fastboot");
                    int edlCount = allDeviceInfos.Count(d => d.DeviceType == "EDL");
                    int mode901DCount = allDeviceInfos.Count(d => d.DeviceType == "901D");

                    var statusParts = new List<string>();
                    if (adbCount > 0) statusParts.Add($"ADB:{adbCount}");
                    if (unauthorizedCount > 0) statusParts.Add($"未授权:{unauthorizedCount}");
                    if (fastbootCount > 0) statusParts.Add($"Fastboot:{fastbootCount}");
                    if (edlCount > 0) statusParts.Add($"EDL:{edlCount}");
                    if (mode901DCount > 0) statusParts.Add($"901D:{mode901DCount}");

                    displayInfo = $"多设备连接 | {string.Join(" ", statusParts)}";
                    currentMode = "multiple_mixed";
                }

                result.Devices.AddRange(allDeviceInfos);

                if (!edlDetectedThisRound && previousMode == "edl" && !string.IsNullOrEmpty(previousEdlPort))
                {
                    result.LogEvents.Add(new DeviceLogEvent
                    {
                        Message = $"9008设备已断开: {previousEdlPort}",
                        Color = Color.Red
                    });
                    _activeEdlPort = string.Empty;
                    _lastDeviceKeys.RemoveAll(k => k.StartsWith("edl_single:") || k.StartsWith("edl_multi:"));
                }
                else if (!edlDetectedThisRound)
                {
                    _activeEdlPort = string.Empty;
                }

                if (allDeviceInfos.Count == 0)
                {
                    _lastDeviceKeys.Clear();
                }

                result.DisplayInfo = displayInfo;
                result.Mode = currentMode;
                _lastMode = currentMode;
                return result;
            }
        }

        /// <summary>
        /// 获取设备状态
        /// </summary>
        public string GetDeviceStatus(string serial)
        {
            try
            {
                // 检查是否在ADB模式
                string adbResult = RunCommand($"adb -s {serial} devices");
                if (adbResult.Contains(serial) && adbResult.Contains("device"))
                {
                    return "adb";
                }
                
                // 检查是否在Fastboot模式
                string fastbootResult = RunCommand($"fastboot -s {serial} devices");
                if (fastbootResult.Contains(serial))
                {
                    return "fastboot";
                }
            }
            catch {}
            
            return "unknown";
        }

        /// <summary>
        /// 获取ADB设备列表
        /// </summary>
        public List<Tuple<string, string, string>> GetAdbDevices()
        {
            var devices = new List<Tuple<string, string, string>>();
            try
            {
                var result = RunCommand("adb devices -l");
                if (!string.IsNullOrEmpty(result))
                {
                    var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "device", "unauthorized", "offline", "recovery", "sideload"
                    };

                    var lines = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed.StartsWith("*"))
                            continue;

                        if (trimmed.Contains("List of devices attached") || trimmed.StartsWith("adb server", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string serial = parts[0];
                            string status = parts[1];
                            if (!allowedStatuses.Contains(status))
                                continue;

                            string mode = "系统";

                            if (status.Equals("sideload", StringComparison.OrdinalIgnoreCase))
                            {
                                mode = "Sideload";
                            }
                            else if (status.Equals("recovery", StringComparison.OrdinalIgnoreCase))
                            {
                                mode = "Recovery";
                            }
                            else if (status.Equals("device", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    string bootMode = RunCommand($"adb -s {serial} shell getprop ro.bootmode").Trim().ToLowerInvariant();
                                    string sideloadProp = RunCommand($"adb -s {serial} shell getprop ro.sideload").Trim();
                                    bool isSideload = sideloadProp == "1" || bootMode.Contains("sideload");

                                    if (isSideload)
                                    {
                                        mode = "Sideload";
                                    }
                                    else if (bootMode.Contains("recovery"))
                                    {
                                        mode = "Recovery";
                                    }
                                    else
                                    {
                                        mode = "系统";
                                    }
                                }
                                catch
                                {
                                    mode = "系统";
                                }
                            }
                            else
                            {
                                mode = status;
                            }

                            devices.Add(Tuple.Create(serial, status, mode));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"ADB设备检测失败: {ex.Message}");
            }
            
            return devices;
        }

        /// <summary>
        /// 重启设备到正常模式
        /// </summary>
        public async Task<bool> RebootDevice(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus == "adb")
                {
                    string result = await RunCommandAsync($"adb -s {serial} reboot");
                    return string.IsNullOrEmpty(result) || result.Contains("reboot");
                }

                await RunCommandAsync($"fastboot -s {serial} reboot");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"重启设备失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重启设备到Recovery模式
        /// </summary>
        public async Task<bool> RebootToRecovery(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus == "adb")
                {
                    string adbOutput = await RunCommandAsync($"adb -s {serial} reboot recovery 2>&1");
                    EnsureCommandSucceeded(adbOutput, "重启到Recovery");
                    return true;
                }

                string fastbootOutput = await RunCommandAsync($"fastboot -s {serial} reboot recovery 2>&1");
                EnsureCommandSucceeded(fastbootOutput, "执行 fastboot reboot recovery");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"重启到Recovery失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重启设备到Fastboot模式
        /// </summary>
        public async Task<bool> RebootToFastboot(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus == "adb")
                {
                    string adbOutput = await RunCommandAsync($"adb -s {serial} reboot bootloader 2>&1");
                    EnsureCommandSucceeded(adbOutput, "重启到Fastboot");
                    return true;
                }

                string fastbootOutput = await RunCommandAsync($"fastboot -s {serial} reboot bootloader 2>&1");
                EnsureCommandSucceeded(fastbootOutput, "执行 fastboot reboot bootloader");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"重启到Fastboot失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重启设备到FastbootD模式
        /// </summary>
        public async Task<bool> RebootToFastbootD(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus == "adb")
                {
                    string adbOutput = await RunCommandAsync($"adb -s {serial} reboot fastboot 2>&1");
                    EnsureCommandSucceeded(adbOutput, "重启到FastbootD");
                    return true;
                }

                string fastbootOutput = await RunCommandAsync($"fastboot -s {serial} reboot fastboot 2>&1");
                EnsureCommandSucceeded(fastbootOutput, "执行 fastboot reboot fastboot");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"重启到FastbootD失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 小米设备踢入EDL模式
        /// </summary>
        public async Task<bool> KickXiaomiToEdl(string serial)
        {     
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus == "adb")
                {
                    string rebootBootloaderOutput = await RunCommandAsync($"adb -s {serial} reboot bootloader 2>&1");
                    EnsureCommandSucceeded(rebootBootloaderOutput, "重启到Fastboot");
                    await Task.Delay(2000);
                }

                string fastbootOutput = await RunCommandAsync($"fastboot -s {serial} oem edl 2>&1");
               EnsureCommandSucceeded(fastbootOutput, "执行");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"小米踢EDL失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 联想或安卓设备踢入EDL模式
        /// </summary>
        public async Task<bool> KickLenovoOrAndroidToEdl(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus == "adb")
                {
                    string adbEdlResult = await RunCommandAsync($"adb -s {serial} reboot edl 2>&1");
                    if (!CommandOutputIndicatesFailure(adbEdlResult))
                    {
                        return true;
                    }

                    string rebootBootloaderOutput = await RunCommandAsync($"adb -s {serial} reboot bootloader 2>&1");
                    EnsureCommandSucceeded(rebootBootloaderOutput, "重启到Fastboot");
                    await Task.Delay(2000);
                }

                string fastbootResult = await RunCommandAsync($"fastboot -s {serial} oem reboot-edl 2>&1");
                EnsureCommandSucceeded(fastbootResult, "执行 fastboot oem reboot-edl");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"联想或安卓踢入EDL失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换槽位
        /// </summary>
        public async Task<SlotSwitchResult> SwitchSlot(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus != "fastboot")
                {
                    throw new Exception("切换槽位需要设备处于Fastboot模式");
                }

                string currentSlot = await GetCurrentSlotInternal(serial);
                string newSlot = currentSlot == "a" ? "b" : "a";
                string result = await RunCommandAsync($"fastboot -s {serial} --set-active={newSlot}");
                return new SlotSwitchResult
                {
                    Success = result.Contains("OKAY"),
                    CurrentSlot = currentSlot,
                    TargetSlot = newSlot
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"切换槽位失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 擦除FRP
        /// </summary>
        public async Task<bool> EraseFRP(string serial)
        {
            try
            {
                string deviceStatus = EnsureDeviceStatus(serial);
                if (deviceStatus != "fastboot")
                {
                    throw new Exception("此功能需要设备处于Fastboot模式");
                }

                string result = await RunCommandAsync($"fastboot -s {serial} erase frp");
                return result.Contains("OKAY");
            }
            catch (Exception ex)
            {
                throw new Exception($"擦除谷歌锁失败: {ex.Message}");
            }
        }

        private List<SerialPortInfo> BuildSerialPortSnapshot()
        {
            var serialPorts = new List<SerialPortInfo>();
            ManagementObjectSearcher searcher = null;
            ManagementObjectCollection collection = null;
            try
            {
                searcher = new ManagementObjectSearcher("SELECT DeviceID, PNPDeviceID, Name FROM Win32_SerialPort");
                collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    try
                    {
                        serialPorts.Add(new SerialPortInfo
                        {
                            DeviceId = obj["DeviceID"]?.ToString() ?? string.Empty,
                            PnpId = obj["PNPDeviceID"]?.ToString() ?? string.Empty,
                            Description = obj["Name"]?.ToString() ?? string.Empty
                        });
                    }
                    finally
                    {
                        obj?.Dispose();
                        ReleaseComObject(obj);
                    }
                }
            }
            finally
            {
                collection?.Dispose();
                ReleaseComObject(collection);
                searcher?.Dispose();
                ReleaseComObject(searcher);
            }

            return serialPorts;
        }

        private IEnumerable<string> QueryPnPEntitiesForEdl(List<SerialPortInfo> serialPorts)
        {
            var detectedPorts = new List<string>();
            ManagementObjectSearcher searcher = null;
            ManagementObjectCollection collection = null;
            try
            {
                string query = "SELECT Name, Caption, PNPDeviceID FROM Win32_PnPEntity WHERE " +
                               "(Name LIKE '%9008%' OR Name LIKE '%QDLoader%' OR Name LIKE '%QHSUSB%' OR Name LIKE '%HS-USB%' OR Name LIKE '%EDL%' " +
                               "OR Caption LIKE '%9008%' OR Caption LIKE '%QDLoader%' OR Caption LIKE '%QHSUSB%' OR Caption LIKE '%HS-USB%' OR Caption LIKE '%EDL%' " +
                               "OR PNPDeviceID LIKE '%VID_05C6%')";
                searcher = new ManagementObjectSearcher(query);
                collection = searcher.Get();

                var serialLookup = serialPorts
                    .Where(p => !string.IsNullOrEmpty(p.PnpId))
                    .GroupBy(p => p.PnpId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => NormalizeComPort(g.First().DeviceId), StringComparer.OrdinalIgnoreCase);

                foreach (ManagementObject obj in collection)
                {
                    try
                    {
                        string name = obj["Name"]?.ToString();
                        string caption = obj["Caption"]?.ToString();
                        string pnpId = obj["PNPDeviceID"]?.ToString();

                        if (!IsEdlDescriptor(name) && !IsEdlDescriptor(caption) && !IsEdlPnpId(pnpId))
                        {
                            continue;
                        }

                        string port = NormalizeComPort(name);
                        if (string.IsNullOrEmpty(port))
                        {
                            port = NormalizeComPort(caption);
                        }

                        if (string.IsNullOrEmpty(port) && !string.IsNullOrEmpty(pnpId) && serialLookup.TryGetValue(pnpId, out string mappedPort))
                        {
                            port = mappedPort;
                        }

                        if (!string.IsNullOrEmpty(port))
                        {
                            detectedPorts.Add(port);
                        }
                    }
                    finally
                    {
                        obj?.Dispose();
                        ReleaseComObject(obj);
                    }
                }
            }
            finally
            {
                collection?.Dispose();
                ReleaseComObject(collection);
                searcher?.Dispose();
                ReleaseComObject(searcher);
            }

            return detectedPorts;
        }

        private static bool IsEdlPortCandidate(SerialPortInfo info)
        {
            if (info == null)
            {
                return false;
            }

            if (IsEdlPnpId(info.PnpId))
            {
                return true;
            }

            return IsEdlDescriptor(info.Description);
        }

        private static bool IsEdlDescriptor(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string upper = text.ToUpperInvariant();
            foreach (var keyword in EdlDescriptorKeywords)
            {
                if (upper.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEdlPnpId(string pnpId)
        {
            if (string.IsNullOrWhiteSpace(pnpId))
            {
                return false;
            }

            string upper = pnpId.ToUpperInvariant();
            if (!upper.Contains("VID_05C6"))
            {
                return false;
            }

            foreach (var pid in KnownEdlPids)
            {
                if (upper.Contains(pid))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeComPort(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.ToUpperInvariant();
            }

            return ExtractComPortFromText(trimmed);
        }

        private static string ExtractComPortFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = ComPortRegex.Match(text);
            return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        }

        private static readonly string[] CommandFailureIndicators = new[]
        {
            "fail",
            "failed",
            "error",
            "unknown command",
            "not allowed",
            "permission denied",
            "waiting for device",
            "command not supported",
            "not supported",
            "device not found"
        };

        private bool CommandOutputIndicatesFailure(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return false;

            string normalized = output.ToLowerInvariant();
            foreach (var indicator in CommandFailureIndicators)
            {
                if (normalized.Contains(indicator))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureCommandSucceeded(string output, string context)
        {
            if (CommandOutputIndicatesFailure(output))
            {
                string detail = string.IsNullOrWhiteSpace(output) ? "命令无输出" : output.Trim();
                throw new Exception($"{context}失败: {detail}");
            }
        }

        private string EnsureDeviceStatus(string serial)
        {
            string status = GetDeviceStatus(serial);
            if (status == "adb" || status == "fastboot")
            {
                return status;
            }

            throw new Exception("设备不在ADB或Fastboot模式");
        }

        private async Task<string> GetCurrentSlotInternal(string serial)
        {
            string result = await RunCommandAsync($"fastboot -s {serial} getvar current-slot");
            if (!result.Contains("current-slot:"))
            {
                throw new Exception("获取槽位失败");
            }

            return result.Split(':')[1].Trim();
        }

        private void ReleaseComObject(object comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
            catch
            {
            }
        }
    }
}