using ExamHelper.Core.Settings;
using ExamHelper.Windows.Providers;
namespace ExamHelper.App.Settings;
public sealed partial class SettingsViewModel
{
    private IModelDiscovery _discovery = null!;
    public async Task FetchModelsAsync(CancellationToken ct)
    {
        if(IsBusy || SelectedAiProfile is not { } profile) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        try
        {
            var endpoint = profile.BuildSettings(); var key = profile.ApiKey; var address = profile.BaseAddress;
            Status = "正在拉取模型列表……";
            var models = await _discovery.ListAsync(endpoint,key,linked.Token);
            if(_disposed || !AiProfiles.Contains(profile) || profile.BaseAddress != address || profile.ApiKey != key)
            { Status = "配置已变化，已丢弃旧模型列表。"; return; }
            profile.DiscoveredModels.Clear();
            foreach(var model in models) profile.DiscoveredModels.Add(new(model));
            Status = models.Count == 0 ? "接口未返回模型，可手动添加。" : "请勾选需要的模型，再点击加入。";
        }
        catch(OperationCanceledException) { if(!_disposed) Status = "模型拉取已取消。"; }
        catch(Exception error) { if(!_disposed) Status = Explain(error) + " 可继续手动添加模型。"; }
        finally { IsBusy = false; }
    }
    public void AddDiscoveredModels()
    {
        if(SelectedAiProfile is not { } p) return;
        foreach(var row in p.DiscoveredModels.Where(m => m.IsSelected))
            if(!string.IsNullOrWhiteSpace(row.Name) && !p.Models.Any(m => m.Name.Trim() == row.Name.Trim())) p.AddModel(new AiModelProfile { Name = row.Name,DiscoveryMetadata = row.Metadata });
        Status = "已加入勾选模型；默认模型不变，保存后生效。";
    }
}
