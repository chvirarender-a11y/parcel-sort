# 包裹分拣 Parcel Sort

竖屏单手的颜色分拣解谜游戏原型。把传送带上的彩色包裹点进同色货车，货车装满 3 个就开走；颜色对不上的包裹会进入暂存架，暂存架满了就失败。

当前是**色块版原型**：玩法完整，画面只用彩色方块，没有正式美术、动画和广告。

## 怎么运行

1. 安装 [Unity Hub](https://unity.com/download)，再通过 Hub 安装 **Unity 6 LTS**（勾选 Android Build Support）。
2. Unity Hub → Add → Add project from disk，选择这个文件夹。第一次打开需要几分钟导入。
3. 打开后直接点上方的 ▶ Play。即使是空场景，游戏也会自动生成画面。
4. 在 Game 窗口左上角把分辨率改成竖屏（例如 1080x1920），效果更接近手机。

想装到 Android 手机上：File → Save 把当前场景存为 `Assets/Scenes/Main.unity`，在 File → Build Profiles 里加入这个场景，切换到 Android 平台，连接手机后点 Build And Run。

## 玩法规则

- 每个堆只能点最上面的包裹。
- 有同色货车且没装满 → 包裹上车；否则 → 进入暂存架。
- 货车装满就开走，下一辆进来，并自动装走暂存架里的同色包裹。
- 全部包裹装车即过关；暂存架满了就失败。
- 道具：撤销、+1 暂存格（每关一次）、失败后复活（把最近 3 个暂存包裹放回原处，以后会接激励广告）。
- 星级：暂存架用得越少星越多。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| `Assets/Scripts/Core` | 游戏规则、关卡格式、求解器、关卡生成器。纯 C#，不依赖 Unity |
| `Assets/Scripts/Game` | Unity 画面和交互（用代码生成 UI） |
| `Assets/Editor` | 编辑器菜单：Parcel Sort → Check All Levels / Reset Progress |
| `Assets/Tests/EditMode` | 单元测试（Window → General → Test Runner） |
| `Assets/Resources/Levels` | 关卡文件 `level_001.json` 起 |
| `tools/` | 不开 Unity 也能跑测试和生成关卡的脚本（需要 Mono） |

## 关卡格式

```json
{
  "id": 5,
  "bufferSize": 5,
  "dockCount": 2,
  "truckCapacity": 3,
  "trucks": "rcroco",
  "stacks": ["rc", "croooc", "rrcor", "co", "roc"]
}
```

- 每个字母是一种颜色：`r` 红、`g` 绿、`b` 蓝、`y` 黄、`p` 紫、`o` 橙、`c` 青、`w` 白。
- `stacks` 每个字符串是一堆包裹，从下到上。**大写字母**表示一开始是盖着的（问号），到最上面才翻开。
- `trucks` 是货车到达顺序。每种颜色的包裹数必须等于该颜色货车数 × `truckCapacity`。

## 不开 Unity 运行测试

需要 Mono（Ubuntu：`sudo apt install mono-mcs mono-runtime`），在项目根目录执行：

```bash
./tools/run-tests.sh     # 跑全部单元测试，包括检查每个关卡都能通关
./tools/gen-levels.sh    # 重新生成 15 个关卡（约 5-10 分钟）
```

关卡生成方式：随机生成候选关卡，求解器确认能通关，再用「随机乱点的通关率」衡量难度，只保留难度落在目标区间的关卡。前 15 关按「易-易-中-易-难」的节奏排列。
