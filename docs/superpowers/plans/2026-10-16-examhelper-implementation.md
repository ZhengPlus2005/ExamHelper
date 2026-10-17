# ExamHelper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Windows 11 x64 单屏环境交付可按当前用户安装的 ExamHelper EXE，支持历史对话、受捕获保护的悬停辅助窗口、截图与语音图文提问。

**Architecture:** C#／.NET＋WPF，采用 MVVM；Core 保留纯状态和会话规则，Windows 层封装 Win32、采集、存储与 HTTP，App 负责窗口和视图绑定。先通过 Windows 交互关卡，再接入截图／语音、AI／存储与窗口整合，最终打包并完成真实捕获测试。操作通过同一命令入口执行，异步结果绑定会话及操作代次，防止过期写入。

**Tech Stack:** .NET 10 LTS、WPF、System.Text.Json、HttpClient、Microsoft.Data.Sqlite、System.Security.Cryptography.ProtectedData、NAudio、xUnit、Inno Setup 6。第三方依赖在第一次实际恢复时选择稳定版本并锁定，不使用浮动版本或预览版。

**Spec:** [已认可的设计文档](../specs/2026-10-16-examhelper-design.md)，含用户明确接受的第 14 节。计划日期：2026-10-16；状态：用户已认可计划并选择当前会话顺序执行，当前优先完成阶段 A。

## Global Constraints

- 首版采用 C#／.NET＋WPF；仅 Windows 11 x64、单屏、普通权限答题程序。
- 当前用户 EXE 安装，不要求管理员权限；发布自包含程序，不要求最终用户安装 SDK。
- 主页和设置常规交互；辅助模式普通悬停一秒，“返回主页”和“退出”悬停两秒。
- 移动、缩放、滚动：悬停一秒激活，停稳一秒固定，期间暂停其他悬停；滑块允许横向偏离。
- 单次录音最长 60 秒，只录麦克风，手动或到时停止后提交 ASR，不自动发送。
- 语音追加的撤销入口显示三秒，鼠标进入暂停消失倒计时，悬停一秒仅撤销最近一次增量。
- 默认 KIMI，允许自定义兼容接口；ASR 使用阿里云百炼北京地域 `qwen3-asr-flash`，中英混合自动识别。
- AI 图片测试和语音测试均通过才能进入；对应连接配置变化使该项测试失效；保护初始化失败禁止进入。
- 当前对话保留上下文；“开始考试”始终新建；“继续此对话”显式恢复；新对话禁用条件按设计执行。
- 返回主页清空草稿；退出保存草稿；隐藏不停止任务且快捷键仍有效；设置只从主页进入。
- 2026-10-17 用户调整：辅助模式文字只经语音识别输入，草稿只读且不可聚焦；删除取消输入、输入法切换及恢复，发送按钮与全局发送快捷键保留。
- 默认中文；选择／判断先答案后简短理由；简答直接作答；编程代码块，默认 C++ 且可设置。
- 只渲染纯文本与标注语言的代码块，不做其他 Markdown／公式／Mermaid 渲染。
- 历史完整保存截图，删除记录清理关联图片；密钥按 Windows 当前用户加密；录音不存入历史。
- 不自动重试外部请求，不静默裁剪历史／图片，不将模拟或接口返回成功等同于真实兼容性通过。

## Review Focus

1. 鼠标不动而缩略图／按钮位置改变：不能链式触发；任务 2 的 `LayoutChangeRequiresPointerReentry` 固定行为。
2. 清空／返回主页后识别或流式片段晚到：不能重建草稿、跨会话写入；任务 10、14 的过期结果测试覆盖。
3. 失败时已有新草稿、重试后又失败：保留两份内容且只出现一个用户消息；任务 14 的失败恢复测试覆盖。
4. 100%／150%／200% DPI、鼠标离开小窗、草稿预览鼠标操作：坐标正确，辅助窗口不抢答题界面键盘焦点；任务 3、4 实测覆盖。
5. 图片删除中断、磁盘满或强制结束：不得误删仍引用的图片或声称保存成功；任务 13 的故障注入覆盖。

## 执行约定与当前环境

当前仓库只有设计文档，无产品代码。规划阶段检查显示 `dotnet` 不在 PATH，常见 .NET 与 Inno Setup 安装目录也不存在；这只说明尚未发现工具，不代表已经准备好编译环境。本阶段不安装开发工具。

执行前按 using-git-worktrees 检查工作区及隔离要求，在适合的工作区操作；以下路径相对所选工作区根目录，原始根目录为 `D:\Code\ExamHelper`。采用单个顺序计划：这些模块共同构成一个桌面产品，任务间具有明确依赖，不分别建立独立产品规格。

每个产品逻辑任务先写行为测试并看到预期失败，再补最小实现、运行针对测试和必要集成检查、提交。首次缺少类型导致的编译失败可以作为红灯，后续红灯应尽量是行为断言失败。不可把下载失败、权限错误或测试未发现当作红灯。每任务提交前用 `git diff --check`；提交只包含该任务文件。下文“提交”使用 `git add <该任务 Files 中实际变更的路径>` 后运行给定 `git commit -m`，不使用无差别全仓库暂存。

纯 UI 的外观和安装步骤以实际运行检查为主，不为静态布局编写镜像测试。网络协议、并发、数据损失和交互状态必须有可重复自动测试。实测结果写到 `docs/verification/`，只有实测完成才填写通过。

## 工程与文件职责

| 路径 | 职责 |
| --- | --- |
| `ExamHelper.slnx`、`global.json`、`Directory.Build.props`、`Directory.Packages.props` | SDK、依赖、可重复编译；依赖锁文件随各项目提交 |
| `src/ExamHelper.Core/` | `net10.0`，按 Interaction、Drafts、Sessions、Settings、Audio、Ai 分目录；不依赖 WPF／Win32 |
| `src/ExamHelper.Windows/` | `net10.0-windows`，Native、Capture、Audio、Providers、Persistence、Settings 实现 |
| `src/ExamHelper.App/` | `net10.0-windows`＋UseWPF，App、Home、Settings、Overlay、Controls、Composition |
| `tests/ExamHelper.Core.Tests/` | 时间可控的状态、语音增量、命令及会话测试 |
| `tests/ExamHelper.Windows.Tests/` | 假 HTTP、临时 SQLite、原生边界和 STA 集成测试，不自动调用付费服务 |
| `tests/ExamHelper.TestSupport/` | 手动时钟、假服务、分块 HTTP、临时目录与 STA 运行辅助 |
| `tools/ExamHelper.InteractionLab/` | 单独的开发验证程序，验证窗口机制；不随安装器分发、不绕过产品进入条件 |
| `packaging/ExamHelper.iss`、`scripts/` | 发布和当前用户安装器构建 |
| `docs/verification/` | 交互关卡、服务实测、捕获矩阵、安装及发布检查记录 |

### 固定默认参数

辅助小窗初始 640×720 DIP、最小 480×360 DIP，右上角距工作区边缘 24 DIP；屏幕较小时将尺寸限制在工作区。背景不透明度默认 0.85，可调范围 0.35～1.00，文字不继承整体 Opacity。标题栏使用独立拖动区，右下缩放手柄 24×24 DIP。滚动条有效命中宽度 24 DIP。移动／停止判断以物理像素转换后的 DIP 为准，单次位移超过 3 DIP 重置一秒停稳计时，使用累计位移基准避免缓慢移动被误判停止。

交互活跃期间每 16ms 采样全局鼠标位置；窗口隐藏时不运行悬停计时，若已进入移动／缩放／拖动先结束交互。仅在有任务或拖动时运行必要的采样，空闲 UI 使用事件驱动。所有超时测试使用可注入 `TimeProvider`，不依赖真实睡眠。

默认全局快捷键：Ctrl＋Alt＋F1 截图直发、F2 截图入草稿、F3 录音切换、F4 发送、F5 不绑定、F6 清空输入、F7 新对话、F8 停止回答、F9 隐藏／显示、F10 折叠／展开、F11 返回主页、F12 退出；其中 F2～F12 的有效绑定同样带 Ctrl＋Alt。语音撤销 Ctrl＋Alt＋Z。Esc 仅在有移动／缩放／滚动操作时临时注册为结束交互；结束后立即注销。全部可在设置修改，系统冲突需提示并修正，不能静默占用或宣称绑定成功。

录音编码为 16kHz、单声道、16bit PCM WAV；无有效样本则不上传。AI 请求首包／空闲超时各 60 秒，单次总上限 10 分钟；ASR 请求上限 90 秒。超时均作为可手动重试的失败，停止操作立即取消本地等待，不能保证服务端已取消计费。无新增计费提醒弹窗。

### 共享契约：在所属任务中创建，后续引用不得另起同义类型

| 定义文件 | 固定接口或类型 |
| --- | --- |
| `Core/Interaction/InteractionTypes.cs` | `PointerSample(double X, double Y, long Timestamp)`；`InteractionMode { Idle, Dwell, Moving, Resizing, Scrolling }`；时间戳使用 TimeProvider 单调计时 |
| `Core/Drafts/DraftTypes.cs` | `ImageRef(Guid Id, string RelativePath, int Width, int Height)`；`DraftSnapshot(string Text, IReadOnlyList<ImageRef> Images, long Revision)`；文字仅由内部语音追加更新，不暴露键盘编辑接口 |
| `Core/Sessions/SessionTypes.cs` | `SessionSnapshot` 包含 Id、Generation、消息、Draft、PendingRequest、SpeechState、AnswerState；`RequestSnapshot` 包含 RequestId、SessionId、Generation、Text、Images、Prompt、Status；实现为不可变 record |
| `Core/Sessions/SessionTypes.cs` | `MessageSnapshot` 包含 Id、RequestId、Role、Text、Images、Status、CreatedAt；`MessageStatus { Pending, Complete, Failed, Stopped }`；`AnswerState { Idle, Generating }`；`SpeechState { Idle, Recording, Recognizing, RetryPending }` |
| `Core/Interaction/AppCommand.cs`、`Core/Sessions/CommandPolicy.cs` | AppCommand 在任务 2 创建，包含所有按钮和快捷键命令、隐藏／折叠及重试／丢弃；任务 6 创建 `bool CanExecute(AppCommand command, SessionSnapshot state)` |
| `Core/Audio/AudioContracts.cs` | `RecordedAudio(Guid OperationId, string TemporaryPath, TimeSpan Duration)`；`IMicrophoneRecorder.StartAsync(string deviceId, CancellationToken ct) -> Task`、`StopAsync(CancellationToken ct) -> Task<RecordedAudio>`、`CancelAsync() -> Task`；`IAsrClient.TranscribeAsync(RecordedAudio audio, AsrSettings settings, CancellationToken ct) -> Task<string>` |
| `Core/Ai/AiContracts.cs` | `AiChunk(string Text, bool IsFinal)`；`IAiClient.StreamAsync(AiRequest request, AiSettings settings, CancellationToken ct) -> IAsyncEnumerable<AiChunk>`；`AiRequest` 含有序 Messages 及请求标识 |
| `Core/Settings/SettingsTypes.cs` | `AppSettings` 含 AiSettings、AsrSettings、UiSettings、HotkeySettings、默认语言、直发提示词；AiSettings 含 BaseUri、Model、SecretId、ProviderKind；AsrSettings 另含 DeviceId；SecretId 引用加密存储 |
| `Core/Settings/ISecretStore.cs` | 任务 6 创建接口 `SetAsync(string id, string secret, CancellationToken ct) -> Task`、`GetAsync(string id, CancellationToken ct) -> Task<string?>`；任务 10／11 注入接口，任务 12 实现 UserSecretStore，测试使用内存假实现 |
| `Core/Sessions/ISessionStore.cs` | `Task SaveAsync(SessionSnapshot snapshot, CancellationToken ct)`；`Task<SessionSnapshot?> LoadAsync(Guid id, CancellationToken ct)`；`Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)`；`Task DeleteAsync(Guid id, CancellationToken ct)` |
| `Core/Common/ServiceFailure.cs` | `ServiceFailure : Exception` 带分类 Authentication、Permission、RateLimit、InvalidModel、InvalidPayload、Transport、Timeout、Storage 和非敏感错误码；本地取消独立处理，不显示为服务失败 |

UI／Windows 命名空间一律 `ExamHelper.<Project>.<Feature>`，表中 Core／Windows／App 前缀分别映射至上述项目根目录。接口中异步省略写出的 Task 类型以下文所属任务为准，所有有外部 I/O 的接口接收 CancellationToken。

## 阶段 A：Windows 交互与技术关卡

当前进度（2026-10-17）：用户明确报告真实手势、不抢焦点、Windows 截图／录屏和 Chrome 屏幕共享均通过，并决定阶段 A 验收通过。阶段 A 已关闭，进入阶段 B。OBS、腾讯会议、其他 DPI 和未细分后端保留未实测记录，最终任务 18 再核对；不得扩大用户报告的工具范围。任务 6 实现及针对测试已通过。

### Task 1：可构建的受保护窗口验证程序

**Files:** 创建根级工程配置、`.gitignore`、`README.md`；三个 src 项目和三个 tests 项目各自的 `.csproj`；`Windows/Native/WindowProtection.cs`、`NativeMethods.cs`；`tools/ExamHelper.InteractionLab/{ExamHelper.InteractionLab.csproj,App.xaml,MainWindow.xaml,MainWindow.xaml.cs}`；`tests/ExamHelper.Windows.Tests/Native/WindowProtectionTests.cs`；`docs/verification/windows-interaction.md`。

**Interfaces:** 产出 `WindowProtection.ApplyAndVerify(nint hwnd) -> ProtectionResult`，后者含 Success、ErrorCode、EffectiveAffinity；原生调用放在 `IWindowProtectionApi` 供假实现测试。正式 App 暂只创建普通主页壳，不出现绕过资格检查的辅助入口。

- [ ] 准备开发工具：从微软官方渠道安装或定位 .NET 10 x64 SDK；没有管理员权限时用用户目录或工作区 `.tools/dotnet`，显式使用完整路径。记录 `dotnet --info`；创建工程并将实际 SDK 完整版本写入 global.json，rollForward=latestPatch；依赖写中央版本文件、各项目生成 packages.lock.json。选择稳定 xUnit＋VS adapter＋Test SDK、NAudio、SQLite、ProtectedData 的具体版本，不安装 Visual Studio 作为必需条件。
- [ ] 编写 `RejectsNativeFailureAndAffinityMismatch`：假接口返回 false／读回非 `0x11`，均断言 `Assert.False(result.Success)`；成功时 `Assert.Equal(0x11u, result.EffectiveAffinity)`。
- [ ] 运行 `dotnet test tests/ExamHelper.Windows.Tests --filter WindowProtectionTests`，确认缺少保护实现或不满足读回检查而失败；不得因 SDK 不可用继续。
- [ ] 实现接口，检查 SetWindowDisplayAffinity／GetWindowDisplayAffinity，保留 Win32 错误码。实验窗口先创建 HWND 并施加保护再显示；采用 WindowStyle=None、AllowsTransparency=True、背景 alpha；文字保持不透明。若此路径与保护不兼容，记录复现后先调整窗口实现，不能降级为无保护继续。
- [ ] 再运行针对测试与 `dotnet build ExamHelper.slnx -c Debug`；运行 `dotnet run --project tools/ExamHelper.InteractionLab`，用系统截图检查窗口排除与桌面内容完整，记录真实结果。
- [ ] 提交：`git commit -m "feat: add protected Windows interaction lab"`。

### Task 2：悬停状态机与防连续触发

**Files:** 创建 `Core/Interaction/{InteractionTypes.cs,AppCommand.cs,DwellController.cs}`；`App/Controls/DwellBehavior.cs`；`tests/ExamHelper.TestSupport/ManualTimeProvider.cs`；`tests/ExamHelper.Core.Tests/Interaction/DwellControllerTests.cs`；修改实验窗口验证按钮。

**Interfaces:** `DwellController.Enter(string targetId, TimeSpan delay)`、`Leave()`、`Tick() -> string?`、`Suspend()`、`InvalidateLayout()`。一次返回目标后进入等待重新进入状态；使用注入 TimeProvider。

基准测试形态如下，ManualTimeProvider 的 `Advance(TimeSpan elapsed)` 由本任务实现；其他计时测试使用同一辅助类：

```csharp
[Fact]
public void TriggersOnceAt1000ms()
{
    var clock = new ManualTimeProvider();
    var dwell = new DwellController(clock);
    dwell.Enter("capture", TimeSpan.FromSeconds(1));
    clock.Advance(TimeSpan.FromMilliseconds(999));
    Assert.Null(dwell.Tick());
    clock.Advance(TimeSpan.FromMilliseconds(1));
    Assert.Equal("capture", dwell.Tick());
    Assert.Null(dwell.Tick());
}
```

- [ ] 编写 `TriggersOnceAt1000ms`、`ExitNeeds2000ms`、`LeaveCancels`、`LayoutChangeRequiresPointerReentry`：999ms／1999ms 断言 null；阈值返回目标；继续 Tick 不再触发；布局失效后鼠标不动也不触发新目标。
- [ ] 运行 `dotnet test tests/ExamHelper.Core.Tests --filter DwellControllerTests`，确认红灯。
- [ ] 实现状态机与显示进度的 WPF attached behavior；禁用、隐藏、窗口关闭、互斥交互启动时取消计时。应用先执行同一命令，不在 behavior 内复制业务判断。
- [ ] 重新运行针对测试；实验窗重复悬停、移开、删图模拟与恢复显示，验证不重复执行、无独立提示窗口泄漏。
- [ ] 提交：`git commit -m "feat: implement single-fire dwell interactions"`。

### Task 3：全局鼠标跟随、缩放及免点击滚动

**Files:** 创建 `Core/Interaction/{ManipulationController.cs,ViewportState.cs}`；`Windows/Native/PointerSampler.cs`；`App/Controls/{WindowManipulationBehavior.cs,HoverScrollBehavior.cs}`；`tests/ExamHelper.Core.Tests/Interaction/ManipulationTests.cs`；修改实验窗口及交互验证记录。

**Interfaces:** `ManipulationController.Begin(InteractionMode mode, PointerSample start)`、`Update(PointerSample sample) -> ManipulationUpdate`、`End()`；更新含位移和 IsFinished。`PointerSampler.Read() -> PointerSample` 使用 GetCursorPos；`ViewportState` 决定新内容是否跟随底部。

- [ ] 编写 `StopsAfter1000msWithin3Dip`、`SlowCumulativeMotionResetsStillness`、`ScrollIgnoresHorizontalDeviation`、`ReadingAboveBottomDoesNotJump`；明确断言 999ms 未固定、1000ms 固定，慢移累计超过 3 DIP 不固定。
- [ ] 运行 `dotnet test tests/ExamHelper.Core.Tests --filter "FullyQualifiedName~ManipulationTests"`，确认红灯。
- [ ] 实现基于初始窗口几何的绝对变换，物理像素／DIP 转换集中处理；缩放限最小尺寸与工作区；滚动由滑块行程比例映射到 ScrollViewer offset，内容不足一屏时不激活。共享交互锁阻止其他 dwell。
- [ ] 运行针对测试；在 100%／150%／200% DPI 验证离窗拖动、横向偏离、停稳、慢移、底部跟随；Esc 临时注册由任务 5 接入后补测，记录当前已测项。
- [ ] 提交：`git commit -m "feat: add hover window manipulation and reading scroll"`。

### Task 4：语音专用草稿与不激活窗口（2026-10-17 修订）

原英文编辑及焦点恢复路径已由用户明确取消，不再作为验收门槛。移除 HoverTextInputBehavior、FocusLease、InputLanguageService、InputStateRestorer 及其专用测试；保留修改历史以供追溯。

**Files:** 创建 `App/Controls/NonActivatingWindowBehavior.cs`；修改实验窗口及默认快捷键；创建 `tests/ExamHelper.Windows.Tests/Native/VoiceOnlyDraftTests.cs`；同步规格和验证报告。

**Interfaces:** `NonActivatingWindowBehavior(Window window)` 实现 IDisposable，显示窗口不激活，WM_MOUSEACTIVATE 返回 MA_NOACTIVATE。草稿通过不可聚焦的 TextBlock 展示，文字只通过语音结果追加。主页和设置不使用此行为。

- [x] 先写回归，验证只读草稿、无悬停聚焦、无取消按钮、F5 不绑定且 F9／F12 保持原快捷键；旧实现两项失败，确认红灯。
- [x] 移除旧路径并运行 VoiceOnlyDraftTests，验证模拟语音能追加，清空按钮能清除；实际麦克风和 ASR 在任务 9、10 实现。
- [x] 验证不激活窗口消息处理，重新执行受控捕获和快捷键集成检查。
- [x] 更新自包含验证 EXE；真实悬停、移动／缩放／滚动仍保持原验证要求，不因取消编辑而自动标为完成。

### Task 5：快捷键注册、冲突与交互关卡

**Files:** 创建 `Core/Settings/HotkeyBinding.cs`、`Windows/Native/HotkeyRegistry.cs`、`tests/ExamHelper.Windows.Tests/Native/HotkeyRegistryTests.cs`；修改实验窗、`windows-interaction.md`。

**Interfaces:** `HotkeyRegistry.Register(IReadOnlyList<HotkeyBinding> bindings) -> HotkeyRegistrationReport`、`UnregisterAll()`、`SetManipulationEscape(bool active)`；事件只产出命令标识，由后续统一命令入口处理。

- [ ] 编写 `ReportsDuplicatesAndNativeConflict`、`UnregistersOnLeave`、`EscapeOnlyDuringManipulation`，断言未成功注册的绑定不显示可用，离开后注册集合为空。
- [ ] 运行 `dotnet test tests/ExamHelper.Windows.Tests --filter HotkeyRegistryTests`，确认红灯。
- [ ] 用 RegisterHotKey／WM_HOTKEY、MOD_NOREPEAT 实现；预检测内部重复，记录系统返回失败，注册部分失败则回滚并展示具体冲突键。辅助模式隐藏时维持注册，退出模式注销。
- [ ] 运行针对测试；实验窗验证 Esc 结束拖动、隐藏后快捷键可用、抢占冲突可见、退出后别的应用可用这些键。
- [ ] 完成阶段 A 关卡：窗口透明与文字清晰、截图排除、免点击移动／缩放／滚动、不抢焦点、草稿只读且不可聚焦均有真实结果。任何关键项失败先诊断，不进入业务集成；这是技术验证关卡，不额外等待形式化用户批准。
- [ ] 提交：`git commit -m "feat: register scoped hotkeys and verify native interaction gate"`。

## 阶段 B：草稿、截图与语音

### Task 6：草稿、请求模型与统一命令规则

**Files:** 创建 `Core/Drafts/{DraftTypes.cs,DraftBuffer.cs}`、`Core/Sessions/{SessionTypes.cs,CommandPolicy.cs}`、`Core/Audio/AudioContracts.cs`、`Core/Ai/AiContracts.cs`、`Core/Settings/{SettingsTypes.cs,ISecretStore.cs}`、`Core/Common/ServiceFailure.cs`；创建 `tests/ExamHelper.Core.Tests/Drafts/DraftBufferTests.cs`、`Sessions/CommandPolicyTests.cs`。

**Interfaces:** 按共享契约建立不可变快照；`DraftBuffer.Snapshot()`、`AppendText(string transcript)`、`AppendImage(ImageRef image)`、`RemoveImage(Guid id)`、`Clear()`；每次修改递增 Revision。`CommandPolicy.CanExecute` 不读取 UI 控件。

- [x] 编写 `PreservesImageOrder`、`ClearRemovesTextAndImagesOnly`；命令组合测试断言草稿非空禁用直发、AI 回答时可加图不可发送、有录音／识别／待重试录音时不可新对话或发送、隐藏不改变任务资格。
- [x] 运行 `dotnet test tests/ExamHelper.Core.Tests --filter "FullyQualifiedName~DraftBufferTests|FullyQualifiedName~CommandPolicyTests"`，确认红灯。
- [x] 实现模型和统一判断。HasDraft 对文字使用 `Length > 0`，包含空白也算草稿；无字无图的发送禁用。存在待重试 AI 请求时禁用新发送、直发、新对话，但保留追加语音／截图和明确的重试／丢弃入口。
- [x] 再运行针对测试，断言禁用原因可供 UI 展示；暂不实现外部请求或持久化。
- [x] 提交：`git commit -m "feat: model drafts and command availability"`。

### Task 7：三秒语音增量撤销

**Files:** 创建 `Core/Drafts/SpeechInsertionUndo.cs`、`tests/ExamHelper.Core.Tests/Drafts/SpeechInsertionUndoTests.cs`；修改 `DraftBuffer.cs` 加入操作标识、范围追踪和 edit 通知。

**Interfaces:** `SpeechInsertionUndo.Append(Guid operationId, string transcript)`、`PointerEnter()`、`PointerLeave()`、`TryUndo() -> bool`、`Invalidate()`；使用同一 DraftBuffer 和 TimeProvider，暴露剩余时间与 CanUndo。

- [x] 编写 `UndoOnlyLatestInsertionKeepsEarlierTextAndImages`、`ExpiresAt3000ms`、`HoverPausesExpiry`、`NewRecognitionReplacesUndoTarget`、`ClearInvalidatesUndo`；例如“原文”＋识别“结果”撤销后断言等于“原文”，图片 Id 不变。
- [x] 运行 `dotnet test tests/ExamHelper.Core.Tests --filter SpeechInsertionUndoTests`，确认红灯。
- [x] 实现语音增量追踪；追加分隔符只在原文末尾无换行且有内容时使用 `Environment.NewLine`，该分隔符随本次一起撤销。只允许撤销最近一次识别增量；新录音／发送／清空等生命周期使入口失效。
- [x] 重新运行测试，补测连续识别、清空后的过期撤销及中文 UTF-16 代理对边界，不能拆坏字符或把新文字一起回滚。
- [x] 提交：`git commit -m "feat: undo only the latest speech insertion"`。

### Task 8：单屏全屏截图与图片仓库

**Files:** 创建 `Core/Drafts/IImageStore.cs`、`Core/Drafts/IScreenCapture.cs`；`Windows/Capture/{FullScreenCapture.cs,FileImageStore.cs}`；`tests/ExamHelper.Windows.Tests/Capture/{FullScreenCaptureTests.cs,FileImageStoreTests.cs}`；修改实验窗提供截图保存预览。

**Interfaces:** `IScreenCapture.CaptureAsync(CancellationToken ct) -> Task<CapturedImage>`；CapturedImage 含 PNG bytes、Width、Height；`IImageStore.AddAsync(CapturedImage image, CancellationToken ct) -> Task<ImageRef>`、`ReadAsync(ImageRef image, CancellationToken ct) -> Task<byte[]>`、`DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds, CancellationToken ct)`。

- [x] 编写 `StoresOriginalBytesAndOrderIndependentIds`、`RejectsPathEscape`，断言读回原图完全一致、路径不能越过图片根目录；捕获假后端断言只接受一个有效显示器和完整物理像素尺寸。
- [x] 运行 `dotnet test tests/ExamHelper.Windows.Tests --filter "FullyQualifiedName~Capture"`，确认红灯。
- [x] 实现单屏检测与 PNG 编码：原生 GDI 屏幕 DC／BitBlt 路径先在阶段 A 已验证的保护窗口上实测，资源全部 Dispose；若出现黑块或丢底图，改用经实测可排除辅助窗的 WGC 路径并记录，不先隐藏保护再声称通过。不支持的多屏状态明确拒绝截图，不合并显示器。
- [x] 运行测试；在不同 DPI 与小窗可见、只读草稿、折叠状态截图，核对尺寸、底层题目完整及无小窗。检查缩略图只是预览，原图未被缩小覆盖。当前受控 125% DPI 后端实测通过；其他 DPI／折叠和实际全屏预览人工检查统一保留到任务 18，不宣称已测。
- [x] 提交：`git commit -m "feat: capture one full screen and preserve source images"`。

### Task 9：麦克风采集与 60 秒自动停录

**Files:** 创建 `Windows/Audio/{MicrophoneRecorder.cs,AudioEncoder.cs}`；`Core/Audio/RecordingController.cs`；`tests/ExamHelper.Core.Tests/Audio/RecordingControllerTests.cs`、`tests/ExamHelper.Windows.Tests/Audio/AudioEncoderTests.cs`。

**Interfaces:** 实现 IMicrophoneRecorder；`RecordingController.StartAsync(string deviceId, CancellationToken ct)`、`StopAsync(CancellationToken ct) -> Task<RecordedAudio>`、`CancelAsync()`；Stop 的自动／手动竞争共享一次完成结果，提供电平与时长状态。

- [x] 编写 `StopsOnceAt60Seconds`、`ManualStopRacingTimeoutProducesOneClip`、`CancelDeletesAudio`；59.999 秒仍录音、60 秒触发一次停止。编码测试断言 WAV 头为 16000Hz／mono／16bit。
- [x] 运行 `dotnet test tests/ExamHelper.Core.Tests --filter RecordingControllerTests` 与 Windows 的 AudioEncoderTests，确认红灯。
- [x] 以 NAudio WASAPI 麦克风采集、重采样编码；临时文件写在用户数据下的 TempAudio，任何错误释放设备。空音频不提交；断开设备或权限拒绝显示错误；重试不重新录制。
- [x] 运行针对测试；实测默认麦克风、选择设备、拔除、无权限、手停和 60 秒截止；隐藏时继续录音。本机默认麦克风两秒手停测试已通过，报告列出可用的 Steam Streaming Microphone 与 Realtek 外部麦克风；具体默认设备名称未采集，不猜测。测试录音已删除、无上传。真实 60 秒、选择设备／拔除／权限与隐藏场景保留到设置和最终验收，不把假时钟测试代替这些实测。
- [x] 提交：`git commit -m "feat: record microphone audio with a 60 second limit"`。

### Task 10：阿里云识别、手动重试与晚到结果隔离

**Files:** 创建 `Windows/Providers/AliyunAsrClient.cs`、`Core/Audio/SpeechCoordinator.cs`；`tests/ExamHelper.TestSupport/{FakeHttpHandler.cs,DeferredAsrClient.cs}`；`tests/ExamHelper.Windows.Tests/Providers/AliyunAsrClientTests.cs`、`tests/ExamHelper.Core.Tests/Audio/SpeechCoordinatorTests.cs`。

**Interfaces:** 实现 IAsrClient；`SpeechCoordinator.ToggleAsync(CancellationToken ct)`、`RetryAsync(CancellationToken ct)`、`DiscardAsync()`、`CancelAsync()`；配置从只读快照传入，识别结果用 `(SessionId, Generation, OperationId, Transcript)` 事件交付草稿。

- [x] 编写协议测试：Base64 音频、模型 qwen3-asr-flash、北京端点、不传 language；401／429／超时转换到 ServiceFailure。协调器测试 `LateTranscriptAfterClearIsIgnored`、`FailedClipRetryKeepsSameAudio`、`SuccessDeletesTemporaryAudio`。
- [x] 运行两个项目的相关 Tests 过滤，确认红灯；假 HTTP 不访问真实云端。
- [x] 实现 Chat Completions 音频请求，ASR 用非流式返回，配置允许北京业务空间域名并保留官方仍支持的北京公共域名作为预填。失败保留录音置 RetryPending；重试／丢弃明确可见；取消增加代次，过期返回不能注入文字。
- [x] 运行测试；真实测试安排任务 15 的设置页，用中英混合句验证，音频与结果不写历史。录音识别成功通过任务 7 追加并建立撤销范围。
- [x] 提交：`git commit -m "feat: transcribe microphone clips with Aliyun Beijing ASR"`。

## 阶段 C：图文 AI、配置、保存与会话

### Task 11：KIMI／兼容接口、提示词和流式协议

**Files:** 创建 `Windows/Providers/{ChatCompletionsClient.cs,SseReader.cs,ProviderErrorMapper.cs}`、`Core/Ai/{PromptBuilder.cs,ContextBuilder.cs}`；`tests/ExamHelper.Windows.Tests/Providers/ChatCompletionsClientTests.cs`、`tests/ExamHelper.Core.Tests/Ai/PromptBuilderTests.cs`；扩展 FakeHttpHandler 支持任意字节分块。

**Interfaces:** 实现 IAiClient；`PromptBuilder.BuildSystem(AppSettings settings) -> string`；`ContextBuilder.Build(SessionSnapshot session, RequestSnapshot request, AppSettings settings) -> AiRequest`；图片通过 IImageStore 读取，保持顺序。

- [x] 编写 `DirectCaptureUsesPresetOrdinarySendDoesNot`、`DefaultLanguageIsCpp`、`RetryDoesNotDuplicateUserMessage`；协议测试 `ParsesSplitUtf8AndSseEvents`、`EofWithoutFinishIsIncomplete`、`CancelsWhileWaiting`、`ProviderErrorsNeverAutoRetry`；断言先后图片 Id 对应输入顺序。
- [x] 运行 Core 的 PromptBuilderTests 和 Windows 的 ChatCompletionsClientTests，确认红灯。
- [x] 实现 `/chat/completions`，只拼接一次路径，支持 BaseUri 子路径；HTTP streaming 用 ResponseHeadersRead，自行按 SSE 空行组帧，保留跨包 UTF-8。正文只显示 content，不将 reasoning_content 当作答案；finish_reason=length 标记未完成，异常 EOF 不标成功，usage-only 块不丢先前内容。
- [x] 使用默认大陆地址 `https://api.moonshot.cn/v1`，默认视觉模型 `kimi-k2.6`；KIMI 专属预设仅针对已验证支持的型号发送 `"thinking":{"type":"disabled"}`，通用兼容模式不发送专属参数。不强加 temperature。开始实现时再次核对官方模型可用性，实际账户仍须测试通过；不静默切换供应商或模型。
- [x] 运行测试；基础提示词写入中文、题型规则和可配置语言，精确沿用规格中的直发提示词；保存原始输出。超出服务容量显示错误，不自动删除历史或图片；部分回答重试用同一 RequestId，新的尝试替换该请求的回答视图，旧尝试不重复加入上下文。
- [x] 提交：`git commit -m "feat: stream multimodal KIMI and compatible API responses"`。

### Task 12：设置持久化、加密密钥与验证资格

**Files:** 创建 `Core/Settings/{ISettingsStore.cs,ServiceVerification.cs}`；`Windows/Settings/{JsonSettingsStore.cs,UserSecretStore.cs,ServiceTestRunner.cs}`；`App/Assets/service-test.png`；`tests/ExamHelper.Windows.Tests/Settings/SettingsStoreTests.cs`、`tests/ExamHelper.Core.Tests/Settings/ServiceVerificationTests.cs`。

**Interfaces:** `ISettingsStore.LoadAsync(ct) -> Task<AppSettings>`、`SaveAsync(AppSettings settings, ct)`；`UserSecretStore.SetAsync(string id, string secret, ct)`、`GetAsync(string id, ct) -> Task<string?>`；`ServiceVerification.CanStart(AppSettings settings) -> bool`；`ServiceTestRunner.TestTextAsync`／`TestImageAsync`／`TestSpeechAsync` 各接收当前配置和 ct，返回 ServiceTestResult。

- [x] 编写 `AiEditInvalidatesOnlyAi`、`AsrDeviceEditInvalidatesOnlyAsr`、`AppearanceAndPromptDoNotInvalidate`；存储测试断言配置文件和诊断日志均无明文密钥，损坏配置不覆盖原文件而返回可恢复错误。
- [x] 运行针对测试确认红灯。
- [x] 用 CurrentUser DPAPI 存密钥、JSON 存设置及指纹；指纹包含端点、模型、ProviderKind、密钥内容的本地 HMAC 摘要，语音另含 DeviceId。规范化端点，保存前验证 URI。密钥更改不以 SecretId 不变误判未变。
- [x] 创建内置测试图“EH7Q9”，图片测试必须返回标记才记通过；ASR 用用户实际录音，展示结果后由用户点击“识别正确”记通过，空结果不允许通过；文字测试单独展示，不额外改变进入门槛。标记结果时核对配置指纹，测试期间修改配置不能给新配置发通过证。
- [x] 运行测试和实际 CurrentUser 加解密检查；初始设置含默认语言、提示词、透明度及快捷键。修改快捷键不使服务证据失效，密钥解密失败需要重新填写而不清空历史。
- [x] 提交：`git commit -m "feat: persist encrypted settings and service verification state"`。

### Task 13：历史、图片引用、退出草稿与故障恢复

**Files:** 创建 `Core/Sessions/ISessionStore.cs`；`Windows/Persistence/{SqliteSessionStore.cs,DatabaseSchema.cs,ImageReferenceCleaner.cs,AppDataPaths.cs,RedactedLogger.cs}`；`tests/ExamHelper.Windows.Tests/Persistence/{SqliteSessionStoreTests.cs,ImageReferenceCleanerTests.cs}`；修改图片存储接入引用清理。

**Interfaces:** 实现 ISessionStore；SessionSummary 含 Id、Title、UpdatedAt、HasSavedDraft；`ImageReferenceCleaner.ReconcileAsync(CancellationToken ct)` 查所有消息、草稿和 pending 引用后清理孤图；`AppDataPaths.Root` 固定为 `%LOCALAPPDATA%\ExamHelper`。

- [x] 编写 `RoundTripsTextImagesDraftAndPartialAnswer`、`DeleteConversationKeepsOtherReferences`、`InterruptedImageDeleteIsRecoverable`、`DiskFullDoesNotReportSaved`；启动恢复测试断言崩溃前 Generating 改成 Failed，TempAudio 被清除但历史图片保留。
- [x] 运行 `dotnet test tests/ExamHelper.Windows.Tests --filter "FullyQualifiedName~Persistence"`，确认红灯。
- [x] 建立 schema v1：Sessions、Messages、Requests、Images、ImageLinks、Drafts、SchemaVersion；外键／事务启用，JSON 保存不可变参数快照。原图先临时写再原子改名，DB 成功引用后才可清理。删除先事务移除引用，再清理文件；失败记录下次重试，不删除仍有引用者。
- [x] 保存失败保留内存状态并显示错误，返回／退出不显示保存成功；流式回答每 500ms 或结束时持久化一次，语音追加及截图变更后自动保存。启动恢复中断请求、清理失效临时录音、对账孤图；删除操作只允许用户数据根目录下已确认文件，不递归删除任意计算路径。
- [x] 运行测试，已验证数据库只读、引用清理失败后重试和保存中断状态恢复；真实磁盘写满、进程在事务中间强制结束留待任务 18，不标为已实测。历史标题从首条文字截取，纯截图对话用时间标题；不额外调用 AI 生成标题。
- [x] 提交：`git commit -m "feat: save conversations drafts and image references reliably"`。

### Task 14：会话编排、失败重试与生命周期

**Files:** 创建 `Core/Sessions/{SessionCoordinator.cs,RequestRecovery.cs}`；`tests/ExamHelper.TestSupport/{InMemorySessionStore.cs,DeferredAiClient.cs}`；`tests/ExamHelper.Core.Tests/Sessions/{SessionCoordinatorTests.cs,RequestRecoveryTests.cs}`。

**Interfaces:** `SessionCoordinator.StartNewAsync(ct)`、`ContinueAsync(Guid id, ct)`、`ExecuteAsync(AppCommand command, ct)`、`ReturnHomeAsync(ct)`、`ExitAsync(ct)`；事件 StateChanged 产出 SessionSnapshot，不从后台线程直接操作 WPF。构造注入 AI、语音、截图、图片仓库、设置、存储与 TimeProvider。

- [ ] 编写 `FailureRestoresEmptyDraft`、`FailureKeepsNewDraftAndPendingRequest`、`RetryTwiceKeepsOneUserMessage`、`LateChunkAfterNewSessionIsIgnored`、`ReturnClearsButExitRestoresDraft`、`ClearCancelsSpeechAndDiscardsLateResult`，断言请求快照不可变、恢复图像顺序不变、上下文无重复请求。
- [ ] 运行 Core 的 SessionCoordinatorTests／RequestRecoveryTests，确认红灯。
- [ ] 实现操作串行入口与会话代次，网络流不占住整个命令锁；发送前原子冻结快照、写入单个用户消息、腾空草稿，然后异步接收回答。每个命令进入时重新检查 CanExecute，防止按钮和快捷键同时发送。
- [ ] 失败空草稿恢复时关联同一 RequestId，提供重试／丢弃；已恢复草稿若被追加语音／截图或清空，先解决失败请求后才能作为新请求发送，不能把原快照与修改后的草稿混作同一次重试。非空草稿使用独立 pending 卡片。停止回答保留部分答案且允许继续追问；中途失败必须先重试或丢弃。丢弃后历史仍显示失败状态，上下文排除未完成的失败尝试。
- [ ] 返回主页先废弃代次并取消任务，再清理草稿和录音、保存历史；退出保存含 pending 的快照；隐藏不取消任务。所有线程回调先核对 session／generation／operation。用延迟假服务跑上述测试，再运行全部 Core 测试。
- [ ] 提交：`git commit -m "feat: orchestrate sessions and preserve recoverable failures"`。

## 阶段 D：产品界面、打包与验收

### Task 15：主页、设置与真实服务测试入口

**Files:** 修改任务 1 创建的 `App/{App.xaml,App.xaml.cs}`；创建 `App/Composition/AppComposition.cs`、`App/Home/{HomeWindow.xaml,HomeWindow.xaml.cs,HomeViewModel.cs}`、`App/Settings/{SettingsWindow.xaml,SettingsWindow.xaml.cs,SettingsViewModel.cs}`、`App/Overlay/IOverlayHost.cs`；`tests/ExamHelper.Windows.Tests/App/EntryEligibilityTests.cs`；`docs/verification/service-tests.md`。App 项目引用 Core 与 Windows；启动设置 ShutdownMode=OnExplicitShutdown。

**Interfaces:** `HomeViewModel.StartAsync(ct)`／`ContinueAsync(Guid id, ct)` 对接 SessionCoordinator；`SettingsViewModel.RunTextTestAsync`／`RunImageTestAsync`／`RunSpeechTestAsync` 与 SaveAsync 对接任务 12。窗口创建经 `IOverlayHost.OpenAsync(ct) -> Task<ProtectionResult>`，具体由任务 16 实现，测试先用假 host。

- [ ] 编写 `UnverifiedCannotStartOrContinue`、`ConfigChangedDuringTestDoesNotPassNewConfig`、`DeleteRequiresConfirmation`、`ProtectionFailureKeepsHomeVisible`，断言没有成功创建空历史会话；普通主页查看历史不要求服务通过。
- [ ] 运行 `dotnet test tests/ExamHelper.Windows.Tests --filter EntryEligibilityTests`，确认红灯。
- [ ] 实现常规可点击主页、历史只读查看、删除确认、继续入口、开始入口；设置提供所有字段、麦克风选择、电平、真实测试结果、默认代码语言与可修改快捷键。仅在保护通过并保存会话后隐藏主页，失败关闭临时 host。开始始终新建，继续载入既有会话。
- [ ] 运行针对测试与 Debug 构建，设置中填入用户凭据后真实测 KIMI 文本／图片和阿里云混合语音，记录区域、模型、耗时、实际识别与结果，不记录密钥。无有效凭据则保持未验证，测试入口可交付但不宣布服务联调已通过。
- [ ] 提交：`git commit -m "feat: add home settings and explicit service verification"`。

### Task 16：辅助小窗与流式纯文本／代码块呈现

**Files:** 创建 `App/Overlay/{OverlayWindow.xaml,OverlayWindow.xaml.cs,OverlayViewModel.cs,OverlayHost.cs}`、`App/Controls/{AnswerView.xaml,AnswerView.xaml.cs,ThumbnailStrip.xaml,ThumbnailStrip.xaml.cs}`、`Core/Ai/CodeBlockParser.cs`；`tests/ExamHelper.Core.Tests/Ai/CodeBlockParserTests.cs`；`tests/ExamHelper.Windows.Tests/App/OverlayLifecycleTests.cs`。

**Interfaces:** `OverlayHost` 实现 IOverlayHost；`CodeBlockParser.Parse(string accumulatedText, bool isFinal) -> IReadOnlyList<AnswerSegment>`，AnswerSegment 包含 Kind、Text、Language；OverlayViewModel 只通过 ExecuteAsync 发命令，不直接 HTTP／写 DB。

- [ ] 编写 `IncompleteFenceDoesNotLoseText`、`PlainMarkdownStaysLiteral`、`HideKeepsRequestsAndHotkeys`、`ReturnClearsDraftAndUnregisters`、`ExitSavesDraft`、`RecreatedHandleMustReapplyProtection`；验证同一 hover 结束不能因内容重排再触发。
- [ ] 运行相关 Core／Windows 测试确认红灯，WPF 测试用 STA 调度器辅助，不在普通测试线程直接 new Window。
- [ ] 组合此前交互：顶部截图直发／截图入草稿／录音／新对话／停止／折叠／主页／退出；无设置入口。下方回答、缩略图、只读语音草稿、清空输入／发送；撤销语音及各失败重试按钮按状态出现。信息多时布局换行但不覆盖命中区域。
- [ ] 流式回答以原始文本为真源解析 fence，允许未闭合代码块，等宽代码标注语言；代码过长优先换行，避免只有无法操作的横向滚动。回答列表增量更新、节流刷新，不每个 token 重建整个窗口。所有 tooltip／错误提示在同一保护 HWND 内，不弹普通 Toast 承载问题或答案。
- [ ] 实现捕获保护先于展示、关闭不是隐藏、隐藏／折叠时结束操作锁，不操作输入法或恢复焦点；截图／录音／删除不抢焦点。全局注册失败保持主页展示并列出冲突供修改。运行测试并按规格全流程手测：图文发送、三秒撤销、链式误删防护、长回答往返阅读、隐藏后台任务、返回与退出恢复。
- [ ] 提交：`git commit -m "feat: assemble the protected hover assistant window"`。

### Task 17：自包含发布与当前用户 EXE 安装包

**Files:** 创建 `scripts/{Test.ps1,Publish.ps1,Build-Installer.ps1}`、`packaging/ExamHelper.iss`、`docs/verification/installation.md`、`THIRD-PARTY-NOTICES.md`；修改 App 的 manifest、README、.gitignore。

**Interfaces:** `Test.ps1 -Configuration Release`；`Publish.ps1 -Version 0.1.0` 输出 `artifacts/publish/win-x64/`；`Build-Installer.ps1 -Version 0.1.0 -IsccPath <path>` 输出 `artifacts/installers/ExamHelper-Setup-0.1.0-x64.exe`，脚本失败返回非零，不创建伪安装器。

- [ ] 编写脚本并验证工具缺失时返回明确非零与安装说明；每次构建记录 SDK、锁定包、版本及 git SHA。准备 Inno Setup 6 官方编译器，开发机安装器工具路径可显式指定，不强制用户管理员安装。
- [ ] 发布 `dotnet publish src/ExamHelper.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/win-x64`，先运行发布目录 EXE 验证 SQLite 原生库及资源齐全；不能只复制入口 exe。
- [ ] 设置 `PrivilegesRequired=lowest`、asInvoker、安装到 `{localappdata}\Programs\ExamHelper`、开始菜单快捷方式；`ArchitecturesAllowed=x64os`，版本与产品类型检查只接受 Windows 11 工作站（build ≥22000），拒绝 Windows 10／ARM／Windows Server；App 启动同样检查。不能只依赖 `MinVersion=10.0`，Windows 11 同样使用 10.0 内核版本。
- [ ] 用 `Build-Installer.ps1` 构建，生成 SHA-256 校验文件；安装包排除实验工具、临时录音、开发配置与凭据。整理第三方许可，未复制的 OpenCluely 代码不加入依赖。
- [ ] 在未装 .NET 的 Windows 11 x64 标准用户环境实测安装／启动／升级／卸载、无 UAC 提权、历史与设置持久化。卸载默认保留用户数据，并在说明中提供手动清理路径，不默认删除历史。
- [ ] 提交：`git commit -m "build: package a self-contained per-user Windows installer"`。

### Task 18：兼容性证据、最终回归与交付

**Files:** 创建 `docs/verification/{capture-matrix.md,release-checklist.md}`；补齐 `windows-interaction.md`、`service-tests.md`、`installation.md` 和 README。

**Interfaces:** 无新产品 API；交付输出为安装器、SHA-256、说明与真实验证记录，发现缺陷回到所属任务修复并重跑受影响检查。

- [ ] 运行 `dotnet restore ExamHelper.slnx --locked-mode`、`dotnet build ExamHelper.slnx -c Release --no-restore`、`dotnet test ExamHelper.slnx -c Release --no-build`。预期所有适用测试通过且发现测试数大于零；记录实际结果，不把 Skip 记为通过。
- [ ] 执行规格第 13 节全部验收场景；追加断网／401／429、录音设备断开、写盘失败、强制结束恢复、答题窗口正常输入期间操作辅助小窗、连续快捷键与悬停竞争。确认失败请求不覆盖新草稿、无额外自动请求、退出无音频残留。
- [ ] 在系统截图工具、截图工具录屏、Xbox Game Bar、Chrome、OBS、腾讯会议逐项实测。Chrome 覆盖整屏／窗口／标签页的适用来源；OBS 记录捕获后端；腾讯会议检查接收端。用无敏感内容的验证窗进行测试，记录工具版本、Windows 构建、模式、结果及证据路径。
- [ ] 若某工具不可用或某模式不适用，准确写“未验证”或“不适用”及原因；用户要求的适用路径未验证或失败时，明确作为交付限制，不能把完成安装包写成全部验收通过。修复必要缺陷后重跑相应路径。
- [ ] 按 executing-plans／所选执行方式完成最终代码审查、verification-before-completion 和分支收尾流程；无用户授权不自行合并、推送或发布安装器到外部平台。提供本地可检查结果。
- [ ] 提交：`git commit -m "docs: record release verification and compatibility evidence"`。

## 需求覆盖索引

### 各任务测试命令

下列命令均在工作区根目录执行。先写测试时预期为相关行为失败，完成实现后预期全部通过、失败数为零且发现测试数大于零。阶段 A 的实测和阶段 D 的发布检查按任务正文额外执行，不能由以下单元测试替代。

| 任务 | 命令 |
| --- | --- |
| 1 | `dotnet test tests/ExamHelper.Windows.Tests --filter WindowProtectionTests` |
| 2 | `dotnet test tests/ExamHelper.Core.Tests --filter DwellControllerTests` |
| 3 | `dotnet test tests/ExamHelper.Core.Tests --filter ManipulationTests` |
| 4 | `dotnet test tests/ExamHelper.Windows.Tests --filter VoiceOnlyDraftTests` |
| 5 | `dotnet test tests/ExamHelper.Windows.Tests --filter HotkeyRegistryTests` |
| 6 | `dotnet test tests/ExamHelper.Core.Tests --filter "FullyQualifiedName~DraftBufferTests\|FullyQualifiedName~CommandPolicyTests"` |
| 7 | `dotnet test tests/ExamHelper.Core.Tests --filter SpeechInsertionUndoTests` |
| 8 | `dotnet test tests/ExamHelper.Windows.Tests --filter "FullyQualifiedName~FullScreenCaptureTests\|FullyQualifiedName~FileImageStoreTests"` |
| 9 | `dotnet test tests/ExamHelper.Core.Tests --filter RecordingControllerTests`；`dotnet test tests/ExamHelper.Windows.Tests --filter AudioEncoderTests` |
| 10 | `dotnet test tests/ExamHelper.Core.Tests --filter SpeechCoordinatorTests`；`dotnet test tests/ExamHelper.Windows.Tests --filter AliyunAsrClientTests` |
| 11 | `dotnet test tests/ExamHelper.Core.Tests --filter PromptBuilderTests`；`dotnet test tests/ExamHelper.Windows.Tests --filter ChatCompletionsClientTests` |
| 12 | `dotnet test tests/ExamHelper.Core.Tests --filter ServiceVerificationTests`；`dotnet test tests/ExamHelper.Windows.Tests --filter SettingsStoreTests` |
| 13 | `dotnet test tests/ExamHelper.Windows.Tests --filter "FullyQualifiedName~Persistence"` |
| 14 | `dotnet test tests/ExamHelper.Core.Tests` |
| 15 | `dotnet test tests/ExamHelper.Windows.Tests --filter EntryEligibilityTests` |
| 16 | `dotnet test tests/ExamHelper.Core.Tests --filter CodeBlockParserTests`；`dotnet test tests/ExamHelper.Windows.Tests --filter OverlayLifecycleTests` |
| 17 | `pwsh -File scripts/Test.ps1 -Configuration Release`，随后执行任务正文两个构建脚本 |
| 18 | 任务正文的 locked-mode restore、Release build、Release test 及完整人工矩阵 |

表格里的 `\|` 是 Markdown 表格转义，实际传给 dotnet 的过滤表达式使用普通 `|`，没有反斜杠。

### 规格章节映射

| 规格章节 | 实施任务 |
| --- | --- |
| 1 平台、技术与范围 | 1、11、16、17 |
| 2 窗口、进入条件、生命周期 | 1、5、12、14～16 |
| 3 悬停、移动、缩放、滚动 | 2、3、5、16 |
| 4 语音专用草稿与不激活窗口 | 4、5、16 |
| 5 截图、图片、发送 | 6、8、11、14、16 |
| 6 录音、识别、撤销 | 7、9、10、16 |
| 7 AI、上下文、题型、流式 | 11、14、16 |
| 8 命令禁用矩阵 | 6、14、16 |
| 9 设置、测试、快捷键 | 5、12、15 |
| 10 捕获与工具验证 | 1、8、16、18 |
| 11 保存、清理、错误恢复 | 10、12～14、17 |
| 12 架构边界 | 共享契约、全部任务 |
| 13 阶段与验收 | 阶段 A 关卡、17、18 |
| 14.1～14.7 补充规则 | 3、6、7、10～18 |
| 15 参考资料 | 任务实现时核对官方资料及许可 |

## 自审与执行交接

本计划自审需确认：共享契约在所属任务有定义；依赖任务先于使用；所有状态限制进入同一命令入口；语音与请求失败恢复不覆盖新增草稿；测试／保护接口成功没有冒充真实兼容通过；默认参数和固定文案与规格一致。未完成条目保留勾选框，实际实施中更新，不提前标为完成。

建议在当前会话由主代理按顺序执行，原因是 Windows 交互和会话状态接口相互依赖，连续调试更容易保留实测上下文。也可选择逐任务子代理实现与独立审查；仅在用户选定这种方式后使用相应流程。计划经用户审核并选定执行方式后才开始任务 1。

### 规划时核对的官方资料

- [.NET 支持版本](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)：选用 .NET 10 LTS，避免新项目采用临近支持结束的版本。
- [窗口保护 API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)、[WPF 透明窗口](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0)：窗口机制需实测联合效果。
- [KIMI 大陆 API 快速开始](https://platform.kimi.com/docs/get-api-key)、[Kimi K2.6](https://platform.kimi.com/docs/guide/kimi-k2-6-quickstart)：大陆 Chat Completions 地址与视觉模型预设；执行时再次核对。
- [阿里云 ASR API](https://help.aliyun.com/zh/model-studio/qwen-asr-api-reference)：北京域名、音频 Base64 和混合语种参数。
- [NAudio](https://github.com/naudio/NAudio)：麦克风采集与音频转换。
- [Inno Setup 权限](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)、[架构限制](https://jrsoftware.org/ishelp/topic_setup_architecturesallowed.htm)：当前用户安装及架构检查。
