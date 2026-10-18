using System.IO;
using System.Text.RegularExpressions;
namespace ExamHelper.Windows.Persistence;
public static class AppDataPaths
{
    public static AppDataLayout Resolve(string localAppData) => new(Path.Combine(Path.GetFullPath(localAppData),"ExamHelper","0.4"));
    public static string Root => Resolve(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).Root;
    public static string Images => Path.Combine(Root,"Images");
    public static string TempAudio => Path.Combine(Root,"TempAudio");
    public static string Settings => Path.Combine(Root,"settings.json");
    public static string Secrets => Path.Combine(Root,"Secrets");
    public static string Database => Path.Combine(Root,"history.db");
    public static string Logs => Path.Combine(Root,"Logs");
    public static void CleanTemporaryAudio(string directory)
    {
        var root = Path.GetFullPath(directory);
        if(!Directory.Exists(root)) return;
        if((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("临时录音目录不能是符号链接。");
        foreach(var path in Directory.EnumerateFiles(root,"*",SearchOption.TopDirectoryOnly))
            if(Regex.IsMatch(Path.GetFileName(path),"^[0-9a-fA-F]{32}(\\.raw)?\\.wav$")) File.Delete(path);
    }
}