# 服务与录音验证记录

2026-10-17：麦克风编码及截止控制实现，完整 Release 测试 59 项通过（Core35 Windows24）。六十秒截止与手动停止竞争由可控时钟测试验证；真实 Windows Media Foundation 重采样测试验证 48kHz 浮点双声道输入转为 16kHz／单声道／16bit PCM WAV。

本机默认麦克风两秒采集实测通过：输出 1.99 秒，格式符合要求，测试音频随后删除，上传次数 0。证据 artifacts/phase-b-audio-check/audio-result.json。报告列出的可用设备包括 Steam Streaming Microphone 和 Realtek 外部麦克风；当次实际默认端点名称未记录，不能据列表推断用了哪一项。此结果证明采集和编码，不证明云端识别质量。设备选择、真实六十秒截止、拔除设备、拒绝权限及隐藏继续录音尚待后续实测。

NAudio 版本固定为 2.2.1。实现使用 WASAPI 麦克风采集及 MediaFoundationResampler；没有使用系统声音回环采集。参考 [NAudio 官方重采样说明](https://github.com/naudio/NAudio/blob/main/Docs/Resampling.md)。

云端 ASR 与 AI 尚无用户凭据，未发送真实服务请求。设置页应由用户填写并执行测试，协议测试不可替代服务通过资格。
