using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Common;
namespace ExamHelper.Windows.Settings;
public sealed class UserSecretStore : ISecretStore
{
    private readonly string _root;
    public UserSecretStore(string root)
    {
        _root = Path.GetFullPath(root); Directory.CreateDirectory(_root);
        if((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("密钥目录不能是符号链接。",nameof(root));
    }
    private string PathFor(string id)
    {
        if(!Regex.IsMatch(id,"^[a-zA-Z0-9_-]{1,64}$")) throw new ArgumentException("密钥标识无效。",nameof(id));
        return Path.Combine(_root,id + ".bin");
    }
    public async Task SetAsync(string id,string secret,CancellationToken ct)
    {
        var path = PathFor(id); var plain = Encoding.UTF8.GetBytes(secret);
        try
        {
            var encrypted = ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);
            await AtomicFile.WriteAsync(path,encrypted,ct).ConfigureAwait(false);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or CryptographicException)
        { throw new ServiceFailure(FailureCategory.Storage,"无法保存当前用户的加密密钥。",inner:error); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public async Task<string?> GetAsync(string id,CancellationToken ct)
    {
        var path = PathFor(id); if(!File.Exists(path)) return null;
        byte[]? plain = null;
        try
        {
            if((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("不读取密钥链接。");
            var encrypted = await File.ReadAllBytesAsync(path,ct).ConfigureAwait(false);
            plain = ProtectedData.Unprotect(encrypted,null,DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch(CryptographicException) { throw new ServiceFailure(FailureCategory.Authentication,"保存的密钥无法解密，请在设置中重新填写。"); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        { throw new ServiceFailure(FailureCategory.Storage,"无法读取加密密钥。",inner:error); }
        finally { if(plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }
}