# 项目目录、工程依赖与源码导航

以下结构以 `develop` 分支为准；省略了大量 FlatBuffers 类型文件、WinForms Designer、GLSL 与 `.resx`，不代表这些文件不存在。

```text
gftool/
├── .github/
│   └── workflows/
│       ├── sync-upstream.yml        # 原作者 main → 本仓库 main
│       ├── dotnet-desktop.yml       # main 五工具构建及 nightly
│       ├── sync-develop.yml        # main/PokeDocs → develop
│       └── develop-portable.yml    # develop 五工具 + 共用 SQLite 发布
├── GFTool.Core/                    # 通用格式解析、缓存、文件系统、压缩
│   ├── Cache/
│   │   ├── GFPakHashCache.cs       # 兼容旧哈希缓存，名称解析接入点
│   │   └── PokeDocsDatabaseLocator.cs  # 查找共用 Data/PokeDocs.sqlite
│   ├── Flatbuffers/                # TR / GF 模型、动画、场景、UI 等类型
│   ├── Serializers/                # TRPFS/ONEFILE、GFPAK 等读写/解析
│   ├── Structures/                 # 二进制文件结构
│   ├── Compression/                # Oodle/LZ4 适配
│   ├── Math/Hash/GFFnv64a.cs       # GFFNV 64-bit 哈希
│   └── Utils/                      # FlatBuffer 与路径工具
├── GFTool.Renderer/                # 场景、模型、纹理、动画及 GLSL 着色器
│   ├── Scene/GraphicsObjects/
│   ├── Core/Graphics/
│   └── Shaders/
├── GFTool.RenderControl_WinForms/  # WinForms 控件与渲染器桥接
├── TrinityModLoader/               # Mod 管理器 UI 与 ModPack/ModEntry
├── TrinityFileExplorer/            # TRPFD/TRPFS 浏览器与 SQLite 搜索 UI
│   ├── TrinityExplorerWindow.cs
│   ├── ExplorerFileViewer.cs
│   ├── ExplorerPackViewer.cs
│   ├── ExportProgressWindow.cs
│   ├── PokeDocsStore.cs            # SQLite 查询封装
│   └── PokeDocsSearchWindow.cs     # 搜索/导出窗口
├── TrinityModelViewer/
│   ├── ModelViewerForm.cs
│   └── Export/GltfExporter.cs
├── TrinitySceneView/
│   ├── SceneViewerForm.cs
│   └── Loading/SceneLoader.cs
├── TrinityUikitEditor/
│   ├── UikitEditor.cs
│   └── TRUiView.cs
├── NxMiddleware/                    # Git submodule：外部库源码
├── PokeDocs/                        # Git submodule：哈希、Lua、FBS 等资料
├── tools/
│   └── PokeDocsIndexer/build.py    # TXT/FBS → SQLite（Python 标准库）
├── docs/                            # 本目录及现有技术说明
├── TrinityModLoader.sln             # 解决方案入口
├── .gitmodules
├── .gitignore
└── LICENSE
```

## 1. 五个 EXE 与共享库

`TrinityModLoader.sln` 同时包含以下 Windows 程序：

- `TrinityModLoader`
- `TrinityFileExplorer`
- `TrinityModelViewer`
- `TrinitySceneView`
- `TrinityUikitEditor`

辅助项目包括 `GFTool.Core`、`GFTool.Renderer`、`GFTool.RenderControl_WinForms`，以及 `NxMiddleware` 子模块里的 `BnTxx`、`LayoutLibrary`、`SarcLib`。

**路径定位在 Core，SQL 查询目前在 Explorer。** 五个程序引用 `GFTool.Core`，因此都有使用 `PokeDocsDatabaseLocator` 的条件，但当前只有 `TrinityFileExplorer/PokeDocsStore.cs` 真正使用 `Microsoft.Data.Sqlite` 执行检索。不要误以为数据库自动接入了其余四个 UI。

## 2. 关键功能应从哪里读起？

| 要研究的功能 | 推荐源码 |
| --- | --- |
| 应用启动和初始化 | 各应用 `Program.cs`、主窗体 |
| RomFS 选择与读入 | `TrinityFileExplorer/TrinityExplorerWindow.cs` |
| 文件目录树、包内查看 | `ExplorerFileViewer.cs`、`ExplorerPackViewer.cs` |
| TRPFS/TRPFD 的具体数据结构 | `GFTool.Core/Flatbuffers/TR/ResourceDictionary/` |
| 文件压缩/解压 | `GFTool.Core/Compression/` |
| 哈希计算与旧缓存 | `GFFnv64a.cs`、`GFPakHashCache.cs` |
| SQLite 生成规则 | `tools/PokeDocsIndexer/build.py` |
| SQLite 搜索和导出 | `TrinityFileExplorer/PokeDocsStore.cs`、`PokeDocsSearchWindow.cs` |
| 模型/动画渲染 | `GFTool.Renderer/Scene/GraphicsObjects/` |
| glTF 导出 | `TrinityModelViewer/Export/GltfExporter.cs` |
| 场景递归加载 | `TrinitySceneView/Loading/SceneLoader.cs` |
| 整合包如何生成 | `.github/workflows/develop-portable.yml` |

## 3. Git 子模块、生成文件与版本

`.gitmodules` 记录：

- `NxMiddleware` → `Reisyukaku/NxMiddleware`。
- `PokeDocs` → `pkZukan/PokeDocs` 的 main；Git 仓库实际固定到一个**提交 SHA**，不会因上游改变而静默漂移。

`PokeDocs.sqlite` 是**构建生成物**，不应提交到 Git 历史；它由 Python 脚本创建，版本用 `PokeDocs.version.txt` 和 SQLite `metadata.pokedocs_revision` 追溯。

## 4. Release ZIP 的物理结构

```text
GFTool-Windows-x64/
├── Data/
│   ├── PokeDocs.sqlite             # 只此一份
│   └── PokeDocs.version.txt
├── TrinityModLoader/
│   └── TrinityModLoader.exe
├── TrinityFileExplorer/
│   └── TrinityFileExplorer.exe
├── TrinityModelViewer/
│   └── TrinityModelViewer.exe
├── TrinitySceneView/
│   └── TrinitySceneView.exe
├── TrinityUikitEditor/
│   └── TrinityUikitEditor.exe
├── LICENSE.txt
└── README.txt
```

`dotnet publish` 可能保留额外必需的原生 DLL 或数据文件；以上只示意主要路径，**不要为了“单文件”手工删除发布目录里的支持文件**。

SQLite 位置由 `GFTool.Core/Cache/PokeDocsDatabaseLocator.cs` 解析：环境变量 `GFTOOL_POKEDOCS_DB`（如有效）→ EXE 所在目录 `Data/` → EXE 上一级目录 `Data/`。因此整合包的五个工具目录与根级 `Data/` 必须保持同级。
