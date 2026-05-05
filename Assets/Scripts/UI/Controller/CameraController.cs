using UnityEngine;

/// <summary>
/// 大世界摄像机控制器
/// 负责处理大世界视图的平移（中键拖拽）、缩放（滚轮，以鼠标为中心），
/// 以及将摄像机视野严格限制在指定的地图边界内。
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("缩放参数设置")]
    public float zoomSpeed = 5f;  // 缩放速度系数
    public float minZoom = 3f;    // 最小缩放值（Orthographic Size 越小，视野越近）
    public float maxZoom = 15f;   // 最大缩放值（Orthographic Size 越大，视野越远）

    [Header("地图边界设置")]
    public float mapMinX = -1f;   // 地图左边界
    public float mapMaxX = 138f;  // 地图右边界
    public float mapMinY = -1f;   // 地图下边界
    public float mapMaxY = 103f;  // 地图上边界

    private Camera cam;           // 摄像机组件引用
    private Vector3 dragOrigin;   // 记录平移操作时的拖拽起始世界坐标
    public bool IsControlDisabled = false;  // 控制权移交锁

    void Start()
    {
        cam = GetComponent<Camera>();

        // 游戏初始化时，将摄像机焦点定位到地图正中心
        FocusOnMapCenter();
    }

    void LateUpdate()
    {
        // 采用 LateUpdate 处理摄像机逻辑，确保在逻辑帧中所有的物理或物体移动结算完毕后再移动画面
        // 如果控制权被剥夺，彻底跳过本帧的所有摄像机运算
        if (IsControlDisabled) return;
        // 1. 处理鼠标中键拖拽平移
        HandlePan();

        // 2. 处理鼠标滚轮缩放（以鼠标当前悬停位置为缩放锚点）
        HandleZoomToMouse();

        // 3. 执行边界约束，确保摄像机可视区域不会越出地图限定的边界
        ClampCamera();
    }

    /// <summary>
    /// 将摄像机移动到设定地图边界的几何正中心。
    /// </summary>
    private void FocusOnMapCenter()
    {
        // 计算地图中心点的 X 和 Y 坐标
        float centerX = (mapMinX + mapMaxX) / 2f;
        float centerY = (mapMinY + mapMaxY) / 2f;

        // 移动摄像机位置，保持 Z 轴深度不变
        transform.position = new Vector3(centerX, centerY, transform.position.z);

        // 聚焦后必须执行一次边界约束。
        // 防止当前初始正交大小（视野）过大，导致即使处于中心点也会超出地图可视边界。
        ClampCamera();
    }

    /// <summary>
    /// 处理摄像机的平移逻辑（基于鼠标中键拖拽）。
    /// </summary>
    private void HandlePan()
    {
        // 鼠标中键按下的瞬间，记录当前的鼠标世界坐标作为拖拽参考原点
        if (Input.GetMouseButtonDown(2))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }

        // 鼠标中键保持按压期间，持续计算位置偏移并反向移动摄像机
        if (Input.GetMouseButton(2))
        {
            Vector3 currentMousePosition = cam.ScreenToWorldPoint(Input.mousePosition);
            // 差值 = 原点 - 当前点（摄像机需要向鼠标移动的反方向平移以产生“拖拽地图”的错觉）
            Vector3 difference = dragOrigin - currentMousePosition;
            transform.position += difference;
        }
    }

    /// <summary>
    /// 处理摄像机的缩放逻辑（基于鼠标滚轮），并保持缩放中心为当前的鼠标物理悬停点。
    /// </summary>
    private void HandleZoomToMouse()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            // 缩放前，记录鼠标当前在屏幕上的位置所对应的真实世界坐标
            Vector3 mouseWorldPosBefore = cam.ScreenToWorldPoint(Input.mousePosition);

            // 根据滚轮输入计算新的正交视野大小，并进行最大/最小值钳制
            float newSize = cam.orthographicSize - scrollInput * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);

            // 缩放后，再次获取此时鼠标屏幕位置对应的世界坐标
            Vector3 mouseWorldPosAfter = cam.ScreenToWorldPoint(Input.mousePosition);

            // 计算缩放导致的世界坐标偏移量，并通过移动摄像机来补偿该偏移
            // 从而实现完美的“以鼠标指针为中心进行缩放”的 UX 体验
            Vector3 offset = mouseWorldPosBefore - mouseWorldPosAfter;
            transform.position += offset;
        }
    }

    /// <summary>
    /// 摄像机边界约束逻辑。确保正交摄像机的四角可视范围不会越过设定的 mapMin/Max 边界。
    /// 具备防呆机制，处理了视野比地图更大的极端情况。
    /// </summary>
    private void ClampCamera()
    {
        // 提取正交摄像机当前视野的半高和半宽
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        // 计算地图设定的绝对宽高
        float mapWidth = mapMaxX - mapMinX;
        float mapHeight = mapMaxY - mapMinY;

        Vector3 clampedPosition = transform.position;

        // --- X 轴边界约束 ---
        // 防呆：如果当前可视范围的宽度已经大于地图总宽度，强制将摄像机锁定在水平中心
        if (mapWidth < camHalfWidth * 2)
        {
            clampedPosition.x = (mapMinX + mapMaxX) / 2f;
        }
        else
        {
            // 常规：限制摄像机的 X 坐标，使其左右两边的视野边界最多只能贴齐地图边界
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, mapMinX + camHalfWidth, mapMaxX - camHalfWidth);
        }

        // --- Y 轴边界约束 ---
        // 防呆：如果当前可视范围的高度已经大于地图总高度，强制将摄像机锁定在垂直中心
        if (mapHeight < camHalfHeight * 2)
        {
            clampedPosition.y = (mapMinY + mapMaxY) / 2f;
        }
        else
        {
            // 常规：限制摄像机的 Y 坐标，使其上下两边的视野边界最多只能贴齐地图边界
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, mapMinY + camHalfHeight, mapMaxY - camHalfHeight);
        }

        // 回写最终约束后的安全位置
        transform.position = clampedPosition;
    }
}