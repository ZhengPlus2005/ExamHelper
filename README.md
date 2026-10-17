# ExamHelper

Windows 11 x64 WPF 项目，按已认可的设计和计划开发。当前阶段验证原生窗口交互，尚无可用的 AI 答题产品。

开发需要 .NET 10 SDK（global.json 锁定版本），使用 `dotnet build ExamHelper.slnx` 和 `dotnet test ExamHelper.slnx`。

独立验证窗口：`dotnet run --project tools/ExamHelper.InteractionLab`。开发验证专用，不随产品分发。

捕获集成验证：`dotnet run --project tools/ExamHelper.InteractionLab -- --verify artifacts/capture`。仅创建并捕获自身无敏感内容的参考窗口，输出 PNG 与 JSON 结果；此验证不代替 OBS、Chrome、会议工具或系统截图工具实测。

本轮自包含验证程序位于 `D:\Code\ExamHelper\artifacts\windows-core\ExamHelper.InteractionLab.exe`，双击即可运行。Ctrl+Alt+F9 隐藏／显示，Ctrl+Alt+F12 退出。验证窗口中的截图、录音等命令仅演示触发，不发送网络请求。

阶段 A 的测试结果与尚未完成的实测项见 [Windows 验证记录](docs/verification/windows-interaction.md)。辅助模式已取消键盘文字输入，草稿仅显示语音结果。真实鼠标手势、外部答题程序上的不抢焦点体验和其他捕获路径未全部验收，暂未进入业务阶段。
