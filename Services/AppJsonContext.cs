using System.Text.Json.Serialization;
using OPFlashTool.Services;

namespace OPFlashTool
{
    // 云端功能已移除 - 保留空实现以保持编译兼容性
    [JsonSerializable(typeof(ApiListResponse))]
    [JsonSerializable(typeof(ApiUrlResponse))]
    [JsonSerializable(typeof(ApiUpdateResponse))]
    [JsonSerializable(typeof(ApiAnnouncementResponse))]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(CryptoService.EncryptedRequest))]
    [JsonSerializable(typeof(SuperDef))]
    [JsonSerializable(typeof(CloudChipService.ChipListPayload))]
    [JsonSerializable(typeof(CloudChipService.ChipUrlPayload))]
    [JsonSerializable(typeof(CloudChipService.AnnouncementPayload))]
    [JsonSerializable(typeof(CloudChipService.UpdateCheckPayload))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
