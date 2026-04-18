// 引用 System.IO 命名空间，提供文件和目录操作类（如 FileInfo、File）
using System.IO;
// 引用 UnityEditor 命名空间，提供 Unity 编辑器相关类（如 MenuItem、AssetDatabase、Selection）
using UnityEditor;
// 引用 UnityEngine 命名空间，提供 Unity 引擎核心功能（如 Object、Debug）
using UnityEngine;

// 定义 MoveABToSA 类，用于将选中的 AB 包移动到 StreamingAssets 文件夹
// 该类只在编辑器中使用，不会被打包到游戏中
public class MoveABToSA
{
    // 使用 MenuItem 特性在 Unity 菜单栏中添加菜单项
    // 菜单路径为 "AB包工具/移动选中资源到StreamingAssests中"
    // [MenuItem("AB包工具/移动选中资源到StreamingAssests中")]
    // 定义私有静态方法，执行移动 AB 包到 StreamingAssets 的操作
    private static void MoveABToStreamingAssets()
    {
        // 使用 Selection.GetFiltered 获取当前在 Project 窗口中选中的资源
        // typeof(Object): 指定筛选的资源类型为 Object（所有类型）
        // SelectionMode.DeepAssets: 包含选中文件夹内的所有子资源（递归搜索）
        Object[] selectedAsset = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        // 检查是否有选中的资源，如果没有选中的资源，直接返回
        if (selectedAsset.Length == 0)
            return;


        // 定义字符串变量，用于拼接 AB 包对比信息
        // 格式为：文件名 文件大小 MD5值|文件名 文件大小 MD5值|...
        string abCompareInfo = "";

        // 遍历所有选中的资源对象
        foreach (Object asset in selectedAsset)
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
}
