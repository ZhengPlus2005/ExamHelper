using ExamHelper.App.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Settings;
public sealed class DiscoveredModelEditor(ModelMetadata metadata) : ObservableObject
{
    private bool _selected;
    public ModelMetadata Metadata { get; } = metadata;
    public string Name => Metadata.Id;
    public bool IsSelected { get => _selected; set => Set(ref _selected,value); }
}
