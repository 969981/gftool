# 核心架构、数据流与扩展接口

## 1. 总体架构

```text
    PokeDocs Git 子模块（TXT / Lua / FBS）
                      │
                      ▼
    tools/PokeDocsIndexer/build.py
    解析、分类、索引、完整性校验
                      │
                      ▼
       Data/PokeDocs.sqlite（单份）
                      │
  GFTool.Core/PokeDocsDatabaseLocator
                      │
       ┌──────────────┴────────────────────┐
       ▼                                   ▼
Trinity File Explorer              其他四个工具
 PokeDocsStore + 搜索 UI            公共路径定位入口
 SQLite 查询、名字反解              （暂无搜索窗口）
       │
       ▼
 用户自己的 RomFS
 data.trpfd + data.trpfs → 解包/导出文件
```

`GFTool.Core` 提供格式/哈希/压缩工具和数据库**路径定位**；具体 `Microsoft.Data.Sqlite` 查询代码放在 `TrinityFileExplorer/PokeDocsStore.cs`。五个 EXE 运行时是独立进程，数据库以只读文件方式共享，不启动服务端，也没有各自的数据库副本。

## 2. 游戏文件链路

**文件名还原** 与 **文件数据读取** 是两个不同问题：

1. `data.trpfd` 包含文件哈希、文件到归档的映射等元数据；`data.trpfs` 是实际文件数据所在的容器。
2. `TrinityExplorerWindow` 调用 `FlatBufferConverter`、`ONEFILESerializer` 等类解析 TRPFD/TRPFS。
3. `ExplorerFileViewer` 和 `ExplorerPackViewer` 用哈希查询可读路径。旧逻辑入口是 `GFPakHashCache.GetName(ulong)`，develop 的解析器优先尝试 SQLite（用户选定的 `SV` / `ZA` 和 `fd` / `trpak`），再回退旧缓存。
4. 用户选择导出资源后，程序**必须**从匹配的 RomFS 定位真实文件，按需要经 Oodle 解压，再写入指定输出目录。

`GFFNV.Hash(string)` 是按 UTF-8 字节执行 64-bit FNV-1a 的哈希实现，定义在 `GFTool.Core/Math/Hash/GFFnv64a.cs`。路径字符、斜杠、大小写影响哈希值；导入 PokeDocs 时优先保存原文提供的 HEX，而不是随意重新计算覆盖。

## 3. SQLite 生成过程

源码：`tools/PokeDocsIndexer/build.py`。构建期使用 Python 3.12 与标准库 `sqlite3`：

```text
PokeDocs/
  SV/Hashlists/FileSystem/hashes_inside_fd.txt
  SV/Hashlists/FileSystem/hashes_inside_trpak.txt
  ZA/Hashlists/FileSystem/hashes_inside_fd.txt
  ZA/Hashlists/FileSystem/hashes_inside_trpak.txt
  */Hashlists/Lua/*.txt
  */Hashlists/Audio/*.txt
  **/*.fbs
       │
       ▼
流式读入 → 按文件分类 → 规范化哈希 → 批次写入 → 建索引
       │
       ▼
SQLite integrity_check + 数据量阈值检查
       │
       ▼
PokeDocs.sqlite + pokedocs-index-summary.json
```

### 表及用途

| 表 | 核心字段 | 用途 |
| --- | --- | --- |
| `metadata` | `key`、`value` | 数据库结构版本、PokeDocs 提交、记录数 |
| `source_files` | `path`、`sha256`、`file_type`、统计 | 逐个源文件追溯和校验 |
| `hash_entries` | `game`、`scope`、`hash_hex`、`name`、`extension`、`source_id`、`line_number` | FD/TRPAK/Lua 哈希索引 |
| `text_entries` | `game`、`scope`、`name`、来源/行号 | 音频等纯文本名称 |
| `flatbuffer_schemas` | `game`、`path`、`content`、`source_id` | FBS 原始定义文本 |
| `hash_fts` | FTS5 trigram | 名称子串搜索加速（能创建时启用） |

- `hash_hex` 采用**固定 16 位大写 HEX TEXT**，不是 SQLite 有符号 `INTEGER`；避免 `ulong` 在符号位上的歧义。
- `name` 可为空，代表哈希已知但尚无名称。
- `source_id` + `line_number` 记录来源；不跨游戏/命名空间简单覆盖。
- 构建器为可识别的文件系统大型 TXT 应用最低行数检查；不意味着所有被跳过的行均是错误。跳过数量和导入数量应在 `pokedocs-index-summary.json` 中审计。
- SQLite 数据库当前由 CI **全量重建**，而不是通过网络直接更新用户本地文件。

## 4. 运行时查询链路

- `TrinityFileExplorer/TrinityExplorerWindow.cs`：创建 `PokeDocsStore.OpenShared()`，连接菜单与主界面，选择游戏，导出选中资源。
- `TrinityFileExplorer/PokeDocsStore.cs`：`SqliteOpenMode.ReadOnly` 打开单一共享数据库，校验 `metadata.schema_version`，进行参数化 SELECT 查询和 `ResolveName()`。
- `TrinityFileExplorer/PokeDocsSearchWindow.cs`：选择 `game`、`scope`、输入关键词，列表展示查询结果；每次搜索当前限制为 **500 条**，并不等于完整匹配总数。
- 只需导出哈希/路径/Schema 时**不需要** RomFS；选择提取真正的 `.trmdl`、`.trpak` 相关二进制时**需要**用户的 RomFS 与解压依赖。

索引更新后，用户使用新整合包中的 `Data/PokeDocs.sqlite`；如果只有数据库包，也可替换这份共享文件（替换前关闭所有工具）。变量 `GFTOOL_POKEDOCS_DB` 可指向外部已有文件。

## 5. 扩展建议及风险边界

目前共享层只解决**数据库定位**，不是五工具通用的 SQL 服务。后续若要让 Model Viewer、Mod Loader、Scene Viewer、UIKit 也具备搜索功能，可抽出真正的 `IPokeDocsRepository` / `PokeDocsStore` 到公共库，统一数据库 Schema 版本、查询接口、游戏作用域和缓存策略；不要复制四份查询代码。

主要风险与待测项：

- 数据来源可能存在无名称哈希、多名称候选或版本差异，结果不能直接当作文件必然存在的证明。
- 真正导出时需要按用户当前 RomFS 的索引验证命中；只在 SQLite 中命中不足以完成提取。
- Release 的 Windows Actions 不进行真实 GUI 与 OpenGL 交互测试；渲染器可能依赖额外支持文件。
- `GFPAKHashCache.bin` 是兼容路径，新数据库原则上只读；避免由软件写入/覆盖共享索引。
- 新增对 SQLite 的查询及插件扩展时保持参数化 SQL，不直接拼接外部输入。
