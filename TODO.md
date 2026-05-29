# OneFileBox 实现计划

> 更新日期：2026-05-29
> 当前版本：r977（SVN）
> 目标：完成所有缺失功能的跨平台实现

---

## 一、P0 核心功能

### P0-1：右键菜单（ContextMenu）
**文件**：`Views/MainWindow.axaml`
**内容**：
- [ ] 目录/文件右键菜单：`打开` / `复制路径` / `删除` / `重命名` / `新建文件` / `同步`
- [ ] 多选时的批量右键菜单：`批量删除` / `批量复制` / `批量提交`
- [ ] Avalonia `ContextMenu` + `MenuItem`
- [ ] `FileItemViewModel` 新增 `CanDelete / CanRename` 属性

**关联**：`FileItemViewModel.cs`, `MainWindow.axaml.cs`

---

### P0-2：Repository 切换 → RepoGlobalManager.SwitchToAsync 联动
**文件**：`ViewModels/MainWindowViewModel.cs`
**内容**：
- [ ] `OnSelectedRepositoryChanged` 调用 `RepoGlobalManager.SwitchToAsync()`
- [ ] 仓库切换后自动 `LoadDirectoryAsync()` 刷新文件列表
- [ ] `GlobalManager.FilesChanged` 事件 → 触发 UI 刷新
- [ ] `GlobalManager.SyncNotification` → 显示在状态栏
- [ ] `GlobalManager.ConflictDetected` → 弹出 `ConflictWindow`

**关联**：`MainWindowViewModel.cs`, `RepoGlobalManager.cs`

---

### P0-3：多选 + 批量操作
**文件**：`ViewModels/MainWindowViewModel.cs`, `Views/MainWindow.axaml`
**内容**：
- [ ] `DataGrid` 添加 `DataGridCheckBoxColumn`（多选列）
- [ ] `SelectedItems` 集合：`ObservableCollection<FileItemViewModel>`
- [ ] `SelectAllCommand` — 全选当前目录
- [ ] `ClearSelectionCommand` — 清除选择
- [ ] 批量删除：`DeleteSelectedAsync()` → 遍历调用 `SvnCliService.DeleteAsync`
- [ ] 批量复制：跳转到 `FileCopier` 流程
- [ ] 批量提交：`CommitSelectedAsync()` → 打包提交

**关联**：`MainWindowViewModel.cs`, `MainWindow.axaml`, `SvnCliService.cs`

---

### P0-4：FileCopier 服务
**文件**：
- `Services/FileAnalyzer.cs`（已有模型）
- `Services/FileCopier.cs`（新实现）
- `Views/FileCopyProgressWindow.axaml`（新窗口）
- `Views/FileCopyProgressWindow.axaml.cs`

**内容**：
- [ ] `FileAnalyzer.Analyze()` — 扫描源路径，生成 `FileCopyPlan`
- [ ] `FileCopier.ExecuteAsync()` — 执行复制，进度回调
- [ ] 复制完成后自动 `svn add` + `CommitAsync`
- [ ] `FileCopyProgressWindow` — Progress bar + 文件名 + 取消按钮
- [ ] `CopyProgress` 类：`CurrentFile / BytesCopied / TotalBytes / ProgressPercent`
- [ ] 支持跨仓库复制（不同本地路径 → 各自 SVN add）

**关联**：`FileCopyPlan.cs`, `SvnCliService.cs`, `SyncService.cs`（复用 AddPath + Commit）

---

## 二、P1 重要功能

### P1-1：InputDialog（重命名 / 新建文件）
**文件**：`Views/InputDialog.axaml` + `.axaml.cs`

**内容**：
- [ ] 通用输入对话框：标题 + 提示文本 + 输入框 + OK/Cancel
- [ ] 重命名：预填充原文件名，光标选中扩展名之前
- [ ] 新建文件：输入新文件名，选择文件类型

**关联**：`MainWindow.axaml.cs`

---

### P1-2：SQLite SyncRecordStore
**文件**：
- `Services/SqliteSyncRecordStore.cs`
- `Services/SyncRecordService.cs`（已有模型）
- `Views/SyncRecordWindow.axaml` + `.axaml.cs`（同步历史窗口）

**内容**：
- [ ] `SqliteSyncRecordStore` — SQLite CRUD 操作
  - 表：`SyncRecords (Id, Timestamp, RepoName, FilePath, Operation, Result, Message)`
  - 保留期：`MaxAgeDays=10`，`MaxRecordsPerRepo=10000`
- [ ] `SyncRecordService` — 内存缓存 + SQLite 持久化
- [ ] `SyncRecordWindow` — 显示同步历史列表
  - 时间 / 仓库名 / 文件路径 / 操作 / 结果
  - 支持排序和搜索

**关联**：`SyncRecord.cs`, `SyncService.cs`（调用 AddRecord）

---

### P1-3：NewFileService（新建文件模板）
**文件**：`Services/NewFileService.cs`

**内容**：
- [ ] `Create(string fullPath)` — 根据扩展名生成最小有效内容
- [ ] 支持格式：
  - `.txt` — 空文件
  - `.docx` — ZIP（[Content_Types].xml + word/document.xml）
  - `.xlsx` — ZIP（[Content_Types].xml + xl/workbook.xml + xl/worksheets/sheet1.xml）
  - `.pptx` — ZIP（[Content_Types].xml + ppt/presentation.xml）
  - `.png` — 1x1 透明像素（PNG 签名 + IHDR + IDAT + IEND）
  - `.bmp` — 1x1 白色像素 BMP
- [ ] 右键菜单集成：`新建 → txt/docx/xlsx/pptx/png/bmp`

**关联**：`MainWindow.axaml`（右键菜单）, `InputDialog.axaml`

---

## 三、P2 增强体验

### P2-1：AboutWindow
**文件**：`Views/AboutWindow.axaml` + `.axaml.cs`

**内容**：
- [x] 程序名称：OneFileBox
- [x] 版本号：`1.0.0`
- [x] 描述：跨平台 SVN 文件管理器
- [x] 版权信息
- [x] 链接：GitHub 仓库地址

**关联**：`MainWindow.axaml`（Settings 或 Help 菜单入口）

---

### P2-2：ProgressWindow（通用进度窗口）
**文件**：`Views/ProgressWindow.axaml` + `.axaml.cs`

**内容**：
- [x] `SetStatus(string msg)` — 状态文本
- [x] `SetProgress(double percent)` — 精确进度
- [x] `ShowCancelButton(bool)` — 显示/隐藏取消按钮
- [x] `CancellationToken` — 支持取消操作（event）
- [x] 通用进度窗口，各操作复用

**关联**：`CheckoutWindow.axaml`（替换 ProgressBar）, `FileCopyProgressWindow`

---

### P2-3：SyncStatusType 状态管理
**文件**：`ViewModels/MainWindowViewModel.cs`

**内容**：
- [ ] `SyncStatusType enum`：`Idle / Syncing / Success / Failed`
- [ ] `[ObservableProperty] private SyncStatusType _syncStatus`
- [ ] `SyncStatus` 在 `SyncService` 启动时 = `Syncing`
- [ ] `SyncService.SyncNotification` → 触发 `SyncStatus = Success`（3s 后恢复 Idle）
- [ ] UI 状态栏显示同步图标（旋转/绿勾/红叉）

**关联**：`SyncService.cs`, `MainWindow.axaml`

---

### P2-4：MessageBox 替代
**说明**：Avalonia 有原生 `MessageBox`，无需自定义窗口
- [ ] 检查 `Avalonia.Controls.MessageBox` 用法
- [ ] 替换 SVNFileBox 的 `MessageBoxWindow` 调用

---

### P2-5：多语言国际化（Language Switch）
**文件**：`Services/LocalizationService.cs`
**内容**：
- [ ] `SetLanguage(string lang)` — 切换语言后重新加载所有字符串
- [ ] `LanguageChanged` 事件 → 通知所有订阅者
- [ ] `Views` 重新绑定文本（避免重启）
- [ ] 检查 SettingsWindow 的语言切换是否生效

**关联**：`SettingsWindow.axaml.cs`, `App.axaml.cs`

---

## 四、已实现功能（参考）

| 功能 | 文件 | 状态 |
|------|------|------|
| SvnCliService | `Services/SvnCliService.cs` | ✅ |
| FileWatcherService | `Services/FileWatcherService.cs` | ✅ |
| SyncService | `Services/SyncService.cs` | ✅ |
| RepoManager | `Services/RepoManager.cs` | ✅ |
| RepoGlobalManager | `Services/RepoGlobalManager.cs` | ✅ |
| MainWindowViewModel | `ViewModels/MainWindowViewModel.cs` | ✅ |
| MainWindow | `Views/MainWindow.axaml` + `.cs` | ✅ |
| AddLocalRepoWindow | `Views/AddLocalRepoWindow.axaml` + `.cs` | ✅ |
| CheckoutWindow | `Views/CheckoutWindow.axaml` + `.cs` | ✅ |
| ConflictWindow | `Views/ConflictWindow.axaml` + `.cs` | ✅ |
| SettingsWindow | `Views/SettingsWindow.axaml` + `.cs` | ✅ |
| SplashWindow | `Views/SplashWindow.axaml` + `.cs` | ✅ |
| System Tray | `App.axaml` + `MainWindow.axaml.cs` | ✅ |
| ConfigService | `Services/ConfigService.cs` | ✅ |
| LocalizationService | `Services/LocalizationService.cs` | ✅ |

---

## 五、实现顺序

```
第一轮（P0 核心）：
  1. P0-2：Repository 切换 → SwitchToAsync（最基础，影响全局）
  2. P0-1：右键菜单（交互入口）
  3. P0-3：多选 + 批量操作（右键菜单需要）
  4. P0-4：FileCopier 服务（文件复制是核心需求）

第二轮（P1 重要）：
  5. P1-1：InputDialog（重命名需要）
  6. P1-2：SQLite SyncRecordStore（同步历史）
  7. P1-3：NewFileService（新建文件需要 InputDialog）

第三轮（P2 增强）：
  8. P2-1：AboutWindow
  9. P2-2：ProgressWindow
  10. P2-3：SyncStatusType 状态
  11. P2-4：MessageBox 替代
  12. P2-5：多语言国际化
```

---

### P2-6：CheckoutWindow 双重功能（密码过期触发）
**文件**：`Views/CheckoutWindow.axaml` + `.axaml.cs`
**说明**：
- **功能一**：正常 checkout（从 SVN URL 检出仓库）
- **功能二**：密码过期时重新验证（SVN 缓存失效 → 触发 `CredentialExpired` → 调用 CheckoutWindow 重新输入密码）
- 当前 OneFileBox 的 CheckoutWindow 只有功能一，功能二待实现

**关联**：`SvnCliService.CredentialExpired` 事件 → `CheckoutWindow.ShowDialog()` 重新验证

---

## 六、注意事项

- **编译优先**：每实现一个功能，立即 `dotnet build` 验证
- **不要积压**：每个功能单独 commit（SVN），不要等所有功能做完一起推
- **Avalonia 限制**：
  - 没有 `ListView`/`GridView`，用 `DataGrid`
  - 没有 WPF `PasswordBox`，用普通 `TextBox`
  - `OpenFolderDialog` → `StorageProvider.OpenFolderPickerAsync`
  - `Window.Owner` 是 protected，直接 `ShowDialog()` 不设 Owner
- **跨平台**：不用 `System.Drawing`，IconExtractor 方案用 emoji 回退
- **凭据存储**：不用 DpapiService，SVN 有自己的凭据缓存机制