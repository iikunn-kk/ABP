// 引用 System.IO 命名空间，提供文件和目录操作类（如 DirectoryInfo、FileInfo、File）
using System.IO;
// 引用 UnityEditor 命名空间，提供 Unity 编辑器相关类（如 MenuItem、AssetDatabase）
using UnityEditor;
// 引用 UnityEngine 命名空间，提供 Unity 引擎核心功能（如 Debug）
using UnityEngine;
// 引用 System.Security.Cryptography 命名空间，提供加密算法类（如 MD5）
using System.Security.Cryptography;
// 引用 System.Text 命名空间，提供文本处理类（如 StringBuilder）
using System.Text;
// 引用 Best.HTTP 的安全协议命名空间（此引用可能未使用，可以移除）
using Best.HTTP.SecureProtocol.Org.BouncyCastle.Asn1;

// 定义 CreateABCompare 类，用于创建 AB 包对比文件
// 该类只在编辑器中使用，用于生成 AB 包的对比信息文件
public class CreateABCompare
{
    // 使用 MenuItem 特性在 Unity 菜单栏中添加菜单项
    // 菜单路径为 "AB包工具/创建对比文件"
    // [MenuItem("AB包工具/创建对比文件")]
    // 定义公共静态方法，执行创建 AB 包对比文件的操作
    public static void CreateABCompareFile()
    {
        // 获取文件夹信息，创建或打开指定目录
        // Application.dataPath: Unity 项目的 Assets 文件夹路径
        // "/ArtRes/AB/PC": AB 包存储的相对路径
        // Directory.CreateDirectory(): 如果目录不存在则创建，如果存在则直接返回
        DirectoryInfo directory = Directory.CreateDirectory(Application.dataPath + "/ArtRes/AB/PC");
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
        File.WriteAllText(Application.dataPath + "/ArtRes/AB/PC/ABCompareInfo.txt", abCompareInfo);

        // 刷新 Unity 编辑器的资源数据库，使新创建的文件在 Project 窗口中可见
        // Refresh() 会重新扫描 Assets 文件夹，更新编辑器显示
        AssetDatabase.Refresh();

        // 在控制台输出对比文件创建成功的日志
        Debug.Log("AB包对比文件生成成功");
    }

    // 定义公共静态方法，用于计算文件的 MD5 哈希值
    // filePath: 文件的完整路径
    // 返回值: 32 位十六进制字符串表示的 MD5 值
    public static string GetMD5(string filePath)
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
}
