using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public class ABTools : EditorWindow
{
    private int nowSelIndex = 0;
    private string[] targetStrings = new string[] { "PC", "IOS", "Android" };
    //资源服务器默认IP地址
    private string serverIP = "ftp://127.0.0.1";
    [MenuItem("AB包工具/打开工具窗口")]

    private static void OpenWindow()
    {
        ABTools window = EditorWindow.GetWindowWithRect(typeof(ABTools), new Rect(0, 0, 350, 220)) as ABTools;
        window.Show();
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 150, 15), "平台选择");
        nowSelIndex = GUI.Toolbar(new Rect(10, 30, 250, 20), nowSelIndex, targetStrings);

        GUI.Label(new Rect(10, 60, 150, 15), "资源服务器地址");
        serverIP = GUI.TextField(new Rect(10, 80, 150, 20), serverIP);

        //创建对比文件 按钮
        if (GUI.Button(new Rect(10, 110, 100, 40), "创建对比文件"))
            CreateABCompareFile();
        //保存默认资源到SteamingAssets 按钮
        if (GUI.Button(new Rect(115, 110, 225, 40), "保存默认资源到StreamingAssests"))
            MoveABToStreamingAssets();
        //上传AB包和对比文件 按钮
        if (GUI.Button(new Rect(10, 160, 330, 40), "上传AB包和对比文件"))
            UploadAllABFile();

    }

    //生成AB包对比文件
    public void CreateABCompareFile()
    {
        // 获取文件夹信息，创建或打开指定目录
        // Application.dataPath: Unity 项目的 Assets 文件夹路径
        // "/ArtRes/AB/PC": AB 包存储的相对路径
        // Directory.CreateDirectory(): 如果目录不存在则创建，如果存在则直接返回

        //根据选择的平台读取对应平台下的内容
        DirectoryInfo directory = Directory.CreateDirectory(Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex]);
        // 获取目录下的所有文件信息
        // GetFiles() 返回 FileInfo 数组，包含每个文件的详细信息（名称、大小、路径等）
        FileInfo[] fileInfos = directory.GetFiles();

        // 定义字符串变量，用于拼接 AB 包对比信息
        // 格式为：文件名 文件大小 MD5值|文件名 文件大小 MD5值|...
        string abCompareInfo = "";

        // 遍历目录中的所有文件
        foreach (FileInfo info in fileInfos)
        {
            // 没有后缀名的才是 AB 包
            // AB 包在 Unity 中构建时没有扩展名，通过检查扩展名是否为空来识别
            if (info.Extension == "")
            {
                // Debug.Log("文件名" + info.Name);
                // 将 AB 包信息拼接成字符串，格式为：文件名 文件大小 MD5值
                // info.Name: 文件名（如 "player"）
                // info.Length: 文件大小（字节数）
                // GetMD5(): 调用静态方法计算文件的 MD5 哈希值
                abCompareInfo += info.Name + " " + info.Length + " " + GetMD5(info.FullName);

                // 在每个 AB 包信息后添加竖线分隔符，用于后续拆分
                abCompareInfo += "|";

            }
            // Debug.Log("**********************");
            // Debug.Log("文件名" + info.Name);
            // Debug.Log("文件路径" + info.FullName);
            // Debug.Log("文件后缀名" + info.Extension);
            // Debug.Log("文件大小" + info.Length);
        }
        // 去掉最后一个字符（竖线），避免后续拆分时产生空字符串
        // Substring(0, length-1) 从开头截取到倒数第二个字符
        abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);

        // Debug.Log(abCompareInfo);

        // 存储拼接好的 AB 包资源信息到文件
        // Application.dataPath: Unity 项目的 Assets 文件夹路径
        // "/ArtRes/AB/PC/ABCompareInfo.txt": 对比文件的保存路径
        // WriteAllText() 如果文件不存在则创建，如果存在则覆盖
        File.WriteAllText(Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex] + "/ABCompareInfo.txt", abCompareInfo);

        // 刷新 Unity 编辑器的资源数据库，使新创建的文件在 Project 窗口中可见
        // Refresh() 会重新扫描 Assets 文件夹，更新编辑器显示
        AssetDatabase.Refresh();

        // 在控制台输出对比文件创建成功的日志
        Debug.Log("AB包对比文件生成成功");
    }


    // filePath: 文件的完整路径
    // 返回值: 32 位十六进制字符串表示的 MD5 值
    public string GetMD5(string filePath)

    {
        // 将文件以流的形式打开
        // using 语句确保流对象在使用后自动释放，即使发生异常也能正确清理资源
        using (FileStream file = new FileStream(filePath, FileMode.Open))
        {
            // 创建 MD5 哈希算法实例
            // MD5CryptoServiceProvider 是 .NET 提供的 MD5 实现
            MD5 md5 = new MD5CryptoServiceProvider();
            // 利用 API 得到数据的 MD5 码，返回 16 个字节的数组（128 位哈希值）
            // ComputeHash() 读取整个文件流并计算哈希值
            byte[] mdSInfo = md5.ComputeHash(file);


            // 关闭文件流（这一行是冗余的，因为 using 块结束时已自动调用 Dispose/Close）
            file.Close();

            // 把 16 个字节转换为十六进制，拼接成字符串，为了减小 MD5 码的长度
            // StringBuilder 用于高效的字符串拼接，避免频繁创建字符串对象
            StringBuilder sb = new StringBuilder();
            // 遍历 MD5 字节数组，逐个字节转换为十六进制字符串
            for (int i = 0; i < mdSInfo.Length; i++)
            {
                // 将字节转换为 2 位十六进制字符串
                // "x2": 表示 2 位小写十六进制，不足补零（如 0x0A -> "0a"）
                // Append() 将转换后的字符串添加到 StringBuilder
                sb.Append(mdSInfo[i].ToString("x2"));//转成十六进制的
            }
            // 将 StringBuilder 转换为字符串并返回
            return sb.ToString();

        }
    }


    //将选中资源移动到SteamingAssets文件夹中
    private void MoveABToStreamingAssets()
    {
        // 使用 Selection.GetFiltered 获取当前在 Project 窗口中选中的资源
        // typeof(Object): 指定筛选的资源类型为 Object（所有类型）
        // SelectionMode.DeepAssets: 包含选中文件夹内的所有子资源（递归搜索）
        UnityEngine.Object[] selectedAsset = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.DeepAssets);
        // 检查是否有选中的资源，如果没有选中的资源，直接返回
        if (selectedAsset.Length == 0)
            return;


        // 定义字符串变量，用于拼接 AB 包对比信息
        // 格式为：文件名 文件大小 MD5值|文件名 文件大小 MD5值|...
        string abCompareInfo = "";

        // 遍历所有选中的资源对象
        foreach (UnityEngine.Object asset in selectedAsset)
        {
            // 获取资源在项目中的相对路径（如 "Assets/ArtRes/AB/PC/player"）
            string assetPath = AssetDatabase.GetAssetPath(asset);
            // 从完整路径中提取文件名（包括扩展名）
            // LastIndexOf("/") 查找最后一个斜杠的位置
            // Substring() 从该位置截取到字符串末尾
            string fileName = assetPath.Substring(assetPath.LastIndexOf("/"));

            // 检查文件名中是否包含点号（即是否有扩展名）
            // AB 包没有扩展名，如果有点号则说明不是 AB 包，跳过该文件
            if (fileName.IndexOf('.') != -1)
                continue;
            // 使用 AssetDatabase.CopyAsset 将资源复制到 StreamingAssets 文件夹
            // 参数1: 源路径（项目中的资源路径）
            // 参数2: 目标路径（StreamingAssets 中的路径）
            AssetDatabase.CopyAsset(assetPath, "Assets/StreamingAssets" + fileName);

            // 创建 FileInfo 对象，用于获取文件的详细信息
            // Application.streamingAssetsPath 获取 StreamingAssets 文件夹的绝对路径
            FileInfo fileInfo = new FileInfo(Application.streamingAssetsPath + fileName);

            // 将 AB 包信息拼接成字符串，格式为：文件名 文件大小 MD5值
            // fileInfo.Name: 获取文件名（如 "player"）
            // fileInfo.Length: 获取文件大小（字节数）
            // CreateABCompare.GetMD5(): 调用 CreateABCompare 类的静态方法计算 MD5 值
            abCompareInfo += fileInfo.Name + " " + fileInfo.Length + " " + CreateABCompare.GetMD5(fileInfo.FullName);
            // 在每个 AB 包信息后添加竖线分隔符，用于后续拆分
            abCompareInfo += "|";
        }
        // 去掉最后一个符号（竖线），为了之后拆分字符串方便
        // Substring(0, length-1) 从开头截取到倒数第二个字符，去掉最后一个字符
        abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);

        // 将拼接好的 AB 包对比信息写入到 StreamingAssets 文件夹下的 ABCompareInfo.txt 文件
        // WriteAllText() 如果文件不存在则创建，如果存在则覆盖
        File.WriteAllText(Application.streamingAssetsPath + "/ABCompareInfo.txt", abCompareInfo);
        // 刷新 Unity 编辑器的资源数据库，使新创建的文件在 Project 窗口中可见
        AssetDatabase.Refresh();
    }


    private void UploadAllABFile()
    {
        //获取文件夹信息
        DirectoryInfo directory = Directory.CreateDirectory(Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex] + "/");
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
    private async void FtpUploadFile(string filePath, string fileName)
    {
        await Task.Run(() =>
        {
            try
            {
                string url = serverIP + ":2121/AB/" + targetStrings[nowSelIndex] + "/" + Uri.EscapeDataString(fileName);
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
