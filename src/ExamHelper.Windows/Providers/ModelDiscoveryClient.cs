using System.Collections.Immutable;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Providers;
public interface IModelDiscovery
{
    Task<IReadOnlyList<ModelMetadata>> ListAsync(AiSettings endpoint,string apiKey,CancellationToken ct);
}
public sealed class ModelDiscoveryClient(HttpClient http) : IModelDiscovery
{
    public async Task<IReadOnlyList<ModelMetadata>> ListAsync(AiSettings endpoint,string apiKey,CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,ProviderEndpoint.Models(endpoint.BaseUri));
        if(string.IsNullOrWhiteSpace(apiKey)) throw new ServiceFailure(FailureCategory.Authentication,"请填写 API 密钥。");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",apiKey);
        using var response = await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
        if(!response.IsSuccessStatusCode) throw await ProviderErrorMapper.FromResponseAsync(response,apiKey,ct).ConfigureAwait(false);
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(body,cancellationToken: ct).ConfigureAwait(false);
            var results = new List<ModelMetadata>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in json.RootElement.GetProperty("data").EnumerateArray())
            {
                if(results.Count >= 5000) break;
                if(!row.TryGetProperty("id",out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) || !seen.Add(id.GetString()!)) continue;
                ImmutableArray<string>? levels = null;
                if(row.TryGetProperty("effort",out var effort) && effort.ValueKind == JsonValueKind.Object && effort.TryGetProperty("supported_levels",out var list) && list.ValueKind == JsonValueKind.Array)
                    levels = list.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())).Select(v => v.GetString()!).Distinct().ToImmutableArray();
                results.Add(new(id.GetString()!,Flag(row,"supports_image_in"),Flag(row,"supports_reasoning"),levels));
            }
            return results;
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new ServiceFailure(FailureCategory.InvalidPayload,"模型列表格式无法识别，可手动填写模型 ID。"); }
    }
    private static bool? Flag(JsonElement row,string name) => row.TryGetProperty(name,out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}
