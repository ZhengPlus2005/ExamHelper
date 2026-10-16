# ExamHelper

Windows 11 x64 WPF 项目，按已认可的设计和计划开发。当前阶段验证原生窗口交互，尚无可用的 AI 答题产品。

开发需要 .NET 10 SDK（global.json 锁定版本），使用 `dotnet build ExamHelper.slnx` 和 `dotnet test ExamHelper.slnx`。

独立验证窗口：`dotnet run --project tools/ExamHelper.InteractionLab`。开发验证专用，不随产品分发。

捕获集成验证：`dotnet run --project tools/ExamHelper.InteractionLab -- --verify artifacts/capture`。仅创建并捕获自身无敏感内容的参考窗口，输出 PNG 与 JSON 结果；此验证不代替 OBS、Chrome、会议工具或系统截图工具实测。
