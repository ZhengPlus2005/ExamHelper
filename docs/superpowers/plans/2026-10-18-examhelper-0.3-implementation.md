# ExamHelper 0.3.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax. 用户已选当前会话顺序实施；本计划与设计一并等待最后开工确认，确认前不实施。

**Goal:** 实现已逐项确认的多模型聊天工作区、辅助改版、每对话草稿与后台任务、Markdown／HTML、托盘及0.3.0安装包。

**Architecture:** ConversationWorkspace持有每对话SessionCoordinator，AiGenerationGate限制全应用一个生成；选中对话和后台生成解耦。保留Core／Windows／WPF，AppLifecycle统一收起、返回和保存后退出，Markdown及辅助选择器由原生WPF呈现。

**Tech Stack:** .NET10／WPF、SQLite、NAudio、Markdig1.4.1、AvalonEdit6.3.1.120、InnoSetup6.7.3。

**Spec:** docs/superpowers/specs/2026-10-18-examhelper-0.3-design.md；基础提交9cb6c044f6f7165f957caa186fafcf658f0655c1。以下均为未执行步骤。

## Global Constraints

- 仅Windows11 x64，当前用户普通权限；保留捕获保护、所属屏全屏截图及完整原图。
- 主窗口聊天不要求任何测试；辅助只选图片测试通过模型，进入还须语音验证。
- 全应用同时一个AI回答；视图切换不取消后台AI／识别，结果不得串对话。
- 草稿按对话保存，不转为消息；退出保存失败不得关进程。主窗口Enter发送、Shift＋Enter换行，辅助不接受键盘文本。
- 移除独立隐藏快捷键，保留F9；新装50%，旧值保留；禁止重编号既有AppCommand。
- 思考档位不逐档重测；不支持的字段不得发送。默认值不被会话选择覆盖。
- 不读取真实用户凭据／历史验证，不进行付费调用；最终确认前不安装依赖或修改代码。
- 迁移先备份并事务提交，失败留原数据；只更新本轮明确取代的旧测试断言。
- 最终一次独立完整增量审查，必要问题一轮RED→GREEN；不反复派发重审。
- 下文dotnet指D:\Code\ExamHelper\.tools\dotnet\dotnet.exe；每任务针对测试GREEN后还须完整回归。

## Review Focus

1. A生成与B编辑、导图、重命名交错，不串写回复／草稿／标题／引用：Task3／4／5。
2. 改或删配置恰逢请求准备、测试、拉模型，保持快照和原凭据绑定，不自动换服务：Task1／2。
3. 返回主页时识别未完，之后切B或退出保存失败，结果回A且失败可重试：Task3／8。
4. 125%／200%负坐标屏，长模型列表、Markdown和缩略图：同HWND保护、不抢焦点、发送可达：Task6／7／10。
5. 旧库、缺图、危险链接、导出中断：备份及原文件保留、HTML不加载外图、不导出草稿：Task4／6／9。

## Shared interfaces

Core/Settings/ModelCatalog.cs定义：
- AiModelProfile：Id(Guid)、Name(真实模型ID)、DefaultThinking、CapabilityOverride、DiscoveryMetadata(ModelMetadata可空)、VerifiedFingerprint。
- ModelSelection(Guid ProfileId,Guid ModelId,ThinkingChoice Thinking)，另存ProfileName及ModelName供配置删除后显示原名；ThinkingChoice包含Kind(ServiceDefault／Enabled／Disabled／Effort)及可选Effort。
- ThinkingCapability：控制种类、请求格式和支持档位，区分开关、档位、开关＋档位及thinking对象／reasoning_effort请求。
- ModelMetadata：模型ID、可空图片／推理标志及可空档位；信息缺失不能解释为任意支持。
- ModelCatalog.Resolve(AppSettings,ModelSelection,bool requireImageVerified,ServiceVerification)返回有效AiSettings，不静默回退。
- AiSettings增加有效思考选项；AiServiceProfile新增Models及DefaultModelId，旧单模型字段仅兼容镜像。

会话快照增加CustomTitle、FirstSentAt、Selection；消息／AiChunk／AiMessage增加可选供应商推理内容，显示Text与内部协议字段分离。默认值兼容旧快照反序列化。

### Task 1: 多模型目录、资格和设置编辑

**Files:** 修改Core/Settings/{SettingsTypes,AiProfileCatalog,ServiceVerification}.cs，Windows/Settings/{JsonSettingsStore,ServiceTestRunner}.cs，App/Settings/{AiProfileEditor,SettingsViewModel,SettingsWindow.xaml}；新增Core/Settings/{ModelCatalog,ThinkingCapability}.cs、App/Settings/ModelEditor.cs；Core／Windows设置测试。

**Interfaces:** 产出Shared interfaces目录和Resolve。DefaultModelId必须引用本配置模型，新增模型服务默认；资格按配置＋模型，地址／密钥影响全部，改某模型ID只影响该项。

- [ ] 写ModelCatalogRevisionTests及ModelSettingsRevisionTests：旧单模型导入、有效旧证据只给原模型、默认切换不污染资格、其他配置草稿不被测试保存；新安装Assert.Equal(0.5,settings.Ui.Opacity)，旧值及省略字段保留原语义；只删HideOverlay绑定，不删其他功能的自定义F12。
- [ ] 运行dotnet test ExamHelper.slnx -c Release --filter "FullyQualifiedName~ModelCatalogRevisionTests|FullyQualifiedName~ModelSettingsRevisionTests"，确认RED并记录真实失败。
- [ ] 实现格式3迁移、模型行、默认选择和手工思考覆盖，移除服务类型。保留独立测试／凭据事务／关闭确认。导入旧K2.6明确禁用行为，新增模型服务默认。
- [ ] 针对测试GREEN及完整回归；覆盖测试A时换选择、改密钥、删模型，结果不得归给新内容。
- [ ] 提交feat: add model catalogs and per-model settings。

### Task 2: 模型发现、思考请求和参数冻结

**Files:** 新增Windows/Providers/{ModelDiscoveryClient,ThinkingRequestWriter,FrozenSecretStore}.cs；修改ChatCompletionsClient.cs、ProviderEndpoint.cs、ProviderErrorMapper.cs、Core/Ai/AiContracts.cs、ContextBuilder.cs及设置拉取入口；测试ModelDiscoveryTests.cs、ThinkingRequestTests.cs。

**Interfaces:** IModelDiscovery.ListAsync(AiSettings endpoint,string apiKey,CancellationToken)返回Task<IReadOnlyList<ModelMetadata>>。IAiClient新增默认PrepareAsync(AiSettings,CancellationToken)返回Task<IAiClient>，默认返回this兼容假实现，真实客户端返回冻结凭据的客户端；原StreamAsync保留。

- [ ] 写失败测试：K3仅传reasoning_effort的low／high／max，K2.6只传thinking.type；默认省略，未知不伪造档位，手工覆盖优先；SSE推理片段不会被误作无活动，必要响应信息可保存回传。
- [ ] 运行dotnet test tests/ExamHelper.Windows.Tests -c Release --filter "FullyQualifiedName~ModelDiscoveryTests|FullyQualifiedName~ThinkingRequestTests"，确认RED。
- [ ] 实现/models规范化、元数据及原配置绑定，勾选才加入；PrepareAsync在建立请求工作前冻结密钥。失败显示状态码／服务错误码及可读信息，不泄漏凭据。
- [ ] 假HTTP断言请求体和授权头GREEN：Prepare后改／删配置不改旧调用，拉取中切配置或改地址不覆盖新表单；完整回归。
- [ ] 提交feat: discover models and freeze provider requests。

### Task 3: 每对话运行时和应用级单生成

**Files:** 新增Core/Sessions/{ConversationWorkspace,AiGenerationGate,SessionCoordinator.Home}.cs；按职责拆分SessionCoordinator.cs；修改SessionTypes.cs、CommandPolicy.cs、DraftBuffer.cs、Audio/SpeechCoordinator.cs；新增ConversationWorkspaceTests.cs、BackgroundConversationTests.cs及TestSupport/ConversationWorkspaceRig.cs。

**Interfaces:** ConversationWorkspace(ISessionStore,Func<SessionCoordinator> factory)；NewAsync(bool auxiliary,ct)、OpenAsync(Guid,ct)返回SessionCoordinator；Selected、AuxiliarySessionId、GeneratingSessionId；FlushAllAsync(ct)、ReturnHomeAsync(ct)、ExitAsync(ct)。factory注入同一AiGenerationGate。Coordinator增加UpdateDraftTextAsync(string,ct)、AppendImagesAsync(IReadOnlyList<ImageRef>,ct)、SelectModelAsync(ModelSelection,ct)、RenameAsync(string,ct)。

- [ ] 写可控任务测试：A生成时选B编辑，Assert.Equal(aId,workspace.GeneratingSessionId)，增量只进A，B发送立即禁止且不排队；A完成可发B。返回主页停录提交识别，选B后结果仍进A；切模型只改下一次请求。
- [ ] 运行dotnet test tests/ExamHelper.Core.Tests -c Release --filter "FullyQualifiedName~ConversationWorkspaceTests|FullyQualifiedName~BackgroundConversationTests"，确认RED。
- [ ] 实现按需运行时、每会话串行变更及带身份的全局租约；文字／图片服务测试也使用该租约，模型GET拉取不占用。视图关闭不销毁任务，后台有效性基于session/request/generation，不依赖选择或辅助标记。内容、模型、凭据及租约在开始时冻结；新对话统一走workspace并重新绑定。各运行时独立管理临时录音和识别，不能复用同一个会被另一会话Cancel删除的录音对象。
- [ ] GREEN及全套：迟到取消不释放新租约，重试用当前选择及原问题，停止／清空／退出失败不覆盖新草稿，保留60秒录音及撤销。
- [ ] 提交feat: keep conversation work independent of the visible view。

### Task 4: 迁移、标题和草稿持久化

**Files:** 新增Windows/Persistence/SessionMigration.cs、Core/Sessions/ConversationTitle.cs；修改DatabaseSchema.cs、SqliteSessionStore.cs、RequestRecovery.cs、ImageReferenceCleaner.cs及存储调用；测试ConversationMigrationTests.cs、ConversationTitleTests.cs。

**Interfaces:** ConversationTitle.Get(SessionSnapshot)；默认“对话 yyyy-MM-dd HH:mm:ss”，无首发“对话（未发送）”，CustomTitle优先。SessionMigration.EnsureAsync(SqliteConnection,string databasePath,ct)，一致性备份后事务更新版本2。

- [ ] 建临时旧库失败测试：保留旧历史、草稿、原图和失败请求；首发来自第一条用户消息，重试不改时间，手工标题不被流式保存覆盖；仅消息及草稿皆空才移除；备份／迁移失败原库不破坏。
- [ ] 运行dotnet test ExamHelper.slnx -c Release --filter "FullyQualifiedName~ConversationMigrationTests|FullyQualifiedName~ConversationTitleTests"，确认RED。
- [ ] 实现版本保护、备份和标题缓存；文本500ms防抖，切换及退出强制flush，切换失败保持原会话。清理器合并持久化与内存草稿／待提交图片引用，不能提前删导图。
- [ ] GREEN及全套：A流式＋改名不回滚名称，原图只在无引用后清理，空白辅助退出不留历史；全过程只用测试数据库。
- [ ] 提交feat: persist per-conversation drafts and time-based titles。

### Task 5: 主页聊天、输入和图片

**Files:** 修改App/Home/{HomeViewModel,HomeWindow.xaml,HomeWindow.xaml.cs}、Composition/AppComposition.cs；新增Home/{ConversationMessageViewModel,HomeDraftController,HomeInputBehavior}.cs，Windows/Images/ImageImportService.cs、App/Images/ImageViewerWindow.xaml/cs、Controls/ModelSelector.xaml/cs；测试HomeChatTests.cs、ImageImportTests.cs。

**Interfaces:** ImageImportService.ImportFilesAsync(IEnumerable<string>,ct)与ImportBitmapAsync(BitmapSource,ct)返回Task<IReadOnlyList<ImageRef>>，调用方捕获目标Coordinator。ModelSelector接收目录、ModelSelection、是否限制已验证项及交互方式，不直接发API。Composition将Task3同一个生成租约注入对话与服务测试。

- [ ] 写WPF／假服务失败测试：主页没有任何验证也能发、无ASR不阻断，Enter发／ShiftEnter换行／输入法组合中不发；粘贴拖放只入原会话草稿。图片与所属消息的文字顺序正确。
- [ ] 运行dotnet test tests/ExamHelper.Windows.Tests -c Release --filter "FullyQualifiedName~HomeChatTests|FullyQualifiedName~ImageImportTests"，确认RED。
- [ ] 实现主页新对话、输入和普通选择，删继续及草稿bool文案；消息先圆角缩略图再正文，图片原像素归一PNG，失败留草稿。
- [ ] GREEN及全套：缩略图打开原图、缩放／平移／Esc，切历史恢复草稿及选择，后台历史列表更新不改变当前选中项，配置已删不回退；开始考试总新建辅助对话，不带主页草稿。
- [ ] 提交feat: chat and manage image drafts in the home window。

### Task 6: 共用Markdown和代码高亮

**Files:** Directory.Packages.props、对应csproj／lock，新增Core/Markdown/MarkdownPipelineFactory.cs、App/Controls/{MarkdownView,MarkdownWpfRenderer,CodeHighlighter}.cs；改AnswerView.xaml/cs及主页模板，更新许可；测试MarkdownViewTests.cs。

**Interfaces:** MarkdownPipelineFactory.Parse(string)返回Markdig.Syntax.MarkdownDocument，DisableHtml且只启用所需表格扩展；MarkdownWpfRenderer.Render(document,bool auxiliary)返回WPF内容；CodeHighlighter用非可视TextDocument生成不可聚焦Run。

- [ ] 写标题、强调、列表、引用、表格、链接、围栏代码与未闭合流式围栏测试；无原始HTML执行和远程图片请求，未知语言原文保留，辅助无可编辑／抢焦点控件。
- [ ] 运行dotnet test tests/ExamHelper.Windows.Tests -c Release --filter FullyQualifiedName~MarkdownViewTests，确认RED。
- [ ] 加Markdig1.4.1、AvalonEdit6.3.1.120，更新锁文件及BSD2／MIT许可。主页、小窗、导出共享语法；小窗代码复制走悬停、主页常规点击，不使用TextEditor作可见控件。
- [ ] GREEN及全套，查看长C++、表格和中文列表渲染；沿用75ms界面更新节流且最终帧必刷新，保留向上翻看的位置，不启用数学、Mermaid或HTML扩展。
- [ ] 提交feat: render markdown and highlighted code in both chat views。

### Task 7: 辅助布局和受保护悬停选择

**Files:** 修改App/Overlay/{OverlayViewModel,OverlayWindow.xaml,OverlayWindow.xaml.cs,OverlayHost}.cs、Controls/ThumbnailStrip.xaml/cs、ModelSelector；新增Controls/HoverModelSelectorBehavior.cs及环形进度绘制；测试OverlayRevision03Tests.cs。

**Interfaces:** 同HWND悬停选择器把选择写回关联Coordinator；OverlayHost绑定AuxiliarySessionId，主页选B不能改截图owner。保留原生窗口保护和不激活策略。

- [ ] 写UI失败测试：删右上指定文字；折叠动作名切换；底部截图／入草稿／录音在清空前；灰罩白圈叉1秒红环、离开复位、只删一张。
- [ ] 运行dotnet test tests/ExamHelper.Windows.Tests -c Release --filter FullyQualifiedName~OverlayRevision03Tests，确认RED。
- [ ] 实现布局、1秒展开／选择及移出关闭，只提供辅助合格模型；移动缩放时暂停选择删除，长列表可滚轮；2秒主页隐藏红进度，其余绿色。
- [ ] GREEN及全套，480×360、折叠、长列表和长回答仍可操作；原生焦点及捕获测试覆盖选择器与Markdown，双屏物理截图回归。
- [ ] 提交feat: refine overlay controls and protected hover model selection。

### Task 8: 托盘、退出、说明和图标

**Files:** 新增App/Lifecycle/{TrayIconService,AppLifecycle}.cs、Help/UsageWindow.xaml/cs；修改App.xaml.cs、HomeWindow、OverlayHost、Composition、Assets/app-icon*、Generate-AppIcon.ps1及打包引用；测试AppLifecycleTests.cs。

**Interfaces:** AppLifecycle.ShowHomeAsync(ct)、ReturnHomeAsync(ct)返回Task，RequestExitAsync(ct)返回Task<bool>；TrayIconService发单击和ExitRequested，成功退出才Dispose托盘。使用WinForms NotifyIcon封装，开启UseWindowsForms但从隐式Using移除System.Windows.Forms／System.Drawing，服务内用显式别名避免WPF歧义。

- [ ] 假托盘失败测试：主页X仅隐藏、任务不取消，单击恢复／辅助时返回，退出取消任务并保存全部草稿和部分回答；保存失败Assert.False(exitSucceeded)，托盘及进程保留；重试成功且重复退出幂等。
- [ ] 运行dotnet test tests/ExamHelper.Windows.Tests -c Release --filter FullyQualifiedName~AppLifecycleTests，确认RED。
- [ ] 实现生命周期，不强杀；返回主页不等识别完成，但结果仍归原会话，主页可处理识别重试。加设置左侧使用说明和设计原文免责声明。
- [ ] 用用户附件转换PNG及七尺寸ICO，覆盖EXE、窗口、托盘和安装器；GREEN及全套，查看实际图标、说明及单击事件，不让双击造成重复返回。
- [ ] 提交feat: add tray lifecycle and refreshed application shell。

### Task 9: 单文件离线HTML导出

**Files:** 新增Core/Export/{HtmlConversationExporter,IMarkdownHtmlRenderer}.cs、Core/Markdown/MarkdownHtmlRenderer.cs、App/Home/ConversationExportController.cs；测试HtmlConversationExporterTests.cs；主页SaveFileDialog及AtomicFile写出。

**Interfaces:** HtmlConversationExporter(IImageStore,IMarkdownHtmlRenderer).BuildAsync(SessionSnapshot,ct)返回Task<string>；IMarkdownHtmlRenderer.Render(string)返回安全HTML并复用Task6解析规则。

- [ ] 写失败测试：消息1的两图位于消息1文字之前，不聚到末尾；所有图片src为data:image/png;base64；有标题时间，无草稿文字／图片；HTML转义、危险链接不激活；缺图／写失败不覆盖既有导出文件。
- [ ] 运行dotnet test tests/ExamHelper.Core.Tests -c Release --filter FullyQualifiedName~HtmlConversationExporterTests，确认RED。
- [ ] 实现冻结快照、内嵌样式和原图、已收到部分回答的导出，编码正文及属性；不导出配置凭据和内部推理字段，不依赖CDN或脚本才能读正文。
- [ ] GREEN及全套，在浏览器查看自有合成文件，断网可读、图片解码和代码排版正确；原记录不改。
- [ ] 提交feat: export selected conversations as self-contained HTML。

### Task 10: 综合验收和0.3.0发布

**Files:** Test.ps1、Publish.ps1、Build-Installer.ps1及版本默认，InteractionLab受控夹具，README、docs/verification/revision-0.3.0.md及交付文件。

- [ ] 执行scripts/Test.ps1 -Configuration Release -DotnetPath D:\Code\ExamHelper\.tools\dotnet\dotnet.exe，锁定恢复／构建／全套零失败、发现测试数大于0，不以0.2历史结果冒充0.3。
- [ ] 按Review Focus验证真实双屏125%／200%、同HWND选择和Markdown保护／不抢焦点、托盘单击右键、中文输入法、粘贴拖放、迁移与退出失败；只捕获自有参考画面，不读真实配置。无法实测项如实记录。
- [ ] 最终独立增量审查，必要问题同轮RED→GREEN及完整回归；提交最终产品和说明，不重复重审。
- [ ] 干净提交发布0.3.0自包含应用／安装器／SHA-256并离线自检。确认无原安装冲突后，独立目录普通权限安装、0.2升级及卸载；人工入口不以静默编译代替。
- [ ] 导出准确提交源码ZIP和交付说明至D:\Code\ExamHelper\artifacts，保留0.2旧包、托管工作树，记录验收边界；未请求推送或合并。

## 文档自检

已按设计第1～12节核对任务覆盖，模型目录／请求冻结／独立运行时／持久化／主页／渲染／辅助／托盘／导出／发布分别归入Task1～10。核对共享类型及异步返回值，补上旧选择显示名称、独立录音所有权和服务测试共享生成限制。没有待定占位，所有执行步骤仍未勾选；本轮目前只完成文档，不是代码或测试通过声明。
