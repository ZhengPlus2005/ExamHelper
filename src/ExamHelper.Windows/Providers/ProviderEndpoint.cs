namespace ExamHelper.Windows.Providers;
public static class ProviderEndpoint
{
    public static Uri ChatCompletions(Uri baseUri)
    {
        if (!baseUri.IsAbsoluteUri || !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment) ||
            (baseUri.Scheme != "https" && !(baseUri.Scheme == "http" && baseUri.IsLoopback)))
            throw new ArgumentException("API 地址须为 HTTPS（本机服务允许 HTTP），且不能含账户、查询或片段。", nameof(baseUri));
        var text = baseUri.AbsoluteUri.TrimEnd('/');
        return new Uri(text.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? text : text + "/chat/completions");
    }
    public static Uri Models(Uri uri) => new(ChatCompletions(uri).AbsoluteUri[..^"chat/completions".Length] + "models");
    public static bool IsBeijingAsr(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" &&
        (uri.Host.Equals("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase) ||
         (uri.Host.EndsWith(".cn-beijing.maas.aliyuncs.com", StringComparison.OrdinalIgnoreCase) && uri.Host.Length > ".cn-beijing.maas.aliyuncs.com".Length));
}