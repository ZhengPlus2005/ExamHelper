# ExamHelper 0.2.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax.

**Goal:** 实现用户九项修订并生成 0.2.0 当前用户安装器。

**Architecture:** 保留 Core／Windows／WPF 分层；配置目录提供有效 AI 快照，屏幕选择由 native HWND 定位；设置编辑模型负责未保存与每配置证据，小窗仅承载显示／命令。

**Tech Stack:** C# .NET10 WPF、现有 NAudio／SQLite、Inno6.7.3，无新增依赖。

**Spec:** docs/superpowers/specs/2026-10-17-examhelper-0.2-design.md

## Global Constraints
- 不读取／改写真实用户凭据、历史来执行验证。
- 不重编号旧 AppCommand；保留历史数据和原图，保留保护／资格门槛。
- 安装当前用户，版本0.2.0，旧 AppId 保持；源码保留当前已附加隔离工作树。
- 使用原生矢量图标和合成答题预览，不将用户完整浏览器截图分发。

## Review Focus
- 测试 A 配置过程中切换／修改／删除 B 或 A，结果只归属原配置，未验证不能开始。
- 关闭设置恰逢防抖保存或服务测试，取消关闭不丢字段，失败保存不关闭。
- 负坐标屏／不同 DPI／隐藏窗口／拔屏后，截图仍指向所属屏，移动不被主屏限制。
- 非 float 原生麦克风／扩展 PCM／静音样本的音量显示正确，不增加录音残留。
- 完成页创建快捷方式可失败／被取消／静默升级，不能删无关桌面文件或用户历史。

### Task 1: 多配置目录、迁移和独立资格
**Files:** Core/Settings/{SettingsTypes,AiProfileCatalog,ServiceVerification}.cs；Windows/Settings/JsonSettingsStore.cs；Core/Windows 设置测试。
**Interfaces:** AiServiceProfile；AppSettings.AiProfiles／ActiveAiProfileId；AiProfileCatalog.Normalize(AppSettings)；保留有效 Ai 字段供现有客户端。
- [x] 写旧配置导入、两配置切换资格独立、修改非启用配置不影响当前的失败测试并运行 RED。
- [x] 实现目录规范化、JSON 验证和密钥摘要刷新；运行针对测试 GREEN 与全套回归。
- [x] 提交。

### Task 2: 多配置设置 UI 与关闭未保存保护
**Files:** App/Settings/{AiProfileEditor,SettingsViewModel,SettingsWindow}.cs/xaml；Composition；App 设置测试。
**Interfaces:** SelectedAiProfile、ActivateSelected／AddProfile／DeleteSelected；HasUnsavedChanges；RequestCloseAsync(Func<Task<SettingsCloseChoice>>,ct)。
- [x] 写新增／启用／删除／独立测试（含测试中换配置／删除），以及保存／丢弃／取消／失败保留测试 RED。
- [x] 实现独立密钥编辑、列表与状态，关闭异步保护；原有提示词防抖仍可用。运行 GREEN 与全套。
- [x] 提交。

### Task 3: 不透明度与音量显示
**Files:** App/Controls/OpacityPreview.xaml；Settings；Windows/Audio/AudioPeakMeter.cs、MicrophoneRecorder.cs；对应测试。
**Interfaces:** OpacityPercent；AudioPeakMeter.Measure(samples,format)；MicrophoneLevel 映射到0～1。
- [x] 写百分比／预览绑定、PCM16/24/32/float/静音样本映射测试 RED。
- [x] 实现合成答题双层预览和样本峰值／dB映射，运行 GREEN／完整回归并渲染查看。
- [x] 提交。

### Task 4: 小窗提示、隐藏、进度与滚轮
**Files:** Core Interaction/Settings命令；App Overlay、Controls滚轮行为；对应状态和 WPF 测试。
**Interfaces:** HideOverlay 追加；HintButton 同 HWND 面板；两秒 Delay，红／绿前景；滚轮行为不安装 HoverScrollBehavior。
- [x] 写隐藏保留请求／快捷键、提示离开关闭、无悬停滚动命令、滚轮改变 offset 回归 RED。
- [x] 实现按钮／快捷键迁移与展示；测试 GREEN 全套。
- [x] 提交。

### Task 5: 多屏物理捕获和位置
**Files:** Windows/Capture/FullScreenCapture.cs、Native/MonitorGeometry.cs；App窗口交互与Composition；UiSettings；对应测试。
**Interfaces:** IScreenCaptureBackend.GetWindowDisplay(hwnd)；FullScreenCapture 后端+窗口回调；NativeWindowBounds 和可选 Ui.Placement。
- [x] 写两屏所属 HWND、负坐标原像素、取消／无所属窗拒绝与拔屏恢复数学测试 RED。
- [x] 实现 MonitorFromWindow／GetMonitorInfo、物理移动／缩放与位置恢复；运行 GREEN 全套、已有受控捕获。
- [x] 提交。

### Task 6: 图标、完成页快捷方式、发布
**Files:** Assets/app-icon.svg/png/ico；窗口icon及csproj；packaging/ExamHelper.iss；scripts/Generate-AppIcon.ps1；说明／验证记录。
- [x] 生成／检查矢量与多尺寸图标，添加完成页可选快捷方式及自身卸载项。
- [x] locked restore、Release build/test；独立全分支审查，必要问题一轮RED→GREEN。
- [ ] 提交最终代码／说明后从干净提交发布0.2.0，生成安装器／校验、自检；不覆盖0.1.0。
- [ ] 记录无法实测的硬件／安装向导路径，不冒充全部通过，交付。
