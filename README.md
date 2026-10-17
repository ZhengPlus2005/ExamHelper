# ExamHelper

Windows 11 x64、单屏、C#／.NET 10＋WPF 桌面应用。启动显示历史主页，服务验证及捕获保护通过后进入置顶半透明辅助小窗。辅助模式采用悬停和可配置快捷键，草稿文字只由麦克风语音识别追加，不接受键盘编辑；主页和设置正常点击与输入。

本地预览安装包：D:\Code\ExamHelper\artifacts\installers\ExamHelper-Setup-0.1.0-x64.exe，相邻 .sha256 可核对散列。无需单独安装 .NET；也可保持发布目录完整后运行 artifacts/publish/win-x64/ExamHelper.App.exe。不要只复制入口 EXE。

首次使用从主页进入设置，配置 KIMI／兼容 AI 地址、密钥与模型，以及阿里云北京识别地址、密钥和麦克风。执行 AI 图片测试，完成语音测试并确认“识别正确”后才能开始。文字测试单独提供，不代替图片测试。密钥按 Windows 当前用户加密；不要求在聊天中提供密钥。开始始终新建对话，继续此对话恢复历史及退出保存的草稿。

普通按钮悬停一秒，主页／退出两秒；移开取消，触发后必须重新进入。标题和右下角手柄可免点击移动／缩放，回答滚动条支持悬停拖动。截图可直发或多图加入草稿；直发只在空草稿且任务允许时启用。录音最长六十秒，识别失败保留录音供手动重试，成功后显示三秒撤销入口。发送与重试保留当前上下文。返回主页清空草稿，退出保存草稿；隐藏仅影响显示。

默认 Ctrl+Alt+F1 截图直发、F2 入草稿、F3 录音、F4 发送、F6 清空、F7 新对话、F8 停止回答、F9 隐藏／显示、F10 折叠、F11 主页、F12 退出，Ctrl+Alt+Z 撤销语音。F5 留空。设置可修改组合并检查冲突。

当前代码与安装预览已实现。阶段 A 已由用户实测验收；Windows 自带截图／录屏及 Chrome 屏幕共享通过，正式窗口另完成受控 GDI 捕获和原生检查。真实云服务需在设置中配置用户自己的凭据后验证；OBS、腾讯会议、其他 DPI 和未细分捕获后端仍未实测，不宣称所有环境均已通过。兼容性、持久化及安装证据见 docs/verification/。

开发使用 global.json 锁定的 .NET 10 SDK。运行 scripts/Test.ps1 -Configuration Release -DotnetPath <dotnet.exe> 完成锁定恢复与完整回归；scripts/Publish.ps1 -Version 0.1.0 发布自包含程序；scripts/Build-Installer.ps1 -Version 0.1.0 -IsccPath <ISCC.exe> 生成当前用户安装器。可用 OutputDirectory／PublishDirectory 参数指定输出。构建信息和依赖散列存于发布目录 build-info.json，第三方许可见 THIRD-PARTY-NOTICES.md 和 packaging/licenses/。

tools/ExamHelper.InteractionLab 是独立开发验证工具，不随安装器分发。--verify 只保存已知绿色参考区域；--verify-product 检查正式保护窗口的合成内容；--verify-audio 进行本机麦克风两秒采集并删除测试录音，均不上传。实验窗“全屏截图预览”会真实保存原图至 %LOCALAPPDATA%\ExamHelper\InteractionLab\Images。

应用设置、历史、原图位于 %LOCALAPPDATA%\ExamHelper，卸载默认保留。清理用户数据前请自行备份，不默认删除历史。
最终本机回归：139 项通过，零跳过，Release 编译零警告零错误。独立审查发现的四项必要问题已修复，具体触发链及未完成验收见 [交付清单](docs/verification/release-checklist.md) 与 [最终审查](docs/verification/final-review.md)。已知轻微问题：编辑预置提示词后立即关闭可能未保存，当前请用“保存并返回”。
