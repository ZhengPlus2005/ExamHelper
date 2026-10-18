using System.Net;
using ExamHelper.Core.Common;
namespace ExamHelper.Windows.Providers;
public static class ProviderErrorMapper
{
    public static async Task<ServiceFailure> FromResponseAsync(System.Net.Http.HttpResponseMessage response,string key,CancellationToken ct)
    {
        var basic = FromStatus(response.StatusCode); string code = basic.Code ?? "", detail = basic.Message;
        try
        {
            using var reader = new System.IO.StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false));
            var buffer = new char[8192]; var count = await reader.ReadBlockAsync(buffer.AsMemory(),ct).ConfigureAwait(false);
            using var json = System.Text.Json.JsonDocument.Parse(new string(buffer,0,count));
            var root = json.RootElement; var error = root.TryGetProperty("error",out var nested) ? nested : root;
            if(error.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if(error.TryGetProperty("code",out var c)) code = c.ToString();
                if(error.TryGetProperty("message",out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String) detail = m.GetString() ?? detail;
            }
        }
        catch(Exception e) when(e is System.Text.Json.JsonException or System.IO.IOException) { }
        string Redact(string value) => (string.IsNullOrEmpty(key) ? value : value.Replace(key,"[已隐藏]",StringComparison.Ordinal));
        code = Redact(code); detail = Redact(detail);
        if(code.Length > 120) code = code[..120]; if(detail.Length > 800) detail = detail[..800];
        return new(basic.Category,$"HTTP {(int)response.StatusCode} / {code}：{detail}",code);
    }
    public static ServiceFailure FromStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => new(FailureCategory.Authentication, "服务密钥无效或已过期。", ((int)status).ToString()),
        HttpStatusCode.Forbidden => new(FailureCategory.Permission, "当前账户没有调用该服务的权限。", ((int)status).ToString()),
        HttpStatusCode.TooManyRequests => new(FailureCategory.RateLimit, "服务限流或余额／配额不足，请稍后手动重试。", ((int)status).ToString()),
        HttpStatusCode.NotFound => new(FailureCategory.InvalidModel, "服务路径或模型不存在，请检查配置。", ((int)status).ToString()),
        HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnprocessableEntity
            => new(FailureCategory.InvalidPayload, "服务不接受此请求，请检查模型能力、请求大小和参数。", ((int)status).ToString()),
        _ => new(FailureCategory.Transport, "服务请求失败，请稍后手动重试。", ((int)status).ToString())
    };
}