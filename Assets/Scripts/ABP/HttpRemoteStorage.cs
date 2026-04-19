using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// HTTP / HTTPS / CDN 传输实现。
/// 运行时使用 UnityWebRequest，与所有平台（含 IL2CPP）兼容。
/// </summary>
public class HttpRemoteStorage : IRemoteStorage
{
    private readonly string baseUrl;
    private readonly string token;

    public string DisplayName => "HTTP/CDN";

    /// <summary>
    /// </summary>
    /// <param name="baseUrl">资源根，如 http://cdn.example.com/AB</param>
    /// <param name="token">可选 Authorization 头，如 "Bearer xxx"</param>
    public HttpRemoteStorage(string baseUrl, string token = null)
    {
        this.baseUrl = (baseUrl ?? "").TrimEnd('/');
        this.token = token;
    }

    /// <summary>使用 ABHotUpdateConfig 默认配置构造</summary>
    public HttpRemoteStorage()
    {
        baseUrl = ABHotUpdateConfig.ServerBaseUrl.TrimEnd('/');
        token = ABHotUpdateConfig.HttpToken;
    }

    private string BuildUrl(string platformFolder, string fileName)
    {
        return baseUrl + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(fileName);
    }

    public async Task<bool> UploadAsync(string localFilePath, string remoteFileName)
    {
        string platformFolder = ABHotUpdateConfig.GetPlatformFolder();
        string url = BuildUrl(platformFolder, remoteFileName);
        byte[] data = File.ReadAllBytes(localFilePath);

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", token);
            req.SendWebRequest();
            while (!req.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.Log(remoteFileName + " HTTP上传成功");
                return true;
            }
            UnityEngine.Debug.LogError("HTTP 上传失败 " + remoteFileName + ": " + req.error);
            return false;
        }
    }

    public async Task<bool> DownloadAsync(string remoteFileName, string localFilePath)
    {
        string platformFolder = ABHotUpdateConfig.GetPlatformFolder();
        string url = BuildUrl(platformFolder, remoteFileName);

        using (var req = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", token);
            req.SendWebRequest();
            while (!req.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string dir = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(localFilePath, req.downloadHandler.data);
                UnityEngine.Debug.Log(remoteFileName + " HTTP下载成功");
                return true;
            }
            UnityEngine.Debug.LogError("HTTP 下载失败 " + remoteFileName + ": " + req.error);
            return false;
        }
    }

    public async Task<string> DownloadTextAsync(string remoteFileName)
    {
        string platformFolder = ABHotUpdateConfig.GetPlatformFolder();
        string url = BuildUrl(platformFolder, remoteFileName);

        using (var req = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", token);
            req.SendWebRequest();
            while (!req.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
                return req.downloadHandler.text;

            UnityEngine.Debug.LogWarning("HTTP 下载文本失败 " + remoteFileName + ": " + req.error);
            return null;
        }
    }

    // ===================== 编辑器专用：自定义平台目录 =====================

    public async Task<bool> UploadAsync(string localFilePath, string remoteFileName, string overrideBaseUrl, string platformFolder)
    {
        string root = (string.IsNullOrEmpty(overrideBaseUrl) ? baseUrl : overrideBaseUrl.TrimEnd('/'));
        string url = root + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(remoteFileName);
        byte[] data = File.ReadAllBytes(localFilePath);

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", token);
            req.SendWebRequest();
            while (!req.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.Log(remoteFileName + " HTTP上传成功");
                return true;
            }
            UnityEngine.Debug.LogError("HTTP 上传失败 " + remoteFileName + ": " + req.error);
            return false;
        }
    }

    public async Task<bool> DownloadAsync(string remoteFileName, string localFilePath, string overrideBaseUrl, string platformFolder)
    {
        string root = (string.IsNullOrEmpty(overrideBaseUrl) ? baseUrl : overrideBaseUrl.TrimEnd('/'));
        string url = root + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(remoteFileName);

        using (var req = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", token);
            req.SendWebRequest();
            while (!req.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string dir = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(localFilePath, req.downloadHandler.data);
                UnityEngine.Debug.Log(remoteFileName + " HTTP下载成功");
                return true;
            }
            UnityEngine.Debug.LogError("HTTP 下载失败 " + remoteFileName + ": " + req.error);
            return false;
        }
    }

    public async Task<string> DownloadTextAsync(string remoteFileName, string overrideBaseUrl, string platformFolder)
    {
        string root = (string.IsNullOrEmpty(overrideBaseUrl) ? baseUrl : overrideBaseUrl.TrimEnd('/'));
        string url = root + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(remoteFileName);

        using (var req = UnityWebRequest.Get(url))
        {
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", token);
            req.SendWebRequest();
            while (!req.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
                return req.downloadHandler.text;

            UnityEngine.Debug.LogWarning("HTTP 下载文本失败 " + remoteFileName + ": " + req.error);
            return null;
        }
    }
}
