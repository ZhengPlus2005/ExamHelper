using System.IO;
namespace ExamHelper.Windows.Persistence;
public sealed record AppDataLayout(string Root)
{
    public string Settings => Path.Combine(Root,"settings.json");
    public string Secrets => Path.Combine(Root,"Secrets");
    public string Database => Path.Combine(Root,"history.db");
    public string Images => Path.Combine(Root,"Images");
    public string TempAudio => Path.Combine(Root,"TempAudio");
    public string Logs => Path.Combine(Root,"Logs");
}
