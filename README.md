# Unity AssetBundle 热更新框架

一套完整的 Unity AssetBundle 构建与热更新解决方案，涵盖编辑器端 AB 包构建/上传/对比、运行时增量下载/MD5 校验/资源加载，支持 FTP 与 HTTP/CDN 双协议。

---

## 目录

- [架构总览](#架构总览)
- [文件结构](#文件结构)
- [编辑器端 — ABTools](#编辑器端--abtools)
- [运行时端 — ABP](#运行时端--abp)
- [传输协议抽象](#传输协议抽象)
- [对比文件格式](#对比文件格式)
- [快速上手](#快速上手)
- [配置说明](#配置说明)
- [扩展自定义协议](#扩展自定义协议)
- [注意事项](#注意事项)

---

## 架构总览

```
┌──────────────────────────────────────────────────────┐
│               Editor / ABTools.cs                    │
│          （开发阶段 · 编辑器工具 · 仅开发者使用）          │
│                                                      │
│  构建 AB 包 → 生成对比文件 → 上传到远端服务器             │
│      ↓ 产生         ↓ 产生          ↓ 传输            │
│  ArtRes/AB/{PC}/  ABCompareInfo.txt  FTP/HTTP 服务器  │
└───────────────────────────┬──────────────────────────┘
                            │  产物（AB 包 + 对比文件）
                            ↓
┌──────────────────────────────────────────────────────┐
│          Scripts/ABP/ （运行时 · 打包进游戏 · 玩家用）    │
│                                                      │
│  ABUpdateMgr  ← 下载对比文件 → 对比差异 → 增量下载 AB 包 │
│       ↓                                              │
│  ABMgr        ← 从本地加载 AB 包给游戏逻辑使用            │
└──────────────────────────────────────────────────────┘

              ┌──── IRemoteStorage ────┐
              │                        │
     FtpRemoteStorage       HttpRemoteStorage
     (FtpWebRequest)        (UnityWebRequest)
```

**核心思路**：Editor 端是「生产者」——负责构建 AB 包、生成对比文件、上传到远端；运行时是「消费者」——从远端下载对比文件、对比差异、增量下载 AB 包、最终加载给游戏使用。两端通过统一的传输协议接口 `IRemoteStorage` 对齐。

---

## 文件结构

```
Assets/
├── Editor/
│   └── ABTools.cs              # 编辑器工具窗口（构建/上传/下载/对比）
│
└── Scripts/ABP/
    ├── ABHotUpdateConfig.cs    # 热更新集中配置（协议、服务器、凭据、工厂方法）
    ├── ABUpdateMgr.cs          # 运行时热更新管理器（下载对比文件、增量下载、MD5 校验）
    ├── ABMgr.cs                # AB 包加载管理器（同步/异步加载、依赖处理、单例）
    ├── ABHashUtil.cs           # MD5 文件哈希工具
    ├── IRemoteStorage.cs       # 传输协议接口
    ├── FtpRemoteStorage.cs     # FTP/FTPS 传输实现
    └── HttpRemoteStorage.cs    # HTTP/HTTPS/CDN 传输实现
```

---

## 编辑器端 — ABTools

通过 Unity 菜单 `AB包工具 → 打开工具窗口` 打开，提供以下功能：

### 平台选择

Toolbar 切换 **PC / IOS / Android** 三种构建目标，所有操作均基于当前选中的平台。

### 构建 AB 包

| 按钮 | 说明 |
|------|------|
| 构建当前平台 AB 包 | 调用 `BuildPipeline.BuildAssetBundles`，输出到 `Assets/ArtRes/AB/{平台}/` |
| 构建所有平台 | 依次构建 PC、iOS、Android 三个平台 |

### 资源服务器配置

- **协议选择**：Toolbar 切换 `FTP` / `HTTP`
- **服务器地址**：手动输入，FTP 须 `ftp://` 前缀，HTTP 须 `http://` 或 `https://` 前缀
- **配置管理**：
  - 「保存当前配置」：将当前地址和协议保存为命名配置，持久化到 EditorPrefs
  - 「删除选中配置」：删除后自动重新编号，保持配置名连续
  - 「配置切换」：下拉选择已保存的配置，一键切换服务器

### 操作按钮

| 按钮 | 说明 |
|------|------|
| 创建对比文件 | 扫描 `ArtRes/AB/{平台}/` 下所有无扩展名的 AB 包文件，计算 MD5，生成 `ABCompareInfo.txt` |
| 保存到 StreamingAssets | 将 Project 中选中的 AB 资源复制到 StreamingAssets，生成首包对比文件 |
| 上传选中文件 | 将文件列表中勾选的 AB 包逐个上传到远端服务器，带进度条、耗时统计、上传历史 |
| 下载远端到本地 | 从远端下载 `ABCompareInfo.txt` 获取文件列表，然后全量下载所有 AB 包到 `ArtRes/AB/{平台}/` |
| 对比远端差异 | 下载远端对比文件，与本地 AB 包 MD5 对比，显示「新增 / 修改 / 远端多余 / 不变」差异列表 |

### AB 包文件列表

列出当前平台目录下所有 AB 包文件，显示文件名、大小、MD5，支持：
- 勾选/取消勾选单个文件
- 全选 / 全不选
- 实时显示已选文件数和总大小

### 远端对比预览

以颜色标记显示差异结果：

| 标记 | 含义 | 颜色 |
|------|------|------|
| ＋ 新增 | 本地有、远端没有 | 绿色 |
| ～ 修改 | 两端都有但 MD5 不同 | 黄色 |
| ✕ 远端多余 | 远端有、本地没有 | 红色 |
| = 不变 | 两端完全一致 | 默认 |

### 上传历史

记录最近 20 次上传操作，显示时间、平台、文件数、总大小、耗时、成功/失败状态。

### 底部状态条

实时显示当前操作进度、进度条、详细计数。

---

## 运行时端 — ABP

### ABHotUpdateConfig — 集中配置

所有热更新相关参数集中管理，便于统一修改和接入 CI：

| 配置项 | 类型 | 说明 |
|--------|------|------|
| `CurrentStorageType` | `StorageType` 枚举 | `FTP` 或 `HTTP`，决定使用哪种传输协议 |
| `ServerBaseUrl` | `string` | 资源根地址，FTP 如 `ftp://127.0.0.1`，HTTP 如 `http://cdn.example.com` |
| `FtpPort` | `int` | FTP 端口，默认 `2121` |
| `FtpUser` | `string` | FTP 用户名 |
| `FtpPassword` | `string` | FTP 密码 |
| `UseFtps` | `bool` | 是否启用 FTPS |
| `HttpToken` | `string` | HTTP 认证 Token（可选），如 `"Bearer xxx"`，CDN 场景留空 |

关键方法：
- `GetPlatformFolder()` — 根据编译宏返回 `"PC"` / `"IOS"` / `"Android"`
- `BuildFileUrl(fileName)` — 按当前协议拼接远端文件完整 URL
- `CreateStorage()` — 工厂方法，按当前配置创建 `IRemoteStorage` 实例

> **注意**：正式环境请改用服务器下发配置或加密存储，勿将真实密码提交仓库。

### ABUpdateMgr — 热更新管理器

单例 MonoBehaviour，负责运行时的增量更新流程：

```
CheckUpdate(overCallBack, updateInfoCallBack)
  │
  ├─ 1. 下载远端 ABCompareInfo.txt → 保存为 ABCompareInfo_TMP.txt
  ├─ 2. 解析远端对比文件 → remoteABInfo
  ├─ 3. 加载本地 ABCompareInfo.txt（persistentDataPath → streamingAssetsPath）
  ├─ 4. 对比差异：本地缺失 或 MD5 不一致 → 加入下载列表
  ├─ 5. 删除本地多余的 AB 包文件
  ├─ 6. 增量下载差异 AB 包（失败自动重试，最多 5 轮）
  ├─ 7. 下载后 MD5 校验，不一致则删除并重试
  └─ 8. 用最新远端对比文件覆盖本地 ABCompareInfo.txt
```

特性：
- **增量更新**：只下载有差异的文件，不重复下载已有且未变的 AB 包
- **MD5 校验**：下载后验证文件完整性，校验失败自动重试
- **自动重试**：整轮下载失败最多重试 5 次
- **线程安全**：通过 `Queue<Action>` + `RunOnMainThread` 将回调调度到主线程
- **协议切换**：通过 `SetStorage()` 可运行时动态切换传输协议

### ABMgr — AB 包加载管理器

继承自 `SingletonAutoMono<ABMgr>` 的单例 MonoBehaviour，负责从本地加载 AB 包资源：

- **路径解析**：优先加载 `persistentDataPath`（热更新下载的包），回退到 `streamingAssetsPath`（首包）
- **依赖处理**：自动加载主包获取 `AssetBundleManifest`，解析并加载所有依赖包
- **重复加载保护**：通过 `Dictionary<string, AssetBundle>` 缓存已加载的 AB 包
- **同步加载**：`LoadRes(abName, resName)` / `LoadRes(abName, resName, Type)` / `LoadRes<T>(abName, resName)`
- **异步加载**：`LoadResAsync(abName, resName, callBack)` 等泛型/Type 重载
- **GameObject 自动实例化**：加载的资源如果是 GameObject，自动 `Instantiate` 后返回
- **卸载**：`UnLoad(abName)` 卸载单个包，`ClearAB()` 卸载所有包

### ABHashUtil — MD5 工具

提供 `ComputeMD5File(string fullPath)` 方法，使用 `System.Security.Cryptography.MD5` 计算文件流哈希，返回小写十六进制字符串。Editor 端和运行时共用。

---

## 传输协议抽象

### IRemoteStorage 接口

```csharp
public interface IRemoteStorage
{
    Task<bool> UploadAsync(string localFilePath, string remoteFileName);
    Task<bool> DownloadAsync(string remoteFileName, string localFilePath);
    Task<string> DownloadTextAsync(string remoteFileName);
    string DisplayName { get; }
}
```

### FtpRemoteStorage

- 基于 `FtpWebRequest` 实现
- 支持 FTP / FTPS（`EnableSsl`）
- 运行时和编辑器通用，提供两组方法：
  - 基础方法：自动从 `ABHotUpdateConfig` 读取 baseUrl、平台目录
  - 编辑器专用重载：可传入自定义 `overrideBaseUrl` 和 `platformFolder`，支持编辑器多平台切换
- URL 格式：`{baseUrl}:{port}/AB/{platformFolder}/{fileName}`

### HttpRemoteStorage

- 基于 `UnityWebRequest` 实现，全平台兼容（含 IL2CPP）
- 支持 Bearer Token 认证
- 同样提供基础方法 + 编辑器专用重载
- URL 格式：`{baseUrl}/AB/{platformFolder}/{fileName}`（无端口号）

### 工厂方法

```csharp
// 使用 ABHotUpdateConfig 当前配置创建
IRemoteStorage storage = ABHotUpdateConfig.CreateStorage();

// 指定协议和地址创建
IRemoteStorage storage = ABHotUpdateConfig.CreateStorage(StorageType.HTTP, "http://cdn.example.com");

// 运行时动态切换
ABUpdateMgr.Instance.SetStorage(new HttpRemoteStorage("http://cdn.example.com"));
```

---

## 对比文件格式

`ABCompareInfo.txt` 是 Editor 端和运行时端之间的核心桥梁：

```
文件名 大小 MD5|文件名 大小 MD5|...
```

示例：

```
model 1234567 d41d8cd98f00b204e9800998ecf8427e|texture 987654 e99a18c428cb38d5f260853678922e03
```

- 每个条目以 `|` 分隔
- 条目内以空格分隔三个字段：文件名、字节数、MD5 哈希
- 无扩展名的 AB 包文件才会被收录
- Editor 端通过「创建对比文件」按钮生成
- 运行时通过 `ABUpdateMgr.ParseABCompareInfo()` 解析

---

## 快速上手

### 1. 编辑器端：构建并上传

1. 打开工具窗口：菜单 `AB包工具 → 打开工具窗口`
2. 选择目标平台（PC / IOS / Android）
3. 点击「构建当前平台 AB 包」
4. 点击「创建对比文件」生成 `ABCompareInfo.txt`
5. 选择协议（FTP / HTTP），输入服务器地址
6. 在文件列表中勾选要上传的文件
7. 点击「上传选中文件」

### 2. 运行时端：检测并更新

```csharp
ABUpdateMgr.Instance.CheckUpdate(
    overCallBack: isOver => Debug.Log("更新完成: " + isOver),
    updateInfoCallBack: info => Debug.Log(info)
);
```

### 3. 运行时端：加载资源

```csharp
// 同步加载
var prefab = ABMgr.Instance.LoadRes<GameObject>("model", "MyModel");

// 异步加载
ABMgr.Instance.LoadResAsync<GameObject>("model", "MyModel", obj => {
    // 使用 obj
});
```

### 4. 制作首包

1. 在 Project 窗口选中要打包进首包的 AB 资源
2. 点击「保存到 StreamingAssets」
3. 构建玩家安装包，首包将包含这些 AB 资源

---

## 配置说明

修改 `ABHotUpdateConfig.cs` 中的静态字段即可切换运行时默认配置：

```csharp
// 切换为 HTTP/CDN 模式
ABHotUpdateConfig.CurrentStorageType = ABHotUpdateConfig.StorageType.HTTP;
ABHotUpdateConfig.ServerBaseUrl = "http://cdn.example.com";
ABHotUpdateConfig.HttpToken = "Bearer my-token";
```

```csharp
// 切换为 FTP 模式
ABHotUpdateConfig.CurrentStorageType = ABHotUpdateConfig.StorageType.FTP;
ABHotUpdateConfig.ServerBaseUrl = "ftp://192.168.1.100";
ABHotUpdateConfig.FtpPort = 2121;
ABHotUpdateConfig.FtpUser = "admin";
ABHotUpdateConfig.FtpPassword = "password";
ABHotUpdateConfig.UseFtps = false;
```

编辑器端的服务器配置保存在 EditorPrefs 中，与运行时配置独立，互不影响。

---

## 扩展自定义协议

如需支持其他传输协议（如 OSS、S3 等），只需：

1. 实现 `IRemoteStorage` 接口：

```csharp
public class OssRemoteStorage : IRemoteStorage
{
    public string DisplayName => "OSS";

    public async Task<bool> UploadAsync(string localFilePath, string remoteFileName)
    {
        // 实现上传逻辑
    }

    public async Task<bool> DownloadAsync(string remoteFileName, string localFilePath)
    {
        // 实现下载逻辑
    }

    public async Task<string> DownloadTextAsync(string remoteFileName)
    {
        // 实现文本下载逻辑
    }
}
```

2. 在 `ABHotUpdateConfig.StorageType` 枚举中添加新类型，并更新 `CreateStorage()` 工厂方法。

3. 编辑器端如需 Toolbar 切换，在 `ABTools.cs` 的 `storageTypeNames` 和 `CreateEditorStorage()` 中添加对应分支。

---

## 注意事项

- **密码安全**：`ABHotUpdateConfig` 中的 FTP 凭据为明文，正式环境请改用服务器下发或加密存储
- **AB 包文件名**：无扩展名的文件才会被识别为 AB 包，`.txt` 和其他扩展名文件会被对比文件和文件列表自动排除
- **平台目录**：远端和本地的平台目录名（PC / IOS / Android）必须一致，由 `ABHotUpdateConfig.GetPlatformFolder()` 统一管理
- **主线程限制**：`UnityWebRequest` 必须在主线程启动，`HttpRemoteStorage` 已通过 `await Task.Yield()` 处理；`FtpRemoteStorage` 内部使用 `Task.Run` 在子线程执行
- **Unity 版本**：建议 Unity 2020.3 或更高版本（`UnityWebRequest` API 稳定版）
- **IL2CPP 兼容**：`HttpRemoteStorage` 使用 `UnityWebRequest`，天然兼容 IL2CPP；`FtpRemoteStorage` 使用 `System.Net.FtpWebRequest`，在部分 IL2CPP 平台上可能需要额外配置
