using System.IO;
using System.Text.Json;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Common;
using ExamHelper.Windows.Providers;
namespace ExamHelper.Windows.Settings;
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private readonly string _path = Path.GetFullPath(path);
    public async Task<AppSettings> LoadAsync(CancellationToken ct)
    {
        if(!File.Exists(_path)) return new();
        try
        {
            var bytes = await File.ReadAllBytesAsync(_path,ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(bytes);
            var settings = JsonSerializer.Deserialize<AppSettings>(bytes)
                ?? throw new JsonException();
            if(!document.RootElement.TryGetProperty("FormatVersion",out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var format) || format != 4)
                throw new ArgumentException("设置版本不属于 0.4 系列。");
            settings = AiProfileCatalog.Normalize(settings); Validate(settings); return settings;
        }
        catch(Exception error) when (error is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        { throw new ServiceFailure(FailureCategory.Storage,"无法读取设置，原文件已保留。请检查或备份后修复配置。",inner:error); }
    }
    public async Task SaveAsync(AppSettings settings,CancellationToken ct)
    {
        settings = AiProfileCatalog.Normalize(settings);
        Validate(settings);
        try { await AtomicFile.WriteAsync(_path,JsonSerializer.SerializeToUtf8Bytes(settings,new JsonSerializerOptions { WriteIndented = true }),ct).ConfigureAwait(false); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        { throw new ServiceFailure(FailureCategory.Storage,"设置保存失败，原文件已保留。",inner:error); }
    }
    public static void Validate(AppSettings settings)
    {
        if(settings.Ai is null || settings.Asr is null || settings.Ui is null || settings.Hotkeys is null || settings.Evidence is null)
            throw new ArgumentException("设置不完整。");
        if(settings.FormatVersion != 4 || settings.Ui.Placement is { } placement && (placement.Width <= 0 || placement.Height <= 0))
            throw new ArgumentException("设置格式或窗口物理尺寸无效。");
        if(settings.AiProfiles is not { } profiles || profiles.IsDefault || profiles.Any(p => p is null || p.Settings is null || p.Name is null || p.BaseAddress is null || p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.Settings.SecretId)) ||
            profiles.GroupBy(p => p.Id).Any(g => g.Count() > 1) || profiles.GroupBy(p => p.Settings.SecretId).Any(g => g.Count() > 1))
            throw new ArgumentException("AI 配置身份或密钥标识无效。");
        if(settings.ActiveAiProfileId is { } active && !profiles.Any(p => p.Id == active)) throw new ArgumentException("启用的 AI 配置不存在。");
        foreach(var profile in profiles)
        {
            if(profile.Models is not { } models || models.IsDefault || models.Any(m => m is null || m.Id == Guid.Empty || string.IsNullOrWhiteSpace(m.Name) || !ThinkingCapability.For(m).Allows(m.DefaultThinking)) ||
                models.GroupBy(m => m.Id).Any(g => g.Count() > 1) || models.GroupBy(m => m.Name.Trim(),StringComparer.Ordinal).Any(g => g.Count() > 1) ||
                profile.DefaultModelId is { } modelId && !models.Any(m => m.Id == modelId))
                throw new ArgumentException("模型列表、默认模型或思考选项无效。");
            if(profile.Settings.BaseUri is { } endpoint && (endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0))
                throw new ArgumentException("API 地址不能包含账户、查询或片段。");
        }
        ProviderEndpoint.ChatCompletions(settings.Asr.BaseUri);
        if(!ProviderEndpoint.IsBeijingAsr(settings.Asr.BaseUri)) throw new ArgumentException("语音地址必须位于阿里云北京地域。");
        if(string.IsNullOrWhiteSpace(settings.Asr.Model) || string.IsNullOrWhiteSpace(settings.DefaultCodeLanguage))
            throw new ArgumentException("语音模型名称与默认编程语言不能为空。");
        if(!double.IsFinite(settings.Ui.Opacity) || settings.Ui.Opacity is < 0.35 or > 1 || !double.IsFinite(settings.Ui.Width) ||
            !double.IsFinite(settings.Ui.Height) || settings.Ui.Width <= 0 || settings.Ui.Height <= 0 ||
            (settings.Ui.Left.HasValue && !double.IsFinite(settings.Ui.Left.Value)) || (settings.Ui.Top.HasValue && !double.IsFinite(settings.Ui.Top.Value)))
            throw new ArgumentException("窗口尺寸或透明度无效。");
        if(settings.Hotkeys.Bindings.IsDefault || settings.Hotkeys.Bindings.GroupBy(b => (b.Modifiers,b.VirtualKey)).Any(g => g.Count() > 1))
            throw new ArgumentException("存在重复快捷键。");
    }
}
