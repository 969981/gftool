# GFTool develop 技术文档

> 适用分支：[`969981/gftool@develop`](https://github.com/969981/gftool/tree/develop)。本目录依据 **2026-10-30** 的源码和 CI 流程编写；发生上游同步、接口或发布方式变化时，应随代码更新。本文描述的是当前实现，不是全部功能的完成承诺。

GFTool 由五个 Windows 工具、共享的 `GFTool.Core`、渲染组件和两份 Git 子模块组成。**develop** 将 [PokeDocs](https://github.com/pkZukan/PokeDocs) 的 TXT/FBS 资料转换为 `SQLite`，并在便携版整合包中只保留一份 `Data/PokeDocs.sqlite`。

## 阅读导航

| 文档 | 内容 | 适合谁 |
| --- | --- | --- |
| [FEATURES.md](FEATURES.md) | 五个软件现有功能、文件格式、已实现/未实现范围 | 使用者、需求整理 |
| [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) | 目录树、主要项目、重要源码入口、子模块 | 初次阅读源码者 |
| [ARCHITECTURE.md](ARCHITECTURE.md) | 代码依赖、TRPFS/TRPFD、SQLite 查询与哈希解析链路 | 开发和逆向研究 |
| [DOTNET10_SLNX_MIGRATION.md](DOTNET10_SLNX_MIGRATION.md) | .NET 10 目标框架、SDK 版本、SLNX 迁移及兼容性说明 | 迁移维护 |
| [BUILD_AND_RELEASE.md](BUILD_AND_RELEASE.md) | 本地从零编译、SQLite 生成、整合打包、Actions/Release | 开发、CI 维护 |
| [TROUBLESHOOTING.md](TROUBLESHOOTING.md) | 数据库、RomFS、Oodle、子模块、构建/发布排错 | 测试和维护 |

已有专题文档继续保留：

- [POKEDOCS_SQLITE.md](POKEDOCS_SQLITE.md)：数据库表、便携目录与单份共享机制。
- [POKEDOCS_INTEGRATION.md](POKEDOCS_INTEGRATION.md)：PokeDocs 子模块接入与更新。
- [UPSTREAM_SYNC_AND_RELEASE.md](UPSTREAM_SYNC_AND_RELEASE.md)：`main` 上游同步、独立 Nightly 的历史与维护说明。

## 快速开始

```powershell
git clone --branch develop --recurse-submodules https://github.com/969981/gftool.git
cd gftool
dotnet restore TrinityModLoader.slnx
dotnet build TrinityModLoader.slnx -c Release --no-restore
```

构建出应用并不等于已经生成 SQLite。若要完整复现 **develop 五合一发布包**，还需 Python 3.12 运行 `tools/PokeDocsIndexer/build.py`，将数据库放到 `GFTool-Windows-x64/Data/`，并对五个程序分别 `dotnet publish`。详见 [BUILD_AND_RELEASE.md](BUILD_AND_RELEASE.md)。

## 官方来源与分支边界

- 当前开发仓库：<https://github.com/969981/gftool>
- 原作者源码：<https://github.com/pkZukan/gftool>
- 哈希及 Schema 来源：<https://github.com/pkZukan/PokeDocs>
- `main`：跟踪 GFTool 上游，发布 `nightly`。
- `develop`：在 `main` 基础上增加 PokeDocs/SQLite，发布 `develop-nightly`。
- 下载地址：[develop-nightly](https://github.com/969981/gftool/releases/tag/develop-nightly)；[main nightly](https://github.com/969981/gftool/releases/tag/nightly)。

**注意**：五个 EXE 独立运行，并非合并成一个 EXE。当前只有 **Trinity File Explorer** 包含 PokeDocs SQLite 搜索界面，其余四个工具仅通过 `GFTool.Core` 具备定位共享数据库的基础。数据库是资源索引，不含任何实际游戏 RomFS、`.trmdl`、`.trpak`、商业压缩库或密钥。

## 文档维护约定

- 所有命令优先针对仓库根目录；PowerShell 示例以 Windows 为主。
- 技术说明附源码路径，发布流程以 `.github/workflows/` 为最终依据。
- 区分 **源码功能**、**CI 通过** 与 **Windows GUI/游戏实测**，不可把编译成功等同于运行行为已验证。
- 变更数据库结构、脚本参数、发布目录、菜单入口时，同步更新本目录。
