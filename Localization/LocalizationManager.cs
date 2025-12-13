using System;
using System.Collections.Generic;
using System.Globalization;

namespace OPFlashTool.Localization
{
    public static class LocalizationManager
    {
        public static string CurrentLanguage { get; private set; }

        private static Dictionary<string, Dictionary<string, string>> _resources;

        static LocalizationManager()
        {
            // Detect system language
            var culture = CultureInfo.CurrentUICulture;
            if (culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                CurrentLanguage = "en";
            }
            else
            {
                CurrentLanguage = "zh"; // Default to Chinese
            }

            InitializeResources();
        }

        private static void InitializeResources()
        {
            _resources = new Dictionary<string, Dictionary<string, string>>
            {
                ["zh"] = new Dictionary<string, string>
                {
                    // Login Form
                    ["Title_Login"] = "用户登录 - OP Flash Tool",
                    ["Label_PleaseLogin"] = "请登录",
                    ["Label_Username"] = "用户名:",
                    ["Label_Password"] = "密码:",
                    ["Button_Login"] = "登录",
                    ["Status_Verifying"] = "正在验证...",
                    ["Error_EnterUserPass"] = "请输入用户名和密码",
                    ["Status_LoginSuccess"] = "登录成功！",
                    ["Error_LoginFailed"] = "登录失败: ",
                    
                    // Main Form
                    ["Msg_SessionExpired"] = "会话已过期或账户已被封禁，程序将退出。",
                    ["Title_SecurityWarning"] = "安全警告",
                    ["Log_StartScan"] = "开始扫描设备...",
                    ["Log_Rebooting"] = "正在重启设备...",
                    ["Log_RebootSuccess"] = "重启指令已发送",
                    ["Error_RebootFailed"] = "重启失败",
                    ["Button_Scanning"] = "扫描中({0})",
                    ["Log_WaitingDevice"] = "正在等待设备连接... {0}s",
                    ["Log_DeviceConnected"] = "设备已连接！",
                    ["Log_ScanTimeout"] = "扫描超时，未检测到设备。",
                    ["Log_ScanError"] = "端口扫描错误: ",
                    ["Log_DeviceDetected"] = "检测到设备: ",
                    ["Button_Refresh"] = "自动检测",
                    ["Label_Port"] = "端口:",
                    ["Label_CloudChip"] = "云端芯片:",
                    ["Button_Flash"] = "开始刷机",
                    ["Status_Ready"] = "就绪",
                    ["Msg_SelectFirmware"] = "请先选择固件！",
                    ["Msg_SelectPort"] = "请先选择端口！",
                    ["Log_DownloadStarted"] = "开始下载: {0}",
                    ["Log_DownloadComplete"] = "下载完成",
                    ["Log_DownloadError"] = "下载错误: ",
                    ["Log_Flashing"] = "正在刷入...",
                    ["Log_FlashComplete"] = "刷机完成！",
                    ["Log_FlashError"] = "刷机失败: ",
                    
                    // Designer
                    ["Title_MainForm"] = "OPPO EDL Flash Tool v1.0.0bata",
                    ["Header_Files"] = "▎ 引导和刷机包文件",
                    ["Label_Prog"] = "引导文件:",
                    ["Label_Digest"] = "Digest文件:",
                    ["Label_Sign"] = "Sign文件:",
                    ["Label_RawXml"] = "Raw XML:",
                    ["Label_PatchXml"] = "Patch XML:",
                    ["Header_Partitions"] = "▎ 设备分区表",
                    ["Placeholder_Search"] = "🔍 使用分区名称筛选",
                    ["Button_SelectAll"] = "全选",
                    ["Button_UnselectAll"] = "全不选",
                    ["Header_Logs"] = "▎ 操作日志",
                    ["Button_Read"] = "读取分区",
                    ["Button_Write"] = "写入分区",
                    ["Button_Erase"] = "擦除分区",
                    ["Button_Reboot"] = "重启设备",
                    ["Header_Check"] = "☐",
                    ["Header_Lun"] = "LUN",
                    ["Header_Name"] = "分区名称",
                    ["Header_Size"] = "大小",
                    ["Header_Start"] = "起始扇区",
                    ["Header_Sectors"] = "扇区数",
                    ["Header_Image"] = "镜像文件",
                    ["Header_Reboot"] = "▎ 重启到",
                    ["Button_RebootSystem"] = "重启到系统",
                    ["Button_RebootRec"] = "重启到Recovery",
                    ["Button_RebootFastboot"] = "重启到Fastboot",
                    ["Button_RebootEdl"] = "重启到EDL模式",
                    ["Header_Actions"] = "▎ 功能操作",
                    ["Label_Storage"] = "存储类型:",
                    ["Radio_Ufs"] = "UFS",
                    ["Radio_Emmc"] = "EMMC",
                    ["Check_NoProg"] = "不发引导",
                    ["Check_GenXml"] = "生成XML",
                    ["Check_ProtectLun5"] = "保护LUN5",
                    ["Check_EnableVip"] = "VIP验证",
                    ["Button_SendProg"] = "⚡ 发送引导",
                    ["Button_ReadGpt"] = "▦ 读取分区表",
                    ["Button_ReadPart"] = "⬆ 提取选中分区",
                    ["Button_WritePart"] = "⬇ 写入选中分区",
                    ["Button_ErasePart"] = "✖ 擦除选中分区",
                    ["Button_MergeSuper"] = "▤ 合并散包super",
                    ["Button_Browse"] = "选择",
                    ["Button_Copy"] = "复制日志",
                    
                    // Logic
                    ["Log_TaskRunning"] = "已有任务正在运行，请稍候...",
                    ["Log_TaskStart"] = "开始 {0}...",
                    ["Log_TaskComplete"] = "{0} 完成。",
                    ["Log_TaskCancelled"] = "{0} 已取消。",
                    ["Log_TaskError"] = "错误 ({0}): ",
                    ["Error_NoDevice"] = "未连接设备",
                    ["Error_NoProg"] = "请先选择引导文件",
                    ["Log_HandshakeStart"] = "正在启动原生协议握手...",
                    ["Log_PortOpen"] = "端口 {0} 已打开，开始 Sahara 握手...",
                    ["Log_ChipId"] = "[Info] 检测到芯片 ID: {0:X}",
                    ["Log_ChipName"] = "[Info] 识别为: {0}",
                    ["Log_AutoStorage"] = "[Info] 自动选择存储类型: {0}",
                    ["Error_SaharaFail"] = "错误: Sahara 引导失败！",
                    ["Log_FirehoseWait"] = "引导文件已上传，等待 Firehose 启动 (2s)...",
                    ["Log_VipStrategy"] = "正在执行验证策略: {0}...",
                    ["Error_VipFail"] = "错误: VIP 验证失败！",
                    ["Error_StorageConfig"] = "错误: 存储配置失败！",
                    ["Log_Ready"] = "握手与配置成功！设备已就绪。",
                    ["Error_Protocol"] = "原生协议异常: ",
                    ["Log_ReadGptStart"] = "开始读取分区表...",
                    ["Error_GptConfig"] = "存储配置失败，无法读取 GPT",
                    ["Log_VipGptSuccess"] = "[VIP] GPT LUN{0} 读取成功",
                    ["Log_GptSaved"] = "GPT LUN{0} 已保存 ({1} sectors)",
                    ["Error_GptRead"] = "读取 GPT 异常: ",
                    ["Log_SectorSize"] = "[Info] 检测到物理扇区大小: {0} bytes",
                    ["Log_GptComplete"] = "分区表读取完成，共找到 {0} 个分区",
                    ["Error_NoPartSelected"] = "未选择任何分区",
                    ["Log_ReadPartStart"] = "开始提取选中分区...",
                    ["Error_StorageConfigShort"] = "存储配置失败",
                    ["Log_ReadingPart"] = "顶针正在回读分区 {0} (LUN{1})...",
                    ["Log_ReadSuccess"] = "读取成功: {0}",
                    ["Log_ReadFail"] = "读取失败: {0}",
                    ["Log_GenXml"] = "正在生成 rawprogram.xml ...",
                    ["Log_XmlSuccess"] = "XML 生成成功: {0}",
                    ["Error_ReadPart"] = "提取分区异常: ",
                    ["Error_NoPartOrImage"] = "未选择任何分区或未指定镜像文件",
                    ["Log_WritePartStart"] = "开始写入选中分区...",
                    ["Log_SkipLun5Write"] = "[保护] 跳过 LUN5 写入: {0}",
                    ["Log_SkipFileMissing"] = "跳过 {0}: 文件不存在 ({1})",
                    ["Log_WritingPart"] = "顶针正在后入对应分区 {0} (LUN{1})...",
                    ["Log_AutoFindSector"] = "[Info] {0} 未指定起始扇区，尝试自动查找...",
                    ["Log_WriteSuccess"] = "写入成功: {0}",
                    ["Log_WriteFail"] = "写入失败: {0}",
                    ["Error_WritePart"] = "写入分区异常: ",
                    ["Log_ErasePartStart"] = "开始擦除选中分区...",
                    ["Log_SkipLun5Erase"] = "[保护] 跳过 LUN5 擦除: {0}",
                    ["Log_ErasingPart"] = "正在擦除 {0} (LUN{1})...",
                    ["Log_EraseSuccess"] = "擦除成功: {0}",
                    ["Log_EraseFail"] = "擦除失败: {0}",
                    ["Error_ErasePart"] = "擦除分区异常: ",
                    ["Log_ParseXml"] = "正在解析 XML: {0}...",
                    ["Warn_NoPartInXml"] = "警告: 未能在 XML 中找到有效的分区信息",
                    ["Log_XmlParsed"] = "XML 解析完成，已加载 {0} 个分区",
                    ["Option_Manual"] = "手动选择 (Manual)",
                    ["Error_LoadCloud"] = "无法加载云端机型列表: ",
                    ["Status_Downloading"] = "正在下载云端文件...",
                    ["Status_DownLoader"] = "正在下载 Loader: {0}",
                    ["Status_DownDigest"] = "正在下载 Digest: {0}",
                    ["Status_DownSig"] = "正在下载 Signature: {0}",
                    ["Status_CloudLoaded"] = "已加载云端机型: {0}",
                    ["Status_VipFiles"] = " (含VIP文件)",
                    ["Status_DownComplete"] = "下载完成",
                    ["Error_NoUrl"] = "无法获取文件下载地址。请检查服务器配置或数据库是否包含 digest/sig 字段。",
                    ["Status_GetUrlFail"] = "获取下载地址失败",
                    ["Error_Download"] = "下载失败: ",
                    ["Status_DownloadFail"] = "下载失败"
                },
                ["en"] = new Dictionary<string, string>
                {
                    // Login Form
                    ["Title_Login"] = "User Login - OP Flash Tool",
                    ["Label_PleaseLogin"] = "Please Login",
                    ["Label_Username"] = "Username:",
                    ["Label_Password"] = "Password:",
                    ["Button_Login"] = "Login",
                    ["Status_Verifying"] = "Verifying...",
                    ["Error_EnterUserPass"] = "Please enter username and password",
                    ["Status_LoginSuccess"] = "Login Successful!",
                    ["Error_LoginFailed"] = "Login Failed: ",

                    // Main Form
                    ["Msg_SessionExpired"] = "Session expired or account banned. Application will exit.",
                    ["Title_SecurityWarning"] = "Security Warning",
                    ["Log_StartScan"] = "Scanning for devices...",
                    ["Log_Rebooting"] = "Rebooting device...",
                    ["Log_RebootSuccess"] = "Reboot command sent",
                    ["Error_RebootFailed"] = "Reboot failed",
                    ["Button_Scanning"] = "Scanning({0})",
                    ["Log_WaitingDevice"] = "Waiting for device... {0}s",
                    ["Log_DeviceConnected"] = "Device connected!",
                    ["Log_ScanTimeout"] = "Scan timeout. No device detected.",
                    ["Log_ScanError"] = "Port scan error: ",
                    ["Log_DeviceDetected"] = "Device Detected: ",
                    ["Button_Refresh"] = "Auto Detect",
                    ["Label_Port"] = "Port:",
                    ["Label_CloudChip"] = "Cloud Chip:",
                    ["Button_Flash"] = "Start Flash",
                    ["Status_Ready"] = "Ready",
                    ["Msg_SelectFirmware"] = "Please select firmware first!",
                    ["Msg_SelectPort"] = "Please select a port!",
                    ["Log_DownloadStarted"] = "Download started: {0}",
                    ["Log_DownloadComplete"] = "Download complete",
                    ["Log_DownloadError"] = "Download error: ",
                    ["Log_Flashing"] = "Flashing...",
                    ["Log_FlashComplete"] = "Flash Complete!",
                    ["Log_FlashError"] = "Flash Failed: ",

                    // Designer
                    ["Title_MainForm"] = "OPPO EDL Flash Tool v1.0.0bata",
                    ["Header_Files"] = "▎ Boot & Flash Files",
                    ["Label_Prog"] = "Programmer:",
                    ["Label_Digest"] = "Digest:",
                    ["Label_Sign"] = "Sign:",
                    ["Label_RawXml"] = "Raw XML:",
                    ["Label_PatchXml"] = "Patch XML:",
                    ["Header_Partitions"] = "▎ Partition Table",
                    ["Placeholder_Search"] = "🔍 Filter partitions",
                    ["Button_SelectAll"] = "Select All",
                    ["Button_UnselectAll"] = "Unselect All",
                    ["Header_Logs"] = "▎ Operation Logs",
                    ["Button_Read"] = "Read",
                    ["Button_Write"] = "Write",
                    ["Button_Erase"] = "Erase",
                    ["Button_Reboot"] = "Reboot",
                    ["Header_Check"] = "☐",
                    ["Header_Lun"] = "LUN",
                    ["Header_Name"] = "Name",
                    ["Header_Size"] = "Size",
                    ["Header_Start"] = "Start Sector",
                    ["Header_Sectors"] = "Sectors",
                    ["Header_Image"] = "Image File",
                    ["Header_Reboot"] = "▎ Reboot To",
                    ["Button_RebootSystem"] = "Reboot System",
                    ["Button_RebootRec"] = "Reboot Recovery",
                    ["Button_RebootFastboot"] = "Reboot Fastboot",
                    ["Button_RebootEdl"] = "Reboot EDL",
                    ["Header_Actions"] = "▎ Actions",
                    ["Label_Storage"] = "Storage:",
                    ["Radio_Ufs"] = "UFS",
                    ["Radio_Emmc"] = "EMMC",
                    ["Check_NoProg"] = "No Prog",
                    ["Check_GenXml"] = "Gen XML",
                    ["Check_ProtectLun5"] = "Protect LUN5",
                    ["Check_EnableVip"] = "VIP Auth",
                    ["Button_SendProg"] = "⚡ Send Prog",
                    ["Button_ReadGpt"] = "▦ Read GPT",
                    ["Button_ReadPart"] = "⬆ Read Part",
                    ["Button_WritePart"] = "⬇ Write Part",
                    ["Button_ErasePart"] = "✖ Erase Part",
                    ["Button_MergeSuper"] = "▤ Merge Super",
                    ["Button_Browse"] = "Browse",
                    ["Button_Copy"] = "Copy Log",

                    // Logic
                    ["Log_TaskRunning"] = "Task already running, please wait...",
                    ["Log_TaskStart"] = "Starting {0}...",
                    ["Log_TaskComplete"] = "{0} Complete.",
                    ["Log_TaskCancelled"] = "{0} Cancelled.",
                    ["Log_TaskError"] = "Error ({0}): ",
                    ["Error_NoDevice"] = "No device connected",
                    ["Error_NoProg"] = "Please select programmer file first",
                    ["Log_HandshakeStart"] = "Starting native protocol handshake...",
                    ["Log_PortOpen"] = "Port {0} opened, starting Sahara handshake...",
                    ["Log_ChipId"] = "[Info] Detected Chip ID: {0:X}",
                    ["Log_ChipName"] = "[Info] Identified as: {0}",
                    ["Log_AutoStorage"] = "[Info] Auto-selected storage: {0}",
                    ["Error_SaharaFail"] = "Error: Sahara handshake failed!",
                    ["Log_FirehoseWait"] = "Programmer uploaded, waiting for Firehose (2s)...",
                    ["Log_VipStrategy"] = "Executing auth strategy: {0}...",
                    ["Error_VipFail"] = "Error: VIP Authentication failed!",
                    ["Error_StorageConfig"] = "Error: Storage configuration failed!",
                    ["Log_Ready"] = "Handshake & Config successful! Device ready.",
                    ["Error_Protocol"] = "Native Protocol Exception: ",
                    ["Log_ReadGptStart"] = "Reading Partition Table...",
                    ["Error_GptConfig"] = "Storage config failed, cannot read GPT",
                    ["Log_VipGptSuccess"] = "[VIP] GPT LUN{0} Read Success",
                    ["Log_GptSaved"] = "GPT LUN{0} Saved ({1} sectors)",
                    ["Error_GptRead"] = "Read GPT Exception: ",
                    ["Log_SectorSize"] = "[Info] Detected physical sector size: {0} bytes",
                    ["Log_GptComplete"] = "Partition table read complete, found {0} partitions",
                    ["Error_NoPartSelected"] = "No partitions selected",
                    ["Log_ReadPartStart"] = "Extracting selected partitions...",
                    ["Error_StorageConfigShort"] = "Storage config failed",
                    ["Log_ReadingPart"] = "Reading {0} (LUN{1})...",
                    ["Log_ReadSuccess"] = "Read Success: {0}",
                    ["Log_ReadFail"] = "Read Failed: {0}",
                    ["Log_GenXml"] = "Generating rawprogram.xml ...",
                    ["Log_XmlSuccess"] = "XML Generated: {0}",
                    ["Error_ReadPart"] = "Extract Partition Exception: ",
                    ["Error_NoPartOrImage"] = "No partition selected or image file missing",
                    ["Log_WritePartStart"] = "Writing selected partitions...",
                    ["Log_SkipLun5Write"] = "[Protect] Skipping LUN5 Write: {0}",
                    ["Log_SkipFileMissing"] = "Skipping {0}: File missing ({1})",
                    ["Log_WritingPart"] = "Writing {0} (LUN{1})...",
                    ["Log_AutoFindSector"] = "[Info] {0} start sector not specified, trying auto-detect...",
                    ["Log_WriteSuccess"] = "Write Success: {0}",
                    ["Log_WriteFail"] = "Write Failed: {0}",
                    ["Error_WritePart"] = "Write Partition Exception: ",
                    ["Log_ErasePartStart"] = "Erasing selected partitions...",
                    ["Log_SkipLun5Erase"] = "[Protect] Skipping LUN5 Erase: {0}",
                    ["Log_ErasingPart"] = "Erasing {0} (LUN{1})...",
                    ["Log_EraseSuccess"] = "Erase Success: {0}",
                    ["Log_EraseFail"] = "Erase Failed: {0}",
                    ["Error_ErasePart"] = "Erase Partition Exception: ",
                    ["Log_ParseXml"] = "Parsing XML: {0}...",
                    ["Warn_NoPartInXml"] = "Warning: No valid partition info found in XML",
                    ["Log_XmlParsed"] = "XML Parsed, loaded {0} partitions",
                    ["Option_Manual"] = "Manual Selection",
                    ["Error_LoadCloud"] = "Failed to load cloud chips: ",
                    ["Status_Downloading"] = "Downloading cloud files...",
                    ["Status_DownLoader"] = "Downloading Loader: {0}",
                    ["Status_DownDigest"] = "Downloading Digest: {0}",
                    ["Status_DownSig"] = "Downloading Signature: {0}",
                    ["Status_CloudLoaded"] = "Loaded Cloud Chip: {0}",
                    ["Status_VipFiles"] = " (With VIP Files)",
                    ["Status_DownComplete"] = "Download Complete",
                    ["Error_NoUrl"] = "Failed to get download URLs. Check server config.",
                    ["Status_GetUrlFail"] = "Failed to get URLs",
                    ["Error_Download"] = "Download Failed: ",
                    ["Status_DownloadFail"] = "Download Failed"
                }
            };
        }

        public static string GetString(string key)
        {
            if (_resources.ContainsKey(CurrentLanguage) && _resources[CurrentLanguage].ContainsKey(key))
            {
                return _resources[CurrentLanguage][key];
            }
            // Fallback to Chinese if key missing in current language
            if (_resources["zh"].ContainsKey(key))
            {
                return _resources["zh"][key];
            }
            return key; // Return key itself if not found
        }
        
        // Helper for formatted strings
        public static string GetString(string key, params object[] args)
        {
            string format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
