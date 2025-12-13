// 云端功能已移除 - 此文件保留空实现以保持编译兼容性
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OPFlashTool.Services
{
    public class ChipInfo
    {
        public string ChipName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class ChipUrls
    {
        public string LoaderUrl { get; set; } = "";
        public string LoaderName { get; set; } = "";
        public string DigestUrl { get; set; } = "";
        public string DigestName { get; set; } = "";
        public string SignUrl { get; set; } = "";
        public string SignName { get; set; } = "";
        public string SigUrl { get; set; } = "";
        public string SigName { get; set; } = "";
    }

    public class DownloadProgress
    {
        public double Percent { get; set; }
        public double BytesPerSecond { get; set; }
    }

    public class CloudChipService
    {
        public class ChipListPayload { }
        public class ChipUrlPayload { }
        public class AnnouncementPayload { }
        public class UpdateCheckPayload { }

        public Task<List<ChipInfo>> GetChipsAsync() => Task.FromResult(new List<ChipInfo>());
        
        public Task<ChipUrls?> GetChipUrlsAsync(string chipName) => Task.FromResult<ChipUrls?>(null);
        
        public Task<string?> DownloadFileAsync(string url, string path, IProgress<DownloadProgress>? progress = null) 
            => Task.FromResult<string?>(null);
        
        public Task<string> GetAnnouncementAsync() => Task.FromResult("");
        
        public Task<AppUpdateInfo?> CheckAppUpdateAsync(string version) => Task.FromResult<AppUpdateInfo?>(null);
    }

    public class AppUpdateInfo
    {
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string Hash { get; set; } = "";
        public bool ForceUpdate { get; set; }
    }

    // API Response 类型 (空实现)
    public class ApiListResponse { }
    public class ApiUrlResponse { }
    public class ApiUpdateResponse { }
    public class ApiAnnouncementResponse { }
}
