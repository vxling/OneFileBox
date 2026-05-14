# OneFileBox 设计文档

## 项目信息
- **定位**：跨平台桌面文件管理应用，参考 SVNFileBox 核心功能
- **框架**：Avalonia 12.0.3 + .NET 10.0
- **架构**：MVVM + 接口分离（支持多协议扩展）
- **目录**：`/home/osuser/aiworks/projects/repos/OneFileBox/`

---

## 技术栈

| 组件 | 版本 | 说明 |
|------|------|------|
| Avalonia | **12.0.3** | 跨平台 UI 框架 |
| Avalonia.Desktop | **12.0.3** | Desktop 运行时 |
| Avalonia.Themes.Fluent | **12.0.3** | Fluent 主题 |
| Avalonia.Fonts.Inter | **12.0.3** | Inter 字体 |
| AvaloniaUI.DiagnosticsSupport | **2.2.0** | DevTools（替换 Diagnostics） |
| CommunityToolkit.Mvvm | **8.4.0** | MVVM 基础设施 |
| TargetFramework | net10.0 | |

> ⚠️ **Avalonia 12 升级说明**：原脚手架为 11.3.12，本次升级到 12.0.3。

---

## Avalonia 12.x Breaking Changes（升级必读）

### 1. 包版本 + 包名变更
```xml
<!-- 移除 (已废弃) -->
Avalonia.Diagnostics 11.3.12

<!-- 新增 -->
AvaloniaUI.DiagnosticsSupport 2.2.0
```
调用方式：`AttachDevTools()` → `AttachDeveloperTools()`

### 2. 编译绑定默认启用
`<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` 已默认 true，所有 XAML `{Binding}` 自动为 `CompiledBinding`。

### 3. 文本整形器（Skia 用户必加）
```csharp
AppBuilder.Configure<App>()
    .UseSkia()
    .UseHarfBuzz();  // 否则启动抛异常
```

### 4. 窗口装饰系统重构
- `Window.ExtendClientAreaChromeHints` → **移除**，改用 `Window.WindowDecorations` + `ExtendClientAreaToDecorationsHint`
- `TitleBar`, `CaptionButtons`, `ChromeOverlayLayer` → **移除**

### 5. TopLevel API
- 不能直接 cast Visual → TopLevel，必须 `TopLevel.GetTopLevel(Visual)`

### 6. 剪贴板 API
- `DataObject` → `DataTransfer`
- `GetTextAsync()` → `TryGetTextAsync()`

### 7. 选择行为（触控/笔）
- 选择在**释放**时触发，按下不触发

---

## 架构设计

```
OneFileBox/
├── Models/
│   ├── FileItem.cs           # 文件/目录项（含协议类型、SVN状态）
│   ├── Repository.cs         # 仓库定义（URL、协议、本地路径）
│   ├── SyncRecord.cs         # 同步记录
│   └── ConnectionConfig.cs   # 连接配置（加密存储）
├── Services/
│   ├── IFileService.cs       # 文件协议抽象接口
│   ├── SvnService.cs         # SVN 实现（SharpSvn/CLI）
│   ├── SftpService.cs        # SFTP 实现（预留）
│   ├── FileWatcherService.cs # 本地文件系统监控
│   ├── SyncEngine.cs         # 同步调度（FileWatcher + 轮询）
│   ├── QueueCommitProcessor.cs  # 5s 防抖批量提交
│   ├── ConfigService.cs      # JSON 配置持久化
│   └── CryptoService.cs      # 凭证加密（DPAPI/libsecret）
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── RepositoryListViewModel.cs
│   ├── FileListViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── MainWindow.axaml
│   ├── RepositoryListView.axaml
│   ├── FileListView.axaml
│   └── SettingsView.axaml
├── Converters/
├── Localization/
└── Assets/
```

### 核心架构决策

| 决策 | 选择 | 说明 |
|------|------|------|
| 协议层 | `IFileService` 接口 + 注入 | 当前 SVN，后续扩展 SFTP/WebDAV |
| SVN 调用 | SharpSvn（Windows）/ svn CLI（Linux） | 跨平台兼容 |
| 并发控制 | `SemaphoreSlim(1,1)` | 串行化所有协议操作 |
| 同步策略 | FileWatcher + 60s 轮询 | 同 SVNFileBox |
| 冲突处理 | Last-Write-Wins | 时间戳比对 |
| 凭证存储 | DPAPI（Win）/ libsecret（Linux） | 安全存储 |
| UI 框架 | Avalonia + FluentTheme | 跨平台 XAML |

---

## 功能规划

### Phase 1 - 核心（优先）
1. **仓库管理** — 添加仓库（URL checkout / 本地 working copy），JSON 配置持久化
2. **双向同步**
   - 上传：FileWatcher 监本地变化 → 5s 防抖 → `svn add/commit`
   - 下载：60s 轮询比对 HEAD → `svn update`
3. **冲突处理** — Last-Write-Wins 自动解决
4. **文件浏览** — 导航 working copy，显示 SVN 状态徽章（Modified/Added/Deleted/Conflicted/Unversioned）
5. **同步记录** — 每次操作记日志（timestamp、file、operation、result）

### Phase 2 - 增强
6. **系统托盘** — 最小化到托盘，同步事件通知
7. **设置面板** — 同步间隔、代理、自动启动、最小化到托盘
8. **重试队列** — 失败文件（如被 Excel 锁定）每个轮询周期重试，3次后告警

### Phase 3 - 扩展
9. **多协议支持** — SFTP、WebDAV 扩展
10. **跨平台打包** — Linux AppImage / macOS bundle

---

## 参考项目

| 项目 | 路径 | 用途 |
|------|------|------|
| SVNFileBox（WPF） | `/home/osuser/aiworks/projects/repos/SVNFileBox/SVNFileBoxWpf/` | C# MVVM 参考 |
| SVNFileBox（Qt） | `/home/osuser/aiworks/projects/repos/SVNFileBox/SVNFileBox-Qt/` | 跨平台参考 |