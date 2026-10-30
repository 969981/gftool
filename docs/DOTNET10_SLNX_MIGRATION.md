# develop: .NET 10 LTS 与 SLNX 迁移

> 本页只描述 `969981/gftool` 的 `develop` 分支。原作者 `pkZukan/gftool` 和本仓库 `main` **未在本次提交中修改**。

## 1. 迁移范围

| 分类 | 迁移前 | 迁移后 |
| --- | --- | --- |
| SDK | .NET 8 SDK | .NET 10 SDK (`10.0.x`) |
| 解决方案 | `TrinityModLoader.sln` | `TrinityModLoader.slnx` |
| 库：GFTool.Core、GFTool.Renderer | `net8.0` | `net10.0` |
| 其余六个本仓库 Windows 项目 | `net8.0-windows` 或 `net8.0-windows7.0` | 对应的 `net10.0-windows` 或 `net10.0-windows7.0` |
| `Microsoft.Data.Sqlite` | 8.0.10 | 10.0.14 |
| `System.Drawing.Common` | 8.0.10 | 10.0.14 |
| `NxMiddleware`（三个工程） | 第三方 Git 子模块 | 保持原上游版本与 TFM，不修改子模块 |
| SQLite / 五工具 ZIP | 独立的 .NET 8 自包含发布 | .NET 10 自包含发布，仍为五个 EXE + 一份 `Data/PokeDocs.sqlite` |

保留旧 `SharpCompress`、`Tomlyn.Signed`、`FlatSharp`、`OpenTK` 版本，避免一次引入与 TFM 升级不相关的 API 大改；依赖跨版本兼容性以 .NET 10 CI 的 `restore/build/publish` 为准。

## 2. SDK 约束

根目录新增 `global.json`：

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

本意是限定在 **10.x SDK**，允许运行较新的 10.0 feature band/补丁，而非绑定某一个具体的旧补丁。CI 使用 `actions/setup-dotnet@v4` 与 `dotnet-version: '10.0.x'`。本地检查：

```powershell
dotnet --version
dotnet --list-sdks
dotnet --info
```

可在微软官方下载页面安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。如果本地仍只装有 .NET 8，`global.json` 会让 CLI 报未找到兼容 SDK；安装 10.x 后即可。

## 3. SLNX 格式

旧解决方案共 **11 个项目**：8 个本仓库工程、`NxMiddleware` 里的 `BnTxx` / `LayoutLibrary` / `SarcLib`。迁移保留了 `Middleware` 解决方案文件夹以及全部 11 个工程的引用。

微软支持的标准迁移命令是：

```powershell
# 仅适用于仍有旧 .sln 的工作树或历史版本：
dotnet sln TrinityModLoader.sln migrate
```

**本分支已生成 SLNX 并删除旧 SLN**，无需重复运行该命令。新工程直接使用：

```powershell
git clone --branch develop --recurse-submodules https://github.com/969981/gftool.git
cd gftool
dotnet sln TrinityModLoader.slnx list
dotnet restore TrinityModLoader.slnx
dotnet build TrinityModLoader.slnx -c Release --no-restore
dotnet test TrinityModLoader.slnx -c Release --no-build
```

不要在根目录并存同名 `.sln` 和 `.slnx`，以免命令行及 IDE 自动发现时混淆。无法识别 SLNX 的旧版 Visual Studio，请升级到支持 SLNX 的版本，或使用 .NET 10 CLI。

### 影响文件

- 八个 `*.csproj` 中 `TargetFramework` 已更新。
- 根目录使用 `TrinityModLoader.slnx` 和 `global.json`。
- `.github/workflows/develop-portable.yml` 更新为 `10.0.x` 和 `.slnx`；增加 `dotnet sln ... list` 并验证 11 个项目。
- `.github/workflows/dotnet-desktop.yml` 在 **develop 分支版本** 同步更新，供将来迁移工作流使用；**main 分支本身没有修改**。
- `docs/BUILD_AND_RELEASE.md`、`docs/PROJECT_STRUCTURE.md`、`docs/README.md`、根目录 `README.md` 同步调整说明。

## 4. 打包与兼容性原则

五个 Windows 应用仍采用：

```powershell
dotnet publish TrinityFileExplorer/TrinityFileExplorer.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false
```

`.NET 10` 自包含不需要目标电脑另装 .NET 10 Desktop Runtime，但外部 Shader、Oodle 库、资源文件等依赖仍按原软件的使用约束处理。**不能保证跨到 .NET 10 后 Windows 7/8 仍可运行**：TFM 中 `windows7.0` 是 API 版本标识，不是 OS 运行兼容性承诺；建议在受支持的 Windows 10/11 x64 上实机验证。

本轮只调整目标框架、依赖和解决方案/CI，不改 PokeDocs SQLite 架构、数据内容或游戏资源解析逻辑。升级后仍应检查 Model Viewer 的 OpenGL/动画、File Explorer 的 SQLite 关键字搜索/导出、五个应用启动等。自动 CI 的构建/ZIP 校验不能替代 GUI 冒烟测试。

## 5. 升级验收标准

- `dotnet sln TrinityModLoader.slnx list` 能枚举全部 11 个项目。
- 八个本仓库项目均编译为 `net10.0` 或 `net10.0-windows*`；不改 Git 子模块里的工程文件。
- `dotnet restore/build/test/publish` 无错误，五个 Windows EXE 正常产出。
- 整合 ZIP 内的五个 EXE 均存在，`Data/PokeDocs.sqlite` **恰好一份**。
- 通过 `PRAGMA integrity_check`，并校验 SV/ZA 的 FD 和 TRPAK 索引数据量。
- `develop-nightly` Release 上传完成；旧的 `main/nightly` 发布机制不受影响。
- 在 Windows 上另外测试 GUI 启动及部分真实游戏文件的加载/导出。

## 6. 参考

- [.NET 10 下载与 SDK 版本](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [dotnet sln 命令与 migrate](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln)
- [SLNX 新格式说明](https://devblogs.microsoft.com/visualstudio/new-simpler-solution-file-format/)
- [GFTool develop 编译与发布](BUILD_AND_RELEASE.md)
