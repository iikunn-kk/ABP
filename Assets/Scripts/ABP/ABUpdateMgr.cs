// 引用 System 命名空间，提供基础数据类型和异常类
using System;
using System.Collections;

// 引用 System.Collections.Generic 命名空间，提供泛型集合类（如 Dictionary、List）
using System.Collections.Generic;
// 引用 System.IO 命名空间，提供文件和目录操作类（如 File、FileStream）
using System.IO;
// 引用 System.Net 命名空间，提供网络请求类（如 FtpWebRequest、NetworkCredential）
using System.Net;
// 引用 System.Threading.Tasks 命名空间，提供异步编程支持（如 Task）
using System.Threading.Tasks;

// 引用 UnityEngine 命名空间，提供 Unity 引擎核心功能（如 Debug、MonoBehaviour）
using UnityEngine;
// 引用 UnityEngine.Events 命名空间，提供 Unity 事件系统（如 UnityAction）
using UnityEngine.Events;
using UnityEngine.Networking;

// 引用 UnityEngine.UIElements 命名空间，提供 UI 系统支持
using UnityEngine.UIElements;

// 定义 ABUpdateMgr 类，继承自 MonoBehaviour，使其可以挂载到 GameObject 上并使用 Unity 生命周期方法
public class ABUpdateMgr : MonoBehaviour
{
    // 定义 FTP 服务器主机地址常量，使用本地回环地址 127.0.0.1 表示本机
    private const string FtpHost = "127.0.0.1";
    // 定义 FTP 服务器端口号常量，使用非标准端口 2121（FileZilla Server 默认是 21）
    private const int FtpPort = 2121;
    // 定义 FTP 服务器远程目录路径常量，AB 包存储在 /AB/PC/ 目录下
    private const string FtpRemoteDir = "/AB/PC/";
    // 定义 FTP 登录用户名常量，用于身份验证
    private const string FtpUser = "Coolcoolcoo";
    // 定义 FTP 登录密码常量，与用户名配合完成身份验证
    private const string FtpPassword = "Coolcoolcoo123";
    //资源服务器IP
    private string serverIP = "ftp://127.0.0.1";

    // 定义静态布尔变量，控制是否启用 FTPS（FTP over SSL/TLS）加密传输
    // 503 Use AUTH first 说明服务器要求显式 FTPS（AUTH TLS）
    private static readonly bool UseFtps = false;
    // 定义静态布尔变量，控制是否允许不安全的自签名证书
    // 测试环境自签名证书可放行；正式环境请设为 false 以提高安全性
    private static bool AllowInsecureCertificate = true;
    // 定义静态私有字段，用于存储 ABUpdateMgr 的单例实例
    private static ABUpdateMgr instance;
    // 定义静态公共属性，提供对单例实例的访问
    public static ABUpdateMgr Instance
    {
        // 定义 get 访问器，用于获取单例实例
        get
        {
            // 检查实例是否为空，如果为空则需要创建
            if (instance == null)
            {
                // 创建一个新的 GameObject 对象，命名为 "ABUpdateMgr"
                GameObject obj = new GameObject("ABUpdateMgr");
                // 在该 GameObject 上添加 ABUpdateMgr 组件，并将引用赋值给 instance
                instance = obj.AddComponent<ABUpdateMgr>();
            }
            // 返回单例实例
            return instance;
        }
    }


    // 定义公共字典字段，用于存储远端 AB 包的信息
    // 键为 AB 包名称（string），值为 ABInfo 对象
    // 用于存储远端AB包的信息的字典，
    public Dictionary<string, ABInfo> remoteABInfo = new Dictionary<string, ABInfo>();

    //用于存储本地AB包信息的字典
    public Dictionary<string, ABInfo> localABInfo = new Dictionary<string, ABInfo>();

    // 待下载的 AB 包列表，存储所有需要下载的 AB 包名称
    // List<T> 是动态数组，可以动态添加和删除元素
    private List<string> downLoadList = new List<string>();

    /// <summary>
    /// 用于检测资源热更新的函数
    /// </summary>
    /// <param name="overCallBack"></param>
    /// <param name="updateInfoCallBack"></param>
    public void CheckUpdate(UnityAction<bool> overCallBack, UnityAction<string> updateInfoCallBack)
    {
        //为了避免上一次报错而残留信息 所以我们直接清空它
        remoteABInfo.Clear();
        localABInfo.Clear();
        downLoadList.Clear();


        //1加载远端资源对比文件
        DownLoadABCompareFile((isOver) =>
        {
            updateInfoCallBack?.Invoke("开始更新资源");
            if (isOver)
            {
                updateInfoCallBack?.Invoke("对比文件下载结束");
                string remoteInfo = File.ReadAllText(Application.persistentDataPath + "/ABCompareInfo_TMP.txt");
                updateInfoCallBack?.Invoke("解析远端对比文件完成");
                GetRemoteABCompareFileInfo(remoteInfo, remoteABInfo);
                //2加载本地资源对比文件
                GetLocalABCompareFileInfo((isOver) =>
                {
                    if (isOver)
                    {

                        updateInfoCallBack?.Invoke("解析本地文件完成");
                        //3对比他们，然后进行AB包下载
                        updateInfoCallBack?.Invoke("开始对比");
                        foreach (string abName in remoteABInfo.Keys)
                        {
                            //1判断 哪些资源是新的 然后记录下来 之后用于下载
                            //由于本地对比信息中没有叫做这个名字的包，所以我们记录下它
                            if (!localABInfo.ContainsKey(abName))
                            {
                                downLoadList.Add(abName);
                            }
                            else
                            {
                                //2判断 哪些资源是需要更新的 然后记录 之后用于下载
                                //对比他们的md5码 判断是否需要更新
                                if (localABInfo[abName].md5 != remoteABInfo[abName].md5)
                                {
                                    downLoadList.Add(abName);
                                }
                                //如果md5相同 证明是同一个资源 不需要更新

                                //每次检测完一个名字的AB包，就移除本地的信息，那么本地剩下的信息 就是远端没有的内容 就可以把他们删除了
                                localABInfo.Remove(abName);
                            }
                        }
                        updateInfoCallBack?.Invoke("对比完成");
                        updateInfoCallBack?.Invoke("删除无用的AB包文件");
                        //上面对比完了，我们就删除没用的内容，再下载AB包
                        foreach (string abName in localABInfo.Keys)
                        {
                            //如果可读写文件夹中有内容  我们就删除它
                            //默认资源中的 信息 我们没办法删除
                            if (File.Exists(Application.persistentDataPath + "/" + abName))
                                File.Delete(Application.persistentDataPath + "/" + abName);
                        }
                        updateInfoCallBack?.Invoke("下载和更新AB包文件");
                        //下载待更新列表中的所有AB包
                        //下载
                        DownLoadABFile((isOver) =>
                        {
                            if (isOver)
                            {
                                //下载完所有的AB包文件后
                                //把本地的AB包对比文件 更新为最新的
                                //把之前读取出来的 远端对比文件信息 存储到本地
                                updateInfoCallBack?.Invoke("更新本地AB包对比文件为最新");
                                File.WriteAllText(Application.persistentDataPath + "/ABCompareInfo.txt", remoteInfo);
                            }
                            overCallBack(isOver);
                        },
                        updateInfoCallBack);
                    }

                    else
                        overCallBack?.Invoke(false);
                });
            }
            else
            {
                overCallBack?.Invoke(false);
            }

        });


    }



    // 定义公共方法，用于下载并解析 AB 对比文件
    // 该方法从 FTP 服务器下载 ABCompareInfo.txt，解析后填充到 remoteABInfo 字典
    public async void DownLoadABCompareFile(UnityAction<bool> overCallBack)
    {
        // 调用 DownLoadFile 方法，从 FTP 服务器下载 ABCompareInfo.txt 文件到本地持久化数据路径
        // Application.persistentDataPath 是 Unity 提供的持久化数据路径，不同平台路径不同
        Debug.Log(Application.persistentDataPath);
        bool isOver = false;
        int reDownLoadMaxNum = 5;//最大的重试次数
        string localPath = Application.persistentDataPath;
        while (!isOver && reDownLoadMaxNum > 0)
        {
            //通过异步函数，让方法不在主线程中进行执行，防止卡顿主线程
            await Task.Run(() =>
            {
                isOver = DownLoadFile("ABCompareInfo.txt", localPath + "/ABCompareInfo_TMP.txt");

            });
            --reDownLoadMaxNum;
        }
        //回调函在此处进行调用，告诉外部成功与否
        overCallBack?.Invoke(isOver);
        if (isOver)
        {

        }

    }

    public void GetRemoteABCompareFileInfo(string info, Dictionary<string, ABInfo> ABInfo)
    {
        // 在控制台输出本地持久化数据路径，用于调试和验证路径正确性
        Debug.Log(Application.persistentDataPath);
        // 读取本地下载的 ABCompareInfo.txt 文件的全部内容到字符串 info
        // string info = File.ReadAllText(Application.persistentDataPath + "/" + "ABCompareInfo_TMP.txt");


        // 使用竖线 '|' 作为分隔符，将 info 字符串拆分为字符串数组
        // 数组的每个元素代表一个 AB 包的信息（格式：name size md5）
        string[] strs = info.Split('|');//把每个AB包的信息拆分出来
        // 声明字符串数组变量，用于存储单个 AB 包的详细信息
        string[] infos = null;
        // 遍历 strs 数组，处理每个 AB 包的信息
        for (int i = 0; i < strs.Length; i++)
        {
            // 使用空格 ' ' 作为分隔符，将单个 AB 包的信息拆分为 name、size、md5 三个部分
            infos = strs[i].Split(' ');//又把一个AB包的信息拆分出来

            // 将解析出的 AB 包信息添加到 remoteABInfo 字典中
            // infos[0] 是 AB 包名称，作为字典的键
            // new ABInfo(...) 创建新的 ABInfo 对象，作为字典的值
            //记录每一个远端AB包的信息，之后用来对比
            //用AB包的名字作为键,AB包的信息作为值
            ABInfo.Add(infos[0], new ABInfo(infos[0], infos[1], infos[2]));
        }

    }


    public void GetLocalABCompareFileInfo(UnityAction<bool> overCallBack)
    {
        if (File.Exists(Application.persistentDataPath + "/ABCompareInfo.txt"))
        {
            StartCoroutine(GetLocalABCompareFileInfo("file:///" + Application.persistentDataPath + "/ABCompareInfo.txt", overCallBack));
        }
        else if (File.Exists(Application.streamingAssetsPath + "/ABCompareInfo.txt"))
        {
            string path =
#if UNITY_ANDROID
            Application.streamingAssetsPath;
#else
            "file:///" + Application.streamingAssetsPath;
#endif

            StartCoroutine(GetLocalABCompareFileInfo(path + "/ABCompareInfo.txt", overCallBack));
        }
        //如果两个都不进的话，证明第一次并且没有默认资源
        else
        {
            overCallBack?.Invoke(true);
        }
    }

    private IEnumerator GetLocalABCompareFileInfo(string filePath, UnityAction<bool> overCallBack)
    {
        //使用UnityWebRequst去加载本地文件
        UnityWebRequest req = UnityWebRequest.Get(filePath);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            GetRemoteABCompareFileInfo(req.downloadHandler.text, localABInfo);
            overCallBack?.Invoke(true);
        }
        else
        {
            overCallBack?.Invoke(false);
        }

    }

    // 定义异步公共方法，用于批量下载 AB 包文件
    // async 关键字表示这是一个异步方法，可以使用 await 关键字
    // UnityAction<bool> overCallBack: 下载完成回调，参数为是否全部下载成功（true/false）
    // UnityAction<int, int> updatePro: 进度更新回调，参数为（当前完成数，总下载数）
    public async void DownLoadABFile(UnityAction<bool> overCallBack, UnityAction<string> updatePro)
    {
        // // 1. 遍历字典的键（即 AB 包名称），将所有需要下载的 AB 包名称添加到待下载列表
        // foreach (string name in remoteABInfo.Keys)
        // {
        //     downLoadList.Add(name);
        // }
        // 定义布尔变量，用于标记单个文件是否下载完成
        bool isOver = false;
        // 定义本地保存路径，所有 AB 包都保存到持久化数据目录
        string localPath = Application.persistentDataPath + "/";
        // 定义临时列表，用于记录本轮下载成功的文件名
        List<string> tempList = new List<string>();
        // 定义最大重试次数，允许失败后重试 5 次
        int reDownLoadMaxNum = 5;
        // 定义已下载完成的文件数量计数器
        int downLoadOverNum = 0;
        // 定义需要下载的总文件数量
        int downLoadMaxNum = downLoadList.Count;
        // 循环下载，直到所有文件下载完毕或达到最大重试次数
        while (downLoadList.Count > 0 && reDownLoadMaxNum > 0)
        {
            // 遍历当前待下载列表，尝试下载每个文件
            for (int i = 0; i < downLoadList.Count; i++)
            {
                // 重置下载完成标志
                isOver = false;
                // 使用 Task.Run 将同步的下载操作放到线程池中执行，避免阻塞主线程
                // await 关键字等待任务完成，但不会阻塞主线程
                await Task.Run(() =>
                {
                    // 调用 DownLoadFile 方法下载文件，返回值表示是否下载成功
                    isOver = DownLoadFile(downLoadList[i], localPath + downLoadList[i]);
                });
                // 如果文件下载成功
                if (isOver)
                {
                    // 调用进度更新回调，通知外部更新进度（当前完成数 +1，总数不变）
                    updatePro(++downLoadOverNum + "/" + downLoadMaxNum);
                    // 将下载成功的文件名添加到临时列表，稍后从待下载列表中移除
                    tempList.Add(downLoadList[i]);//下载成功的记录下来
                }
            }
            // 把成功下载的文件名，从待下载列表中移除
            for (int i = 0; i < tempList.Count; i++)
            {
                downLoadList.Remove(tempList[i]);
            }
            // 清空临时列表，为下一轮下载做准备
            tempList.Clear();
            // 减少剩余重试次数
            --reDownLoadMaxNum;
        }
        // 所有内容都下载完毕了，那么就会传一个 true 给外部；如果还有未下载的文件，传 false
        overCallBack(downLoadList.Count == 0);
    }

    // 定义私有方法，用于从 FTP 服务器下载文件到本地指定路径
    // fileName: 要下载的文件名
    // localPath: 本地保存路径（包含文件名）
    private bool DownLoadFile(string fileName, string localPath)
    {
        // 使用 try-catch 块捕获并处理可能发生的异常
        try
        {
            // 构建完整的 FTP 下载 URL，格式为 ftp://host:port/directory/filename
            // Uri.EscapeDataString 对文件名进行 URL 编码，处理特殊字符
            // 创建 FTP/FTPS 链接

            // string url = $"ftp://{FtpHost}:{FtpPort}{FtpRemoteDir}{Uri.EscapeDataString(fileName)}";
            string pInfo =
#if UNITY_IOS
            "IOS";
#elif UNITY_ANDROID
            "Android";
#else
            "PC";
#endif
            string url = serverIP + ":2121/AB/" + pInfo + "/" + Uri.EscapeDataString(fileName);
            // 使用 WebRequest.Create 创建请求，并转换为 FtpWebRequest 类型
            FtpWebRequest req = WebRequest.Create(url) as FtpWebRequest;

            // 设置 FTP 请求的身份验证凭证，使用用户名和密码
            // 凭证
            req.Credentials = new NetworkCredential(FtpUser, FtpPassword);
            //其他设置
            // 将代理设置为 null，避免使用代理服务器，提高连接速度
            //设置代理为null
            req.Proxy = null;
            // 设置 KeepAlive 为 false，请求完成后关闭控制连接
            // true: 保持连接，适合多次请求；false: 立即关闭，适合单次请求
            //请求完毕后，是否关闭控制连接
            req.KeepAlive = false;
            // 设置 UsePassive 为 true，使用被动模式传输数据
            // 被动模式更稳定，适合客户端在内网、服务器在公网的情况
            // 被动模式更稳
            req.UsePassive = true;
            // 设置 EnableSsl 控制是否启用 SSL/TLS 加密传输
            // 启用 FTPS（显式 AUTH TLS）
            req.EnableSsl = UseFtps;
            // 设置请求方法为下载文件
            // WebRequestMethods.Ftp.UploadFile 是上传，DownloadFile 是下载
            //操作命令上传
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            // 设置 UseBinary 为 true，使用二进制模式传输文件
            // 二进制模式适合传输所有类型的文件（包括文本和二进制文件）
            //指定传输的类型 2进制S
            req.UseBinary = true;
            // 发送 FTP 请求并获取响应，转换为 FtpWebResponse 类型
            FtpWebResponse res = (FtpWebResponse)req.GetResponse();
            // 从响应对象中获取数据流，用于读取服务器返回的文件数据
            Stream downLoadStream = res.GetResponseStream();
            // 创建本地文件流，使用 using 语句确保流对象在使用后自动释放
            // File.Create() 创建或覆盖指定路径的文件
            using (FileStream file = File.Create(localPath))
            {
                // 创建字节数组作为缓冲区，大小为 2048 字节（2KB）
                //一点一点的下载内容
                byte[] bytes = new byte[2048];
                // 从下载流中读取数据到字节数组
                // 参数：目标数组、起始索引（0）、最大读取长度（数组长度）
                // 返回值：实际读取的字节数，0 表示读取到流末尾
                //返回值 代表读取了多少个字符
                int contentLength = downLoadStream.Read(bytes, 0, bytes.Length);
                // 循环读取数据，直到 contentLength 为 0（读取完毕）
                //循环下载文件的数据
                while (contentLength != 0)
                {
                    // 将读取到的字节数组写入本地文件流
                    // 参数：源数组、起始索引（0）、写入长度（实际读取的字节数）
                    //写入到本地文件流
                    file.Write(bytes, 0, contentLength);
                    // 继续读取下一批数据
                    //写完再读
                    contentLength = downLoadStream.Read(bytes, 0, bytes.Length);
                }
            }
            // 在控制台输出文件下载成功的日志
            Debug.Log(fileName + "下载成功");
            return true;
        }
        // 捕获所有可能的异常
        catch (Exception ex)
        {
            // 在控制台输出下载失败的日志，包含异常信息
            Debug.Log("下载失败" + ex.Message);
            return false;
        }
    }
    // 定义私有生命周期方法，在 GameObject 销毁时自动调用
    private void OnDestroy()
    {
        // 将静态单例实例设置为 null，释放单例引用
        instance = null;
    }
    // 定义公共嵌套类 ABInfo，用于存储 AB 包的基本信息
    //AB包信息类
    public class ABInfo
    {
        // 定义公共字段，存储 AB 包的名称
        public string name;
        // 定义公共字段，存储 AB 包的大小（字节）
        public long size;
        // 定义公共字段，存储 AB 包的 MD5 哈希值（用于校验文件完整性）
        public string md5;
        // 定义构造函数，用于初始化 ABInfo 对象
        // 参数：name（AB 包名称，字符串）、size（文件大小，字符串类型）、md5（MD5 值，字符串）
        public ABInfo(string name, string size, string md5)
        {
            // 将传入的 name 参数赋值给当前对象的 name 字段
            this.name = name;
            // 将传入的 size 参数（字符串）转换为 long 类型后赋值给 size 字段
            this.size = long.Parse(size);
            // 将传入的 md5 参数直接赋值给当前对象的 md5 字段
            this.md5 = md5;
        }
    }
}
