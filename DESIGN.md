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
│   ├── SvnModels.cs            # SVN 实体模型（enums + entity classes）
├── Services/
│   ├── SvnCmdHelper.cs         # SVN 命令执行工具（静态类）
│   ├── SvnSyncManager.cs       # 单仓库同步管理器（核心引擎）
│   ├── GlobalSvnRepoManager.cs  # 全局多仓库管理器（单例）
│   └── FolderFileWatcher.cs    # 文件系统监听（单例）
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── FileItemViewModel.cs    # 扩展了 SvnState/SvnStateText
│   ├── RepositoryItemViewModel.cs  # 扩展了 Key/SvnUrl/UserName/Password
│   └── ViewModelBase.cs
├── Views/
│   ├── MainWindow.axaml        # 含 SVN 状态列、拖放支持
│   └── MainWindow.axaml.cs    # 拖放事件处理、HostTopLevel 注入
├── Converters/
├── Localization/
└── Assets/
```

### 核心架构决策

|| 决策 | 选择 | 说明 |
||------|------|------|
|| 协议层 | `IFileService` 接口 + 注入 | 当前 SVN，后续扩展 SFTP/WebDAV |
|| SVN 调用 | svn CLI（全平台统一） | `--username/--password-from-stdin/--no-auth-cache/--non-interactive` |
|| 并发控制 | `Channel<T>` 优先级队列 | 高优先级（新增/删除） vs 低优先级（批量提交/更新） |
|| 同步策略 | FileWatcher + 定时轮询 | 同 SVNFileBox |
|| 文件防抖 | `ConcurrentDictionary` + `CancellationTokenSource`（2秒） | 等文件稳定再提交 |
|| 批量提交 | 凑够30个文件自动触发 OR 手动触发 | 按目录分组 commit |
|| 冲突处理 | 用户确认回调（`ConflictResolvedRequired`） | 支持 UseRemote/UseLocal/KeepAll |
|| 凭证存储 | stdin 管道传递 | `--password-from-stdin` 避免命令行暴露 |
|| UI 框架 | Avalonia 12 + FluentTheme | 跨平台 XAML |

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
## 参考项目
|| 项目 | 路径 | 用途 |
||------|------|------|
|| SVNFileBox（WPF） | `/home/osuser/aiworks/projects/repos/SVNFileBox/SVNFileBoxWpf/` | C# MVVM 参考 ||
|| SVNFileBox（Qt） | `/home/osuser/aiworks/projects/repos/SVNFileBox/SVNFileBox-Qt/` | 跨平台参考 ||

---

## Models 层设计（`OneFileBox_new.Models`）

命名空间 `SvnCloudSync`（源码兼容），实现文件 `Models/SvnModels.cs`。

### Enums
|| 名称 | 值 | 用途 |
||------|---|------|
|| `SvnLogLevel` | `Info`, `Warn`, `Error` | 日志级别 |
|| `SvnOperateType` | `AddFile`, `DeleteFile`, `CleanLock`, `BatchCommit`, `BatchUpdate`, `CommitAll`, `UpdateRemote` | 操作类型 |
|| `SvnConflictResolveMode` | `UseRemote`, `UseLocal`, `KeepAll` | 冲突解决模式 |

### Entity Classes
|| 类名 | 用途 |
||------|------|
|| `SvnFileItemState` | UI 列表专用：文件 SVN 状态（FullPath/Name/IsFolder/StatusCode/StatusText/StateIcon/IsVersionControl/FileSize/ModifyTime） |
|| `SvnSyncRecordItem` | 同步记录（SyncPath/OperateName/IsSuccess/Message/FileSize/OperateTime） |
|| `SvnSyncTask` | Channel 队列任务（OpType/TargetPath） |
|| `SvnFileDiffInfo` | 本地与远程 diff（FullLocalPath/RelativeRemotePath/LocalRevision/RemoteRevision/IsLocalMissing/NeedUpdate） |
|| `SvnExecuteResult` | SVN 命令执行结果（Success/ExitCode/Output/ErrorMsg/UserCanceled） |
|| `SvnProgressInfo` | 进度追踪（CurrentFile/TotalBytes/DoneBytes/Percent/IsActiveWorking/OperateName/LastActiveTime/TaskStartTime） |
|| `SvnLongTaskInfo` | 长任务警告（FileName/FileSizeBytes/CurrentPercent/ElapsedMs/OperateName） |
|| `SvnConflictFileInfo` | 冲突文件信息 |
|| `SvnBatchOperateProgress` | 批量操作进度（CurrentPath/CompletedCount/TotalCount/TipText/IsFinished，含有 `TotalProgress` 计算属性） |

---

## Services 层设计（`OneFileBox_new.Services`）

### SvnCmdHelper — SVN 命令执行工具（静态类）

**职责**：封装所有 `svn` CLI 调用，负责进度解析、重试逻辑、超时控制。

**核心方法**：
|| 方法 | 说明 |
||------|------|
|| `ExecuteAsyncWithProgress(args, workDir, user, pwd, opName, manager, isBigTask)` | 带进度回调的 SVN 执行，支持长任务警告 |
|| `ExecuteWithRetryAsync(args, ..., retryTimes=3, retrySleepMs=1000)` | 自动重试（默认3次，间隔1s） |
|| `SafeAddFileAsync(path, user, pwd, manager)` | 安全 add，返回 `SvnSyncRecordItem` |
|| `SafeDeleteFileAsync(path, user, pwd, manager)` | 安全 delete |
|| `CommitDirAsync(dir, msg, user, pwd, manager)` | 提交目录 |
|| `UpdateDirAsync(dir, user, pwd, manager)` | 更新目录 |
|| `CleanUpAsync(dir, user, pwd, manager)` | 清理锁定 |
|| `ResolveConflictFile(filePath, mode, user, pwd, manager)` | 解决冲突 |

**内部常量**：`LongTimeWarnMs=120000`（2分钟警告），`BigFileMaxTimeoutMs=600000`（10分钟），`NormalMaxTimeoutMs=120000`（2分钟）

**进度解析**：正则 `(\d+)%\s*\((\d+)/(\d+)\s*bytes\)` 解析 SVN --progress 输出

---

### SvnSyncManager — 单仓库同步管理器（核心引擎）

**职责**：管理单个 SVN 仓库的完整同步生命周期。

**事件**（`internal event`，通过 `Raise*()` 方法对外触发）：
|| 事件 | 用途 |
||------|------|
|| `LogReceived` | 日志（Info/Warn/Error） |
|| `FileProgressChanged` | 单文件进度更新 |
|| `LongTaskConfirmRequired` | 长任务确认（返回 bool，用户可取消） |
|| `ConflictResolvedRequired` | 冲突解决回调 |
|| `BatchCommitProgress` | 批量提交进度 |
|| `BatchUpdateProgress` | 批量更新进度 |
|| `SingleSyncCompleted` | 单次同步完成 |

**核心字段**：
|| 字段 | 类型 | 说明 |
||------|------|------|
|| `_highTaskChannel` | `Channel<SvnSyncTask>` | 高优先级队列（新增/删除文件） |
|| `_lowTaskChannel` | `Channel<SvnSyncTask>` | 低优先级队列（批量提交/更新） |
|| `_pendingCommitFiles` | `ConcurrentDictionary<string, byte>` | 待提交文件集 |
|| `_fileDebounceDict` | `ConcurrentDictionary<string, CancellationTokenSource>` | 文件防抖字典 |
|| `_autoUpdateTimer` | `Timer` | 定时轮询远程更新 |

**关键设计——Channel 优先级队列**：
- 高优先级通道（容量1000）：文件新增、删除
- 低优先级通道（容量1000）：批量提交、批量更新
- 消费循环优先读高优先级队列，读不到才读低优先级

**关键设计——文件防抖机制**：
- 触发：`DebounceFileChanged(fullFilePath)`
- 防抖延迟：`DebounceMs = 2000ms`
- 过滤：`.tmp`/`~$`/`.crdownload` 后缀文件不触发
- 稳定后：加入待提交集合，凑够 `BatchCommitFileCount=30` 自动触发批量提交

**关键设计——优雅退出**：
1. `StopSyncService()` 停止定时器、关闭 Channel
2. 若有待提交文件，执行最后一次批量提交
3. 等待当前操作完成（`WaitCurrentOperateIdleAsync`）
4. 取消所有未触发的防抖 CTS

**对外接口**：
|| 方法 | 用途 |
||------|------|
|| `StartSyncService()` | 启动同步服务（消费循环 + 定时更新） |
|| `StopSyncService()` | 停止同步服务 |
|| `DebounceFileChanged(fullFilePath)` | 文件变更防抖入口 |
|| `EnqueueDeleteFile(fullFilePath)` | 删除文件入口 |
|| `EnqueueBatchCommit()` | 手动触发批量提交 |
|| `EnqueueBatchUpdate()` | 手动触发批量更新 |
|| `GetDirectoryFileSvnStateAsync(dirPath)` | 查询目录下所有文件 SVN 状态（只读，不冲突） |
|| `CleanLocalSvnLockAsync()` | 清理本地 SVN 锁定 |
|| `ShutdownAndWaitFinishAsync()` | 优雅关闭（等待队列清空） |

---

### GlobalSvnRepoManager — 全局多仓库管理器（单例）

**职责**：管理多个 `SvnSyncManager` 实例，支持仓库切换和单仓库活跃/休眠。

**设计原则**：同一时刻只有一个仓库处于活跃状态（`CurrentActiveRepo`），切换时旧仓库进入休眠。

**接口**：
|| 方法 | 用途 |
||------|------|
|| `RegisterRepo(key, localPath, svnUrl, user, pwd)` | 注册仓库，返回 `SvnSyncManager` 实例 |
|| `SwitchActiveRepoAsync(repoKey)` | 切换活跃仓库（旧仓库优雅休眠，新仓库启动） |
|| `GetCurrentRepoDirectoryFiles(folderPath)` | 查询当前仓库目录文件状态 |
|| `ShutdownAllAsync()` | 全部仓库优雅关闭 |
|| `GetRepo(key)` | 根据 key 获取仓库管理器 |

---

### FolderFileWatcher — 文件系统监听（单例）

**职责**：封装 `FileSystemWatcher`，监听当前活跃仓库目录的文件变化。

**监听事件**：Created / Changed / Deleted / Renamed

**核心行为**：
- 切换仓库时自动 `SetWatchPath(newRoot)` 更换监听目录
- `EnableRaisingEvents = false` 时停止监听（切换期间）
- `InternalBufferSize = 65536`（64KB，防止大量文件时缓冲区溢出）
- Created/Changed → `CurrentActiveRepo?.DebounceFileChanged()`
- Deleted → `CurrentActiveRepo?.EnqueueDeleteFile()`
- Renamed → 旧路径 EnqueueDeleteFile + 新路径 DebounceFileChanged

---

## 同步机制总览

```
用户/程序创建文件
    ↓
FileSystemWatcher 捕获（Created 事件）
    ↓
DebounceFileChanged(fullFilePath)
    ├─ 过滤临时文件（.tmp/~$/.crdownload）
    ├─ 2秒防抖（ConcurrentDictionary CTS）
    └─ 稳定后 → AddFile → 高优先级 Channel
                    ↓
         凑够30个文件 OR 手动触发
                    ↓
         EnqueueBatchCommit → 低优先级 Channel
                    ↓
         ExecuteBatchCommitAsync()
                    ├─ 按目录分组
                    └─ svn commit -m "yyyy-MM-dd HH:mm"
                          ↓
         SingleSyncCompleted 事件
```

**远程同步**：`_autoUpdateTimer` 每 `AutoUpdateIntervalSec` 秒触发 `EnqueueBatchUpdate()` → 比对 HEAD → `svn update --accept postpone`

---

## 核心设计决策

|| 决策 | 选择 | 说明 |
||------|------|------|
|| SVN 调用 | svn CLI（跨平台） | `--username/--password-from-stdin/--no-auth-cache/--non-interactive` |
|| 并发控制 | `Channel<T>` 优先级队列 | 高优先级（新增/删除） vs 低优先级（批量） |
|| 文件防抖 | `ConcurrentDictionary` + `CancellationTokenSource` | 2秒稳定延迟防抖 |
|| 批量提交阈值 | 30 个文件 OR 手动触发 | 防止单次 commit 文件过多 |
|| 长任务处理 | 用户确认机制 | `LongTaskConfirmRequired` 事件返回 bool |
|| 冲突处理 | `ConflictResolvedRequired` 回调 | 支持 UseRemote/UseLocal/KeepAll |
|| 凭证传递 | stdin 管道 | `--password-from-stdin` 避免命令行暴露 |
|| 优雅退出 | 先停止新任务 → 再等待队列清空 → 最后清理 CTS | 确保不丢待提交文件 |