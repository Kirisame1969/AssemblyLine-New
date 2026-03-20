using System.Collections.Generic;
using UnityEngine;

public class MachineAssemblyTester : MonoBehaviour
{
    private MachineShellData _activeShell;
    private MachineModuleData _previewModule;
    private List<GameObject> _previewVisuals = new List<GameObject>();

    void Update()
    {
        // 1. 按 M 键：生成一个 3x4 的复杂测试机箱
        if (Input.GetKeyDown(KeyCode.M)) CreateTestShell();

        // 2. 按数字键：把不同形状的模块“抓”到鼠标上
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectModule(new MachineCoreData()); // 1x1 核心
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectModule(new RectModuleData(2, 1)); // 2x1 加速器
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectModule(new RectModuleData(2, 2)); // 2x2 大型组件

        // 3. 按 R 键：旋转手上的模块
        if (Input.GetKeyDown(KeyCode.R) && _previewModule != null)
        {
            _previewModule.Rotation = (ModuleRotation)(((int)_previewModule.Rotation + 1) % 4);
        }

        // 4. 核心逻辑：鼠标悬停预览与左键放置
        UpdatePreviewAndPlacement();
    }

    private void CreateTestShell()
    {
        if (_activeShell != null) return; // 已经生成过了

        // 假设我们在世界坐标 (5, 5) 处生成一个 3x4 的机箱
        RectInt bounds = new RectInt(5, 5, 3, 4);
        _activeShell = new MachineShellData(bounds);

        // 【设置障碍】：在 y=1 和 y=2 之间，加一道横向的隔断墙 (长为2格，留一个缺口)
        _activeShell.PartitionWalls.Add(new InternalWall(new Vector2Int(0, 1), new Vector2Int(0, 2)));
        _activeShell.PartitionWalls.Add(new InternalWall(new Vector2Int(1, 1), new Vector2Int(1, 2)));

        // 【设置死区】：把局部坐标 (2, 3) 设为绝对死区（右上角的柱子）
        _activeShell.DeadCells.Add(new Vector2Int(2, 3));

        // --- 视觉表现：画出机箱底板和障碍物 ---
        for (int x = 0; x < bounds.width; x++)
        {
            for (int y = 0; y < bounds.height; y++)
            {
                Vector2 worldPos = new Vector2(bounds.xMin + x, bounds.yMin + y);
                if (_activeShell.DeadCells.Contains(new Vector2Int(x, y)))
                {
                    CreateQuad(worldPos, Color.black, 0.9f); // 黑色的死区
                }
                else
                {
                    CreateQuad(worldPos, new Color(0.2f, 0.2f, 0.2f, 0.5f), 0.95f); // 灰色的可用底板
                }
            }
        }
        
        Debug.Log("✅ 测试机箱已生成！带有一道横向隔墙和一个黑色死区。");
    }

    private void SelectModule(MachineModuleData module)
    {
        _previewModule = module;
        Debug.Log($"拿起了模块，当前旋转: {_previewModule.Rotation}");
    }

    private void UpdatePreviewAndPlacement()
    {
        if (_activeShell == null || _previewModule == null) return;

        // 获取鼠标的世界坐标，并粗略转为网格坐标 (你可能需要用你自己的 GridManager 转换方法)
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int worldGridPos = new Vector2Int(Mathf.RoundToInt(mouseWorld.x), Mathf.RoundToInt(mouseWorld.y));
        
        // 转换为机箱的局部坐标
        Vector2Int localPos = new Vector2Int(worldGridPos.x - _activeShell.Bounds.xMin, worldGridPos.y - _activeShell.Bounds.yMin);
        _previewModule.LocalBottomLeft = localPos;

        // 【调用核心四重锁校验】
        bool canPlace = MachineManager.Instance.CanPlaceModule(_activeShell, _previewModule);

        // --- 视觉表现：绘制鼠标跟随的红绿预览块 ---
        ClearPreview();
        Color previewColor = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f); // 绿灯或红灯
        
        foreach (Vector2Int cell in _previewModule.GetOccupiedLocalCells())
        {
            Vector2 cellWorldPos = new Vector2(_activeShell.Bounds.xMin + cell.x, _activeShell.Bounds.yMin + cell.y);
            GameObject quad = CreateQuad(cellWorldPos, previewColor, 0.8f);
            _previewVisuals.Add(quad);
        }

        // --- 点击放置 ---
        if (Input.GetMouseButtonDown(0))
        {
            if (canPlace)
            {
                MachineManager.Instance.PlaceModule(_activeShell, _previewModule);
                
                // 放置成功，把预览块变成实体的蓝色块
                foreach (GameObject visual in _previewVisuals)
                {
                    visual.GetComponent<Renderer>().material.color = Color.blue;
                }
                _previewVisuals.Clear(); // 清空预览列表，让它们留在地上
                _previewModule = null;   // 手上置空
            }
            else
            {
                Debug.LogWarning(" 这里放不下去！被阻挡了！");
            }
        }
    }

    private void ClearPreview()
    {
        foreach (var obj in _previewVisuals) Destroy(obj);
        _previewVisuals.Clear();
    }

    // 辅助方法：生成一个纯色方块用于显示
    private GameObject CreateQuad(Vector2 pos, Color color, float scale)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = new Vector3(pos.x, pos.y, 0);
        quad.transform.localScale = new Vector3(scale, scale, 1);
        
        // 使用 Unlit/Color 材质避免受光照影响
        Renderer r = quad.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Unlit/Color")); 
        r.material.color = color;
        
        // 关掉碰撞体，防止干扰鼠标点击
        Destroy(quad.GetComponent<Collider>()); 
        return quad;
    }
}