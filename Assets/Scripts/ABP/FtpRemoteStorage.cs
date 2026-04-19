using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

/// <summary>
/// FTP / FTPS 传输实现，可用于编辑器上传 + 运行时下载。
/// </summary>
public class FtpRemoteStorage : IRemoteStorage
{
    private readonly string baseUrl;
    private readonly int port;
    private readonly string user;
    private readonly string password;
    private readonly bool useFtps;

    public string DisplayName => useFtps ? "FTPS" : "FTP";

    public FtpRemoteStorage(string baseUrl, int port, string user, string password, bool useFtps)
    {
        this.baseUrl = (baseUrl ?? "").TrimEnd('/');
        this.port = port;
        this.user = user;
        this.password = password;
        this.useFtps = useFtps;
    }

    /// <summary>使用 ABHotUpdateConfig 默认配置构造</summary>
    public FtpRemoteStorage()
    {
        baseUrl = ABHotUpdateConfig.ServerBaseUrl.TrimEnd('/');
        port = ABHotUpdateConfig.FtpPort;
        user = ABHotUpdateConfig.FtpUser;
        password = ABHotUpdateConfig.FtpPassword;
        useFtps = ABHotUpdateConfig.UseFtps;
    }

    private string BuildUrl(string platformFolder, string fileName)
    {
        return baseUrl + ":" + port + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(fileName);
    }

    private FtpWebRequest CreateRequest(string url, string method)
    {
        FtpWebRequest req = (FtpWebRequest)WebRequest.Create(url);
        req.Credentials = new NetworkCredential(user, password);
        req.Proxy = null;
        req.KeepAlive = false;
        req.UsePassive = true;
        req.EnableSsl = useFtps;
        req.UseBinary = true;
        req.Method = method;
        return req;
    }

    public async Task<bool> UploadAsync(string localFilePath, string remoteFileName)
    {
        return await Task.Run(() =>
        {
            try
            {
                string platformFolder = ABHotUpdateConfig.GetPlatformFolder();
                string url = BuildUrl(platformFolder, remoteFileName);
                FtpWebRequest req = CreateRequest(url, WebRequestMethods.Ftp.UploadFile);
                req.ContentLength = new FileInfo(localFilePath).Length;

                using (Stream uploadStream = req.GetRequestStream())
                using (FileStream file = File.OpenRead(localFilePath))
                {
                    byte[] buf = new byte[2048];
                    int read = file.Read(buf, 0, buf.Length);
                    while (read > 0)
                    {
                        uploadStream.Write(buf, 0, read);
                        read = file.Read(buf, 0, buf.Length);
                    }
                }

                using (FtpWebResponse response = (FtpWebResponse)req.GetResponse())
                {
                    UnityEngine.Debug.Log(remoteFileName + " 上传成功 " + response.StatusDescription);
                }
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("FTP 上传失败 " + remoteFileName + ": " + ex.Message);
                return false;
            }
        });
    }

    public async Task<bool> DownloadAsync(string remoteFileName, string localFilePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                string platformFolder = ABHotUpdateConfig.GetPlatformFolder();
                string url = BuildUrl(platformFolder, remoteFileName);
                FtpWebRequest req = CreateRequest(url, WebRequestMethods.Ftp.DownloadFile);

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var file = File.Create(localFilePath))
                {
                    byte[] buf = new byte[2048];
                    int read;
                    while ((read = respStream.Read(buf, 0, buf.Length)) > 0)
                        file.Write(buf, 0, read);
                }
                UnityEngine.Debug.Log(remoteFileName + " FTP下载成功");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("FTP 下载失败 " + remoteFileName + ": " + ex.Message);
                return false;
            }
        });
    }

    public async Task<string> DownloadTextAsync(string remoteFileName)
    {
        return await Task.Run(() =>
        {
            try
            {
                string platformFolder = ABHotUpdateConfig.GetPlatformFolder();
                string url = BuildUrl(platformFolder, remoteFileName);
                FtpWebRequest req = CreateRequest(url, WebRequestMethods.Ftp.DownloadFile);

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var reader = new StreamReader(respStream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("FTP 下载文本失败 " + remoteFileName + ": " + ex.Message);
                return null;
            }
        });
    }

    // ===================== 编辑器专用：自定义平台目录 =====================

    /// <summary>编辑器上传：指定 baseUrl 和 platformFolder</summary>
    public async Task<bool> UploadAsync(string localFilePath, string remoteFileName, string overrideBaseUrl, string platformFolder)
    {
        return await Task.Run(() =>
        {
            try
            {
                string root = (string.IsNullOrEmpty(overrideBaseUrl) ? baseUrl : overrideBaseUrl.TrimEnd('/'));
                string url = root + ":" + port + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(remoteFileName);
                FtpWebRequest req = CreateRequest(url, WebRequestMethods.Ftp.UploadFile);
                req.ContentLength = new FileInfo(localFilePath).Length;

                using (Stream uploadStream = req.GetRequestStream())
                using (FileStream file = File.OpenRead(localFilePath))
                {
                    byte[] buf = new byte[2048];
                    int read = file.Read(buf, 0, buf.Length);
                    while (read > 0)
                    {
                        uploadStream.Write(buf, 0, read);
                        read = file.Read(buf, 0, buf.Length);
                    }
                }

                using (FtpWebResponse response = (FtpWebResponse)req.GetResponse())
                {
                    UnityEngine.Debug.Log(remoteFileName + " 上传成功 " + response.StatusDescription);
                }
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("FTP 上传失败 " + remoteFileName + ": " + ex.Message);
                return false;
            }
        });
    }

    /// <summary>编辑器下载：指定 baseUrl 和 platformFolder</summary>
    public async Task<bool> DownloadAsync(string remoteFileName, string localFilePath, string overrideBaseUrl, string platformFolder)
    {
        return await Task.Run(() =>
        {
            try
            {
                string root = (string.IsNullOrEmpty(overrideBaseUrl) ? baseUrl : overrideBaseUrl.TrimEnd('/'));
                string url = root + ":" + port + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(remoteFileName);
                FtpWebRequest req = CreateRequest(url, WebRequestMethods.Ftp.DownloadFile);

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var file = File.Create(localFilePath))
                {
                    byte[] buf = new byte[2048];
                    int read;
                    while ((read = respStream.Read(buf, 0, buf.Length)) > 0)
                        file.Write(buf, 0, read);
                }
                UnityEngine.Debug.Log(remoteFileName + " FTP下载成功");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("FTP 下载失败 " + remoteFileName + ": " + ex.Message);
                return false;
            }
        });
    }

    /// <summary>编辑器下载文本：指定 baseUrl 和 platformFolder</summary>
    public async Task<string> DownloadTextAsync(string remoteFileName, string overrideBaseUrl, string platformFolder)
    {
        return await Task.Run(() =>
        {
            try
            {
                string root = (string.IsNullOrEmpty(overrideBaseUrl) ? baseUrl : overrideBaseUrl.TrimEnd('/'));
                string url = root + ":" + port + "/AB/" + platformFolder + "/" + Uri.EscapeDataString(remoteFileName);
                FtpWebRequest req = CreateRequest(url, WebRequestMethods.Ftp.DownloadFile);

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var reader = new StreamReader(respStream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("FTP 下载文本失败 " + remoteFileName + ": " + ex.Message);
                return null;
            }
        });
    }
}
