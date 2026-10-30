# 从源码编译、SQLite 生成与 GitHub 发布

> 适用于 `develop`。`main` 的独立 Nightly 使用 `.github/workflows/dotnet-desktop.yml`，两套发布**不要混用**。

## 1. 所需环境

| 工具 | 用途 |
| --- | --- |
| Windows x64 + PowerShell | 本地运行 WinForms、生成便携包 |
| .NET 8 SDK | 构建 `net8.0` / `net8.0-windows` 项目 |
| Git | 克隆仓库、检出子模块、获取版本 SHA |
| Python 3.12 | 运行 `tools/PokeDocsIndexer/build.py` |
| 可选：GitHub CLI `gh` | 手动检查、触发/查看工作流和 Release |

编译使用 NuGet 恢复 `FlatSharp`、`Microsoft.Data.Sqlite`、`SharpCompress`、`OpenTK` 等依赖。索引生成器本身只使用 Python 标准库，无需 `pip install`。运行 GUI/解压真实资源另有 Oodle 等运行条件，详见 [TROUBLESHOOTING.md](TROUBLESHOOTING.md)。

## 2. 从零克隆（不要漏子模块）

在 PowerShell：

```powershell
git clone --branch develop --recurse-submodules https://github.com/969981/gftool.git
Set-Location gftool
git submodule status
dotnet --version
python --version
```

如果已克隆但 `NxMiddleware/` 或 `PokeDocs/` 为空：

```powershell
git checkout develop
git submodule sync --recursive
git submodule update --init --recursive
```

**注意**：PokeDocs 在仓库里是 Gitlink，拉取 ZIP 源码文件通常不包含子模块的完整内容；应使用 `git clone --recurse-submodules`。

## 3. 生成一份 PokeDocs SQLite

在仓库根目录运行：

```powershell
python tools/PokeDocsIndexer/build.py `
  --root PokeDocs `
  --output dist/GFTool-Windows-x64/Data/PokeDocs.sqlite `
  --summary dist/pokedocs-index-summary.json

git -C PokeDocs rev-parse HEAD |
  Set-Content dist/GFTool-Windows-x64/Data/PokeDocs.version.txt -Encoding ascii
```

预期：日志输出哈希数、纯文本记录数、FBS 数、数据库字节数、`integrity_check: ok`。在 2026-10-30 的成功构建记录中，解析出 **1,315,627** 条哈希，数据库约 **474 MB（未压缩）**。数据源更新后数量可能变化，应以当次 JSON 审计为准。

可选校验（Python 标准库，不需要安装 SQLite 命令行）：

```powershell
@'
import sqlite3
path = "dist/GFTool-Windows-x64/Data/PokeDocs.sqlite"
db = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
print("integrity:", db.execute("PRAGMA integrity_check").fetchone()[0])
print("hash entries:", db.execute("SELECT count(*) FROM hash_entries").fetchone()[0])
print("source revision:", db.execute("SELECT value FROM metadata WHERE key='pokedocs_revision'").fetchone()[0])
'@ | python -
```

生成的 SQLite 不提交 Git；`TrinityFileExplorer/Data/PokeDocs.sqlite` 等生成路径也在 `.gitignore` 中。

## 4. 编译完整解决方案

```powershell
dotnet restore TrinityModLoader.sln
dotnet build TrinityModLoader.sln -c Release --no-restore
dotnet test TrinityModLoader.sln -c Release --no-build --verbosity normal
```

注意检查命令返回值与错误信息；`dotnet test` 执行成功**不代表**已经覆盖每个 GUI/提取行为，项目不一定具备完整的自动化测试套件。

若只想编译 Explorer：

```powershell
dotnet build TrinityFileExplorer/TrinityFileExplorer.csproj -c Release
```

但直接构建项目不会生成完整整合 ZIP，也不会自动下载 PokeDocs；若运行时找不到共享数据库，Explorer 的 SQLite 功能不可用。

## 5. 本地发布五个独立 EXE

先完成第 3 节，保留已有 `dist/GFTool-Windows-x64/Data/`。以下为与 CI 等价的核心发布参数：

```powershell
$ErrorActionPreference = "Stop"
$apps = @(
  "TrinityModLoader",
  "TrinityFileExplorer",
  "TrinityModelViewer",
  "TrinitySceneView",
  "TrinityUikitEditor"
)
$bundle = "dist/GFTool-Windows-x64"

foreach ($app in $apps) {
  $out = Join-Path $bundle $app
  dotnet publish "$app/$app.csproj" `
    -c Release -r win-x64 --self-contained true `
    -o $out `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

  if ($LASTEXITCODE -ne 0) { throw "Failed to publish $app" }
  if (!(Test-Path (Join-Path $out "$app.exe"))) { throw "Missing $app.exe" }
}

Copy-Item LICENSE (Join-Path $bundle "LICENSE.txt") -Force
Compress-Archive -Path $bundle `
  -DestinationPath dist/GFTool-Develop-Windows-x64-Portable.zip `
  -CompressionLevel Optimal -Force
```

`PublishSingleFile=true` 与 `--self-contained true` 是为了减少散落的 .NET 运行时文件、通常避免另装运行时；`PublishTrimmed=false` 避免对反射与渲染相关代码做激进裁剪。**不保证所有外部数据或 native 文件都被合并到 EXE**，应保留 `dotnet publish` 产生的其他支持文件。

最终包的五个目录和单一数据库结构参见 [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)。

### ZIP 结构验证

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = "dist/GFTool-Develop-Windows-x64-Portable.zip"
$z = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $zipPath))
try {
  $names = @($z.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })
  foreach ($app in @("TrinityModLoader","TrinityFileExplorer","TrinityModelViewer","TrinitySceneView","TrinityUikitEditor")) {
    $entry = "GFTool-Windows-x64/$app/$app.exe"
    if ($entry -notin $names) { throw "Missing: $entry" }
  }
  $dbs = @($names | Where-Object { $_ -like "*/PokeDocs.sqlite" })
  if ($dbs.Count -ne 1 -or $dbs[0] -ne "GFTool-Windows-x64/Data/PokeDocs.sqlite") {
    throw "Expected exactly one shared SQLite database"
  }
  "Validated: five EXEs, one shared SQLite"
} finally { $z.Dispose() }
```

## 6. GitHub Actions 工作流

| Workflow | 触发与运行 | 结果 |
| --- | --- | --- |
| `sync-upstream.yml` | `main` 上定时检查原作者 `pkZukan/gftool main`（Cron `17 */6 * * *`，UTC）或手动 | 安全合并到本仓库 `main`，有更新时派发 main 构建 |
| `dotnet-desktop.yml` | `main` push / PR / 手动 | 五工具普通版，`nightly` Release |
| `sync-develop.yml` | 定时检查（Cron `47 */6 * * *`，UTC）或手动 | 合并仓库 `main` 到 `develop`，更新 PokeDocs 子模块，并尝试派发 develop 构建 |
| `develop-portable.yml` | `develop` push / 工作流派发 | 重建 SQLite、编译五工具、验证 ZIP、替换 `develop-nightly` Release |

Actions 的 `schedule` 通常运行在仓库默认分支，且可能延迟；定时同步和构建是否成功应到 [Actions](https://github.com/969981/gftool/actions) 查看，不能仅凭 Cron 存在推断发布必然成功。

**特别说明：**使用 `GITHUB_TOKEN` 推送的提交通常不会触发另一条 `push` 工作流，所以 `sync-develop.yml` 额外调用 `gh workflow run develop-portable.yml --ref develop`。若工作流派发权限或工作流可见性不满足要求，该步骤会失败，需要核对 Actions 权限/默认分支上的工作流注册情况。

### develop 发布物

Release：<https://github.com/969981/gftool/releases/tag/develop-nightly>

- `GFTool-Develop-Windows-x64-Portable.zip`：五软件目录 + **唯一** `Data/PokeDocs.sqlite`。
- `PokeDocs-SQLite-Standalone.zip`：方便其他工具单独使用的数据库（单独资产，不在整合 ZIP 内重复）。
- `pokedocs-index-summary.json`：导入源文件和跳过统计。
- `SHA256SUMS.txt`：ZIP 校验值。

CI 在创建发布包前执行 SQLite 完整性检查、SV/ZA FD/TRPAK 最低记录数量检查、五个 EXE 存在检查及 ZIP 中 SQLite **恰好一份**的检查。发布脚本更新**滚动** `develop-nightly` tag/Release，旧资产可能被覆盖，因此该 tag 不是永久锁定的可复现快照；需要追查问题时，应同时记下源码 SHA 与 PokeDocs SHA。

### main 发布物

另见 [UPSTREAM_SYNC_AND_RELEASE.md](UPSTREAM_SYNC_AND_RELEASE.md)。`main` 仍发布自己的五个独立 ZIP 和五合一包，**不应**把它和 develop 的 SQLite 版本混为一谈。

## 7. 上线前的人工冒烟测试

1. Windows x64 解压整合包，核对五个 EXE 和 `Data/PokeDocs.sqlite`。
2. 逐个启动，确认不是因为缺少 DLL、Shader 或其他发布资源而直接退出。
3. 在 **File Explorer** 中，不选 RomFS，打开 `Help → Search PokeDocs SQLite...`，搜索 SV 和 ZA 的已知路径/哈希。
4. 随机导出 TXT、CSV、JSON、单项 FBS，检查内容和编码。
5. 选真实 RomFS，验证目录显示、FD/TRPAK 名称及单个资源/批量资源导出。
6. 用 Model Viewer 打开可用 `.trmdl`，查看材质/骨骼/纹理并尝试 glTF 导出。
7. 分别检查 Mod Loader、Scene Viewer、UIKit Editor 的核心操作；确认没有因为公共库修改而回归。
8. 替换新版共享 SQLite 后重新启动 Explorer，确认来源 SHA 与数据仍可查。

**CI 通过只能证明构建和静态校验成功，不能代替以上 GUI/游戏数据验证。**
