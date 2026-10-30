# GFTool 五个软件功能说明

> 当前实现的功能梳理；非原作者全部计划功能清单。对应工程文件均在仓库根目录。开发分支部分功能尚处于 WIP（进行中）。

## 1. 功能矩阵

| 程序 | 主要用途 | 当前源码可见的功能 | 状态与限制 |
| --- | --- | --- | --- |
| **Trinity Mod Loader** (`TrinityModLoader.exe`) | 管理 Trinity 游戏 Mod | 新建/选择 Mod Pack，导入 ZIP/RAR 或文件夹 Mod、调整顺序和启用状态、应用 Mod、生成 LayeredFS 输出 | 需要匹配的 RomFS；不在此程序内提供 SQLite 搜索 UI |
| **Trinity File Explorer** (`TrinityFileExplorer.exe`) | 浏览和提取 TRPFD/TRPFS 内容 | 读取描述符、归档/路径视图、哈希反解、浏览文件与 LayeredFS 标记、导出文件/文件夹、打开选中 `.trmdl` 到 Model Viewer | develop 提供离线 PokeDocs SQLite 搜索；提取真实文件需 RomFS 与可用解压依赖 |
| **Trinity Model Viewer** (`TrinityModelViewer.exe`) | 查看模型、骨骼、材质及动画 | 打开 `.trmdl`，浏览骨骼/材质/网格、动画播放与停止，模型/动画导出 glTF 2.0，纹理导出 PNG/BNTX，基础渲染设置 | 模型导入/渲染兼容性随格式与资源依赖变化；部分功能为开发中 |
| **Trinity Scene Viewer** (`TrinitySceneView.exe`) | 浏览 Trinity 场景与对象模板 | 打开 `.trscn`、`.trsot`、`.trsog`，加载/展开场景树，查看对象属性、定位对象及渲染预览 | 原 README 标记工作进行中，不应视为完整场景编辑器 |
| **Trinity UIKit Editor** (`TrinityUikitEditor.exe`) | 查看部分 Trinity UI 布局资料 | 打开文件、解析 UI/场景节点、查看属性 | 当前源码规模较小；暂未确认完整的布局编辑、保存回写闭环 |

### 重要源码入口

- Mod Loader：[`TrinityModLoader/TrinityModLoaderWindow.cs`](../TrinityModLoader/TrinityModLoaderWindow.cs)、[`Models/ModEntry`](../TrinityModLoader/Models/ModEntry/)。
- File Explorer：[`TrinityFileExplorer/TrinityExplorerWindow.cs`](../TrinityFileExplorer/TrinityExplorerWindow.cs)、[`ExplorerPackViewer.cs`](../TrinityFileExplorer/ExplorerPackViewer.cs)。
- Model Viewer：[`TrinityModelViewer/ModelViewerForm.cs`](../TrinityModelViewer/ModelViewerForm.cs)、[`Export/GltfExporter.cs`](../TrinityModelViewer/Export/GltfExporter.cs)。
- Scene Viewer：[`TrinitySceneView/SceneViewerForm.cs`](../TrinitySceneView/SceneViewerForm.cs)、[`Loading/SceneLoader.cs`](../TrinitySceneView/Loading/SceneLoader.cs)。
- UIKit：[`TrinityUikitEditor/UikitEditor.cs`](../TrinityUikitEditor/UikitEditor.cs)、[`TRUiView.cs`](../TrinityUikitEditor/TRUiView.cs)。

## 2. PokeDocs SQLite 的实现范围（develop）

已实现：

- PokeDocs TXT → SQLite 的离线转换，按游戏及来源记录 64 位哈希。
- 独立索引：`fd`（文件描述符）、`trpak`（包内）、Lua 符号、音频文本、FlatBuffers `.fbs`。
- File Explorer 中 `Help → Search PokeDocs SQLite...`；无需 RomFS 即可搜索。
- 按 `game`、`scope` 与路径/名称/哈希条件检索，显示来源与行号。
- 导出当前搜索结果或选中项：`CSV`、`JSON`、`TXT`；单项 `.fbs` 可导出定义文本。
- 选择命中的 FD/TRPAK 记录后，尝试从**用户已加载且匹配的** RomFS 提取资源。
- 五个程序共享一个安装目录结构下的 `Data/PokeDocs.sqlite`；公共的数据库路径定位器可被各项目引用。

当前**没有**实现或不能据此保证：

- 五个软件都有 SQLite 搜索窗口（**目前只有 Explorer 有**）。
- 将五个应用合并成单独的一个 `.exe`（它们是五个独立 `.exe`）。
- 把 `.trmdl`、`.trpak` 或游戏包内数据放进 SQLite；SQLite 只有索引/定义。
- 用 PokeDocs 替代实际游戏 RomFS，或保证所有未知哈希都能还原为名字。
- 全量 GUI 运行/资源导出回归测试：现有 GitHub Actions 验证构建、ZIP 结构及 SQLite 完整性，**不等于实机 GUI 验收**。

## 3. TXT 导入的旧流程与新流程

老版：`Help → Load Hashlist → From File / From PokeDocs` → `GFPakHashCache.AddHashFromList()` → `GFPAKHashCache.bin` → `GetName(hash)`。

develop：生成器在**构建阶段**导入 PokeDocs；运行期由 `PokeDocsStore` 对共享 SQLite 发起只读查询，在 Explorer 的解析层匹配文件名。旧缓存 API 仍作为兼容性后备，手动导入 TXT 菜单未彻底删除。

SQLite 是可检索的名称库；`data.trpfd`/`data.trpfs` 负责确认游戏真实有哪些文件并提供其二进制内容。详情参见 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 4. 格式及实际导出

| 名称 | 角色 | 是否随 develop 发布包附带 |
| --- | --- | --- |
| `PokeDocs.sqlite` | 哈希/符号/Schema 索引 | 是，整合包内仅一份 |
| `PokeDocs.version.txt` | 数据来源提交版本 | 是 |
| `data.trpfd` | 游戏内的文件描述符 | 否，用户提供 |
| `data.trpfs` | 实际归档数据容器 | 否，用户提供 |
| `.trpak` | 归档或数据包 | 否，用户自行选取/提取 |
| `.trmdl`、`.trmsh`、`.trskl` | 模型/网格/骨架等资源 | 否，用户自行选取/提取 |
| `.fbs` | FlatBuffers 结构定义 | 数据库收录定义文本，可搜索和导出 |
| `oo2core_8_win64.dll` | 某些资源解压所需外部库 | 否，用户自行提供合法可用副本 |

具体格式支持受游戏版本与文件结构影响；不能仅凭扩展名断言程序支持所有版本。
