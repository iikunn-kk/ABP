using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public class ABTools : EditorWindow
{
    // ===================== 基础字段 =====================
    private int nowSelIndex = 0;
    private readonly string[] targetStrings = { "PC", "IOS", "Android" };
    private string serverIP;
    private Vector2 scroll;

    // ===================== 状态条 & 上传进度 =====================
    private string statusText = "";
    private string statusDetail = "";
    private float progressValue = -1f;
    private bool isUploading;
    private readonly Queue<UploadTask> uploadQueue = new Queue<UploadTask>();
    private int uploadTotal;
    private int uploadDone;
    private int uploadFailed;
    private long uploadTotalBytes;
    private long uploadTransferredBytes;
    private System.Diagnostics.Stopwatch uploadStopwatch;

    // ===================== AB 包列表预览 =====================
    private List<ABFileEntry> abFileList = new List<ABFileEntry>();
    private Vector2 fileListScroll;
    private bool fileListDirty = true;

    private struct ABFileEntry
    {
        public string FilePath;
        public string FileName;
        public long FileSize;
        public string MD5;
        public bool Selected;
    }

    // ===================== 多服务器配置 =====================
    private int serverConfigIndex = -1;
    private List<FtpServerConfig> serverConfigs;
    private string[] serverConfigNames;
    private const string EditorPrefsFtpKey = "ABTools_FtpServerUrl";
    private const string EditorPrefsFtpConfigsKey = "ABTools_FtpConfigs";
    private const string EditorPrefsFtpConfigIndexKey = "ABTools_FtpConfigIndex";

    [Serializable]
    private class FtpServerConfig
    {
        public string name;
        public string url;
        public string user;
        public string password;
        public int port;
        public bool useFtps;

        public FtpServerConfig() { }
        public FtpServerConfig(string name, string url, string user, string password, int port, bool useFtps)
        {
            this.name = name;
            this.url = url;
            this.user = user;
            this.password = password;
            this.port = port;
            this.useFtps = useFtps;
        }
    }

    [Serializable]
    private class FtpConfigList
    {
        public List<FtpServerConfig> configs = new List<FtpServerConfig>();
    }

    // ===================== 上传历史 =====================
    private List<UploadHistoryEntry> uploadHistory = new List<UploadHistoryEntry>();
    private const string EditorPrefsUploadHistoryKey = "ABTools_UploadHistory";
    private const int MaxHistoryEntries = 20;

    [Serializable]
    private class UploadHistoryEntry
    {
        public string time;
        public string serverUrl;
        public string platform;
        public int fileCount;
        public long totalSize;
        public double elapsedSeconds;
        public bool success;
    }

    [Serializable]
    private class UploadHistoryList
    {
        public List<UploadHistoryEntry> entries = new List<UploadHistoryEntry>();
    }

    // ===================== 远端对比预览 =====================
    private List<RemoteDiffEntry> remoteDiffs = new List<RemoteDiffEntry>();
    private bool isComparingRemote;
    private Vector2 diffScroll;

    private struct RemoteDiffEntry
    {
        public string FileName;
        public DiffType Type;
    }

    private enum DiffType { Add, Modify, Delete, Unchanged }

    // ===================== 下载队列 =====================
    private readonly Queue<UploadTask> downloadQueue = new Queue<UploadTask>();
    private bool isDownloading;
    private int downloadTotal;
    private int downloadDone;
    private int downloadFailed;

    // ===================== 上传任务结构 =====================
    private struct UploadTask
    {
        public string FilePath;
        public string FileName;
    }

    // ===================== 窗口生命周期 =====================

    [MenuItem("AB包工具/打开工具窗口")]
    private static void OpenWindow()
    {
        ABTools window = GetWindow<ABTools>();
        window.titleContent = new GUIContent("AB 包工具");
        window.minSize = new Vector2(400, 560);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("AB 包工具");
        minSize = new Vector2(400, 560);
        serverIP = EditorPrefs.GetString(EditorPrefsFtpKey, ABHotUpdateConfig.ServerBaseUrl);
        LoadServerConfigs();
        LoadUploadHistory();
        fileListDirty = true;
    }

    private void OnDisable()
    {
        SaveServerIP();
        SaveServerConfigs();
        SaveUploadHistory();
    }

    private void SaveServerIP()
    {
        if (serverIP != null)
            EditorPrefs.SetString(EditorPrefsFtpKey, serverIP.Trim());
    }

    private bool IsServerUrlValid()
    {
        string s = serverIP != null ? serverIP.Trim() : "";
        return s.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase);
    }

    // ===================== OnGUI 主界面 =====================

    private void OnGUI()
    {
        float pad = 10f;
        EditorGUILayout.Space(pad);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ---- 平台选择 ----
        EditorGUILayout.LabelField("平台选择", EditorStyles.boldLabel);
        int prevIdx = nowSelIndex;
        nowSelIndex = GUILayout.Toolbar(nowSelIndex, targetStrings, GUILayout.Height(24));
        if (prevIdx != nowSelIndex) fileListDirty = true;

        EditorGUILayout.Space(6);

        // ---- 一键构建 AB 包 ----
        DrawBuildSection();

        EditorGUILayout.Space(6);

        // ---- FTP 服务器配置 ----
        DrawServerConfigSection();

        EditorGUILayout.Space(6);

        // ---- 操作按钮 ----
        DrawActionButtons();

        EditorGUILayout.Space(6);

        // ---- AB 包文件列表 ----
        DrawFileListSection();

        EditorGUILayout.Space(6);

        // ---- 远端对比预览 ----
        DrawRemoteDiffSection();

        EditorGUILayout.Space(6);

        // ---- 上传历史 ----
        DrawUploadHistorySection();

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndScrollView();

        // -------- 底部状态条 --------
        DrawStatusBar(pad);
    }

    // ===================== 构建区 =====================

    private void DrawBuildSection()
    {
        EditorGUILayout.LabelField("构建 AB 包", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("构建当前平台 AB 包", GUILayout.Height(28)))
            {
                BuildAssetBundles();
            }
            if (GUILayout.Button("构建所有平台", GUILayout.Height(28)))
            {
                BuildAllPlatforms();
            }
        }
    }

    private void BuildAssetBundles()
    {
        BuildTarget target = GetBuildTarget(nowSelIndex);
        string outputPath = Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex];
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, target);
        AssetDatabase.Refresh();
        fileListDirty = true;
        SetStatus($"构建完成: {targetStrings[nowSelIndex]}", "", -1f);
    }

    private void BuildAllPlatforms()
    {
        for (int i = 0; i < targetStrings.Length; i++)
        {
            BuildTarget target = GetBuildTarget(i);
            string outputPath = Application.dataPath + "/ArtRes/AB/" + targetStrings[i];
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, target);
        }
        AssetDatabase.Refresh();
        fileListDirty = true;
        SetStatus("所有平台构建完成", "", -1f);
    }

    private BuildTarget GetBuildTarget(int index)
    {
        switch (index)
        {
            case 1: return BuildTarget.iOS;
            case 2: return BuildTarget.Android;
            default: return BuildTarget.StandaloneWindows;
        }
    }

    // ===================== 服务器配置区 =====================

    private void DrawServerConfigSection()
    {
        EditorGUILayout.LabelField("FTP 服务器", EditorStyles.boldLabel);

        // 配置下拉
        if (serverConfigNames != null && serverConfigNames.Length > 0)
        {
            int prevCfg = serverConfigIndex;
            serverConfigIndex = EditorGUILayout.Popup("配置切换", serverConfigIndex, serverConfigNames);
            if (prevCfg != serverConfigIndex && serverConfigIndex >= 0 && serverConfigIndex < serverConfigs.Count)
            {
                ApplyServerConfig(serverConfigs[serverConfigIndex]);
            }
        }

        // 手动输入
        string prevIP = serverIP;
        serverIP = EditorGUILayout.TextField(
            new GUIContent("资源服务器", "须带协议前缀，例如 ftp://127.0.0.1"),
            serverIP);
        if (prevIP != serverIP)
        {
            SaveServerIP();
            serverConfigIndex = -1; // 手动修改时取消下拉选中
        }

        if (!IsServerUrlValid())
        {
            EditorGUILayout.HelpBox("FTP 上传需要填写以 ftp:// 或 ftps:// 开头的地址。", MessageType.Warning);
        }

        // 配置管理按钮
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("保存当前配置", EditorStyles.miniButton, GUILayout.Width(100)))
                SaveCurrentAsConfig();
            if (GUILayout.Button("删除选中配置", EditorStyles.miniButton, GUILayout.Width(100)))
                DeleteSelectedConfig();
        }
    }

    private void ApplyServerConfig(FtpServerConfig cfg)
    {
        serverIP = cfg.url;
        SaveServerIP();
    }

    private void SaveCurrentAsConfig()
    {
        string name = "配置 " + (serverConfigs.Count + 1);
        var cfg = new FtpServerConfig(name, serverIP?.Trim() ?? "",
            ABHotUpdateConfig.FtpUser, ABHotUpdateConfig.FtpPassword,
            ABHotUpdateConfig.FtpPort, ABHotUpdateConfig.UseFtps);
        serverConfigs.Add(cfg);
        RefreshConfigNames();
        serverConfigIndex = serverConfigs.Count - 1;
        SaveServerConfigs();
        SetStatus($"已保存配置: {name}", "", -1f);
    }

    private void DeleteSelectedConfig()
    {
        if (serverConfigIndex >= 0 && serverConfigIndex < serverConfigs.Count)
        {
            string name = serverConfigs[serverConfigIndex].name;
            serverConfigs.RemoveAt(serverConfigIndex);
            RefreshConfigNames();
            serverConfigIndex = -1;
            SaveServerConfigs();
            SetStatus($"已删除配置: {name}", "", -1f);
        }
    }

    private void RefreshConfigNames()
    {
        serverConfigNames = serverConfigs.Select(c => c.name).ToArray();
        if (serverConfigNames.Length == 0)
            serverConfigNames = new[] { "(无配置)" };
    }

    private void LoadServerConfigs()
    {
        string json = EditorPrefs.GetString(EditorPrefsFtpConfigsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonUtility.FromJson<FtpConfigList>(json);
                serverConfigs = list?.configs ?? new List<FtpServerConfig>();
            }
            catch { serverConfigs = new List<FtpServerConfig>(); }
        }
        else
        {
            serverConfigs = new List<FtpServerConfig>();
        }
        serverConfigIndex = EditorPrefs.GetInt(EditorPrefsFtpConfigIndexKey, -1);
        RefreshConfigNames();
    }

    private void SaveServerConfigs()
    {
        var list = new FtpConfigList { configs = serverConfigs };
        EditorPrefs.SetString(EditorPrefsFtpConfigsKey, JsonUtility.ToJson(list));
        EditorPrefs.SetInt(EditorPrefsFtpConfigIndexKey, serverConfigIndex);
    }

    // ===================== 操作按钮区 =====================

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("创建对比文件", "扫描 ArtRes/AB/{平台}，生成 ABCompareInfo.txt"),
                    GUILayout.Height(28)))
                CreateABCompareFile();

            if (GUILayout.Button(
                    new GUIContent("保存到 StreamingAssets",
                        "复制选中 AB 到 StreamingAssets，并写入 ABCompareInfo.txt"),
                    GUILayout.Height(28)))
                MoveABToStreamingAssets();
        }

        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(!IsServerUrlValid() || isUploading || isDownloading || isComparingRemote))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(
                        isUploading ? "上传中…" : "上传选中文件",
                        "上传勾选的文件到 FTP"), GUILayout.Height(30)))
                    StartUploadSelectedFiles();

                if (GUILayout.Button(new GUIContent(
                        isDownloading ? "下载中…" : "下载远端到本地",
                        "从 FTP 下载所有远端 AB 包到本地"), GUILayout.Height(30)))
                    StartDownloadAllFromRemote();
            }
        }

        using (new EditorGUI.DisabledScope(!IsServerUrlValid() || isComparingRemote || isUploading))
        {
            if (GUILayout.Button(new GUIContent("对比远端差异", "比较本地与远端 AB 包差异"),
                    GUILayout.Height(26)))
                StartCompareRemote();
        }
    }

    // ===================== AB 包列表预览 =====================

    private void DrawFileListSection()
    {
        if (fileListDirty)
        {
            RefreshFileList();
            fileListDirty = false;
        }

        EditorGUILayout.LabelField($"AB 包文件 ({abFileList.Count})", EditorStyles.boldLabel);

        if (abFileList.Count == 0)
        {
            EditorGUILayout.HelpBox("当前平台目录下无 AB 包文件。请先构建或手动放置。", MessageType.Info);
            return;
        }

        // 全选 / 全不选
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("全选", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                for (int i = 0; i < abFileList.Count; i++)
                {
                    var e = abFileList[i];
                    e.Selected = true;
                    abFileList[i] = e;
                }
            }
            if (GUILayout.Button("全不选", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                for (int i = 0; i < abFileList.Count; i++)
                {
                    var e = abFileList[i];
                    e.Selected = false;
                    abFileList[i] = e;
                }
            }
            GUILayout.FlexibleSpace();

            long totalSize = abFileList.Where(e => e.Selected).Sum(e => e.FileSize);
            int selCount = abFileList.Count(e => e.Selected);
            EditorGUILayout.LabelField($"已选 {selCount}/{abFileList.Count}  {FormatFileSize(totalSize)}", EditorStyles.miniLabel);
        }

        fileListScroll = EditorGUILayout.BeginScrollView(fileListScroll, GUILayout.Height(Mathf.Min(abFileList.Count * 22f + 4f, 160f)));
        for (int i = 0; i < abFileList.Count; i++)
        {
            var entry = abFileList[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                bool sel = EditorGUILayout.ToggleLeft(entry.FileName, entry.Selected, GUILayout.Width(140));
                if (sel != entry.Selected)
                {
                    entry.Selected = sel;
                    abFileList[i] = entry;
                }
                EditorGUILayout.LabelField(FormatFileSize(entry.FileSize), EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(entry.MD5, EditorStyles.miniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void RefreshFileList()
    {
        abFileList.Clear();
        string dir = Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex];
        if (!Directory.Exists(dir)) return;

        foreach (var fi in new DirectoryInfo(dir).GetFiles())
        {
            if (fi.Extension == "" || fi.Extension == ".txt")
            {
                abFileList.Add(new ABFileEntry
                {
                    FilePath = fi.FullName,
                    FileName = fi.Name,
                    FileSize = fi.Length,
                    MD5 = ABHashUtil.ComputeMD5File(fi.FullName),
                    Selected = true
                });
            }
        }
    }

    // ===================== 远端对比预览 =====================

    private void DrawRemoteDiffSection()
    {
        EditorGUILayout.LabelField("远端对比", EditorStyles.boldLabel);

        if (isComparingRemote)
        {
            EditorGUILayout.HelpBox("正在对比远端文件…", MessageType.Info);
            return;
        }

        if (remoteDiffs.Count == 0)
        {
            EditorGUILayout.HelpBox("点击「对比远端差异」查看本地与远端文件差异。", MessageType.Info);
            return;
        }

        diffScroll = EditorGUILayout.BeginScrollView(diffScroll, GUILayout.Height(Mathf.Min(remoteDiffs.Count * 20f + 4f, 120f)));
        foreach (var diff in remoteDiffs)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string tag = diff.Type == DiffType.Add ? "＋ 新增"
                    : diff.Type == DiffType.Modify ? "～ 修改"
                    : diff.Type == DiffType.Delete ? "✕ 远端多余" : "= 不变";
                Color prev = GUI.color;
                if (diff.Type == DiffType.Add) GUI.color = Color.green;
                else if (diff.Type == DiffType.Modify) GUI.color = Color.yellow;
                else if (diff.Type == DiffType.Delete) GUI.color = Color.red;
                EditorGUILayout.LabelField(tag, GUILayout.Width(80));
                GUI.color = prev;
                EditorGUILayout.LabelField(diff.FileName, EditorStyles.miniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void StartCompareRemote()
    {
        remoteDiffs.Clear();
        isComparingRemote = true;
        SetStatus("正在对比远端…", "", 0f);

        // 先确保本地列表是最新的
        RefreshFileList();

        // 在主线程预缓存所有 Unity API 返回值
        string tmpPath = Application.persistentDataPath + "/ABCompareInfo_TMP.txt";
        string ftpUrl = ABHotUpdateConfig.BuildFtpFileUrl(serverIP.Trim(), targetStrings[nowSelIndex], "ABCompareInfo.txt");
        string ftpUser = ABHotUpdateConfig.FtpUser;
        string ftpPassword = ABHotUpdateConfig.FtpPassword;
        bool useFtps = ABHotUpdateConfig.UseFtps;

        Task.Run(() =>
        {
            try
            {
                FtpWebRequest req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                req.Proxy = null;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.EnableSsl = useFtps;
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.UseBinary = true;

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var file = File.Create(tmpPath))
                {
                    byte[] buf = new byte[2048];
                    int read;
                    while ((read = respStream.Read(buf, 0, buf.Length)) > 0)
                        file.Write(buf, 0, read);
                }
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("远端对比文件下载失败: " + ex.Message);
                return false;
            }
        }).ContinueWith(t =>
        {
            EditorApplication.delayCall += () =>
            {
                if (t.Result && File.Exists(tmpPath))
                {
                    string remoteInfo = File.ReadAllText(tmpPath);
                    var remoteDict = ParseCompareInfo(remoteInfo);
                    var localDict = new Dictionary<string, string>();
                    foreach (var e in abFileList)
                        localDict[e.FileName] = e.MD5;

                    remoteDiffs.Clear();
                    foreach (var kv in remoteDict)
                    {
                        if (!localDict.ContainsKey(kv.Key))
                            remoteDiffs.Add(new RemoteDiffEntry { FileName = kv.Key, Type = DiffType.Delete });
                        else if (localDict[kv.Key] != kv.Value)
                            remoteDiffs.Add(new RemoteDiffEntry { FileName = kv.Key, Type = DiffType.Modify });
                        else
                            remoteDiffs.Add(new RemoteDiffEntry { FileName = kv.Key, Type = DiffType.Unchanged });
                    }
                    foreach (var kv in localDict)
                    {
                        if (!remoteDict.ContainsKey(kv.Key))
                            remoteDiffs.Add(new RemoteDiffEntry { FileName = kv.Key, Type = DiffType.Add });
                    }

                    // 排序：新增 > 修改 > 删除 > 不变
                    remoteDiffs = remoteDiffs
                        .OrderBy(d => d.Type == DiffType.Unchanged ? 3 : d.Type == DiffType.Delete ? 2 : d.Type == DiffType.Modify ? 1 : 0)
                        .ToList();

                    SetStatus($"对比完成: {remoteDiffs.Count} 项差异", "", -1f);
                }
                else
                {
                    SetStatus("远端对比文件下载失败", "", -1f);
                }
                isComparingRemote = false;
            };
        });
    }

    private Dictionary<string, string> ParseCompareInfo(string info)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(info)) return dict;
        string[] strs = info.Split('|');
        foreach (string s in strs)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            string[] parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                dict[parts[0]] = parts[2];
        }
        return dict;
    }

    // ===================== 上传历史 =====================

    private void DrawUploadHistorySection()
    {
        EditorGUILayout.LabelField("上传历史", EditorStyles.boldLabel);

        if (uploadHistory.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无上传记录。", MessageType.Info);
            return;
        }

        // 只显示最近 5 条
        int showCount = Mathf.Min(uploadHistory.Count, 5);
        for (int i = uploadHistory.Count - 1; i >= uploadHistory.Count - showCount; i--)
        {
            var entry = uploadHistory[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                Color prev = GUI.color;
                GUI.color = entry.success ? Color.green : Color.red;
                EditorGUILayout.LabelField(entry.success ? "✓" : "✗", GUILayout.Width(16));
                GUI.color = prev;
                EditorGUILayout.LabelField($"{entry.time}", EditorStyles.miniLabel, GUILayout.Width(130));
                EditorGUILayout.LabelField($"{entry.platform} {entry.fileCount}文件 {FormatFileSize(entry.totalSize)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"{entry.elapsedSeconds:F1}s", EditorStyles.miniLabel, GUILayout.Width(40));
            }
        }

        if (GUILayout.Button("清空历史", EditorStyles.miniButton))
        {
            uploadHistory.Clear();
            SaveUploadHistory();
        }
    }

    private void AddUploadHistoryEntry(int fileCount, long totalSize, double elapsed, bool success)
    {
        var entry = new UploadHistoryEntry
        {
            time = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            serverUrl = serverIP?.Trim() ?? "",
            platform = targetStrings[nowSelIndex],
            fileCount = fileCount,
            totalSize = totalSize,
            elapsedSeconds = elapsed,
            success = success
        };
        uploadHistory.Add(entry);
        if (uploadHistory.Count > MaxHistoryEntries)
            uploadHistory.RemoveRange(0, uploadHistory.Count - MaxHistoryEntries);
        SaveUploadHistory();
    }

    private void LoadUploadHistory()
    {
        string json = EditorPrefs.GetString(EditorPrefsUploadHistoryKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonUtility.FromJson<UploadHistoryList>(json);
                uploadHistory = list?.entries ?? new List<UploadHistoryEntry>();
            }
            catch { uploadHistory = new List<UploadHistoryEntry>(); }
        }
    }

    private void SaveUploadHistory()
    {
        var list = new UploadHistoryList { entries = uploadHistory };
        EditorPrefs.SetString(EditorPrefsUploadHistoryKey, JsonUtility.ToJson(list));
    }

    // ===================== 底部状态条 =====================

    private void DrawStatusBar(float pad)
    {
        if (string.IsNullOrEmpty(statusText) && progressValue < 0f)
        {
            GUILayout.Space(pad);
            return;
        }

        var sepRect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(sepRect, new Color(0.15f, 0.15f, 0.15f, 1f));

        EditorGUILayout.Space(4);

        if (progressValue >= 0f)
        {
            var barRect = EditorGUILayout.GetControlRect(false, 14f);
            barRect.xMin += pad;
            barRect.xMax -= pad;
            EditorGUI.ProgressBar(barRect, progressValue, "");
            EditorGUILayout.Space(2);
        }

        var lineRect = EditorGUILayout.GetControlRect(false, 16f);
        lineRect.xMin += pad;
        lineRect.xMax -= pad;

        if (!string.IsNullOrEmpty(statusText))
        {
            var labelRect = new Rect(lineRect.x, lineRect.y, lineRect.width - 80, lineRect.height);
            EditorGUI.LabelField(labelRect, statusText, EditorStyles.miniLabel);
        }

        if (!string.IsNullOrEmpty(statusDetail))
        {
            var detailRect = new Rect(lineRect.xMax - 60, lineRect.y, 60, lineRect.height);
            var rightStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            EditorGUI.LabelField(detailRect, statusDetail, rightStyle);
        }

        GUILayout.Space(pad);
    }

    // ===================== 上传逻辑 =====================

    private void StartUploadSelectedFiles()
    {
        // 用勾选的文件列表
        var selected = abFileList.Where(e => e.Selected).ToList();
        if (selected.Count == 0)
        {
            SetStatus("没有勾选任何文件。", "", -1f);
            return;
        }

        uploadQueue.Clear();
        foreach (var e in selected)
            uploadQueue.Enqueue(new UploadTask { FilePath = e.FilePath, FileName = e.FileName });

        uploadTotal = uploadQueue.Count;
        uploadDone = 0;
        uploadFailed = 0;
        uploadTotalBytes = selected.Sum(e => e.FileSize);
        uploadTransferredBytes = 0;
        uploadStopwatch = System.Diagnostics.Stopwatch.StartNew();
        isUploading = true;
        SetStatus("准备上传…", $"0/{uploadTotal}", 0f);

        EditorApplication.update += DriveUploadQueue;
    }

    private void DriveUploadQueue()
    {
        if (uploadQueue.Count == 0)
        {
            EditorApplication.update -= DriveUploadQueue;
            isUploading = false;
            uploadStopwatch?.Stop();
            double elapsed = uploadStopwatch?.Elapsed.TotalSeconds ?? 0;
            long avgSpeed = elapsed > 0 ? (long)(uploadTotalBytes / elapsed) : 0;

            string summary = uploadFailed > 0
                ? $"上传完成（{uploadFailed} 个失败）"
                : "上传完成";
            SetStatus($"{summary}  {FormatFileSize(uploadTotalBytes)}  耗时 {elapsed:F1}s  平均 {FormatFileSize(avgSpeed)}/s",
                $"{uploadDone}/{uploadTotal}", -1f);

            AddUploadHistoryEntry(uploadDone, uploadTotalBytes, elapsed, uploadFailed == 0);
            fileListDirty = true;
            return;
        }

        var task = uploadQueue.Dequeue();
        SetStatus($"上传中: {task.FileName}", $"{uploadDone}/{uploadTotal}",
            uploadTotal > 0 ? (float)uploadDone / uploadTotal : 0f);

        FtpUploadFileAsync(task.FilePath, task.FileName, success =>
        {
            if (success) uploadDone++;
            else { uploadFailed++; uploadDone++; }
            SetStatus($"上传完成: {task.FileName}", $"{uploadDone}/{uploadTotal}",
                uploadTotal > 0 ? (float)uploadDone / uploadTotal : 0f);
            Repaint();
        });
    }

    private async void FtpUploadFileAsync(string filePath, string fileName, Action<bool> onDone)
    {
        bool ok = false;
        // 在主线程预缓存，子线程中不能访问 Unity API 和实例字段
        string ftpUrl = ABHotUpdateConfig.BuildFtpFileUrl(serverIP.Trim(), targetStrings[nowSelIndex], fileName);
        string ftpUser = ABHotUpdateConfig.FtpUser;
        string ftpPassword = ABHotUpdateConfig.FtpPassword;
        bool useFtps = ABHotUpdateConfig.UseFtps;

        await Task.Run(() =>
        {
            try
            {
                FtpWebRequest req = WebRequest.Create(ftpUrl) as FtpWebRequest;
                if (req == null)
                    throw new InvalidOperationException("无法创建 FtpWebRequest，请检查 ftp:// 地址和端口。");

                req.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                req.Proxy = null;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.EnableSsl = useFtps;
                req.Method = WebRequestMethods.Ftp.UploadFile;
                req.UseBinary = true;
                req.ContentLength = new FileInfo(filePath).Length;

                using (Stream upLoadStream = req.GetRequestStream())
                using (FileStream file = File.OpenRead(filePath))
                {
                    byte[] bytes = new byte[2048];
                    int contentLength = file.Read(bytes, 0, bytes.Length);
                    while (contentLength != 0)
                    {
                        upLoadStream.Write(bytes, 0, contentLength);
                        contentLength = file.Read(bytes, 0, bytes.Length);
                    }
                }
                using (FtpWebResponse response = (FtpWebResponse)req.GetResponse())
                {
                    UnityEngine.Debug.Log(fileName + " 上传成功 " + response.StatusDescription);
                }
                ok = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("上传失败 " + fileName + ": " + ex.Message);
                ok = false;
            }
        });

        EditorApplication.delayCall += () => onDone?.Invoke(ok);
    }

    // ===================== 下载逻辑 =====================

    private void StartDownloadAllFromRemote()
    {
        downloadQueue.Clear();
        string localDir = Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex] + "/";
        if (!Directory.Exists(localDir))
            Directory.CreateDirectory(localDir);

        // 先下载远端对比文件获取文件列表
        isDownloading = true;
        SetStatus("正在获取远端文件列表…", "", 0f);

        // 在主线程预缓存所有 Unity API 返回值，子线程中不能调用
        string tmpPath = Application.persistentDataPath + "/ABCompareInfo_TMP.txt";
        string ftpUrl = ABHotUpdateConfig.BuildFtpFileUrl(serverIP.Trim(), targetStrings[nowSelIndex], "ABCompareInfo.txt");
        string ftpUser = ABHotUpdateConfig.FtpUser;
        string ftpPassword = ABHotUpdateConfig.FtpPassword;
        bool useFtps = ABHotUpdateConfig.UseFtps;

        Task.Run(() =>
        {
            try
            {
                FtpWebRequest req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                req.Proxy = null;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.EnableSsl = useFtps;
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.UseBinary = true;

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var file = File.Create(tmpPath))
                {
                    byte[] buf = new byte[2048];
                    int read;
                    while ((read = respStream.Read(buf, 0, buf.Length)) > 0)
                        file.Write(buf, 0, read);
                }

                var remoteDict = ParseCompareInfo(File.ReadAllText(tmpPath));
                var tasks = new List<UploadTask>();
                foreach (var kv in remoteDict)
                    tasks.Add(new UploadTask { FilePath = localDir + kv.Key, FileName = kv.Key });
                // 也下载对比文件本身
                tasks.Add(new UploadTask { FilePath = localDir + "ABCompareInfo.txt", FileName = "ABCompareInfo.txt" });
                return tasks;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("获取远端文件列表失败: " + ex.Message);
                return (List<UploadTask>)null;
            }
        }).ContinueWith(t =>
        {
            EditorApplication.delayCall += () =>
            {
                if (t.Result != null)
                {
                    foreach (var task in t.Result)
                        downloadQueue.Enqueue(task);

                    downloadTotal = downloadQueue.Count;
                    downloadDone = 0;
                    downloadFailed = 0;
                    SetStatus($"找到 {downloadTotal} 个远端文件，开始下载…", $"0/{downloadTotal}", 0f);
                    EditorApplication.update += DriveDownloadQueue;
                }
                else
                {
                    isDownloading = false;
                    SetStatus("获取远端文件列表失败", "", -1f);
                }
            };
        });
    }

    private void DriveDownloadQueue()
    {
        if (downloadQueue.Count == 0)
        {
            EditorApplication.update -= DriveDownloadQueue;
            isDownloading = false;
            string summary = downloadFailed > 0
                ? $"下载完成（{downloadFailed} 个失败）"
                : "下载完成";
            SetStatus(summary, $"{downloadDone}/{downloadTotal}", -1f);
            fileListDirty = true;
            return;
        }

        var task = downloadQueue.Dequeue();
        SetStatus($"下载中: {task.FileName}", $"{downloadDone}/{downloadTotal}",
            downloadTotal > 0 ? (float)downloadDone / downloadTotal : 0f);

        FtpDownloadFileAsync(task.FilePath, task.FileName, success =>
        {
            if (success) downloadDone++;
            else { downloadFailed++; downloadDone++; }
            SetStatus($"下载完成: {task.FileName}", $"{downloadDone}/{downloadTotal}",
                downloadTotal > 0 ? (float)downloadDone / downloadTotal : 0f);
            Repaint();
        });
    }

    private async void FtpDownloadFileAsync(string localPath, string fileName, Action<bool> onDone)
    {
        bool ok = false;
        // 在主线程预缓存，子线程中不能访问 Unity API 和实例字段
        string ftpUrl = ABHotUpdateConfig.BuildFtpFileUrl(serverIP.Trim(), targetStrings[nowSelIndex], fileName);
        string ftpUser = ABHotUpdateConfig.FtpUser;
        string ftpPassword = ABHotUpdateConfig.FtpPassword;
        bool useFtps = ABHotUpdateConfig.UseFtps;

        await Task.Run(() =>
        {
            try
            {
                FtpWebRequest req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                req.Proxy = null;
                req.KeepAlive = false;
                req.UsePassive = true;
                req.EnableSsl = useFtps;
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.UseBinary = true;

                using (var response = (FtpWebResponse)req.GetResponse())
                using (var respStream = response.GetResponseStream())
                using (var file = File.Create(localPath))
                {
                    byte[] buf = new byte[2048];
                    int read;
                    while ((read = respStream.Read(buf, 0, buf.Length)) > 0)
                        file.Write(buf, 0, read);
                }
                UnityEngine.Debug.Log(fileName + " 下载成功");
                ok = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("下载失败 " + fileName + ": " + ex.Message);
                ok = false;
            }
        });

        EditorApplication.delayCall += () => onDone?.Invoke(ok);
    }

    // ===================== 其他功能 =====================

    public void CreateABCompareFile()
    {
        DirectoryInfo directory = Directory.CreateDirectory(
            Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex]);
        FileInfo[] fileInfos = directory.GetFiles();

        string abCompareInfo = "";
        foreach (FileInfo info in fileInfos)
        {
            if (info.Extension == "")
            {
                abCompareInfo += info.Name + " " + info.Length + " " + ABHashUtil.ComputeMD5File(info.FullName);
                abCompareInfo += "|";
            }
        }
        if (abCompareInfo.Length == 0)
        {
            EditorUtility.DisplayDialog("AB 包工具", "当前平台目录下未找到无扩展名的 AB 包文件。", "确定");
            return;
        }
        abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);
        File.WriteAllText(Application.dataPath + "/ArtRes/AB/" + targetStrings[nowSelIndex] + "/ABCompareInfo.txt", abCompareInfo);
        AssetDatabase.Refresh();
        fileListDirty = true;
        SetStatus("对比文件生成成功", "", -1f);
        UnityEngine.Debug.Log("AB包对比文件生成成功");
    }

    private void MoveABToStreamingAssets()
    {
        UnityEngine.Object[] selectedAsset = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.DeepAssets);
        if (selectedAsset.Length == 0)
        {
            EditorUtility.DisplayDialog("AB 包工具", "请先在 Project 中选择要复制的 AB 资源。", "确定");
            return;
        }

        string abCompareInfo = "";
        foreach (UnityEngine.Object asset in selectedAsset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string fileName = assetPath.Substring(assetPath.LastIndexOf("/"));
            if (fileName.IndexOf('.') != -1)
                continue;
            AssetDatabase.CopyAsset(assetPath, "Assets/StreamingAssets" + fileName);
            FileInfo fileInfo = new FileInfo(Application.streamingAssetsPath + fileName);
            abCompareInfo += fileInfo.Name + " " + fileInfo.Length + " " + ABHashUtil.ComputeMD5File(fileInfo.FullName);
            abCompareInfo += "|";
        }
        abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);
        File.WriteAllText(Application.streamingAssetsPath + "/ABCompareInfo.txt", abCompareInfo);
        AssetDatabase.Refresh();
        SetStatus("已复制到 StreamingAssets", "", -1f);
    }

    // ===================== 工具方法 =====================

    private void SetStatus(string text, string detail, float progress)
    {
        statusText = text;
        statusDetail = detail;
        progressValue = progress;
        Repaint();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return bytes + "B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + "KB";
        return (bytes / (1024.0 * 1024.0)).ToString("F1") + "MB";
    }
}
