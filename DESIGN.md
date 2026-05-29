# OneFileBox 设计文档

> **重大变更 (2026-05-29)**：项目重启重建，从 SVNFileBox (WPF+SharpSvn) 迁移到 Avalonia 12 + SVN CLI，实现跨平台。
> 之前搞乱了，重新开始。

---

## 项目信息

| 项目 | 值 |
|------|-----|
| 定位 | 跨平台 SVN 文件管理器，基于 SVNFileBox 重写 |
| 框架 | Avalonia 12.0.3 + .NET 10.0 |
| 架构 | MVVM + SvnCliService 平替 SharpSvn |
| 目录 | `~/aiworks/projects/repos/OneFileBox/` |
| SVN 仓库 | `https://66.154.112.116/repos/OneFileBox` (master) |

---

## 技术栈

| 组件 | 版本 | 说明 |
|------|------|------|
| Avalonia | **12.0.3** | 跨平台 UI 框架 |
| Avalonia.Desktop | **12.0.3** | Desktop 运行时 |
| Avalonia.Themes.Fluent | **12.0.3** | Fluent 主题 |
| Avalonia.Fonts.Inter | **12.0.3** | Inter 字体 |
| AvaloniaUI.DiagnosticsSupport | **2.2.1** | DevTools |
| CommunityToolkit.Mvvm | **8.4.1** | MVVM 基础设施 |
| Semi.Avalonia | **12.0.1** | UI 主题 |
| Avalonia.Controls.DataGrid | **12.0.0** | DataGrid 控件 |
| TargetFramework | net10.0 | |

> **跨平台目标**：Windows / macOS / Linux

---

### UI 控件映射（WPF → Avalonia）

| WPF 控件 | Avalonia 替代 | 说明 |
|---------|--------------|------|
| `ListView` + `GridView` | `DataGrid` | Avalonia 没有 ListView/GridView，用 DataGrid 替代 |
| `ListViewItem` | `DataGridRow` | 行容器 |
| `GridViewColumn` | `DataGridColumn` | 列定义 |
| `ListView.ItemContainerStyle` | `DataGrid.RowStyle` | 行样式 |
| `ListView.ItemTemplate` | `DataGrid.RowTemplate` 或 `DataGridTemplateColumn` | 行模板 |
| `CheckBox`（多选列） | `DataGridCheckBoxColumn` | 多选列 |
| `ContextMenu` | `ContextMenu`（Avalonia 支持） | 右键菜单 |
| `ComboBox` | `ComboBox` | 下拉框 |
| `Button` | `Button` | 按钮 |
| `TextBox` | `TextBox` | 文本输入 |
| `TreeView` | `TreeView` | 树形（左侧仓库树） |
| `System.Windows.Forms.NotifyIcon`（托盘） | `Avalonia.Controls.TrayIcon` | 系统托盘图标 |
| `TaskbarIcon`（通知气泡） | `TrayIcon` + `NativeMenu` | 托盘图标 + 右键菜单 |

### 布局参考

```xaml
<!-- WPF GridView（SVNFileBox MainWindow.xaml 核心布局） -->
<ListView ItemsSource="{Binding Files}" SelectedItem="{Binding SelectedFile}">
  <ListView.View>
    <GridView>
      <GridViewColumn Header="" Width="30" CellTemplate="{...checkbox...}"/>
      <GridViewColumn Header="状态" Width="40" DisplayMemberBinding="{Binding StatusIcon}"/>
      <GridViewColumn Header="名称" Width="300" DisplayMemberBinding="{Binding Name}"/>
      <GridViewColumn Header="大小" Width="80" DisplayMemberBinding="{Binding FileSizeText}"/>
      <GridViewColumn Header="修改时间" Width="130" DisplayMemberBinding="{Binding ModifyTimeText}"/>
    </GridView>
  </ListView.View>
</ListView>

<!-- Avalonia DataGrid 等效实现 -->
<DataGrid ItemsSource="{Binding Files}"
          SelectedItem="{Binding SelectedFile}"
          CanUserResizeColumns="True"
          CanUserReorderColumns="True"
          AutoSizeColumnsMode="DataGridAutoSizeColumnsMode.None">
  <DataGrid.Columns>
    <DataGridCheckBoxColumn Header="" Width="30" Binding="{Binding IsSelected}"/>
    <DataGridTextColumn Header="状态" Width="40" Binding="{Binding StatusIcon}" IsReadOnly="True"/>
    <DataGridTextColumn Header="名称" Width="300" Binding="{Binding Name}" IsReadOnly="True"/>
    <DataGridTextColumn Header="大小" Width="80" Binding="{Binding FileSizeText}" IsReadOnly="True"/>
    <DataGridTextColumn Header="修改时间" Width="130" Binding="{Binding ModifyTimeText}" IsReadOnly="True"/>
  </DataGrid.Columns>
</DataGrid>
```

### Avalonia DataGrid 注意事项

1. **虚拟化**：DataGrid 默认启用虚拟化，大文件列表性能好
2. **选择模式**：`SelectionMode="Extended"` 支持 Ctrl/Shift 多选
3. **列宽**：`DataGridLengthUnitType.Auto` / `Pixel` / `Star`
4. **只读列**：文件名列等设 `IsReadOnly="True"`，状态列可编辑
5. **行双击**：`RowPointerPressed` 事件 → 导航进目录
6. **右键菜单**：配合 `ContextMenu` 在 `PointerPressed` 或 `ContextMenuRequested` 显示
7. **编译绑定**：`{Binding Name}` 默认 CompiledBinding，复杂绑定需 `Mode=OneWay`

---

## 核心架构决策

| 决策 | 选择 | 说明 |
|------|------|------|
| SVN 调用层 | `svn` CLI + XML 输出解析 | 替代 SharpSvn，实现跨平台 |
| 进度跟踪 | SVN `--xml --verbose` 输出解析 + notify 模拟 | 对标 SharpSvn 的 Notify 事件 |
| 上层架构 | 与 SVNFileBox 保持一致 | ViewModel/Service 分层不变 |
| SVN 核心服务 | `SvnCliService`（新）平替 `SvnService`（SharpSvn） | 接口一致，底层替换 |
| UI 框架 | Avalonia 12 + Semi.Avalonia 主题 | 先简化迁移，后续加效果 |

---

## 项目结构

```
OneFileBox/
├── Models/                      # 实体模型（从 SVNFileBox 迁移）
│   ├── SvnStatus.cs            # SVN 状态枚举
│   ├── Repository.cs           # 仓库配置
│   ├── FileItem.cs              # 文件项
│   ├── FileCopyItem.cs          # 复制项
│   ├── FileCopyPlan.cs          # 复制计划
│   ├── SyncRecord.cs            # 同步记录
│   ├── ConflictedFileInfo.cs    # 冲突文件信息
│   └── AppConfig.cs             # 应用配置
├── Services/
│   ├── SvnCliService.cs         # ★ 新增：SVN CLI 封装（平替 SharpSvn）
│   ├── SvnCommand.cs            # ★ 修改：命令结构体
│   ├── SvnService.cs            # 待删除（SharpSvn）
│   ├── RepoManager.cs            # 仓库管理器（从 SVNFileBox 迁移）
│   ├── RepoGlobalManager.cs      # 全局仓库管理（单例）
│   ├── FileWatcherService.cs    # 文件监听
│   ├── SyncService.cs           # 同步服务
│   ├── SyncRecordService.cs     # 同步记录
│   └── ConfigService.cs         # 配置服务
├── ViewModels/
│   ├── MainViewModel.cs         # 主窗口 VM
│   ├── SyncRecordDisplay.cs     # 同步记录显示
│   └── (从 SVNFileBox 迁移)
├── Views/
│   ├── MainWindow.axaml         # 主窗口（从 SVNFileBox MainWindow.xaml 迁移）
│   ├── MainWindow.axaml.cs
│   └── (其他窗口后续迁移)
├── Converters/
│   ├── SvnStatusToColorConverter.cs
│   ├── FileTypeIconConverter.cs
│   ├── BoolToVisibleConverter.cs
│   └── (从 SVNFileBox 迁移)
├── Helpers/
│   └── (工具类后续迁移)
├── Assets/
│   └── Icons/                   # 图标资源
├── App.axaml                    # 应用入口
├── App.axaml.cs
├── Program.cs
└── OneFileBox.csproj
```

---

## SvnCliService 设计（SvnService 平替）

### 目标
用 `svn` CLI 调用替代 SharpSvn，保持接口一致，上层代码几乎不用改。

### SVN 命令行特性利用
```bash
# XML 输出解析（结构化结果）
svn status --xml --verbose
svn info --xml
svn log --xml --username X --password-from-stdin

# 进度跟踪（模拟 SharpSvn.Notify）
svn update --verbose --progress

# 非交互式 + 凭证传递
svn commit --non-interactive --trust-server-cert --username X --password-from-stdin
```

### 核心功能映射

| SharpSvn 接口 | SvnCliService 实现 |
|--------------|-------------------|
| `SvnClient.Status()` | `svn status --xml --verbose` → XML 解析 |
| `SvnClient.Update()` | `svn update --verbose --progress` → 进度回调 |
| `SvnClient.Commit()` | `svn commit --non-interactive` |
| `SvnClient.Add()` | `svn add --non-recursive` |
| `SvnClient.Delete()` | `svn delete --force` |
| `SvnClient.Revert()` | `svn revert --recursive` |
| `SvnClient.Info()` | `svn info --xml` |
| `SvnClient.GetRepositoryRoot()` | `svn info --xml` 解析 URL |
| `client.Notify` 事件 | 解析 `svn update --verbose` 输出行 |
| `SvnStatusArgs.RetrieveRemoteStatus` | `svn status --show-updates` |

### SvnCliService 接口（初步）

```csharp
public class SvnCliService : IDisposable
{
    // Tier 1 — ReadOnly（纯本地，无锁，并发读）
    Task<Dictionary<string, FileSvnStatus>> GetStatusAsync(string wcPath, bool depth);
    Task<List<string>> GetServerUpdatePathsAsync(string wcPath);
    Task<string> GetRepoUrlAsync(string wcPath);
    Task<int> GetWorkingCopyRevisionAsync(string wcPath);
    Task<int> GetHeadRevisionAsync(string repoUrl, string? user, string? pwd);

    // Tier 2 — LocalWrite（本地写锁，快速）
    Task<SvnResult> AddFileAsync(string path);
    Task<SvnResult> DeleteFileAsync(string path);
    Task<SvnResult> RevertAsync(string path);
    Task<SvnResult> ResolveAsync(string path, SvnAccept accept);

    // Tier 3 — HeavyWrite（写锁，耗时，网络）
    Task<SvnResult> CommitAsync(string wcPath, string message);
    Task<SvnResult> UpdateAsync(string wcPath);

    // 事件（对标 SharpSvn Notify）
    event Action<string, string>? FileTransferActivity;  // path, action

    // 并发控制
    // _writeSemaphore(1)  序列化所有写操作
    // _readSemaphore(10) 允许并发读
}
```

### SVN XML 输出解析示例

```bash
$ svn status --xml --verbose
<?xml version="1.0" encoding="UTF-8"?>
<status>
  <target path=".">
    <entry path="file.txt" kind="file">
      <wc-status item="modified" props="none" revision="123">
        <commit revision="122">
          <author>user</author>
          <date>2026-05-29T00:00:00.000000Z</date>
        </commit>
      </wc-status>
    </entry>
  </target>
</status>
```

```csharp
// 使用 System.Xml.Linq 解析
XDocument doc = XDocument.Parse(xmlOutput);
foreach (var entry in doc.Descendants("entry"))
{
    var path = entry.Attribute("path")?.Value;
    var kind = entry.Attribute("kind")?.Value;
    var item = entry.Element("wc-status")?.Attribute("item")?.Value;
    // 映射到 FileSvnStatus 枚举
}
```

### 进度跟踪实现

```csharp
// svn update --verbose --progress 输出格式：
// Updating 'file.txt'...
// Updated 5 of 10 files

var progressRegex = new Regex(@"Updating '([^']+)'");
var summaryRegex = new Regex(@"Updated (\d+) of (\d+) files");

Process.Start("svn", "update --verbose --progress");
process.OutputDataReceived += (s, e) =>
{
    var match = progressRegex.Match(e.Data);
    if (match.Success)
        FileTransferActivity?.Invoke(match.Group(1), "update");
};
```

---

## 从 SVNFileBox 迁移指南

### 迁移顺序（由浅入深）

```
1. UI 层（Views + ViewModels）
   ↓
2. Models 层（数据实体）
   ↓
3. Services 层（SvnService → SvnCliService）
   ↓
4. 功能验证（与 SVNFileBox 行为一致）
```

### SVNFileBox 参考文件

| 文件 | 路径 | 用途 |
|------|------|------|
| MainWindow.xaml | `~/aiworks/projects/repos/SVNFileBox/src/Views/MainWindow.xaml` | UI 参考 |
| MainWindow.xaml.cs | `~/aiworks/projects/repos/SVNFileBox/src/Views/MainWindow.xaml.cs` | 逻辑参考 |
| SvnService.cs | `~/aiworks/projects/repos/SVNFileBox/src/Services/SvnService.cs` | 接口定义参考 |
| Models | `~/aiworks/projects/repos/SVNFileBox/src/Models/` | 实体迁移 |
| ViewModels | `~/aiworks/projects/repos/SVNFileBox/src/ViewModels/` | VM 迁移 |
| Converters | `~/aiworks/projects/repos/SVNFileBox/src/Converters/` | 转换器迁移 |
| SPEC.md | `~/aiworks/projects/repos/SVNFileBox/SPEC.md` | 需求规格 |
| TODO.md | `~/aiworks/projects/repos/SVNFileBox/TODO.md` | 任务清单 |

---

## SharpSvn 功能覆盖清单

迁移到 SVN CLI 时，需要完整覆盖以下 SharpSvn 功能，不能有遗漏。

### SvnService 接口（Tier 1-3）

| SharpSvn API | CLI 实现方式 | 优先级 |
|--------------|-------------|--------|
| `SvnClient.Status()` + `SvnStatusEventArgs` | `svn status --xml --verbose` → XML 解析 | P0 |
| `SvnStatusArgs.Depth` (Infinity/Children) | `--depth infinity` / `--depth children` | P0 |
| `SvnStatusArgs.RetrieveAllEntries` | 自动（`--xml` 默认包含所有 entry） | P0 |
| `SvnStatusArgs.RetrieveRemoteStatus` | `svn status --show-updates` → 含 `out-of-date` 信息 | P0 |
| `item.LocalNodeStatus` 枚举映射 | CLI status code → `FileSvnStatus` 枚举 | P0 |
| `item.Conflicted` / `item.TreeConflict` | 在 XML 中检测 `item="conflicted"` + tree-conflict 节点 | P0 |
| `item.IsRemoteUpdated` | 解析 XML 中 `wc-status` 的 `revision` vs 服务器 revision | P0 |
| `SvnClient.GetRepositoryRoot()` | `svn info --xml` 解析 `<url>` + `<root>` | P0 |
| `SvnClient.Info()` + `SvnInfoEventArgs` | `svn info --xml` → 解析 revision/author/date | P0 |
| `SvnClient.GetHeadRevision()` (HEAD) | `svn info -r HEAD --xml`（需要网络） | P0 |
| `SvnClient.GetStatus()` + `out var conflictedResults` | `svn status --xml` 找 `item="conflicted"` | P0 |
| `SvnClient.GetLastChangedTime()` | `svn info --xml` 的 `<date>` 字段 | P1 |
| `SvnClient.GetWorkingCopyRoot()` | 向上找 `.svn` 目录 | P0 |
| `SvnClient.Authentication.ForceCredentials()` | `--username X --password-from-stdin` | P0 |
| `SvnClient.Authentication.SslServerTrustHandlers` | `--non-interactive --trust-server-cert` | P0 |
| `_writeSemaphore(1)` / `_readSemaphore(10)` | C# `SemaphoreSlim`（并发控制不变） | P0 |
| `client.Notify` 事件 | `svn update --verbose` 输出行解析 | P0 |
| `FileTransferActivity` 事件 | 解析输出进度行，触发回调 | P0 |
| `CredentialExpired` 事件 | 检测 auth 失败退出码，返回事件 | P0 |

### Tier 2 — LocalWrite（本地写，快速）

| SharpSvn API | CLI 实现 | 优先级 |
|-------------|---------|--------|
| `SvnClient.Add()` | `svn add --non-recursive <path>` | P0 |
| `SvnClient.Delete()` | `svn delete --force <path>` | P0 |
| `SvnClient.Move()` | `svn move <from> <to>` | P1 |
| `SvnClient.Revert()` + `SvnRevertArgs.Depth` | `svn revert --recursive <path>` | P0 |
| `SvnClient.Resolve()` + `SvnAccept` | `svn resolve --accept [base|working|theirs-full|mine-full] <path>` | P0 |
| `SvnClient.Lock()` + `SvnLockArgs.StealLock` | `svn lock --force <path>` | P2 |
| `TryCleanStaleLocks()` → `SvnClient.CleanUp()` | `svn cleanup` | P0 |
| 写锁并发（`_writeSemaphore`） | C# `SemaphoreSlim`（不变） | P0 |

### Tier 3 — HeavyWrite（网络，慢）

| SharpSvn API | CLI 实现 | 优先级 |
|-------------|---------|--------|
| `SvnClient.Commit()` + `SvnCommitArgs.LogMessage` | `svn commit -m "msg" --non-interactive --trust-server-cert` | P0 |
| `SvnClient.Update()` + `SvnUpdateResult` | `svn update --verbose --progress` | P0 |
| `SvnClient.CheckOut()` + `SvnCheckOutArgs` | `svn checkout <url> <path>` | P0 |
| `SvnClient.List()` + `SvnListArgs`（连接测试） | `svn list <url>` 连接探测 | P0 |
| 活动看门狗（`FileTransferTimeoutMs`） | 解析 `svn --verbose` 输出行，无活动超时取消 | P0 |
| `IsSvnAuthError()` — E170001 检测 | 检查 stderr 包含 `E170001` | P0 |
| 凭证过期重试逻辑 | 清除缓存 + 重试一次 | P1 |
| `SvnAuthenticationException` | 退出码 + stderr auth 失败识别 | P0 |
| `SvnAuthorizationException` | 退出码 13（权限拒绝） | P0 |
| `SvnRepositoryIOException`（SSL/E230001） | stderr E230001 | P0 |
| tree-conflict 检测（提交时） | commit 输出包含 `tree-conflict` | P0 |

### SvnAccept 枚举（Resolve 用）

```
SharpSvn.SvnAccept.TheirsFull  → svn resolve --accept theirs-full
SharpSvn.SvnAccept.MineFull   → svn resolve --accept mine-full
SharpSvn.SvnAccept.Working    → svn resolve --accept working  （tree-conflict 专用）
SharpSvn.SvnAccept.Base       → svn resolve --accept base
```

### SvnDepth 枚举

```
SvnDepth.Infinity  → --depth infinity
SvnDepth.Children  → --depth children（默认）
SvnDepth.Empty     → --depth empty
SvnDepth.Files     → --depth files
```

### 凭证传递（关键）

```bash
# 正确方式（不暴露密码在命令行）：
echo $PASSWORD | svn commit -m "msg" --username X --password-from-stdin --non-interactive --trust-server-cert

# Checkout 同样方式
```

---

---

## 其他技术问题清单

### 1. Timer 实现差异

| WPF / .NET | Avalonia 替代 | 说明 |
|-----------|-------------|------|
| `System.Timers.Timer` | `System.Timers.Timer`（.NET 标准可用） | SyncService / FileWatcherService 中的轮询定时器，无需改 |
| `DispatcherTimer`（WPF） | `Avalonia.Threading.DispatcherTimer` | UI 线程定时器 |
| `Dispatcher.Invoke(() => ...)` | `Dispatcher.InvokeAsync(() => ...)` | UI 线程调用，async 版本 |

### 2. Clipboard（WPF → Avalonia）

| WPF | Avalonia | 说明 |
|-----|----------|------|
| `Clipboard.ContainsFileDropList()` | `TopLevel.Clipboard?.ContainsFileUriList()` | 文件粘贴板检测 |
| `Clipboard.GetFileDropList()` | `TopLevel.Clipboard?.GetFileUriListAsync()` | 获取粘贴文件列表 |
| `Clipboard.SetFileDropList()` | `TopLevel.Clipboard?.SetFileUriList()` | 设置粘贴文件列表 |
| `Clipboard.SetText()` | `TopLevel.Clipboard?.SetTextAsync()` | 设置文本 |
| `Clipboard.ContainsText()` | `TopLevel.Clipboard?.ContainsText()` | 检测文本 |

### 3. MessageBox / Dialog

| WPF | Avalonia | 说明 |
|-----|----------|------|
| `MsgBox.Show()`（自定义 Window） | 迁移 XAML+逻辑到 Avalonia Window | 完整实现，含图标/按钮/本地化 |
| `MessageBoxResult` / `MessageBoxButton` / `MessageBoxImage` | 自定义 enum | `MessageBoxIconType` / `MessageBoxButtonType` |
| `OpenFileDialog` / `SaveFileDialog` | Avalonia 原生 | `OpenFolderDialog` / `OpenFileDialog` / `SaveFileDialog` |

### 4. WPF 特有 API 替换

| WPF | Avalonia | 说明 |
|-----|----------|------|
| `Application.Current.Shutdown()` | `Application.Current.Exit()` | 程序退出 |
| `WindowStartupLocation.CenterScreen` | 相同 | 窗口居中 |
| `Application.Current.Resources` | 相同 | 资源字典 |
| `System.Diagnostics.Process.Start()` | 相同 | 进程启动（通用）|
| `System.IO.Path.GetExtension()` | 相同 | 路径处理（通用）|

### 5. 平台特定功能（需要跨平台替代）

| WPF 特有 | 问题 | 解决方案 |
|---------|------|---------|
| `DpapiService`（DPAPI 加密） | 仅 Windows | 跨平台需替代（libsecret/Security.framework 或简化存储） |
| 注册表（`ThemeService` 用） | 仅 Windows | 改用 JSON 文件 `~/.onefilebox/settings.json` |

### 6. 数据持久化

| WPF | Avalonia | 说明 |
|-----|----------|------|
| 注册表存储主题 | JSON 文件 | `~/.onefilebox/settings.json` |
| SQLite（`SqliteSyncRecordStore`） | `Microsoft.Data.Sqlite` | nuget 包不变，跨平台兼容 |

### 7. WPF XAML 绑定差异

| WPF | Avalonia | 说明 |
|-----|----------|------|
| `RelativeSource={RelativeSource AncestorType=Window}` | 无 AncestorType | 改用 `x:Static` 或 ViewModel 注入 |
| `x:Name` 跨模板引用 | NameScope 不同 | 用 `FindName` 或 ViewModel 命令 |
| `Style.TargetType` + `BasedOn` | 相同 | 样式继承机制一致 |
| `DataTrigger` / `MultiDataTrigger` | 无直接对应 | 用代码切换样式 |
| `GridView` | `DataGrid` | 无 GridView，用 DataGrid 替代 |

### 8. Avalonia DataGrid 特性

| 特性 | 说明 |
|------|------|
| 虚拟化 | 默认启用，大文件列表不用手动优化 |
| SelectionMode | `SelectionMode.Extended` 支持 Ctrl/Shift 多选 |
| `CurrentItem` / `SelectedItem` | 双向绑定当前选中行 |
| `AutoSizeColumnsMode` | `None`/`AllCells`/`ColumnHeader`/`DisplayedCells` |
| 列排序 | `CanUserSortColumns="True"` |
| 行双击导航 | `RowPointerPressed` + 判断 `IsLeftButtonPressed` |
| 右键菜单 | `ContextMenu` 在 `DataGrid` 上直接绑定 |

### 9. 需要迁移的 Services（干净版本，全量）

| Service | 迁移优先级 | 备注 |
|---------|-----------|------|
| `SvnService` | **P0** | → `SvnCliService`（新建，替代 SharpSvn） |
| `SvnCommandExecutor` | **P0** | 改写 SharpSvn 调用 |
| `SyncService` | **P0** | 依赖 SvnCommandExecutor |
| `FileWatcherService` | **P0** | 逻辑不变，using 调整 |
| `RepoManager` | **P0** | 依赖上面三个 |
| `RepoGlobalManager` | **P0** | 依赖 RepoManager |
| `ConfigService` | **P0** | JSON 配置读写，基本不变 |
| `SyncRecordService` | **P1** | SQLite 存储，nuget 包不变 |
| `FileCopier` | **P1** | 文件复制进度，逻辑不变 |
| `DpapiService` | **P3** | Windows DPAPI 加密（暂不需要，SVN 自身有密码缓存） |
| `LocalizationService` | **P2** | i18n，可后续实现 |
| `ThemeService` / `ThemeWatcher` | **P3** | 删除，Semi.Avalonia 主题 + JSON 偏好存储替代，无需监听系统主题 |
| `NewFileService` | **P2** | 新建文件服务，可后续实现 |
| `FileAnalyzer` | **P2** | 文件分析，可后续实现 |
| `MigrationService` | **P3** | 用户数据迁移（SVNFileBox 旧版 → OneFileBox），程序启动时一次性执行，普通用户不用 |
| `SqliteSyncRecordStore` | **P1** | SQLite 底层，由 SyncRecordService 调用 |
| `IconExtractor` | 已删除不用 | 不迁移 |

### 10. 需要迁移的 Windows/Dialogs（干净版本）

| Window | 迁移优先级 | 说明 |
|---------|-----------|------|
| `MainWindow` | **P0** | 主界面，DataGrid 替代 GridView |
| `AddLocalRepoWindow` | **P0** | 添加本地仓库对话框 |
| `CheckoutWindow` | **P1** | 网络仓库 Checkout |
| `ConflictWindow` | **P1** | 冲突解决窗口 |
| `MsgBox`（MessageBoxWindow） | **P1** | 自定义消息框，完整迁移 |
| `SettingsWindow` | **P2** | 设置面板 |
| `FileCopyProgressWindow` | **P2** | 复制进度窗口 |
| `ProgressWindow` | **P2** | 通用进度窗口 |
| `InputDialog` | **P2** | 输入对话框 |
| `AboutWindow` | **P3** | 关于窗口 |
| `SplashWindow` | **P1** | 启动画面（显示迁移进度），用户升级时需要 |
| `AboutWindow` | **P3** | 关于窗口 |

---

## 重构原则

1. **每步编译验证**：每完成一个小任务（比如迁移一个 Model、创建一个 Service 方法），立即 `dotnet build` 验证通过，再继续下一步。不堆砌大量变更后一次性编译。
2. **接口先行**：先定义接口（如 `ISvnCommandExecutor`），再实现具体类（`SvnCliService`），确保上层代码不受影响。
3. **渐进式迁移**：先让 UI 能跑起来（Mock 数据也行），再逐步替换底层实现。

---

## 任务计划

### Phase 0 — 项目重置（已完成）
- [x] 清空 OneFileBox 当前混乱代码
- [x] 重新规划架构
- [x] 更新 DESIGN.md（本文档）

### Phase 1 — 基础搭建（TODO）
- [ ] 确认 Avalonia 12 模板项目可正常运行
- [ ] 创建 `SvnCliService.cs` 空壳 + 基础接口
- [ ] 迁移 Models 层（从 SVNFileBox/src/Models/）
- [ ] 迁移 Converters 层
- [ ] 确认项目可编译

### Phase 2 — UI 迁移（TODO）
- [ ] 迁移 MainWindow.axaml（参考 SVNFileBox/MainWindow.xaml）
- [ ] 迁移 MainWindow.axaml.cs
- [ ] 迁移 ViewModels/MainViewModel.cs
- [ ] 实现仓库列表（左侧栏）
- [ ] 实现文件列表（右侧栏，带 SVN 状态列）
- [ ] 实现右键菜单（Commit/Update/Add/Delete/Revert/Refresh）
- [ ] 实现导航（进入目录、返回上级）
- [ ] 实现添加仓库/Checkout 仓库对话框
- [ ] 验证 UI 交互正常

### Phase 3 — SVN CLI 服务实现（TODO）
- [ ] 实现 `SvnCliService.GetStatusAsync()` — XML 解析
- [ ] 实现 `SvnCliService.UpdateAsync()` — 进度跟踪
- [ ] 实现 `SvnCliService.CommitAsync()`
- [ ] 实现 `SvnCliService.AddFileAsync()` / `DeleteFileAsync()`
- [ ] 实现 `SvnCliService.RevertAsync()` / `ResolveAsync()`
- [ ] 实现 `SvnCliService.InfoAsync()` / `GetRepoUrlAsync()`
- [ ] 实现凭证传递（--password-from-stdin）
- [ ] 实现并发控制（`_writeSemaphore` / `_readSemaphore`）
- [ ] 实现 `FileTransferActivity` 事件（notify 模拟）

### Phase 4 — 核心功能集成（TODO）
- [ ] 替换 `GlobalSvnRepoManager` 中的 SharpSvn 调用
- [ ] 替换 `SvnSyncManager` 中的 SharpSvn 调用
- [ ] 验证文件监听（FileWatcher → Add/Delete 队列）
- [ ] 验证批量提交（凑够 30 个文件自动触发）
- [ ] 验证远程更新（定时轮询 HEAD）
- [ ] 验证冲突处理（ConflictResolvedRequired 回调）

### Phase 5 — 完善与主题（TODO）
- [ ] UI 主题美化（Semi.Avalonia 配色调整）
- [ ] 设置面板（同步间隔、代理、自动启动）
- [ ] 系统托盘（最小化到托盘）
- [ ] 同步记录日志查看器

### Phase 6 — 跨平台与发布（TODO）
- [ ] Linux AppImage 打包
- [ ] macOS bundle 打包
- [ ] Windows 安装包

---

## 架构类图（简化）

```
Views
  └── MainWindow.axaml + .axaml.cs
       ↓ (DataContext)
ViewModels
  └── MainViewModel
       ↓ (调用)
Services
  ├── SvnCliService          ★ 替代 SharpSvn
  ├── RepoGlobalManager       # 全局仓库管理（单例）
  ├── RepoManager             # 单仓库管理
  ├── FileWatcherService      # 文件监听
  ├── SyncService             # 同步引擎
  ├── SyncRecordService       # 记录服务
  └── ConfigService           # JSON 配置持久化
       ↓ (持久化)
Models
  ├── Repository.cs           # 仓库配置
  ├── FileItem.cs             # 文件项
  ├── SvnStatus.cs            # 状态枚举
  ├── SyncRecord.cs           # 同步记录
  └── AppConfig.cs            # 应用配置
```

---

## Avalonia 12 注意事项

1. **编译绑定默认启用**：`{Binding}` 自动为 `CompiledBinding`
2. **文本整形器**：App.cs 需要 `.UseSkia().UseHarfBuzz()`
3. **TopLevel 获取**：`TopLevel.GetTopLevel(visual)` 不能直接 cast
4. **窗口装饰**：`Window.WindowDecorations` 替代旧 API
5. **剪贴板**：`DataTransfer.TryGetTextAsync()` 替代 `DataObject.GetTextAsync()`

---

## SVN CLI 参考命令

```bash
# 状态查询（XML 格式）
svn status --xml --verbose
svn status --xml --show-updates  # 含远程状态

# 信息查询
svn info --xml [path]
svn info --xml [url]

# 更新
svn update --verbose --progress
svn update --accept postpone

# 提交
svn commit --non-interactive --trust-server-cert -m "msg" --username X --password-from-stdin

# 添加/删除
svn add --non-recursive [path]
svn delete --force [path]

# 还原/解决
svn revert --recursive [path]
svn resolve --accept [base|working| theirs-full|mine-full] [path]

# 清理
svn cleanup

# 导出/checkout
svn checkout [url] [path]
svn export [url] [path]
```

---

*本文档更新于 2026-05-29*