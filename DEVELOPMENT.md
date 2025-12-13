# MultiFlash Tool 开发者指南

> ⚠️ **注意**: 本项目采用非商业许可，禁止任何形式的商业用途。

## 项目结构

```
MultiFlash Tool/
├── Form1.cs              # 主窗体 (7个功能模块)
├── Form1.Designer.cs     # 窗体设计器代码
├── DeviceManager.cs      # 设备管理器
├── Services/             # 服务层
│   ├── AppSettings.cs    # 配置管理
│   ├── ConfigManager.cs  # 配置管理器
│   ├── CryptoService.cs  # 加密服务
│   ├── DependencyManager.cs # 依赖管理
│   └── FlashTaskExecutor.cs # 刷写任务执行器
├── Qualcomm/             # 高通协议实现
│   ├── AutoFlasher.cs    # 自动刷机流程
│   ├── FirehoseClient.cs # Firehose 客户端
│   └── SaharaProtocol.cs # Sahara 协议
├── FastbootEnhance/      # Fastboot 增强
│   └── Payload.cs        # Payload 解析
└── Authentication/       # 认证模块
```

## Form1.cs 模块划分

| #region | 功能描述 |
|---------|---------|
| 日志功能 | 日志输出、文件记录 |
| 菜单初始化 | EDL 菜单事件绑定 |
| EDL 高级功能 | GPT 备份/恢复、内存操作 |
| 设备重启事件 | 重启到系统/Recovery/Fastboot |
| 文件选择事件 | 文件对话框处理 |
| 刷写操作 | 分区读写、刷机流程 |
| Payload 操作 | Payload 提取、Super 合并 |

## 编码规范

### 异常处理
```csharp
// 使用 SafeExecuteAsync 包装 async void 事件处理程序
SafeExecuteAsync(async () => {
    await SomeAsyncOperation();
}, "操作名称");

// 避免空 catch，添加日志
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[模块名] 操作失败: {ex.Message}");
}
```

### 配置管理
```csharp
// 使用 AppSettings 读取配置
var apiUrl = AppSettings.Instance.UpdateApiUrl;
var rsaKey = AppSettings.Instance.RsaPublicKey;
```

### 日志输出
```csharp
AppendLog("消息", Color.Black);   // 一般日志
AppendLog("成功", Color.Green);   // 成功
AppendLog("警告", Color.Orange);  // 警告
AppendLog("错误", Color.Red);     // 错误
```

## 依赖包

| 包名 | 版本 | 用途 |
|------|------|------|
| AntdUI | 2.2.1 | UI 框架 |
| SharpZipLib | 1.4.2 | Zip/BZip2 压缩 |
| System.Text.Json | 8.0.5 | JSON 序列化 |
| Newtonsoft.Json | 13.0.4 | JSON 兼容 |
| Google.Protobuf | 3.17.3 | Protobuf 支持 |

## 新功能开发流程

1. 在对应的 `#region` 中添加方法
2. 事件处理程序使用 `SafeExecuteAsync` 包装
3. 添加适当的日志输出
4. 运行 `dotnet build` 检查编译
5. 测试功能

## 注意事项

- **Form1.cs 很大** - 修改前先定位到对应的 #region
- **async void** - 仅用于事件处理程序，使用 SafeExecuteAsync
- **Thread.Sleep** - 仅在串口通信等后台线程中使用
- **配置** - 敏感信息存放在 config.json，不要硬编码
