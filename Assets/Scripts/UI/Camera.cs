using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("摄像机缩放设置")]
    // 已根据 image_1.png 同步
    public float zoomSpeed = 5f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("地图边界设置")]
    // 已根据 image_1.png 同步
    public float mapMinX = -1f;   // 截图显示为 -1
    public float mapMaxX = 138f;  // 截图显示为 138
    public float mapMinY = -1f;   // 截图显示为 -1
    public float mapMaxY = 103f;  // 截图显示为 103

    private Camera cam;
    private Vector3 dragOrigin;

    void Start()
    {
        cam = GetComponent<Camera>();

        // 核心修改：游戏开始时聚焦游戏中心
        FocusOnMapCenter();
    }

    void LateUpdate()
    {
        // 1. 处理鼠标中键拖拽 (保持)
        HandlePan();

        // 2. 处理滚轮缩放 (保持：以鼠标为中心缩放)
        HandleZoomToMouse();

        // 3. 将摄像机限制在边界内
        ClampCamera();
    }

    // 新增方法：用于在游戏开始时聚焦地图中心
    private void FocusOnMapCenter()
    {
        // 步骤 A: 计算地图的几何中心
        float centerX = (mapMinX + mapMaxX) / 2f;
        float centerY = (mapMinY + mapMaxY) / 2f;

        // 步骤 B: 将摄像机直接移动到中心点 (保留 z 轴)
        transform.position = new Vector3(centerX, centerY, transform.position.z);

        // 步骤 C: 即使在 Start 中设置了位置，也需要调用一次 ClampCamera()。
        // 这样可以确保如果摄像机初始视口（Zoom）加上地图中心位置超出了边界（例如地图特别小），也能在游戏第一帧前被正确修正。
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

    private void HandleZoomToMouse()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            Vector3 mouseWorldPosBefore = cam.ScreenToWorldPoint(Input.mousePosition);

            float newSize = cam.orthographicSize - scrollInput * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);

            Vector3 mouseWorldPosAfter = cam.ScreenToWorldPoint(Input.mousePosition);

            Vector3 offset = mouseWorldPosBefore - mouseWorldPosAfter;
            transform.position += offset;
        }
    }

    // 核心边界限制逻辑 (保持，优化了健壮性)
    private void ClampCamera()
    {
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        float mapWidth = mapMaxX - mapMinX;
        float mapHeight = mapMaxY - mapMinY;

        Vector3 clampedPosition = transform.position;

        // 处理 X 轴限制：如果地图比屏幕还小，强制居中
        if (mapWidth < camHalfWidth * 2)
        {
            clampedPosition.x = (mapMinX + mapMaxX) / 2f;
        }
        else
        {
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, mapMinX + camHalfWidth, mapMaxX - camHalfWidth);
        }

        // 处理 Y 轴限制
        if (mapHeight < camHalfHeight * 2)
        {
            clampedPosition.y = (mapMinY + mapMaxY) / 2f;
        }
        else
        {
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, mapMinY + camHalfHeight, mapMaxY - camHalfHeight);
        }

        transform.position = clampedPosition;
    }
}