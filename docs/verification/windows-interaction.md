# Windows 核心交互验证

环境：Windows 11 x64，build 26200，.NET SDK 10.0.402／runtime 10.0.13，当前屏幕 125%（120 DPI）。

## 捕获保护

2026-10-16：InteractionLab 创建绿色参考窗口，在其上展示半透明 WPF 保护窗口。保护窗口显示前设置并读回 WDA_EXCLUDEFROMCAPTURE=0x11，API 均成功，错误码 0。

GDI BitBlt SRCCOPY 前后截图为 1125×950 像素，逐字节差异 0。通过 `--verify artifacts/capture` 重现，证据 reference.png、protected-capture.png、capture-result.json 保留在 artifacts。只捕获自身参考窗口，无上传。

此结果不等于系统截图工具、Chrome、OBS、腾讯会议已通过；这些外部工具尚未验证。

| 项目 | 状态 |
| --- | --- |
| 原生保护失败／读回不符 | 4 个测试通过 |
| 半透明与 GDI 排除 | 当前 125% DPI 实测通过 |
| 悬停与布局变化 | 待实现 |
| 免点击移动／缩放／滚动 | 待实现 |
| 焦点与英文输入恢复 | 待实现 |
| 快捷键注册与冲突 | 待实现 |
| 100%／150%／200% DPI | 未验证，未修改用户系统缩放 |
| 外部截图／录屏路径 | 未验证 |
