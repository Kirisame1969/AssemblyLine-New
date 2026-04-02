using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("摄像机缩放设置")]
    public float zoomSpeed = 5f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("地图边界设置 (在Inspector中填入你的地图大小)")]
    public float mapMinX = -20f; // 地图最左边的X坐标
    public float mapMaxX = 20f;  // 地图最右边的X坐标
    public float mapMinY = -20f; // 地图最下方的Y坐标
    public float mapMaxY = 20f;  // 地图最上方的Y坐标

    private Camera cam;
    private Vector3 dragOrigin;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    // 注意：处理摄像机的代码，建议放在 LateUpdate 中。
    // 这样能确保在所有游戏逻辑（如玩家移动、机器刷新）执行完毕后，摄像机再进行跟随或限制，防止画面抖动。
    void LateUpdate()
    {
        // 1. 处理鼠标中键拖拽
        HandlePan();

        // 2. 处理滚轮缩放
        HandleZoom();

        // 3. 将摄像机限制在边界内 (必须放在移动和缩放之后)
        ClampCamera();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 currentMousePosition = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 difference = dragOrigin - currentMousePosition;
            transform.position += difference;
        }
    }

    private void HandleZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            cam.orthographicSize -= scrollInput * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    // 核心边界限制逻辑
    private void ClampCamera()
    {
        // 步骤 A: 计算当前摄像机能看到的一半高度和一半宽度
        // orthographicSize 本身就是摄像机高度的一半
        float camHalfHeight = cam.orthographicSize;
        // 高度乘以屏幕宽高比，得出宽度的一半
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        // 步骤 B: 计算摄像机中心点允许移动的最小和最大范围
        // 地图最左边 加上 摄像机一半宽度，就是摄像机中心点能到达的最左侧
        float limitMinX = mapMinX + camHalfWidth;
        float limitMaxX = mapMaxX - camHalfWidth;
        float limitMinY = mapMinY + camHalfHeight;
        float limitMaxY = mapMaxY - camHalfHeight;

        // 特殊情况处理：如果地图太小，甚至比你当前缩放的画面还要小，就把摄像机强制固定在地图中心
        if (limitMinX > limitMaxX) limitMinX = limitMaxX = (mapMinX + mapMaxX) / 2f;
        if (limitMinY > limitMaxY) limitMinY = limitMaxY = (mapMinY + mapMaxY) / 2f;

        // 步骤 C: 获取当前位置，并使用 Mathf.Clamp 强制把坐标限制在这个范围内
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, limitMinX, limitMaxX);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, limitMinY, limitMaxY);

        // 将限制后的坐标重新赋值给摄像机
        transform.position = clampedPosition;
    }
}