using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyLine.Core.Manager.GameFlow
{
    public class Bootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // 确保该物体上的所有子组件和管理器在整个游戏周期内永不销毁
            DontDestroyOnLoad(gameObject);

            // 初始化完成后，立刻跳转到主菜单场景
            SceneManager.LoadScene("MainMenuScene");
            
            Debug.Log("[Bootstrapper] 全局管理器初始化完毕，进入主菜单。");
        }
    }
}