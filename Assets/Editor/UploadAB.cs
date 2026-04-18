using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
public class UploadAB
{
    // 本机 FileZilla Server（你已改端口为 2121）
    // private const string FtpHost = "127.0.0.1";
    // private const int FtpPort = 2121;
    // private const string FtpRemoteDir = "/AB/PC/";
    // private const string FtpUser = "Coolcoolcoo";
    // private const string FtpPassword = "Coolcoolcoo123";

    // 503 Use AUTH first 说明服务器要求显式 FTPS（AUTH TLS）
    // private static bool UseFtps = false;
    // 测试环境自签名证书可放行；正式环境请设为 false
    // private static bool AllowInsecureCertificate = true;


    // [MenuItem("AB包工具/上传AB包和对比文件")]
    private static void UploadAllABFile()
    {
        //获取文件夹信息
        DirectoryInfo directory = Directory.CreateDirectory(Application.dataPath + "/ArtRes/AB/PC/");
        FileInfo[] fileInfos = directory.GetFiles();


        foreach (FileInfo info in fileInfos)
        {
            //没有后缀名的才是AB包
            if (info.Extension == "" || info.Extension == ".txt")
            {
                FtpUploadFile(info.FullName, info.Name);
            }
            // Debug.Log("**********************");
            // Debug.Log("文件名" + info.Name);
            // Debug.Log("文件路径" + info.FullName);
            // Debug.Log("文件后缀名" + info.Extension);
            // Debug.Log("文件大小" + info.Length);
        }

    }
    private async static void FtpUploadFile(string filePath, string fileName)
    {
        await Task.Run(() =>
        {
            try
            {
                // if (AllowInsecureCertificate)
                // {
                //     ServicePointManager.ServerCertificateValidationCallback =
                //         (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
                // }

                // 创建 FTP/FTPS 链接
                // string url = $"ftp://{FtpHost}:{FtpPort}{FtpRemoteDir}{Uri.EscapeDataString(fileName)}";
                string url = "ftp://127.0.0.1:2121/AB/PC/" + Uri.EscapeDataString(fileName);
                FtpWebRequest req = WebRequest.Create(url) as FtpWebRequest;
                if (req == null)
                    throw new InvalidOperationException("无法创建 FtpWebRequest，请检查 ftp:// 地址和端口。");

                // 凭证
                req.Credentials = new NetworkCredential("Coolcoolcoo", "Coolcoolcoo123");
                //其他设置
                //设置代理为null
                req.Proxy = null;
                //请求完毕后，是否关闭控制连接
                req.KeepAlive = false;
                // 被动模式更稳
                req.UsePassive = true;
                // 启用 FTPS（显式 AUTH TLS）
                req.EnableSsl = false;
                //操作命令上传
                req.Method = WebRequestMethods.Ftp.UploadFile;
                //指定传输的类型 2进制S
                req.UseBinary = true;
                //上传文件
                //ftp的流对象
                req.ContentLength = new FileInfo(filePath).Length;

                using (Stream upLoadStream = req.GetRequestStream())
                using (FileStream file = File.OpenRead(filePath))
                {
                    //一点一点的上传内容
                    byte[] bytes = new byte[2048];
                    //返回值 代表读取了多少个字符
                    int contentLength = file.Read(bytes, 0, bytes.Length);
                    //循环上传文件的数据
                    while (contentLength != 0)
                    {
                        //写入到上传流
                        upLoadStream.Write(bytes, 0, contentLength);
                        //写完再读
                        contentLength = file.Read(bytes, 0, bytes.Length);
                    }
                }
                using (FtpWebResponse response = (FtpWebResponse)req.GetResponse())
                {
                    Debug.Log(fileName + " 上传成功 " + response.StatusDescription);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("上传失败" + ex.Message);
            }
        });



    }

}
