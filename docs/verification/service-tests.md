# 服务与录音验证记录

2026-10-17：麦克风编码及截止控制实现，完整 Release 测试 59 项通过（Core35 Windows24）。六十秒截止与手动停止竞争由可控时钟测试验证；真实 Windows Media Foundation 重采样测试验证 48kHz 浮点双声道输入转为 16kHz／单声道／16bit PCM WAV。

本机默认麦克风两秒采集实测通过：输出 1.99 秒，格式符合要求，测试音频随后删除，上传次数 0。证据 artifacts/phase-b-audio-check/audio-result.json。报告列出的可用设备包括 Steam Streaming Microphone 和 Realtek 外部麦克风；当次实际默认端点名称未记录，不能据列表推断用了哪一项。此结果证明采集和编码，不证明云端识别质量。设备选择、真实六十秒截止、拔除设备、拒绝权限及隐藏继续录音尚待后续实测。

NAudio 版本固定为 2.2.1。实现使用 WASAPI 麦克风采集及 MediaFoundationResampler；没有使用系统声音回环采集。参考 [NAudio 官方重采样说明](https://github.com/naudio/NAudio/blob/main/Docs/Resampling.md)。

云端 ASR 与 AI 尚无用户凭据，未发送真实服务请求。设置页应由用户填写并执行测试，协议测试不可替代服务通过资格。

2026-10-17：ASR 协议与语音协调器实现，完整 Release 回归 69 项通过（Core38 Windows31）。假 HTTP 覆盖北京业务空间地址、WAV Base64、混合语言未传 language、401／429、超时与主动取消、不完整响应。协调器覆盖清空后晚到结果隔离、同一录音手动重试、自动截止触发识别及成功清理。未调用真实云服务，不能给设置标记服务通过。

依据 [阿里云英文官方 ASR 文档](https://help.aliyun.com/en/model-studio/qwen-asr-api-reference) 核对请求格式；支持北京业务空间端点，公共北京域名仍可用。未固定语言参数，以适配中英混合语音。识别结果带 SessionId／Generation／OperationId，任务 14 再将事件接入三秒撤销及持久化草稿。

2026-10-17：图文流式客户端、基础提示词及上下文构建实现，完整 Release 测试 80 项通过（Core42 Windows38）。测试覆盖 UTF-8 每字节拆分、SSE 组帧、缺少完成标记、length 未完成、usage-only 块、取消／超时、原图顺序、重试问题去重、失败历史排除、普通发送不附加直发提示词。未发真实请求。

已核对 [Kimi 官方 K2.6 指南](https://platform.kimi.ai/docs/guide/kimi-k2-6-quickstart)：该模型支持图片输入及关闭 thinking。仅 KIMI 预设的 kimi-k2.6 发送 thinking disabled，兼容模式不发送此参数；不强制 temperature。仍须用用户账户验证模型可用性，不根据文档直接签发资格。客户端明确拒绝超过 100MB 的请求，不静默缩图或丢上下文。

2026-10-17：设置、DPAPI 和服务资格模块完整回归 92 项通过（Core47 Windows45）。Windows 当前用户真实加解密、配置原子保存、损坏配置保留、同一 SecretId 更换密钥后资格失效、配置变化期间的旧测试不能签发新资格均已测试。测试图为内置 EH7Q9，已检查图像；测试请求只读取内存测试图，不接触桌面或历史图片。语音测试非空结果仅允许人工确认，未确认不能记通过。所有云调用仍为假 HTTP，真实服务未验证。
