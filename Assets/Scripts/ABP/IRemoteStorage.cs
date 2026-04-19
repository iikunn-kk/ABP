using System;
using System.Threading.Tasks;

/// <summary>
/// 远端存储传输协议抽象，FTP / HTTP / CDN 均实现此接口。
/// </summary>
public interface IRemoteStorage
{
    /// <summary>上传本地文件到远端</summary>
    /// <param name="localFilePath">本地文件完整路径</param>
    /// <param name="remoteFileName">远端文件名</param>
    /// <returns>是否成功</returns>
    Task<bool> UploadAsync(string localFilePath, string remoteFileName);

    /// <summary>从远端下载文件到本地</summary>
    /// <param name="remoteFileName">远端文件名</param>
    /// <param name="localFilePath">本地保存完整路径</param>
    /// <returns>是否成功</returns>
    Task<bool> DownloadAsync(string remoteFileName, string localFilePath);

    /// <summary>下载远端文件的文本内容（用于下载对比文件等小文件）</summary>
    /// <param name="remoteFileName">远端文件名</param>
    /// <returns>文件内容，失败返回 null</returns>
    Task<string> DownloadTextAsync(string remoteFileName);

    /// <summary>当前存储类型显示名</summary>
    string DisplayName { get; }
}
