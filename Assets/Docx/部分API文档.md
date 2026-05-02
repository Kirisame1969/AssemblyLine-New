# AssemblyLine 核心类与 API 参考手册

本手册记录了 AssemblyLine 项目中已实装、定义清晰，并在核心业务逻辑中被高频调用的类、接口和核心方法。

按照 MVC 架构标准，划分为数据层、控制层与表现层。

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
* **关键属性与方法**：
  * `Whitelist`：白名单集合。记录允许通过的物品 ID。
  * `MarketPriority`：市场优先级。用于在全局交割时决定被抽取的先后顺序。
  * `IsAllowed(ItemDefinition item)`：放行仲裁。校验传入的物品是否符合当前端口的放行规则。

### `MachineModuleData` (Base Class)
* **作用**：所有机器模块的运行时数据基类，定义模块在机箱内的通用空间与层级属性。
* **关键属性与方法**：
  * `ParentShell`：归属机箱。指向该模块当前被安装的 `MachineShellData` 实例。
  * `LocalBottomLeft`：局部坐标。记录模块在机箱内部网格的左下角锚点位置。
  * `GetOccupiedLocalCells()`：空间占用获取。计算并返回该模块当前占用的所有相对坐标集合。

### `MachineShellData` (Class)
* **作用**：机箱实例的数据总集，维护内部局域物流网与所有子模块的引用。
* **关键属性与方法**：
  * `MainCore`：当前机箱安装的核心逻辑模块引用。
  * `InputPorts` / `OutputPorts`：当前机箱安装的输入/输出匣集合。
  * `RecalculateStats()`：增益重算。在模块安装或拆卸后触发，重新计算机箱的全局运行属性。

---

## 2. 全局管理器服务 (Core Managers / Controllers)

本节记录挂载于大世界中、负责仲裁与推进游戏核心逻辑的单例服务。

### `ConfigManager`
* **作用**：静态资产（ScriptableObject）的全局检索枢纽，在初始化时构建 O(1) 复杂度的字典，负责防重校验与索引维护。
* **关键方法**：
  * `GetItem(string itemID)`：单体查询。根据字符串 ID 获取对应的物品静态图纸。
  * `GetAllItems()`：图鉴遍历。获取全游戏已注册的物品列表，常用于 UI 渲染。

### `MachineManager`
* **作用**：机器物理操作的仲裁者与 Tick 加工引擎，处理空间碰撞、物流交割与状态步进。
* **关键方法**：
  * **装配系**：
    * `CanPlaceModule()`：判定模块是否满足放置条件（空间、隔断、规则）。
    * `PlaceModule()` / `RemoveModule()`：执行模块的实际安装与拆装操作，并触发数据底座更新。
  * **物流系**：
    * `TryIngestItem()`：传送带推入拦截。校验物品是否允许进入机器（含白名单判定）。
    * `TryGetExternalPosition()`：端口坐标投射。计算 I/O 端口在大世界中对应的外部吞吐坐标。
  * **全局调度**：
    * `TryConsumeGlobalItems()`：跨仓库原子级排序扣减，执行严密的市场经济交割。

### `GridManager`
* **作用**：逻辑网格与真实世界坐标的映射器，维护大世界地图的底层占位状态。
* **关键方法**：
  * `GetGridCell(Vector2Int worldPos)`：根据世界坐标获取对应的底层网格单元实例。

### `EconomyManager` & `SimulationController`
* **作用**：经济账户管理与全局时间/实体步进驱动。
* **关键方法**：
  * `EconomyManager.AddFunds()` / `ConsumeFunds()`：增加或扣除玩家资金。
  * `EconomyManager.HasEnoughFunds()`：校验资金是否满足特定金额。
  * `SimulationController.CurrentTick`：获取当前系统的全局逻辑时钟刻度。

---

## 3. 表现与交互层 (View & Interaction)

本节记录负责捕获玩家输入、路由焦点与渲染数据的表现层组件。

### `MachineGUIController`
* **作用**：机器内部装配面板的控制中枢，处理局域 UI 内的用户行为映射。
* **关键方法**：
  * `HandleModuleInteraction()`：空手多态交互拦截。捕获点击事件并根据模块类型（如 `IConfigurablePort`）呼出对应的配置面板。
  * `HandleModulePlacement()`：放置分发。处理手持模块时的放置意图。
  * `SwitchTab()`：处理装配面板内部的 Tab 页签切换逻辑。

### `UIPortConfigPanel` & `UIPortFilterIcon`
* **作用**：白名单/黑名单悬浮配置面板与单项物品图标表现层。
* **关键方法**：
  * `OpenPanel(PortRuleConfig)`：面板唤出与数据挂载。
  * `ToggleItemRule(itemID)`：点击特定图标时，切换其高亮状态并修改底层的规则数据。

### `UIWarehousePanel` & `UIInventorySlot`
* **作用**：仓储库存的表现层主控与单格槽位表现层。
* **关键机制**：
  * 槽位实现了 `IBeginDragHandler`、`IDragHandler`、`IEndDragHandler`、`IDropHandler` 等 EventSystem 接口，实现跨越层级向上传递拖拽动作，规避上层容器（如 ScrollRect）引起的射线吞噬问题。

### `InteractionController`
* **作用**：大世界视觉表现与物理交互总控。
* **关键方法**：
  * `SpawnFloatingText()`：在指定物理坐标生成信息飘字（如经济扣减/增加）。
  * `SpawnItemVisual()`：将纯数据层的物品转化为大世界中的视觉实体。

### `UIRaycastDebugger` (工具类)
* **作用**：UI 事件穿透测试器。在 `Update` 帧中发射模拟射线，用于实时排查 UI 遮挡与隐形透明网格吞噬事件的问题。