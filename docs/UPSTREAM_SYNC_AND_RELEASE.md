# GFTool 上游同步与 Windows 便携版发布

## 上游同步

- 上游仓库： https://github.com/pkZukan/gftool ，跟踪其 main 分支。
- 工作流：.github/workflows/sync-upstream.yml 。每 6 小时检查一次，也可以在 GitHub Actions 手动运行。
- 使用 fetch + merge 保留本仓库提交；发生合并冲突直接失败，绝不强制覆盖本仓库 main。
- 有新提交且同步成功后，自动派发本仓库的 Windows 构建发布工作流。
- 没有上游更新时不创建提交，也不重复发布。

## Windows 打包

- 工作流：.github/workflows/dotnet-desktop.yml 。
- 触发：main 分支推送、手动运行、PR（PR 只构建测试，不发布）。
- 依赖：Windows Runner、.NET 8 SDK、NxMiddleware Git 子模块。
- 方式：dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false 。
- 发布包：
  - GFTool-Windows-x64-Portable.zip：五个工具，按目录分开。
  - TrinityModLoader-Windows-x64-Portable.zip
  - TrinityFileExplorer-Windows-x64-Portable.zip
  - TrinityModelViewer-Windows-x64-Portable.zip
  - TrinitySceneView-Windows-x64-Portable.zip
  - TrinityUikitEditor-Windows-x64-Portable.zip
  - SHA256SUMS.txt：各包校验值。
- 成功后将更新此仓库的滚动 Nightly Release：https://github.com/969981/gftool/releases/tag/nightly 。

## 发布注意事项

单文件发布会打包托管 DLL、运行时及可打包的原生依赖，但不保证全部外部文件消失；请保留每个工具目录里的支持文件。不采用 Trim，因为 WinForms / OpenTK 依赖的动态行为可能受到裁剪影响。Windows Actions 能完成编译和打包验证，图形功能仍需在本机 Windows 进行启动与功能验证。上游的源码及许可证仍予以保留。
