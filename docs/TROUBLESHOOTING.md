# 故障排查与验收提示

## 1. 启动后提示找不到 `PokeDocs.sqlite`

核对解压后的结构：

```text
GFTool-Windows-x64/
  Data/PokeDocs.sqlite
  TrinityFileExplorer/TrinityFileExplorer.exe
  TrinityModelViewer/TrinityModelViewer.exe
  TrinityModLoader/TrinityModLoader.exe
  TrinitySceneView/TrinitySceneView.exe
  TrinityUikitEditor/TrinityUikitEditor.exe
```

不要单独把 Explorer EXE 拖走，或把 SQLite 只放在某个工具的子目录里。搜索顺序由 `GFTool.Core/Cache/PokeDocsDatabaseLocator.cs` 决定：

1. 有效的 `GFTOOL_POKEDOCS_DB` 环境变量；
2. 当前应用目录下的 `Data/PokeDocs.sqlite`；
3. 应用父目录下的 `Data/PokeDocs.sqlite`。

临时指定外部数据库：

```powershell
$env:GFTOOL_POKEDOCS_DB = "D:\Tools\GFTool\Data\PokeDocs.sqlite"
.\TrinityFileExplorer\TrinityFileExplorer.exe
```

变量只对当前 PowerShell 会话及其子进程生效；不必修改 C 盘用户目录。

## 2. SQLite 找到了，但提示版本不兼容/文件损坏

`PokeDocsStore.OpenShared()` 要求 `metadata.schema_version = 1`。数据库是只读打开的，不会自动迁移 Schema。

```powershell
@'
import sqlite3
p = "Data/PokeDocs.sqlite"
db = sqlite3.connect(p)
print(db.execute("PRAGMA integrity_check").fetchone())
print(db.execute("SELECT key,value FROM metadata").fetchall())
'@ | python -
```

上例需在 `GFTool-Windows-x64/` 下执行。若 `integrity_check` 不为 `ok`、数据库不完整、Schema 不匹配，重新获取同一 develop Release 的整合包或独立 SQLite；不要用任意 TXT 文件重命名成 `.sqlite`。

## 3. 可以搜索，但某条文件名查不到/无法提取

- `PokeDocs.sqlite` 只记录**已知哈希与名称**，不证明资源实际存在于当前游戏 RomFS。
- 核对 **游戏选择**（SV/ZA 等）、**scope**（`fd`、`trpak`、Lua、FBS 等）。`fd` 和 `trpak` 不是同一个索引来源。
- 查询窗口最多返回**前 500 条**，先缩小关键词范围。
- 有些条目只有哈希而没有名称；可能无法用文件名搜索出来。
- 输出真实资源时应先加载相应版本 RomFS，并检查 `data.trpfd` / `data.trpfs` 是否存在且有效；缺少真实二进制内容时，数据库无法直接“生成模型”。
- **Oodle**：某些压缩文件需要用户合法取得相容的 `oo2core_8_win64.dll`；本仓库和 Release 不附带该库。
- 选择 `.trmdl` 或 `.trpak` 文件的流程由具体应用及格式决定；不能把 SQLite 索引导出的 CSV/TXT 当游戏资源导入。

## 4. 没有 `NxMiddleware` 或 `PokeDocs`，构建失败

```powershell
git submodule sync --recursive
git submodule update --init --recursive
git submodule status
```

确认仓库位于 `develop`；`PokeDocs/` 应包含实际 `SV/Hashlists`、`ZA/Hashlists`、`.fbs` 等文件。只下载 Github 的 **Source code (zip)** 通常不会拿到子模块内容。

## 5. SQLite 生成失败，或数据量检查未通过

运行：

```powershell
python tools/PokeDocsIndexer/build.py --root PokeDocs `
  --output dist/GFTool-Windows-x64/Data/PokeDocs.sqlite `
  --summary dist/pokedocs-index-summary.json
```

核对 `PokeDocs` Git SHA、四份大型文件系统列表是否完整、脚本的 `--min-rows` 阈值，以及生成日志的 `skipped` 行数。构建期间有大量读写和建立 trigram 全文索引，磁盘必须留足空间（数据库可达数百 MB）。**不要通过降低数据阈值来掩盖错误的原始格式**；先比对 `source_files` 统计。

## 6. ZIP 里只有 Explorer，缺少其他工具

你可能下载了历史旧版 `GFTool-PokeDocs-Explorer-Windows-x64.zip`；开发整合包应选择：

`GFTool-Develop-Windows-x64-Portable.zip`

官方 develop Release：<https://github.com/969981/gftool/releases/tag/develop-nightly>。验证 ZIP 应含五个不同 EXE，以及只在根级 `Data/` 出现一次的 `PokeDocs.sqlite`。不要将 main 的 `nightly` 与 develop 的 `develop-nightly` 混用。

## 7. GitHub Actions 显示成功，但软件无法打开

自动化目前主要覆盖 `dotnet restore/build/test/publish`、SQLite 完整性和 ZIP 文件清单。不会在无人值守的 Windows Runner 中全面执行真实 WinForms/OpenGL/游戏文件交互测试。

建议检查：

- 当前电脑是否 Windows x64；安全软件是否拦截/隔离 EXE；
- 是否完整解压 ZIP，而不是直接在压缩软件预览界面启动；
- `dotnet publish` 保留的支持文件是否被删除或移动；
- 渲染窗口是否能访问所需 Shader、OpenTK/OpenGL 上下文、纹理资源；
- 单文件原生库自解包失败、文件夹权限等问题；
- 必要时在终端启动同一 EXE，收集报错或 Windows 事件日志，再定位具体项目。

排查时记录：`develop` Git SHA、PokeDocs SHA、下载资产文件名、Windows 版本、操作步骤和错误截图/日志。

## 8. 自动同步/发布没有更新

查看仓库 [Actions](https://github.com/969981/gftool/actions) 中的：

- `Sync Upstream Main`
- `Sync Develop and PokeDocs`
- `Develop - PokeDocs SQLite Five-Tool Bundle`

检查上游是否真的发生变更、是否存在 merge 冲突、工作流权限是否允许推送、跨工作流派发是否成功。定时执行是 GitHub 托管的 UTC Cron，不保证严格秒级触发。遇到失败要先看实际 workflow log，再决定手动触发或修复代码。

## 9. SQLite 提取与数据版权边界

PokeDocs 的哈希/Schema 与游戏实际资源是不同内容。发布包只分发索引和软件本身；`RomFS`、`.trmdl`、`.trpak` 等需要用户提供有权使用的数据；Oodle DLL 不包含在仓库。禁止把缺少游戏资源导致的提取失败描述为“SQLite 数据库不完整”。
