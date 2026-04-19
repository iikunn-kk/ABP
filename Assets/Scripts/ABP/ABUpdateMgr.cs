using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class ABUpdateMgr : MonoBehaviour
{
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private static ABUpdateMgr instance;
    public static ABUpdateMgr Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ABUpdateMgr");
                instance = obj.AddComponent<ABUpdateMgr>();
            }
            return instance;
        }
    }

    private void Update()
    {
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                try
                {
                    mainThreadActions.Dequeue()?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }

    private void RunOnMainThread(Action action)
    {
        if (action == null)
            return;
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    // 当前使用的远端存储实例
    private IRemoteStorage storage;

    /// <summary>获取或创建远端存储实例（懒初始化）</summary>
    private IRemoteStorage Storage
    {
        get
        {
            if (storage == null)
                storage = ABHotUpdateConfig.CreateStorage();
            return storage;
        }
    }

    /// <summary>外部切换存储实例（如运行时从服务器获取配置后切换）</summary>
    public void SetStorage(IRemoteStorage remoteStorage)
    {
        storage = remoteStorage;
    }

    // 远端AB包信息
    public Dictionary<string, ABInfo> remoteABInfo = new Dictionary<string, ABInfo>();

    // 本地AB包信息
    public Dictionary<string, ABInfo> localABInfo = new Dictionary<string, ABInfo>();

    // 待下载的AB包列表
    private List<string> downLoadList = new List<string>();

    /// <summary>
    /// 检测资源热更新
    /// </summary>
    public void CheckUpdate(UnityAction<bool> overCallBack, UnityAction<string> updateInfoCallBack)
    {
        remoteABInfo.Clear();
        localABInfo.Clear();
        downLoadList.Clear();

        // 1 加载远端资源对比文件
        DownLoadABCompareFile((isOver) =>
        {
            updateInfoCallBack?.Invoke("开始更新资源");
            if (isOver)
            {
                updateInfoCallBack?.Invoke("对比文件下载结束");
                string remoteInfo = File.ReadAllText(Application.persistentDataPath + "/ABCompareInfo_TMP.txt");
                updateInfoCallBack?.Invoke("解析远端对比文件完成");
                ParseABCompareInfo(remoteInfo, remoteABInfo);
                // 2 加载本地资源对比文件
                GetLocalABCompareFileInfo((isOver) =>
                {
                    if (isOver)
                    {
                        updateInfoCallBack?.Invoke("解析本地文件完成");
                        // 3 对比差异，然后下载AB包
                        updateInfoCallBack?.Invoke("开始对比");
                        foreach (string abName in remoteABInfo.Keys)
                        {
                            if (!localABInfo.ContainsKey(abName))
                            {
                                downLoadList.Add(abName);
                            }
                            else
                            {
                                if (localABInfo[abName].md5 != remoteABInfo[abName].md5)
                                {
                                    downLoadList.Add(abName);
                                }
                                localABInfo.Remove(abName);
                            }
                        }
                        updateInfoCallBack?.Invoke("对比完成");
                        updateInfoCallBack?.Invoke("删除无用的AB包文件");
                        foreach (string abName in localABInfo.Keys)
                        {
                            if (File.Exists(Application.persistentDataPath + "/" + abName))
                                File.Delete(Application.persistentDataPath + "/" + abName);
                        }
                        updateInfoCallBack?.Invoke("下载和更新AB包文件");
                        DownLoadABFile((isOver) =>
                        {
                            if (isOver)
                            {
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

    /// <summary>下载远端对比文件</summary>
    public async void DownLoadABCompareFile(UnityAction<bool> overCallBack)
    {
        Debug.Log(Application.persistentDataPath);
        bool isOver = false;
        int reDownLoadMaxNum = 5;
        string localPath = Application.persistentDataPath;
        while (!isOver && reDownLoadMaxNum > 0)
        {
            isOver = await Storage.DownloadAsync("ABCompareInfo.txt", localPath + "/ABCompareInfo_TMP.txt");
            --reDownLoadMaxNum;
        }
        RunOnMainThread(() => overCallBack?.Invoke(isOver));
    }

    /// <summary>
    /// 解析 AB 对比文件内容，填充到指定字典
    /// </summary>
    public void ParseABCompareInfo(string info, Dictionary<string, ABInfo> ABInfo)
    {
        Debug.Log(Application.persistentDataPath);

        string[] strs = info.Split('|');
        for (int i = 0; i < strs.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(strs[i]))
                continue;
            string[] infos = strs[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (infos.Length < 3)
            {
                Debug.LogWarning("ABCompareInfo 格式异常，跳过片段: " + strs[i]);
                continue;
            }
            ABInfo[infos[0]] = new ABInfo(infos[0], infos[1], infos[2]);
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
        else
        {
            overCallBack?.Invoke(true);
        }
    }

    private IEnumerator GetLocalABCompareFileInfo(string filePath, UnityAction<bool> overCallBack)
    {
        UnityWebRequest req = UnityWebRequest.Get(filePath);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            ParseABCompareInfo(req.downloadHandler.text, localABInfo);
            overCallBack?.Invoke(true);
        }
        else
        {
            overCallBack?.Invoke(false);
        }
    }

    /// <summary>批量下载AB包文件</summary>
    public async void DownLoadABFile(UnityAction<bool> overCallBack, UnityAction<string> updatePro)
    {
        bool isOver = false;
        string localPath = Application.persistentDataPath + "/";
        List<string> tempList = new List<string>();
        int reDownLoadMaxNum = 5;
        int downLoadOverNum = 0;
        int downLoadMaxNum = downLoadList.Count;

        while (downLoadList.Count > 0 && reDownLoadMaxNum > 0)
        {
            for (int i = 0; i < downLoadList.Count; i++)
            {
                isOver = false;
                string bundleName = downLoadList[i];
                string savedPath = localPath + bundleName;
                isOver = await Storage.DownloadAsync(bundleName, savedPath);

                if (isOver && remoteABInfo.TryGetValue(bundleName, out ABInfo meta))
                {
                    string diskMd5 = ABHashUtil.ComputeMD5File(savedPath);
                    if (!string.Equals(diskMd5, meta.md5, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning("下载后 MD5 不一致，将重试: " + bundleName);
                        try { File.Delete(savedPath); }
                        catch (Exception ex) { Debug.Log(ex.Message); }
                        isOver = false;
                    }
                }

                if (isOver)
                {
                    int done = ++downLoadOverNum;
                    RunOnMainThread(() =>
                        updatePro?.Invoke(done + "/" + downLoadMaxNum));
                    tempList.Add(bundleName);
                }
            }
            for (int i = 0; i < tempList.Count; i++)
            {
                downLoadList.Remove(tempList[i]);
            }
            tempList.Clear();
            --reDownLoadMaxNum;
        }
        bool allOk = downLoadList.Count == 0;
        RunOnMainThread(() => overCallBack?.Invoke(allOk));
    }

    private void OnDestroy()
    {
        instance = null;
    }

    // AB包信息类
    public class ABInfo
    {
        public string name;
        public long size;
        public string md5;
        public ABInfo(string name, string size, string md5)
        {
            this.name = name;
            this.size = long.Parse(size);
            this.md5 = md5;
        }
    }
}
