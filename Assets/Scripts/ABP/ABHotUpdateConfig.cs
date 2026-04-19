/// <summary>
/// 热更与 FTP 的可调参数集中在一处，便于替换环境与接入 CI。
/// 正式环境请改用服务器下发配置或加密存储，勿将真实密码提交仓库。
/// </summary>
public static class ABHotUpdateConfig
{
    public const string FtpHost = "127.0.0.1";
    public const int FtpPort = 2121;

    public const string FtpUser = "Coolcoolcoo";
    public const string FtpPassword = "Coolcoolcoo123";

    /// <summary>
    /// 资源根，须带 ftp:// 前缀，例如 ftp://127.0.0.1
    /// </summary>
    public static string ServerBaseUrl = "ftp://127.0.0.1";

    public static bool UseFtps = false;

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

    /// <summary>运行时：按当前编译平台拼接远端路径。</summary>
    public static string BuildFtpFileUrl(string fileName)
    {
        return BuildFtpFileUrl(ServerBaseUrl, GetPlatformFolder(), fileName);
    }

    /// <summary>编辑器或自定义服务器地址、平台目录时使用。</summary>
    public static string BuildFtpFileUrl(string baseUrl, string platformFolder, string fileName)
    {
        string root = string.IsNullOrEmpty(baseUrl) ? ServerBaseUrl : baseUrl.TrimEnd('/');
        return root + ":" + FtpPort + "/AB/" + platformFolder + "/" +
               System.Uri.EscapeDataString(fileName);
    }
}
