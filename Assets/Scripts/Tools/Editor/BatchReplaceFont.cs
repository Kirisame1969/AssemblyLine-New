#if UNITY_EDITOR // 确保打包时不报错

using UnityEngine;
using UnityEditor;
using TMPro; // 如果使用的是旧版UGUI Text，请换成 using UnityEngine.UI; 并将 TMP_Text 替换为 Text

public class BatchReplaceFont : EditorWindow
{
    public TMP_FontAsset newFont;

    [MenuItem("Tools/批量替换字体 (TMP)")]
    public static void ShowWindow()
    {
        GetWindow<BatchReplaceFont>("批量替换字体");
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("1. 请将新的字体文件拖入下方：", EditorStyles.boldLabel);
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("新字体", newFont, typeof(TMP_FontAsset), false);

        GUILayout.Space(20);
        
        if (newFont == null)
        {
            EditorGUILayout.HelpBox("请先在上方分配新字体！", MessageType.Warning);
            return;
        }

        // 功能一：替换场景
        if (GUILayout.Button("替换【当前场景】中所有文字", GUILayout.Height(30)))
        {
            ReplaceInScene();
        }

        GUILayout.Space(10);

        // 功能二：替换所有预制体
        GUI.backgroundColor = Color.yellow; // 给危险操作加个醒目的颜色
        if (GUILayout.Button("替换【所有预制体】中所有文字 (全局扫瞄)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("确认操作", "这将扫描项目中所有的预制体并替换字体，操作无法撤销。建议在此之前已经做好了 Git 提交。\n\n确定要继续吗？", "确定替换", "取消"))
            {
                ReplaceInAllPrefabs();
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private void ReplaceInScene()
    {
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (TMP_Text txt in allTexts)
        {
            // 排除预制体资源，只替换场景中的实例
            if (txt.gameObject.scene.name != null) 
            {
                txt.font = newFont;
                EditorUtility.SetDirty(txt); 
                count++;
            }
        }
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[场景替换] 完成！共替换了 {count} 个文本组件。请按 Ctrl+S 保存场景。");
    }

    private void ReplaceInAllPrefabs()
    {
        // 查找项目中所有预制体的 GUID
        string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        
        int modifiedPrefabCount = 0;
        int modifiedTextCount = 0;

        for (int i = 0; i < prefabGUIDs.Length; i++)
        {
            // 将 GUID 转换为具体的文件路径
            string path = AssetDatabase.GUIDToAssetPath(prefabGUIDs[i]);
            
            // 显示进度条（如果预制体成百上千，不加进度条Unity会像死机了一样）
            EditorUtility.DisplayProgressBar("正在替换预制体字体", $"正在处理: {path}", (float)i / prefabGUIDs.Length);

            // 加载预制体资源
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 获取预制体身上及子物体所有的 Text 组件（包含未激活/隐藏的物体）
            TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
            
            if (texts.Length > 0)
            {
                bool isPrefabModified = false;

                foreach (TMP_Text txt in texts)
                {
                    if (txt.font != newFont) // 如果字体不一样才替换
                    {
                        txt.font = newFont;
                        EditorUtility.SetDirty(txt); // 标记组件已被修改
                        modifiedTextCount++;
                        isPrefabModified = true;
                    }
                }

                // 只有当这个预制体真正被修改过时，才执行保存，节省性能
                if (isPrefabModified)
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                    modifiedPrefabCount++;
                }
            }
        }

        // 清除进度条
        EditorUtility.ClearProgressBar();
        // 强制刷新并保存所有资产
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[预制体替换] 完成！共修改了 <color=green>{modifiedPrefabCount}</color> 个预制体，替换了 <color=green>{modifiedTextCount}</color> 个文本组件。");
    }
}

#endif