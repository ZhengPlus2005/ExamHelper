# ExamHelper 0.4.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. 沿用当前会话顺序实施的既有偏好；本计划须先经用户检查认可，当前仅编写文档，不开始修改产品代码或执行开发步骤。

**Goal:** 完成已确认的 17 项改动，交付全新数据隔离、空白多模型配置、独立服务资格、稳定历史、图片与批量操作、回答计时、统一辅助交互及 0.4.0 安装包。

**Architecture:** 延续 Core／Windows／原生 WPF。ConversationWorkspace 持有独立会话，AiGenerationGate 维持全应用单生成；设置完整性与服务验证分开。历史排序、回答计时、图片坐标和 UI 选择各自保持稳定身份，视图刷新不改变业务时间。

**Tech Stack:** 仓库锁定的 .NET 10／WPF、SQLite、NAudio、Markdig 1.4.1、AvalonEdit 6.3.1.120、Inno Setup 6.7.3；本轮不升级依赖或引入 WebView2。

**Spec:** `docs/superpowers/specs/2026-10-18-examhelper-0.4-design.md`，用户已认可。产品基线 `b46befff603d95e4ac534f5bfa9077892fddcf31`；设计提交 `0c0cad3`。任务执行 BASE 以开工时实际 HEAD 为准，不能用单个提交代替整个任务范围。

## Global Constraints

- 仅 Windows 11 x64、当前用户普通权限，主窗口正常点击／键盘，辅助模式不接受键盘文本、不抢焦点，所有辅助内容保持同一受保护 HWND。
- 数据根为 `%LOCALAPPDATA%\ExamHelper\0.4`，安装目录为 `%LOCALAPPDATA%\Programs\ExamHelper-0.4`。首次为空白，后续 0.4 系列重启和修订恢复本版数据，不读取、迁移或清空 0.3 数据。
- 新安装无 AI 配置、地址、名称或模型；未完成配置可保存、不可调用。不得通过旧默认值或归一化补回 Kimi／空模型。
- 进入辅助只要求捕获保护成功。辅助 AI 模型须文字、图片两项通过；未通过项显示并置灰。语音未通过只禁录音；快捷键与按钮遵守同一业务规则。主窗口资料完整即可直接调用。
- 全应用同时一个 AI 生成或 AI 服务测试；后台结果仍回发起对话。凭据准备租约、图片引用保护、保存失败保留内容及正常退出重试必须保留。
- 历史按最后消息创建时间倒序；纯草稿按会话创建时间。保存／查看／改名／流式片段／跳秒不改变时间，显示固定 UTC＋8。
- 批量点击条目同时勾选并预览；复选框只勾选。删除逐条报告，成功不回滚、失败保留勾选；只停止目标任务，删除当前项后打开最新剩余项，无剩余则空白新对话。
- 草稿图主体预览、右上叉删除；图片保存完整 PNG。整条消息复制原始 Markdown，仅文字；主页无清空按钮，导出置操作行最右并命名“导出”。
- 普通悬停 1 秒绿色，主页／隐藏 2 秒红色，进度沿圆角按钮外轮廓顺时针从顶部中央绕行。图片删除仍是中央白圈叉号及红色圆环。
- 回答计时含准备、等首字及生成；完成／失败／停止保存一位小数耗时，生成中按整秒显示。重试从零，后台不重置；系统校时不得改变耗时。
- 不读取真实凭据／历史验收、不做付费请求、不保存无关屏幕内容；使用临时路径、假 HTTP、可控时钟、自有图片及隔离安装身份。
- 每任务先观察 RED，再实现、针对 GREEN、完整回归并提交。只替换本版明确取代的旧默认／准入／迁移断言，不删除无关安全回归。未观察失败的用例不能记为修复证据。
- 一次最终独立完整增量审查，必要问题同轮 RED→GREEN 后完整回归，不重复派发重审。真实交互未测则如实记录，不把编译、合成事件或静默安装当作人工验收。

## Review Focus

1. 对空白／半填／已通过部分测试的配置，在改密钥、删模型或切配置的同时收到测试／模型拉取结果：保留原草稿，资格不串项，未完成状态不触发 URI 异常或默认 Kimi 回填。归属 Task 1／2。
2. A 流式生成，B 粘贴图片、切换预览或批量删除；较早的加载／导图回调晚到：历史选中不闪、不跳序，结果不串对话，已删除对话不复活，失败目标仍可编辑。归属 Task 3／5／6／8。
3. 原图带 96／120／192 DPI，窗口跨负坐标 125%／200% 屏，缩放后保存且遇到缺图／只读目标：四角完整，坐标一致，原像素及原文件保留。归属 Task 7／11。
4. 请求准备延迟、停止与最后片段交错、同 RequestId 重试、系统 UTC 校时、生成中导出或退出：耗时不跨尝试、不重置、不继续增长，旧回调不完成新计时。归属 Task 4／8／9。
5. 辅助无资格／长模型列表／小尺寸错误状态，以及高 DPI 安装完成页／同名外来快捷方式：禁用与受保护交互一致，发送仍可达，进度不越界，安装复选框对齐且只执行勾选动作。归属 Task 2／9／10／11。

## 执行准备、文件边界与通用命令

开工后复用既有托管工作树 `C:\Users\ZhengPlus\.codex\worktrees\examhelper-windows-core\ExamHelper`，先核对其附件和 Git 状态；不重建、重置或把用户主目录当作测试数据。记录每任务 BASE，使用本计划专属 `.superpowers/sdd/2026-10-18-examhelper-0.4-implementation/` 账本；不复用 0.3 的临时账本。不在本次写计划时运行 task-start 或创建实现文件。

开工的 PowerShell 命令约定如下，各后续 Run 行中的 `$taskDotnet` 使用此值：

```powershell
$taskDotnet = 'D:\Code\ExamHelper\.tools\dotnet\dotnet.exe'
& $taskDotnet restore ExamHelper.slnx --locked-mode
./scripts/Test.ps1 -Configuration Release -DotnetPath $taskDotnet
```

Expected：锁定恢复、Release 构建和已有全套测试退出 0，发现测试数大于 0，记录真实总数、警告和失败。0.3 的历史 254 项是基线资料，不作为本轮结果。后续每任务的“完整回归”均指同一 `scripts/Test.ps1` 命令；RED 不能是“筛选器未发现测试”。

文件职责按以下任务固定：Task 1／2 处理设置、路径、资格和准入；Task 3 处理时间与稳定历史；Task 4 处理单次请求计时；Task 5 处理批量操作；Task 6／7 分别处理图片输入和原图预览保存；Task 8／9 分别处理主窗口和辅助 UI；Task 10 处理图标和安装交互；Task 11 处理实机验收、审查和发布。只为这些职责拆分文件，不做全仓重构。

下文代码块给出用例的核心断言，不重复整份夹具 Arrange。未展示的图像字节、临时路径、控件实例和可控假服务均在对应测试文件内构造；跨任务共享的方法只使用 Interfaces 明确定义的签名，不假设仓库已经存在同名辅助方法。

### Task 1: 全新数据路径与可保存的空白配置

**Files:**
- Create: `src/ExamHelper.Windows/Persistence/AppDataLayout.cs`；`src/ExamHelper.Core/Settings/ProfileCompleteness.cs`；`tests/ExamHelper.TestSupport/TestSettings.cs`。
- Modify: `src/ExamHelper.Windows/Persistence/AppDataPaths.cs`；`src/ExamHelper.App/Composition/AppComposition.cs`；`src/ExamHelper.App/App.xaml.cs`；`src/ExamHelper.Core/Settings/{SettingsTypes,AiProfileCatalog,ModelCatalog,ServiceVerification}.cs`；`src/ExamHelper.Windows/Settings/JsonSettingsStore.cs`；`src/ExamHelper.Windows/Providers/{ProviderEndpoint,ChatCompletionsClient}.cs`；`src/ExamHelper.App/Settings/{AiProfileEditor,ModelEditor,SettingsViewModel,SettingsViewModel.Persistence,SettingsViewModel.Discovery}.cs`；`src/ExamHelper.App/Settings/SettingsWindow.xaml`；`src/ExamHelper.App/Settings/SettingsWindow.xaml.cs`。
- Test: 新增 `tests/ExamHelper.Windows.Tests/Settings/FreshConfiguration04Tests.cs`、`tests/ExamHelper.Windows.Tests/App/FreshStartup04Tests.cs`；修改 `tests/ExamHelper.TestSupport/ConversationWorkspaceRig.cs` 及受默认值取代影响的既有测试 Arrange。

**Interfaces:**
- Produces: `AppDataLayout(string Root)`，具有 `Settings,Secrets,Database,Images,TempAudio,Logs` 路径属性；`AppDataPaths.Resolve(string localAppData): AppDataLayout` 返回其下 `ExamHelper/0.4`。
- `AppComposition.CreateAsync(Func<AppComposition,IOverlayHost>? overlayFactory = null, AppDataLayout? paths = null): Task<AppComposition>`；路径必须在构造全部存储和录音对象前注入，不让字段初始化器提前访问真实目录。
- 设置格式为 4。`AppSettings.AiProfiles`、`AiServiceProfile.Models` 的新对象默认为空集合，`Name/Model` 默认空，`AiSettings.BaseUri` 可空、无默认供应商地址；无启用项时 `ActiveAiProfileId=null`。
- `AiServiceProfile.BaseAddress: string` 保存原始地址编辑值，包括尚未填完的文本；`Settings.BaseUri` 只在可解析时派生，不是默认回填来源。
- `ProfileReadiness(bool Complete,Uri? Endpoint,string? Reason)`；`ProfileCompleteness.Evaluate(AiServiceProfile): ProfileReadiness`。完整性要求名称、绝对 HTTP／HTTPS 地址、非空密钥摘要、有效模型及本配置默认模型；结构损坏与字段未填完分开处理。
- `AiProfileEditor.NewModelId: string` 为独立输入；`AddModel(string modelId): ModelEditor` 拒绝空白，重复 ID 选回既有条目；加载已有模型使用 `AddModel(AiModelProfile model)`，不再用可选 null 参数创建空行。
- 测试专用 `TestSettings.Configured(): AppSettings` 显式创建名为“测试配置”、地址 `https://example.invalid/v1`、模型 `model-a`、假密钥摘要且未验证的配置；同时填写原始 BaseAddress、派生 BaseUri、默认模型和启用配置 ID，避免使用新的空白默认值冒充完整夹具。产品默认值不能为了测试继续预置服务。

- [ ] **Step 1 — 写失败测试。** 在临时 localAppData 下放入旧版设置／历史哨兵，验证新路径只在 `ExamHelper/0.4` 创建且旧文件不变；保存并重载半填地址 `https://`、空名和空模型，编辑值不丢失且不可调用。新增 A、B 配置，添加重复／空 ID、来回选择、保存重载，均不得带入 Kimi、改空原模型或增长空条目。

```csharp
var fresh = new AppSettings();
Assert.Empty(fresh.AiProfiles!.Value);
Assert.Null(fresh.ActiveAiProfileId);
Assert.Null(fresh.Ai.BaseUri);
Assert.Equal("", fresh.Ai.Model);
var profile = new AiServiceProfile { BaseAddress = "https://", Models = [] };
Assert.False(ProfileCompleteness.Evaluate(profile).Complete);
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter 'FullyQualifiedName~FreshConfiguration04Tests|FullyQualifiedName~FreshStartup04Tests'`。Expected：缺少新路径／完整性契约，或仍生成 Kimi、半填地址保存失败、模型被清空；记录实际失败。
- [ ] **Step 3 — 实现路径和设置数据。** 所有存储／资源临时路径统一取注入的 AppDataLayout；单实例名改为 `Local\ExamHelper-0.4-{当前用户SID}`。设置格式 4 不执行旧单模型导入，Normalize 幂等、不补供应商。JsonSettingsStore 允许合法结构的半填配置，未知／损坏格式仍保留原文件并报错；调用前才把 BaseAddress 转为有效 Uri。ServiceVerification 的端点规范化接受空 Uri 并产生稳定空值，不能对空白设置解引用，也不能借假默认地址规避。保持分阶段密钥写入和 SynchronizedSecretStore。
- [ ] **Step 4 — 实现设置编辑。** API 密钥字段紧接地址。独立新模型输入和所选模型编辑分开绑定；默认模型删除后清空 DefaultModelId，允许保存未完成状态。接口加入仅处理勾选的非空、未存在 ID，默认模型不擅自替换。新配置不用“新配置 N”作为实际已填名称。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2；Expected：全部匹配测试通过且计数大于 0。需要服务的旧测试以 TestSettings.Configured 显式 Arrange，主要涉及 Core Settings／Sessions、Windows Settings／Providers／App 的既有夹具；只替换被取消的默认／旧导入断言，不改原子保存、密钥失败回滚和请求隔离断言。运行完整回归，Expected：零失败。
- [ ] **Step 6 — 提交。** 精确暂存本任务变更，`git commit -m "feat: initialize isolated blank configuration for 0.4"`；Expected：仅本任务路径及对应测试 Arrange，记录提交 SHA 和测试证据。

### Task 2: 文字与图片双资格、辅助准入和灰色模型

**Files:** Modify `src/ExamHelper.Core/Settings/{ModelCatalog,ServiceVerification}.cs`；`src/ExamHelper.App/Settings/{ModelEditor,AiProfileEditor,SettingsViewModel,SettingsViewModel.Persistence}.cs`；`src/ExamHelper.Windows/Settings/ServiceTestRunner.cs`；`src/ExamHelper.Core/Sessions/{CommandPolicy,SessionCoordinator,SessionCoordinator.Home}.cs`；`src/ExamHelper.App/Home/HomeViewModel.cs`；`src/ExamHelper.App/Controls/{ModelSelector.xaml,ModelSelector.xaml.cs,ModelSelector.Hover.cs}`；`src/ExamHelper.App/Overlay/OverlayViewModel.cs`；`tests/ExamHelper.TestSupport/{TestSettings,ConversationWorkspaceRig}.cs`；`tools/ExamHelper.InteractionLab/{ProductCaptureFixture,NativeVerification}.cs`。Test: 新增 `tests/ExamHelper.Core.Tests/Settings/AuxiliaryQualification04Tests.cs`、`tests/ExamHelper.Windows.Tests/App/ServiceAdmission04Tests.cs`，更新 `tests/ExamHelper.Windows.Tests/App/EntryEligibilityTests.cs` 和所取代的旧资格断言。

**Interfaces:**
- Consumes Task 1 的完整性和空集合语义。
- Produces `AiTestKind { Text,Image }`；`AiModelProfile.TextVerifiedFingerprint: string?`；现有 `VerifiedFingerprint` 继续专指图片证据，不互相拷贝。
- `ServiceVerification.RecordModelResult(AppSettings,Guid profileId,Guid modelId,AiTestKind kind,bool passed,string testedFingerprint): AppSettings`；只更改匹配快照的一类证据，当前快照不匹配则拒绝／丢弃；同内容重新测试失败清除该类通过资格。
- `ServiceVerification.CanUseInAuxiliary(AppSettings,ModelSelection): bool` 检查完整性及两类指纹。
- `ModelCatalog.Resolve(AppSettings settings,ModelSelection selection,bool requireAuxiliaryVerified,ServiceVerification verification): AiSettings` 的 bool 语义改为双资格，更新调用处，主窗口传 false。
- `ModelChoice(AiModelProfile Model,bool IsEnabled,string? Reason)` 定义在 ModelSelector.xaml.cs；ModelSelector 的 `ModelChoices` 改用该包装，列表包含未通过项。`Configure(AppSettings settings,ModelSelection? selection,bool auxiliary,ServiceVerification verification): void` 的 auxiliary 决定灰色与调用规则。
- 测试共享夹具增加 `TestSettings.Qualified(ServiceVerification proof): AppSettings`，在 Configured 基础上记录本模型文字、图片及假语音证据；更新 ConversationWorkspaceRig 及受影响的 Lab 夹具，显式注入同一个 proof。生产业务层不能因为可选验证器为 null 就放行辅助 AI；缺验证器时辅助 AI／录音资格按未通过处理。

- [ ] **Step 1 — 写失败测试。** 对每模型四种文字／图片组合只允许“都通过”；A 合格不使 B 合格。AI／ASR 均未验证也能 Open 辅助，包括零配置和半填配置，不能因取默认模型而抛错；保护失败仍留主页。无 AI 可截图入草稿，Send／直发／AI 重试禁止；ASR 未通过不能启动录音，但已有录音仍能停止，退出／丢弃可用。主窗口完整配置无需测试。单独传 null 验证器构造协调器，断言辅助调用仍被拒绝。

```csharp
var settings = TestSettings.Configured();
var p = settings.AiProfiles!.Value[0]; var m = p.Models!.Value[0];
var choice = ModelCatalog.Select(p, m); var proof = new ServiceVerification(new byte[32]);
var fingerprint = proof.AiFingerprint(settings with { Ai = ModelCatalog.Resolve(settings, choice, false, proof) });
settings = proof.RecordModelResult(settings, p.Id, m.Id, AiTestKind.Text, true, fingerprint);
Assert.False(proof.CanUseInAuxiliary(settings, choice));
settings = proof.RecordModelResult(settings, p.Id, m.Id, AiTestKind.Image, true, fingerprint);
Assert.True(proof.CanUseInAuxiliary(settings, choice));
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test ExamHelper.slnx -c Release --no-restore --filter 'FullyQualifiedName~AuxiliaryQualification04Tests|FullyQualifiedName~ServiceAdmission04Tests'`。Expected：单图片仍被当合格、未验证仍阻止进入、列表过滤隐藏等真实失败，或新契约编译失败；不可用“无匹配测试”充当 RED。
- [ ] **Step 3 — 实现证据与保存。** 两类指纹均绑定地址／模型／密钥身份与摘要；地址／密钥改动使两类全部失效，模型 ID 改动仅使该项失效，思考不失效。密钥分阶段换 SecretId 时同步重算本次合法测试的两类证据。保持 RunTextTestAsync／RunImageTestAsync 公开入口，只提交所测项，不隐式保存其他配置草稿。
- [ ] **Step 4 — 实现准入与 UI。** Home.StartAsync 去掉服务 CanStart 门槛，保留保护检查；业务 Availability 统一约束按钮和快捷键，不能只在 UI 禁用。所有模型显示，未合格项置灰；默认不合格时显示可选合格模型，由用户明确选中后提交，不能自动换服务。配置／模型变更仅影响后续调用。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2。加入 Review Focus 1 的可控测试：测文字期间改密钥、测图片期间删模型、拉取期间改地址／切配置；旧结果不污染新字段。断言一次测试失败只影响其类别，两个证据在合法 SecretId 阶段提交后仍匹配；重跑已有准备凭据租约与无自动重试测试。Expected：针对及全套零失败。
- [ ] **Step 6 — 提交。** `git commit -m "feat: separate model qualification from auxiliary admission"`；Expected：记录接口变更及真实回归结果。

### Task 3: 稳定聊天时间、UTC＋8 和历史增量同步

**Files:** Create `src/ExamHelper.Core/Sessions/ConversationChronology.cs`、`src/ExamHelper.Core/Common/DisplayTime.cs`、`src/ExamHelper.App/Home/HistoryItemViewModel.cs`、`src/ExamHelper.App/Home/HistoryCollection.cs`。Modify `src/ExamHelper.Core/Sessions/{SessionTypes,SessionCoordinator,ConversationTitle}.cs`；`src/ExamHelper.Windows/Persistence/{DatabaseSchema,SessionMigration,SqliteSessionStore}.cs`；`src/ExamHelper.App/Home/{HomeViewModel,HomeWindow.Rendering,ConversationMessageViewModel}.cs`；`src/ExamHelper.App/Home/HomeWindow.xaml`；`src/ExamHelper.App/Home/HomeWindow.xaml.cs`；`tests/ExamHelper.TestSupport/InMemorySessionStore.cs`。Test: 新增 `tests/ExamHelper.Windows.Tests/Persistence/HistoryChronology04Tests.cs`、`tests/ExamHelper.Windows.Tests/App/StableHistory04Tests.cs`；修改 `ConversationMigrationTests.cs` 中已被全新应用取代的迁移断言。

**Interfaces:**
- Produces `SessionSnapshot.CreatedAt: DateTimeOffset`，StartFreshLockedAsync 显式取当前 TimeProvider；之后不因保存更新。
- `ConversationChronology.SortAt(SessionSnapshot): DateTimeOffset`：最后一条用户／AI 消息 CreatedAt，没有则会话 CreatedAt；不取 UpdatedAt 或流式完成时间。
- 保持 SessionSummary 原四参构造，增加初始化属性 `CreatedAt`、`SortAt`。数据库新增 `CreatedTicksUtc`、`SortTicksUtc`，本版 schema 为 3；按 `SortTicksUtc DESC,CreatedTicksUtc DESC,Id ASC` 排序。
- `DisplayTime.ToUtc8(DateTimeOffset): DateTimeOffset`；`Format(DateTimeOffset,string format = "yyyy-MM-dd HH:mm:ss"): string` 固定偏移＋08:00、InvariantCulture。
- `HistoryItemViewModel(Guid id)`：`Id,Title,SortAt,TimeText`，`Update(SessionSummary): void`；`HistoryCollection : ObservableCollection<HistoryItemViewModel>`，`Apply(IReadOnlyList<SessionSummary>): void` 按 ID 复用并最小增删移动。

- [ ] **Step 1 — 写失败测试。** 新建纯草稿 A、B，给 A 编辑／改名／打开，时间与顺序不变；新增消息才更新排序。用最后消息创建时间验证灰色时间，不能显示 UTC＋0。反复流式刷新后，历史对象引用、当前选中和列表滚动位置保持，不发 Reset。

```csharp
var created = DateTimeOffset.Parse("2026-10-18T00:00:00Z");
var state = new SessionSnapshot { CreatedAt = created, UpdatedAt = created.AddHours(9) };
Assert.Equal(created, ConversationChronology.SortAt(state));
Assert.Equal("2026-10-18 08:00:00", DisplayTime.Format(created));
var list = new HistoryCollection();
var summary = new SessionSummary(state.Id, "对话", state.UpdatedAt, true) { CreatedAt = created, SortAt = created };
list.Apply([summary]); var item = list[0];
list.Apply([summary with { UpdatedAt = created.AddDays(1) }]);
Assert.Same(item, list[0]);
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter 'FullyQualifiedName~HistoryChronology04Tests|FullyQualifiedName~StableHistory04Tests'`。Expected：排序仍随 UpdatedAt 移动、Clear 重建导致引用／选中变化、时间少八小时，或新字段缺失。
- [ ] **Step 3 — 实现存储与时间。** schema 3 的空库初始化及版本检查；旧／未知库若被人为放入新目录则拒绝且不改原文件，不实现旧库迁移。原迁移用例替换为拒绝旧版且数据未改，保留损坏／原子保存保护。消息 CreatedAt 固定；默认标题及消息可见时间使用 DisplayTime；重试复用消息时不改创建时间。
- [ ] **Step 4 — 实现列表同步。** Home.History 使用 HistoryCollection，RefreshAsync 调 Apply，SelectionChanged 按 HistoryItemViewModel.Id 打开会话；删除旧的 Clear＋逐项 Add 和无意义的反复取消选中。计时／正文更新不得用 UpdatedAt 重排历史。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2。补 Review Focus 2：A 生成 20 个片段同时来回打开 B，选中框事件不反复取消、相同时间的次序稳定；重启后 CreatedAt／SortAt 一致。Expected：针对及全套零失败，无 0.3 迁移实现被重新引入。
- [ ] **Step 6 — 提交。** `git commit -m "fix: keep history ordering selection and UTC8 time stable"`。

### Task 4: 单调时钟回答计时、重试与持久化快照

**Files:** Create `src/ExamHelper.Core/Sessions/ResponseTimer.cs`、`src/ExamHelper.Core/Sessions/SessionCoordinator.Timing.cs`。Modify `src/ExamHelper.Core/Sessions/{SessionTypes,SessionCoordinator,RequestRecovery}.cs`；`src/ExamHelper.Core/Export/HtmlConversationExporter.cs`；`tests/ExamHelper.TestSupport/ManualTimeProvider.cs`。Test: 新增 `tests/ExamHelper.Core.Tests/Sessions/ResponseTiming04Tests.cs`，扩充 `tests/ExamHelper.Core.Tests/Export/HtmlConversationExporterTests.cs`。

**Interfaces:**
- Consumes Task 3 的稳定创建时间与 DisplayTime。
- Produces `ResponseTiming(Guid RequestId,int Attempt,DateTimeOffset StartedAt,TimeSpan Elapsed,bool IsRunning)`，`MessageSnapshot.Timing: ResponseTiming?`，`RequestSnapshot.Attempt: int = 1`。
- `ResponseTimer(TimeProvider clock,Guid requestId,int attempt)` 构造时取 UTC 起点和单调时间戳；`Snapshot(): ResponseTiming`；`Finish(): ResponseTiming` 幂等固定终值。
- `SessionCoordinator.SnapshotForPresentation(): SessionSnapshot` 返回采样后不可变快照，不发布 StateChanged、不改创建时间；只更新身份及 Attempt 都匹配的回答。持久化和导出也可使用该采样。
- 测试时钟增加 `JumpUtc(TimeSpan delta): void`，只改 GetUtcNow，不改变 GetTimestamp。

- [ ] **Step 1 — 写失败测试。** 等首字、流式、停止、失败、重试、返回主页及退出都验证；重试同 RequestId 使用新 Attempt，旧完成回调不能停止新计时。准备阶段在配置解析通过后开始本地计时；准备失败未接受发送则保留草稿、不生成假消息。

```csharp
var clock = new ManualTimeProvider();
var timer = new ResponseTimer(clock, Guid.NewGuid(), 1);
clock.Advance(TimeSpan.FromSeconds(12.4)); clock.JumpUtc(TimeSpan.FromHours(-2));
Assert.Equal(TimeSpan.FromSeconds(12.4), timer.Finish().Elapsed);
clock.Advance(TimeSpan.FromSeconds(8));
Assert.Equal(TimeSpan.FromSeconds(12.4), timer.Snapshot().Elapsed);
Assert.False(timer.Snapshot().IsRunning);
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Core.Tests -c Release --no-restore --filter 'FullyQualifiedName~ResponseTiming04Tests|FullyQualifiedName~HtmlConversationExporterTests'`。Expected：缺计时契约／耗时不固定／重试累加或导出无用时。
- [ ] **Step 3 — 实现生命周期。** 在有效准备开始时创建本次局部计时，准备成功后与已接受请求关联；网络发送／首字等待都计入。完成、错误、StopAnswer、取消退出在对应会话锁内结束计时；重试 Attempt＋1 并创建新计时。SnapshotForPresentation 按不可变身份采样，不能把上一尝试的时间写入新回答。
- [ ] **Step 4 — 实现保存与 HTML。** 流式周期保存取已采样耗时，最终保存固定终值；崩溃恢复把运行中的已存耗时标为停止，不加停机时长。HTML 计时写入头部元信息，正文仍仅取 Text。导出生成中快照冻结当时 Elapsed／IsRunning，不在 HTML 中运行脚本跳秒。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2。补 Review Focus 4：假客户端延迟 Prepare，随后最后片段与停止交错；保存后重开／导出等待期间时钟前进，原快照用时固定；无 Timing 不补算。Expected：所有针对测试及全套零失败。
- [ ] **Step 6 — 提交。** `git commit -m "feat: persist per-attempt monotonic response timing"`。

### Task 5: 历史条目操作、批量删除与失败恢复

**Files:** Create `src/ExamHelper.Core/Sessions/BatchDeleteResult.cs`、`src/ExamHelper.App/Home/HistorySelectionController.cs`、`src/ExamHelper.App/Home/HomeWindow.History.cs`。Modify `src/ExamHelper.Core/Sessions/ConversationWorkspace.cs`；`src/ExamHelper.App/Home/{HomeViewModel,HistoryItemViewModel,HomeWindow.Rendering}.cs`；`src/ExamHelper.App/Home/HomeWindow.xaml.cs`；`src/ExamHelper.App/Home/HomeWindow.xaml`。Test: 新增 `tests/ExamHelper.Core.Tests/Sessions/BatchDelete04Tests.cs`、`tests/ExamHelper.Windows.Tests/App/HistorySelection04Tests.cs`。

**Interfaces:**
- Consumes Task 3 的 HistoryCollection、SortAt 和稳定 ID；保留既有 `SessionCoordinator.DeleteStoredAsync(Func<CancellationToken,Task>,CancellationToken)` 的“删除成功才停用运行时”协议。
- Produces `BatchDeleteResult(ImmutableArray<Guid> DeletedIds,ImmutableDictionary<Guid,string> Failures)`；`ConversationWorkspace.DeleteManyAsync(IReadOnlyCollection<Guid> ids,CancellationToken): Task<BatchDeleteResult>`，去重并冻结目标，逐条处理。
- `HistoryItemViewModel.IsChecked: bool`；`HistorySelectionController(HistoryCollection items)` 提供 `IsBatchMode`、`SelectedIds`、`Enter()`、`Complete()`、`ToggleRow(Guid)`、`SetChecked(Guid,bool)`、`SetAll(bool)`。ToggleRow 只负责勾选，调用方再打开对应会话。
- `HomeViewModel.DeleteManyAsync(IReadOnlyCollection<Guid> ids,Func<int,Task<bool>> confirm,CancellationToken): Task<BatchDeleteResult>`；单条删除复用相同结果及切换逻辑。清理失败在 Status 单独报告，不把已删除历史重新插回。

- [ ] **Step 1 — 写失败测试。** 准备三条持久会话，其中一条正在生成、一条存储删除抛错；确认一次，数量为去重目标数。成功项移除，失败项保留、可编辑／重试，非目标任务不停止。测试当前项删除后打开最新剩余，全部删除后只创建空白工作区、不保存空历史。

```csharp
// BatchDelete04Tests 内使用可按 ID 失败的 ISessionStore 包装及 ConversationWorkspaceRig 同类假服务。
Assert.Contains(successId, result.DeletedIds);
Assert.Contains(failedId, result.Failures.Keys);
Assert.DoesNotContain(failedId, result.DeletedIds);
Assert.NotNull(workspace.Find(failedId));
await workspace.Find(failedId)!.UpdateDraftTextAsync("删除失败后仍能编辑", default);
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test ExamHelper.slnx -c Release --no-restore --filter 'FullyQualifiedName~BatchDelete04Tests|FullyQualifiedName~HistorySelection04Tests'`。Expected：缺批量契约、失败项被释放、确认／点击重复触发或删除后未按规则选择。
- [ ] **Step 3 — 实现批量删除。** 冻结 ID，持有导航序列，复用原子删除协议；不要在持有导航锁时再次调用会获取同锁的公开 Delete／Open／New。单条 I/O 失败记录原因并继续；处理中取消则停止后续项，已删结果保留、未处理项保留供重试。删除成功才释放和移除运行时，旧回调不能复活。归属图片清理使用现有扫描租约，保留导入／导出的原图引用。
- [ ] **Step 4 — 实现 UI。** 普通条目右侧悬停显示铅笔／垃圾桶图形，无文字按钮，提供工具提示和可访问名称；移除旧改名／单删工具栏。批量入口显示勾选数、全选／取消全选、删除所选和完成。条目点击同时勾选＋预览，复选框阻止冒泡、只勾选，操作图标也不触发双重行操作。删除期间禁重复提交，开始删除前取消较早的 UI 加载请求。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2。补 Review Focus 2：A 正在流式而删除 B；删除 A 前正好有旧预览加载／导图等待，放行后不能恢复已删项。只有删除当前项成功时才切换最新剩余，失败则留原项；失败项刷新后仍勾选。Expected：针对及全套零失败，图片清理与晚到 Flush 安全回归仍通过。
- [ ] **Step 6 — 提交。** `git commit -m "feat: add previewable batch history deletion"`。

### Task 6: 修复图片粘贴路由与草稿预览／叉号删除

**Files:** Create `src/ExamHelper.Windows/Images/ClipboardImageReader.cs`、`src/ExamHelper.App/Home/HomePasteBehavior.cs`、`src/ExamHelper.App/Controls/DraftImageTile.xaml`、`DraftImageTile.xaml.cs`（同目录）。Modify `src/ExamHelper.App/Home/{HomeWindow.Rendering,HomeDraftController}.cs`；`src/ExamHelper.App/Home/HomeWindow.xaml.cs`；`src/ExamHelper.Windows/Images/ImageImportService.cs`。Test: 新增 `tests/ExamHelper.Windows.Tests/App/ClipboardPaste04Tests.cs`、`DraftImageTile04Tests.cs`，扩充 `HomeChatTests.cs` 和 `ImageImportTests.cs`。

**Interfaces:**
- Consumes 既有 `IImageImporter.ImportBitmapAsync(BitmapSource,ct)`／`ImportFilesAsync(IEnumerable<string>,ct)` 及 HomeDraftController 的发起时目标捕获。
- Produces `ClipboardImagePayload(BitmapSource? Bitmap,IReadOnlyList<string> Files)`；`ClipboardImageReader.Read(System.Windows.IDataObject data): ClipboardImagePayload?`，普通文本返回 null；图片同步解码／复制并 Freeze，优先于伴随文本和 HTML。
- `IClipboardAccess.GetDataObject(): System.Windows.IDataObject?`；真实实现只在粘贴操作期间取数据，测试传合成 IDataObject。
- `HomePasteBehavior(TextBox input,HomeDraftController drafts,IClipboardAccess clipboard,Action<Exception> report)`，实现 IDisposable 卸载事件；接入 ApplicationCommands.Paste 的 PreviewCanExecute／PreviewExecuted，使“只有图片、没有文字”的 Ctrl＋V 也能被接受，普通文本继续走既有文本粘贴。
- `DraftImageTile` 暴露 PreviewSource、`ICommand PreviewCommand`、`ICommand RemoveCommand`；主体与右上叉为独立 Button，不把两按钮互相嵌套。

- [ ] **Step 1 — 写失败测试。** DataObject 分别只含 PNG MemoryStream、BitmapSource、DIB／系统可转换位图、图片＋HTML＋文字、普通文字及 FileDrop。仅图片的 Paste 命令应可执行并只入草稿；图片不伴随插入 URL，普通文本不吞键。新建两图，点击主体只预览，叉号只删目标图且不弹预览。

```csharp
var source = new DataObject(); source.SetData("PNG", new MemoryStream(pngBytes));
var payload = new ClipboardImageReader().Read(source);
Assert.NotNull(payload!.Bitmap);
Assert.Equal(originalWidth, payload.Bitmap.PixelWidth);
Assert.True(payload.Bitmap.IsFrozen);
Assert.Null(new ClipboardImageReader().Read(new DataObject(DataFormats.UnicodeText, "普通文字")));
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter 'FullyQualifiedName~ClipboardPaste04Tests|FullyQualifiedName~DraftImageTile04Tests'`。Expected：仅图片的命令路由未执行、格式不支持、主体点击删除或叉号同时打开预览。
- [ ] **Step 3 — 实现粘贴。** 先按实际 WPF 命令路由复现，不只修原 Pasting 回调。读取 passed IDataObject 的可转换位图、PNG 流及 DIB 内容，必要格式转换集中在 Reader；不在 await 后重新读全局剪贴板。拿到图片后取消默认文字插入，调用统一完整像素导入并跟踪退出 Drain；无图片的命令不改动原文本／IME行为。
- [ ] **Step 4 — 实现草稿图块。** PreviewCommand 打开原图，RemoveCommand 捕获图 ID 与所属运行时；叉号阻断行主体事件。旧 Thumbnail(draft=true) 的整图删除处理替换为该控件。拖入 PNG／JPEG／BMP／GIF 首帧／TIFF 首页继续完整保存，异步完成不跟随新选会话。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2。补 Review Focus 2：A 发起解码后切 B，结果仅写 A；A 已删除时不能被追加复活。STA 测试用假剪贴板及真实 routed command，不仅直接调用 Reader；真实 Ctrl＋V 则在 Task 11 用自有图片验证。Expected：针对及全套零失败。
- [ ] **Step 6 — 提交。** `git commit -m "fix: paste image payloads and separate draft preview from removal"`。

### Task 7: 完整原图预览、DPI 坐标与 PNG 保存

**Files:** Create `src/ExamHelper.App/Images/ImageViewportTransform.cs`、`src/ExamHelper.Windows/Images/ImageSaveService.cs`。Modify `src/ExamHelper.App/Images/{ImageViewerWindow.xaml,ImageViewerWindow.xaml.cs}`；`src/ExamHelper.App/Home/HomeWindow.Rendering.cs`。Test: 新增 `tests/ExamHelper.Windows.Tests/App/ImageViewer04Tests.cs`、`ImageSave04Tests.cs`。

**Interfaces:**
- Consumes IImageStore.ReadAsync 和 Task 6 的主图预览入口，不使用 DecodePixelWidth 缩略缓存当原图。
- Produces `ImageViewportTransform`：`Fit(Size pixelSize,Size viewport): void`、`ZoomAt(double factor,Point anchor): void`、`Pan(Vector delta): void`、`Transform PixelToViewport { get; }`。图像表面使用一个源像素＝一个内部坐标单位，统一缩放／偏移，不叠加两次 DPI 比例或 Stretch。
- `ImageViewerWindow(BitmapSource bitmap,byte[] originalPng,ImageSaveService? save = null)`，原图字节在打开时冻结，供完整保存；保留 Fit、ZoomAt、ZoomFactor 以兼容既有 UI 验证调用。
- `ImageSaveService.SaveAsync(string path,ReadOnlyMemory<byte> originalPng,CancellationToken): Task`，通过 AtomicFile 写原始 PNG 字节；系统 SaveFileDialog 位于 UI，默认扩展名 .png，覆盖询问开启。

- [ ] **Step 1 — 写失败测试。** 生成自有四角颜色不同、中心标记、尺寸 2560×1440 的 PNG，分别编码 96／120／192 DPI。首次适应窗口时四角均在视口内，实际 Image 布局／变换下的完整内容一致；不是只检查缩略图或纯数学对象。缩放平移后保存字节与原 PNG 相同。

```csharp
var transform = new ImageViewportTransform();
transform.Fit(new Size(2560, 1440), new Size(800, 600));
Assert.Equal(new Point(0, 75), transform.PixelToViewport.Transform(new Point(0, 0)));
Assert.Equal(new Point(800, 525), transform.PixelToViewport.Transform(new Point(2560, 1440)));
Assert.Equal(originalPng, await File.ReadAllBytesAsync(savedPath));
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter 'FullyQualifiedName~ImageViewer04Tests|FullyQualifiedName~ImageSave04Tests'`。Expected：四角被裁、Image 布局与渲染变换不一致、缺保存入口或保存不是原图。
- [ ] **Step 3 — 修复真实预览路径。** 沿 HomeWindow.LoadBitmapAsync 原图分支至 ImageViewerWindow 的实际测量／排列／裁切定位四分之一问题，再统一 ImageViewportTransform。视口和图片的尺寸单位必须一致；把滚轮和拖动事件限制在视口，保存工具栏不被平移。窗口变化可重新 Fit，但人工缩放不因后台回答跳秒而重置。
- [ ] **Step 4 — 实现保存。** 草稿和历史均传同一完整 PNG 字节；预览工具栏增加保存、适应窗口入口，Esc 仍关闭。取消不写，缺图在打开时报错，目标只读／写失败保留已有文件，临时文件清理，保存不改历史引用。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2；补 Review Focus 3：真正实例化 WPF 控件检查布局与四角合成截图，保存前先缩放和平移；只读／取消／缺图不破坏源和目标。负坐标双屏的真实 DPI 切换留至 Task 11 受控原生检查。Expected：针对及全套零失败。
- [ ] **Step 6 — 提交。** `git commit -m "fix: display and save full-resolution images across DPI settings"`。

### Task 8: 主窗口正文更新、滚轮、整条复制与精简说明

**Files:** Create `src/ExamHelper.App/Home/HomeMessageView.xaml`、`HomeMessageView.xaml.cs`；`src/ExamHelper.App/Common/ResponseTimingFormatter.cs`；`src/ExamHelper.App/Controls/ChatWheelRouting.cs`；`src/ExamHelper.App/Help/UsageSection.cs`。Modify `src/ExamHelper.App/Home/{HomeWindow.xaml,HomeWindow.xaml.cs,HomeWindow.Rendering.cs,HomeViewModel.cs,ConversationMessageViewModel.cs}`；`src/ExamHelper.App/Controls/{MarkdownView,MarkdownWpfRenderer}.cs`；`src/ExamHelper.App/Help/{UsageWindow.xaml,UsageWindow.xaml.cs}`。Test: 新增 `tests/ExamHelper.Windows.Tests/App/MessagePresentation04Tests.cs`、`UsageContent04Tests.cs`，扩充 `MarkdownViewTests.cs`。

**Interfaces:**
- Consumes Task 3 的时间／稳定 ID、Task 4 的 ResponseTiming／SnapshotForPresentation、Task 6／7 的草稿预览及完整原图。
- Produces `HomeMessageView.Update(MessageSnapshot): void`、`UpdateTiming(ResponseTiming?): void`、`CopyText: string`。相同消息 ID 的控件复用，正文改变才刷新 Markdown；计时改变只更新标签。
- `ResponseTimingFormatter.Format(ResponseTiming?): string`：null 返回空串；运行时按 floor 整秒“已用 12 秒”，结束一位小数“用时 12.4 秒”，InvariantCulture。
- `ChatWheelRouting.ForwardVertical(ScrollViewer outer,int delta): void`，用于代码横向容器 PreviewMouseWheel 交给聊天外层；辅助回答也可复用，不劫持原图查看器的缩放滚轮。
- `UsageSection(string Title,string Body)`；UsageWindow 用分段控件显示，标题 FontWeight=Bold，正文正常，不再把所有标题混进一块普通 TextBlock。

- [ ] **Step 1 — 写失败测试。** 将真实 MouseWheel routed event 从长代码块内发出，外层 VerticalOffset 改变，代码横向条仍工作；不是只调用 ForwardVertical 验证自身。对同一消息跳秒 30 次，MarkdownView／复制按钮引用和上翻偏移保持。提问和回答整条复制取原始 Text，代码围栏保留，排除图／头部／耗时／推理。

```csharp
var timing = new ResponseTiming(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(12.4), true);
Assert.Equal("已用 12 秒", ResponseTimingFormatter.Format(timing));
Assert.Equal("用时 12.4 秒", ResponseTimingFormatter.Format(timing with { IsRunning = false }));
var message = new MessageSnapshot { Role = MessageRole.Assistant, Text = "**答案**\n```cpp\nint x;\n```", ReasoningContent = "hidden" };
var view = new HomeMessageView(); view.Update(message);
Assert.Equal(message.Text, view.CopyText);
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter 'FullyQualifiedName~MessagePresentation04Tests|FullyQualifiedName~UsageContent04Tests|FullyQualifiedName~MarkdownViewTests'`。Expected：代码区吞竖滚轮、正文／控件反复重建、无整条复制／用时或说明仍为普通标题。
- [ ] **Step 3 — 实现增量正文和计时。** 主窗口维护 MessageId→HomeMessageView 映射，不每次清空 HistoryMessages；正常轮询调用所选运行时 SnapshotForPresentation，纯跳秒不增加历史排序时间／完整正文修订。保留向上阅读位置和底部跟随规则。
- [ ] **Step 4 — 实现消息操作和布局。** 消息顶部复制图标复制完整原始 Text，生成中复制已收到内容，原代码块复制仍只复制代码。代码区竖滚轮路由到外层。输入下操作行移除清空，把“导出”放最右；ExportClick 在打开保存对话框前冻结当前运行时采样快照，Task 4 已负责 HTML 用时，不重复测时间。
- [ ] **Step 5 — 更新使用说明并 GREEN。** 移除主窗口两条常驻教学文案；说明分段加粗标题，覆盖空白配置、双资格／语音独立准入、图片主体预览及叉删除、批量点击行为、复制 Markdown、导出及重试计时。沿用学习用途声明。重跑 Step 2，补 Review Focus 4 的生成中导出冻结／后台查看计时以及 Review Focus 2 的 A 生成、B 编辑／复制场景；运行完整回归。Expected：全部零失败。
- [ ] **Step 6 — 提交。** `git commit -m "fix: keep main chat scrolling and message actions stable"`。

### Task 9: 辅助三排铺满、统一轮廓进度及用时展示

**Files:** Create `src/ExamHelper.App/Controls/RoundedPerimeter.cs`、`DwellOutline.cs`、`AuxiliaryDwellButton.xaml`（同目录）。Modify `src/ExamHelper.App/Overlay/{OverlayWindow.xaml,OverlayWindow.xaml.cs,OverlayViewModel.cs}`；`src/ExamHelper.App/Controls/{DwellBehavior.cs,ModelSelector.xaml,ModelSelector.Hover.cs,MarkdownWpfRenderer.cs,AnswerView.xaml,AnswerView.xaml.cs}`。Test: 新增 `tests/ExamHelper.Windows.Tests/App/AuxiliaryLayout04Tests.cs`、`DwellOutline04Tests.cs`，更新 `OverlayRevision03Tests.cs` 中被本版取代的模型包装／布局断言。

**Interfaces:**
- Consumes Task 2 的灰色模型与命令资格、Task 4 的运行时采样、Task 8 的 ResponseTimingFormatter。
- Produces `RoundedPerimeter.Build(Rect bounds,double cornerRadius,double progress): Geometry`，progress 限制 0～1，半径不大于边长一半；起点为顶部中心，顺时针，按实际周长匀速。圆角周长 `2*(w+h)-8*r+2*PI*r`，弧段不能当成直边或在角上跳跃。
- `DwellOutline : FrameworkElement` 暴露依赖属性 `Progress,Stroke,StrokeThickness,CornerRadius`，默认线宽 2 DIP；`Focusable=false,IsHitTestVisible=false`。以按钮框外侧半线宽的轮廓绘制，留足外边距防裁剪。
- `AuxiliaryDwellButtonStyle` 为共享样式键，模板绑定既有 DwellBehavior.Progress／ProgressBrush，不另起触发计时器。普通色沿用 SeaGreen，主页／隐藏沿用红色，中央图片删除环不替换。
- AnswerView 增加 `TimingText: string` 依赖属性；该属性更新只改计时标签，不重建 Segments。OverlayViewModel 轮询使用 SnapshotForPresentation，保留已有 AnswerMessageViewModel 身份。

- [ ] **Step 1 — 写失败测试。** 展开窗口 480×360／640×720 及中间宽度，第一排六项、第二排三项、第三排五项各自横向占满，发送可达；有草稿图和合法重试状态也不遮挡。所有可见辅助悬停按钮的模板含 DwellOutline，不再是底部 ProgressBar；未通过模型无触发进度。

```csharp
Assert.True(RoundedPerimeter.Build(new Rect(0, 0, 120, 36), 5, 0).IsEmpty());
var full = RoundedPerimeter.Build(new Rect(0, 0, 120, 36), 5, 1);
Assert.Equal(new Rect(0, 0, 120, 36), full.Bounds);
Assert.Equal(1d, DwellBehavior.GetDelay(ordinaryButton));
Assert.Equal(2d, DwellBehavior.GetDelay(homeButton));
Assert.Equal("用时 12.4 秒", answerView.TimingText);
```

- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter 'FullyQualifiedName~AuxiliaryLayout04Tests|FullyQualifiedName~DwellOutline04Tests'`。Expected：WrapPanel 留白、不等宽、底条仍存在、用时未显示或跳秒导致正文重建。
- [ ] **Step 3 — 实现轮廓和按钮模板。** 顶部／底部主操作各用等宽自适应布局；条件操作独立放在主行之后。辅助普通按钮、选择入口／菜单项、代码复制按钮显式采用共享样式，主窗口按钮不套用悬停样式。绘制几何仅消费既有进度，保持一秒／两秒、离开重置、防连触发及移动缩放暂停协议。
- [ ] **Step 4 — 实现辅助展示。** AnswerView 的正文和计时分别更新；隐藏／显示后从运行时采样恢复正确用时。提示标题改“快捷键提示”、草稿标签改“语音草稿”，移除冗余的操作解释；保留短任务状态及实际错误，不移除用户需要判断的验证状态。模型列表仍同 HWND、不抢焦点。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑 Step 2。补 Review Focus 5：长模型列表、有未完成配置、禁用项、窄窗错误状态；用控件真实尺寸检查外轮廓不裁剪，指针不动时新选项移动过来不连触发。代码复制、图片中央删除环及消息滚轮仍符合旧规则。实际双屏／捕获保护留至 Task 11。Expected：针对及全套零失败。
- [ ] **Step 6 — 提交。** `git commit -m "feat: fill auxiliary action rows and trace dwell outlines"`。

### Task 10: 白底圆角铅笔资源与安装完成页对齐

**Files:** Create `src/ExamHelper.App/Assets/app-icon-source.jpg`（复制已批准文档参考原图）、`src/ExamHelper.Windows/Native/ApplicationIdentity.cs`、`packaging/FinishedPageOptions.iss`、`tools/InstallerVerification/FinishedPageOptionsProbe.iss`。Modify `scripts/Generate-AppIcon.ps1`；`src/ExamHelper.App/Assets/{app-icon.ico,app-icon.png}`；`src/ExamHelper.App/{ExamHelper.App.csproj,App.xaml.cs,app.manifest}`；`packaging/ExamHelper.iss`；`scripts/{Publish,Build-Installer}.ps1`。无引用后移除旧 `src/ExamHelper.App/Assets/app-icon-source.png`。Test: 新增 `tests/ExamHelper.Windows.Tests/App/IconAssets04Tests.cs`；继续使用 `tools/InstallerVerification/ShortcutOwnershipProbe.iss`。

**Interfaces:**
- Consumes Task 1 的独立身份、数据系列和本版路径；输入原图来自 `docs/superpowers/specs/assets/examhelper-0.4/app-icon-reference.jpg`，不能从渐变旧图生成。
- 图标生成脚本保留 `-SourceImage/-OutputDirectory`，默认新 JPG；每尺寸按完整原图等比绘制，圆角半径为边长 18%，圆角外透明、内部白底，输出 16／24／32／48／64／128／256 ICO 及 256 PNG。
- `ApplicationIdentity.Apply(): void` 在创建窗口前设置 AppUserModelID 为 `ExamHelper.Desktop.0.4`；保持 WPF PerMonitorV2，不扩大已有 NoWarn。
- 正式 0.4 AppId 固定为 `{A0E88B39-9B45-490D-A4A0-C5C731A40400}`；默认目录 `Programs\ExamHelper-0.4`；快捷方式所有权键为 `Software\ExamHelper\Installer\0.4`，不读取旧版所有权作为覆盖许可。
- FinishedPageOptions 提供 `InitializeFinishedOptions()`、`ShowFinishedOptions(Integer pageId)`、`CommitFinishedOptions(): Boolean`；两个同类 TNewCheckBox、同 Left／Width／Font／Height，初始均 Checked=True。保留唯一 `[Run]` 项，描述为“启动应用”，Flags 为 nowait postinstall skipifsilent；隐藏其原生可见 RunList，在点击完成时把启动选择同步到 `RunList.Checked[0]`，让 Inno 正常执行，不自建后台启动协议。

- [ ] **Step 1 — 写失败测试／探针。** 解码生成 PNG，断言角 alpha=0、内部白色不透明、7 个 ICO 尺寸齐全且源 JPG 与文档附件散列一致。实际加载窗口／托盘资源检查同一资产；Inno 探针实例化真实完成页控件，断言两行坐标和默认勾选、取消后的 Commit 行为，而不是只搜索脚本文本。
- [ ] **Step 2 — 观察 RED。** Run: `& $taskDotnet test tests/ExamHelper.Windows.Tests -c Release --no-restore --filter FullyQualifiedName~IconAssets04Tests`；Expected：仍是渐变旧图／方角／新资产缺失。Inno 验证目录固定为本计划账本下的 installer-probe，开工后设置 `$taskProbeDir = Join-Path (Get-Location) '.superpowers/sdd/2026-10-18-examhelper-0.4-implementation/installer-probe'` 并创建目录。Run: `& 'D:/Code/ExamHelper/.tools/InnoSetup6/ISCC.exe' "/DVerificationOutput=$taskProbeDir" tools/InstallerVerification/FinishedPageOptionsProbe.iss`；编译成功后 Run: `Start-Process -FilePath (Join-Path $taskProbeDir 'FinishedPageOptionsProbe.exe') -WindowStyle Hidden -Wait -PassThru`。Expected：新 include 尚缺失或真实 VCL 控件对齐／默认值检查失败；以探针 result.json 的失败作为 RED，不把刻意不安装的进程退出码等同功能失败。探针在 InitializeWizard 中直接调用共享布局与提交函数，写 JSON 后退出；桌面目标与所有权键使用本探针替身，不能碰真实桌面。
- [ ] **Step 3 — 实现统一资源。** Run: `powershell.exe -NoProfile -STA -File scripts/Generate-AppIcon.ps1`；Expected：生成七尺寸和 PNG。检查所有 XAML、EXE、托盘、安装／卸载入口只引用新资源，源文件不裁主体、不重画；设置 ApplicationIdentity，版本默认／manifest 更新到 0.4.0／0.4.0.0。
- [ ] **Step 4 — 实现完成页。** 两个可见复选框使用同类控件和统一 DPI 缩放坐标；初始化默认值只设置一次，页面再次显示不覆盖用户取消。提交先处理所选桌面链接，再同步原生启动项。创建链接失败保持向导和勾选供用户取消重试，不先启动应用。交互默认都选，静默验证保持 skipifsilent 且不创建桌面链接。所有权哈希检查仍保护无关／修改过的同名链接。
- [ ] **Step 5 — GREEN 和完整回归。** 重跑图标测试、图标生成及两个 Inno 探针，Expected：资源与 result.json 通过，探针只触及隔离目录／测试键。补 Review Focus 5 的 125%／200% VCL 控件尺寸／两行对齐断言。完整回归零失败；真实任务栏、托盘和完成页点击在 Task 11 记录，不能仅靠坐标探针宣布人工验收。
- [ ] **Step 6 — 提交。** `git commit -m "feat: unify rounded pencil identity and installer finish options"`。

### Task 11: 完整验收、一次独立审查及 0.4.0 发布

**Files:** Create `tools/ExamHelper.InteractionLab/HomeInteractionVerification.cs`、`DisplayVerification.cs`；`docs/verification/revision-0.4.0.md`。Modify `tools/ExamHelper.InteractionLab/{App.xaml.cs,ProductCaptureFixture.cs,NativeVerification.cs,TrayVerification.cs,ExamHelper.InteractionLab.csproj}`；按需更新 `scripts/Test.ps1` 的结果汇总，不改变失败退出语义；更新 README。交付文件写到 `D:/Code/ExamHelper/artifacts`。

**Interfaces:**
- Consumes Task 1～10 已定义接口和最终源图，不能为了验收走另一套宽松资格或图片预览代码。
- 新增受控参数 `--verify-home <outputDir>` 和 `--verify-displays <outputDir>`，分别调用 `HomeInteractionVerification.RunAsync(string): Task`、`DisplayVerification.RunAsync(string): Task`。保留 `--verify-product/--verify-tray`。夹具使用测试配置、假 AI／ASR、自有 PNG 和临时 AppDataLayout，不调用正常用户配置启动路径。
- 输出结构化 JSON，含 Passed、实际执行项目、DPI、图像尺寸／差异、无网络及数据隔离说明；原生事件与物理鼠标／键盘结果分开记载。验证工具不进入发布目录。

- [ ] **Step 1 — 编写受控综合检查并全套回归。** 覆盖空白启动→半填保存→按模型测试→进入辅助→截图入草稿→返回主页→预览保存→复制／导出→批量失败重试；所有网络为假服务。Run: `./scripts/Test.ps1 -Configuration Release -DotnetPath $taskDotnet`。Expected：锁定恢复、构建零错误，测试数大于 0 且零失败／跳过，记录真实总数与警告，不沿用 254。
- [ ] **Step 2 — 执行本机受控原生验收。** Run: `& $taskDotnet run --project tools/ExamHelper.InteractionLab -c Release --no-build -- --verify-product D:/Code/ExamHelper/artifacts/0.4-native-check`；对 `--verify-home .../0.4-home-check`、`--verify-displays .../0.4-display-check`、`--verify-tray .../0.4-tray-check` 分别运行。Expected：各 result.json 明确通过；捕获只对自有参考画面，保护读回 17、无意外可捕获子窗口；真实负坐标双屏 125%／200% 下预览四角、原图保存、三排和轮廓均正确。
- [ ] **Step 3 — 人工入口与 HTML 验收。** 在自有图片内容上实际 Ctrl＋C／Ctrl＋V、拖入、代码上方滚轮、原图缩放／保存及辅助悬停；检查任务栏和托盘图标，物理单击／右键托盘。浏览器打开合成导出，图片解码且 externalAssets=0、scripts=0，计时冻结、图片按消息归属。仅用明确可用工具操作；授权／工具不可用则标记对应项目未实测，保留测试材料，不绕过授权，不把程序化事件改称物理交互通过。
- [ ] **Step 4 — 最终一次独立完整增量审查。** 从产品基线 b46beff 至当前 HEAD 生成 review-package；以 requesting-code-review 模板给最强可用模型一份干净上下文，附本文 Review Focus 原文和账本 Ruling 行。若模型额度错误且未给结论，记录后改用可用模型完成唯一一次审查。Critical／Important 排级后同轮补 RED→GREEN；Minor 仅记录延期，Declined to judge 每项裁定，不重复派发重审。修复后完整回归，Expected：所有必要问题有真实失败及通过证据。
- [ ] **Step 5 — 提交最终产品与验证记录。** README／验证文档写清本版的新准入、全新数据、人工边界、准确测试数、取舍和延期项。Expected：产品及文档已提交，`git status --porcelain` 为空；不推送、不合并、不改用户原安装。保留托管工作树。
- [ ] **Step 6 — 干净提交发布并离线自检。** 依次运行：

```powershell
./scripts/Publish.ps1 -Version 0.4.0 -DotnetPath $taskDotnet -OutputDirectory D:/Code/ExamHelper/artifacts/publish/win-x64-0.4.0
./scripts/Build-Installer.ps1 -Version 0.4.0 -IsccPath D:/Code/ExamHelper/.tools/InnoSetup6/ISCC.exe -PublishDirectory D:/Code/ExamHelper/artifacts/publish/win-x64-0.4.0 -OutputDirectory D:/Code/ExamHelper/artifacts/installers
```

Expected：生成完整自包含目录及 `ExamHelper-Setup-0.4.0-x64.exe`／`.sha256`；build-info 的 Version=0.4.0、Dirty=false、GitCommit=实际发布提交。随后执行发布目录内的应用 EXE 自检，不能把应用参数传给安装器：

```powershell
$taskSelfTest = Start-Process -FilePath 'D:/Code/ExamHelper/artifacts/publish/win-x64-0.4.0/ExamHelper.App.exe' -ArgumentList '--self-test D:/Code/ExamHelper/artifacts/0.4-self-test.json' -WindowStyle Hidden -Wait -PassThru
if ($taskSelfTest.ExitCode -ne 0) { throw '0.4.0 离线自检失败。' }
Get-Content -LiteralPath 'D:/Code/ExamHelper/artifacts/0.4-self-test.json' -Raw | ConvertFrom-Json
```

Expected：Passed=true、NetworkRequests=0、UserSettingsRead=false。

- [ ] **Step 7 — 最终二进制的普通用户安装／卸载验证。** 先查安装身份冲突，再复制正式 Inno 源脚本至本计划临时目录，使用独立测试 AppId、安装目录、开始菜单／桌面名、所有权键；仅把验证副本的 `[Run]` 参数改为 `--self-test`，不读取真实数据。使用最终发布目录编译它，检查真实完成页两项默认勾选与对齐，分别取消／保留四种组合，核对只执行所选动作；普通用户新装、同系列重装及卸载退出 0，现有程序与旧文件未修改。本版不安排 0.3→0.4 数据升级测试。Expected：日志／JSON 包含安装后的准确发布提交；工具未授权的真实点击明确记未测，静默流程只证明安装流程本身。
- [ ] **Step 8 — 精确源码与交付。** 从实际 build-info 取提交导出源码，不把当前工作目录打包：

```powershell
$taskBuildInfo = Get-Content -LiteralPath 'D:/Code/ExamHelper/artifacts/publish/win-x64-0.4.0/build-info.json' -Raw | ConvertFrom-Json
if ($taskBuildInfo.Dirty -or $taskBuildInfo.Version -ne '0.4.0') { throw '发布信息无效。' }
$taskReleaseCommit = $taskBuildInfo.GitCommit
git archive --format=zip --output=D:/Code/ExamHelper/artifacts/ExamHelper-Source-0.4.0.zip $taskReleaseCommit
if ($LASTEXITCODE -ne 0) { throw '源码归档失败。' }
Get-FileHash -LiteralPath 'D:/Code/ExamHelper/artifacts/ExamHelper-Source-0.4.0.zip' -Algorithm SHA256
```

Expected：源码归档及 SHA-256 生成成功；核对 ZIP 无真实设置、历史、录音、临时构建目录。写 `交付说明-0.4.0.md`、`验证记录-0.4.0.md`、`release-0.4.0.json`，复核安装包及源码散列，保留 0.3 包。若发布后再修改产品，须重新提交、回归并发布，不能让源码与安装器对应不同产品提交。
- [ ] **Step 9 — 最终门禁及归档。** 执行本计划 task-done 的最终全套测试门禁，Expected：真实全套零失败，所有需要的功能／验收证据已记录。完整归档本计划账本、审查结果和日志后，仅清理本计划临时目录；保留托管工作树。最终回复列出所有 Ruling 及其代价、延期 Minor（没有则说明无），链接实际安装包和验证文档，不把尚未做的人工检查声称完成。

## 任务依赖与需求核对

| 顺序 | 可独立验收的交付 | 主要前置 |
| --- | --- | --- |
| 1 | 本版独立路径、可保存空配置、无幽灵模型 | 已认可设计 |
| 2 | 双测试资格与功能级禁用，进入仅看保护 | 1 的完整性／模型数据 |
| 3 | 稳定创建时间、排序及历史条目身份 | 1 的新数据隔离 |
| 4 | 每尝试单调计时、持久化与 HTML 元信息 | 3 的稳定消息时间 |
| 5 | 可预览批量选择、部分失败及删除后切换 | 3 的历史集合／排序 |
| 6 | 图片 Ctrl＋V 路由、主体预览／叉删除 | 1 的临时路径、5 的删除安全 |
| 7 | DPI 正确完整原图及原子 PNG 保存 | 6 的预览入口 |
| 8 | 主窗口稳定正文、复制／滚轮、导出／说明 | 3／4／5／6／7 |
| 9 | 辅助三排铺满、外围进度及计时 | 2／4／8 的资格、时间格式 |
| 10 | 统一图标、全新安装身份和完成页 | 1 的身份、9 的窗口资源 |
| 11 | 真机、独立审查、准确提交发布 | 1～10 |

| 原始需求 | 负责实现／验收的任务 |
| --- | --- |
| 1 统一图标；2 完成页默认和对齐 | 10／11 |
| 3 代码滚轮；5 选中闪动；6 排序和 UTC＋8 | 3／8／11 |
| 4 草稿预览／叉删除／粘贴；7 四分之一原图；8 保存 | 6／7／11 |
| 9 条目图标及批量删除 | 5／11 |
| 10 三排铺满；15 外轮廓进度 | 9／11 |
| 11 密钥顺序；13 全新空白／模型残留 | 1／11 |
| 12 文案与说明加粗；16 整条复制及导出布局 | 8／9／11 |
| 14 AI 双资格、语音独立禁用及辅助准入 | 2／11 |
| 17 实时计时、结束保存、重试从零、HTML | 4／8／9／11 |

## 计划自检与交接

按设计第 1～16 节及 17 项对照表核对覆盖：数据隔离、未完成配置、两类证据、UTC＋8、草稿排序、批量点击及部分失败、复制原始 Markdown、完整原图保存、辅助外围进度、计时尝试身份和安装完成页四种勾选组合均有负责的任务和验收。共享方法以各任务 Interfaces 为准，后续任务只消费已产出的接口；未定义的测试辅助对象须在对应测试文件中实现，不把伪代码当作已有 API。

本计划包含全部待执行步骤，没有任何“已开发／测试通过”的声明。目前仅供用户检查。用户认可计划并允许开始后，按既有当前会话顺序执行方式推进；在此之前不创建产品／测试源文件、不生成图标、不安装依赖、不运行实现命令。
