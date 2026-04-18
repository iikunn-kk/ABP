using UnityEngine;
using UnityEngine.SceneManagement;

public class Main : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ABUpdateMgr.Instance.CheckUpdate((isOver) =>
        {
            if (isOver)
            {
                Debug.Log("检测更新结束，隐藏进度条");
            }
            else
            {
                Debug.Log("网络出错，可以提示玩家去检测网络或者重启游戏");
            }

        }, (str) =>
        {
            //以后可以在这里处理更新加载界面上显示信息相关的逻辑
            Debug.Log(str);
        });
    }

    // Update is called once per frame
    void Update()
    {

    }
}
