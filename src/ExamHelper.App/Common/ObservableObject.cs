using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace ExamHelper.App.Common;
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field,T value,[CallerMemberName]string? name = null)
    {
        if(EqualityComparer<T>.Default.Equals(field,value)) return false;
        field = value; Notify(name); return true;
    }
    public void Notify(string? name = "") => PropertyChanged?.Invoke(this,new(name));
    public static string Explain(Exception error) => error is ExamHelper.Core.Common.ServiceFailure or ArgumentException or InvalidOperationException
        ? error.Message : "操作未完成，请检查配置、网络或本地存储。";
}