/// <summary>
/// 热更可调参数集中在一处，便于替换环境与接入 CI。
/// 正式环境请改用服务器下发配置或加密存储，勿将真实密码提交仓库。
/// </summary>
public static class ABHotUpdateConfig
{
    // ===================== 协议类型 =====================

    /// <summary>远端存储协议类型</summary>
    public enum StorageType
    {
        FTP,
        HTTP
    }

    /// <summary>当前使用的存储协议，默认 FTP</summary>
    public static StorageType CurrentStorageType = StorageType.FTP;

    // ===================== FTP 专用配置 =====================

    public const int FtpPort = 2121;

    public const string FtpUser = "Coolcoolcoo";

    public const string FtpPassword = "Coolcoolcoo123";

    public static bool UseFtps = false;

    // ===================== HTTP 专用配置 =====================

    /// <summary>
    /// HTTP 认证 Token（可选），如 "Bearer xxx"。
    /// CDN 场景一般留空即可。
    /// </summary>
    public static string HttpToken = "";

    // ===================== 通用配置 =====================

    /// <summary>
    /// 资源根地址。
    /// FTP 须带 ftp:// 前缀，例如 ftp://127.0.0.1
    /// HTTP 须带 http:// 或 https:// 前缀，例如 http://cdn.example.com
    /// </summary>
    public static string ServerBaseUrl = "ftp://127.0.0.1";

    // ===================== 平台目录 =====================

    /// <summary>
    /// 构建远端路径中的平台目录名，需与编辑器 AB 输出目录一致。
    /// </summary>
    public static string GetPlatformFolder()
    {
#if UNITY_IOS
        return "IOS";
#elif UNITY_ANDROID
        return "Android";
#else
        return "PC";
#endif
    }

    // ===================== URL 构建 =====================

    /// <summary>运行时：按当前编译平台和协议类型拼接远端路径。</summary>
    public static string BuildFileUrl(string fileName)
    {
        return BuildFileUrl(ServerBaseUrl, GetPlatformFolder(), fileName);
    }

    /// <summary>编辑器或自定义服务器地址、平台目录时使用。</summary>
    public static string BuildFileUrl(string baseUrl, string platformFolder, string fileName)
    {
        string root = string.IsNullOrEmpty(baseUrl) ? ServerBaseUrl : baseUrl.TrimEnd('/');

        if (CurrentStorageType == StorageType.FTP)
            return root + ":" + FtpPort + "/AB/" + platformFolder + "/" +
                   System.Uri.EscapeDataString(fileName);
        else
            return root + "/AB/" + platformFolder + "/" +
                   System.Uri.EscapeDataString(fileName);
    }

    // ===================== 向后兼容 =====================

    /// <summary>向后兼容：等同于 BuildFileUrl</summary>
    public static string BuildFtpFileUrl(string fileName) => BuildFileUrl(fileName);

    /// <summary>向后兼容：等同于 BuildFileUrl</summary>
    public static string BuildFtpFileUrl(string baseUrl, string platformFolder, string fileName)
        => BuildFileUrl(baseUrl, platformFolder, fileName);

    // ===================== 存储工厂 =====================

    /// <summary>
    /// 根据当前配置创建对应的 IRemoteStorage 实例（运行时使用默认配置）。
    /// </summary>
    public static IRemoteStorage CreateStorage()
    {
        switch (CurrentStorageType)
        {
            case StorageType.HTTP:
                return new HttpRemoteStorage(ServerBaseUrl, HttpToken);
            default:
                return new FtpRemoteStorage(ServerBaseUrl, FtpPort, FtpUser, FtpPassword, UseFtps);
        }
    }

    /// <summary>
    /// 创建指定协议类型的存储实例（编辑器可传入自定义参数）。
    /// </summary>
    public static IRemoteStorage CreateStorage(StorageType type, string baseUrl)
    {
        switch (type)
        {
            case StorageType.HTTP:
                return new HttpRemoteStorage(baseUrl, HttpToken);
            default:
                return new FtpRemoteStorage(baseUrl, FtpPort, FtpUser, FtpPassword, UseFtps);
        }
    }
}
