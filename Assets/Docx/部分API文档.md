# AssemblyLine 核心类与 API 参考手册

本手册记录了 AssemblyLine 项目中已实装、定义清晰，并在核心业务逻辑中被高频调用的类、接口和核心方法。

按照 MVC 架构标准，划分为数据层、控制层、表现层，及统管全局的架构生命周期层。

### 注意: 不能简单认为本文件展示的类或接口等仅拥有文本中展示出的几个接口或方法,具体内容仍需自行查阅. 本文件为不完整列表,仅供参考.

---

## 1. 数据抽象与契约层 (Data Abstraction & Interfaces)

本节记录解耦表现层与业务逻辑的核心接口与纯数据基类。

### `IConfigurablePort` (Interface)
* **作用**：为支持白名单/规则设置的端口提供多态特征。表现层面板可依赖此接口读取与修改配置，而无需关心具体的模块类型。
* **关键接口方法**：
  * `GetRules()`：返回该端口绑定的 `PortRuleConfig` 实例引用。

### `PortRuleConfig` (Class)
* **作用**：I/O 端口的独立规则配置载体，封装具体的吞吐拦截逻辑。
* **确认包含属性与方法**：
  * `Whitelist`：白名单集合。记录允许通过的物品 ID。
  * `MarketPriority`：市场优先级。用于在全局交割时决定被抽取的先后顺序。
  * `IsAllowed(ItemDefinition item)`：放行仲裁。校验传入的物品是否符合当前端口的放行规则。

### `MachineModuleData` (Base Class)
* **作用**：所有机器模块的运行时数据基类，定义模块在机箱内的通用空间与层级属性。
* **确认包含属性与方法**：
  * `ParentShell`：归属机箱。指向该模块当前被安装的 `MachineShellData` 实例。
  * `LocalBottomLeft`：局部坐标。记录模块在机箱内部网格的左下角锚点位置。
  * `GetOccupiedLocalCells()`：空间占用获取。计算并返回该模块当前占用的所有相对坐标集合。
  * `ToSaveData()`：多态扁平化快照提取。利用反射记录 `ModuleType` 并提取具体子类（如缓存区、规则）数据的 DTO。

### `MachineShellData` (Class)
* **作用**：机箱实例的数据总集，维护内部局域物流网与所有子模块的引用。
* **确认包含属性与方法**：
  * `MainCore`：当前机箱安装的核心逻辑模块引用。
  * `InputPorts` / `OutputPorts`：当前机箱安装的输入/输出匣集合。
  * `RecalculateStats()`：增益重算。在模块安装或拆卸后触发，重新计算机箱的全局运行属性。
  * `ToSaveData()`：机箱快照提取。将运行时引用剥离，提取为纯基础数据类型（自动丢弃 `DeadCells` 等运行时缓存空间）。

### `InventoryData` (Class) `[已知对外暴露]`
* **作用**：纯数据驱动的扁平化机器库存/仓储系统，供核心逻辑层安全调用。
* **关键方法**：
  * `TryAdd()` / `TryTake()`：执行标准的输入/输出物品吞吐校验。
  * `ExtractItem(string itemID, int amount)`：执行跨库存的定向定量抽取。
  * `CompressSlots()` / `InteractSlots()`：执行内存级的库存碎片整理与同类堆叠合并。
  * `Resize(int newCapacity)`：动态重置容量，并返回缩容导致的溢出物品集合。
  * `RestoreFromSaveData()`：基于传入解析委托，还原 JSON 存档数据。

### `AssemblyLine.Data.SaveData` (Namespace)
* **作用**：统管所有存读档 DTO（Data Transfer Object）。负责斩断引擎组件引用与循环引用黑洞。
* **核心类**：
  * `GameSnapshotData`：存档根节点。包含版本号、资金、及各类子系统的序列化集合。
  * `ModuleSaveData` / `MachineShellSaveData` / `GridCellSaveData` 等降维实体。

---

## 2. 全局管理器服务 (Core Managers / Controllers)

本节记录挂载于大世界中、负责仲裁与推进游戏核心逻辑的单例服务。

### `ConfigManager`
* **作用**：静态资产（ScriptableObject）的全局检索枢纽，在初始化时构建 O(1) 复杂度的哈希索引，负责防重校验与跨场景数据寻址。
* **关键方法**：
  * `GetItem()` / `GetRecipe()` / `GetModule()` / `GetProfile()`：单体查询。根据字符串 ID 极速获取对应的静态配置表实例（为反序列化重构指针服务）。
  * `GetAllItems()`：图鉴遍历。获取全游戏已注册的物品列表。

### `MachineManager`
* **作用**：机器物理操作的仲裁者与 Tick 加工引擎，处理空间碰撞、物流交割与状态步进。
* **关键方法**：
  * `CanPlaceModule()`：判定模块是否满足放置条件（空间、隔断、规则）。
  * `PlaceModule()` / `RemoveModule()`：执行模块的实际安装与拆装操作，并触发数据底座更新。
  * `TryIngestItem()`：传送带推入拦截。校验物品是否允许进入机器（含白名单判定）。
  * `TryConsumeGlobalItems()`：跨仓库原子级排序扣减，执行严密的市场经济交割。
  * `ClearAll()`：热重置。瞬间清空内存中的所有机箱实体。

### `GridManager` & `StripManager`
* **作用**：逻辑网格与外部物流条带的拓扑与占用维护。
* **关键方法**：
  * `GridManager.GetGridCell(Vector2Int)`：寻址。
  * `GridManager.GetAllCells()`：暴露内部二维数组，供存档系统执行稀疏矩阵提取。
  * `StripManager.OnBeltModified()`：基于 BFS 动态执行条带的切分与同化合并。
  * `ClearAll()`：热重置。瞬间解散网格占用与拓扑内存指针。

### `EconomyManager` & `SimulationController`
* **作用**：经济账户管理与全局时间/实体步进驱动。
* **关键方法**：
  * `EconomyManager.AddFunds()` / `ConsumeFunds()`：正常的资金增减拦截。
  * `EconomyManager.SetFunds(long amount)`：[强制模式] 跨过正常拦截逻辑，强行覆盖当前资金并广播 UI 更新，专用于读档。
  * `SimulationController.CurrentTick`：获取当前系统的全局逻辑时钟刻度。

---

## 3. 表现与交互层 (View & Interaction)

本节记录负责捕获玩家输入、路由焦点与渲染数据的表现层组件。遵循自主重绘与事件监听准则。

### `MachineGUIController`
* **作用**：机器内部装配面板的控制中枢，处理局域 UI 内的用户行为映射与动画调度。
* **关键方法与机制**：
  * `OpenPanel()` / `ClosePanel()`：利用 DOTween 进行异步的弹出 (`DOScale`) / 渐隐 (`DOFade`) 动画调度。
  * `HandleModuleInteraction()`：捕获点击事件并根据模块类型（多态）呼出对应的配置面板，如遇到已打开面板则阻断底层大网格点击。
  * **退栈机制**：监听 `SaveLoadManager.OnSimulationDataRestored` 事件，触发强行关闭，防止读档后遗留的悬空指针引发 NRE。

### `UIPortConfigPanel` & `UIWarehousePanel` `[待补充完整结构]`
* **作用**：规则控制与仓储视图的子系统面板。
* **确认包含**：依赖底层的 `IBeginDragHandler` 等接口完成视图层面的物品拖拽，再反馈至 `InventoryData` 数据层。

### `InteractionController`
* **作用**：大世界视觉表现与物理交互总控。
* **关键方法与机制**：
  * `SpawnItemVisual()` / `SpawnFloatingText()` / `PlaceShellInWorld()`：数据视觉实体化操作。
  * **焦土政策与重建**：监听 `OnSimulationDataRestored` 事件，立刻执行所有已知旧视觉元素的 `Destroy`，并基于刷新后的 `GridManager` 进行全量重绘。

### `UIRaycastDebugger` (工具类)
* **作用**：UI 事件穿透测试器。在 `Update` 帧中发射模拟射线，用于实时排查 UI 遮挡与隐形透明网格吞噬事件的问题。

---

## 4. 架构与场景管线 (Game Flow & Architect)

本节记录处理游戏跨场景生命周期与文件级存档业务的顶层设施。

### `Bootstrapper`
* **作用**：挂载于唯一入口 `BootScene` 的启动器。执行 `DontDestroyOnLoad` 以赋予核心管理器跨场景的永生特权，并执行初始跃迁。

### `GameFlowManager`
* **作用**：掌控三场景流转架构（Three-Scene Architecture），携带跨越场景的内存负荷。
* **关键方法**：
  * `StartNewGame()` / `LoadGame(string)` / `ReturnToMainMenu()`：全游戏生命周期跃迁入口。
  * **流转协程**：调度含 `CanvasGroup.blocksRaycasts` 防穿透的 DOTween 黑屏过渡，确保 `LoadSceneAsync` 与组件初始化 (`Awake`) 的绝对时序安全，最后触发数据注入。

### `SaveLoadManager`
* **作用**：磁盘文件与运行内存的中枢转换器。
* **关键机制**：
  * `SaveGame()`：执行“聚合器模式”，从各子系统扒取数据层快照转化为 JSON。
  * `LoadGame()`：执行“双轨重构模式”，Phase 1 基于 ID 解析完成基础实体的实例化；Phase 2 完成双向物理与拓扑指针的重绑定，最终向全系统抛出 `OnSimulationDataRestored` 广播。