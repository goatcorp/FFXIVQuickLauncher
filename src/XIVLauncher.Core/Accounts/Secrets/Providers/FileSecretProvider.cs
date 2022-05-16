using System.Text.Json;

namespace XIVLauncher.Core.Accounts.Secrets.Providers;

public class FileSecretProvider : ISecretProvider
{
    private readonly FileInfo configFile;
    private readonly Dictionary<string, string> savedPasswords;

    public FileSecretProvider(FileInfo configFile)
    {
        this.configFile = configFile;

        if (configFile.Exists)
            this.savedPasswords = JsonSerializer.Deserialize<Dictionary<string, string>>(configFile.OpenText().ReadToEnd())!;

        this.savedPasswords ??= new Dictionary<string, string>();
    }

    public string? GetPassword(string accountName)
    {
        if (this.savedPasswords.TryGetValue(accountName, out var password))
            return password;

        return null;
    }

    public void SavePassword(string accountName, string password)
    {
        if (this.savedPasswords.ContainsKey(accountName))
        {
            this.savedPasswords[accountName] = password;
        }
        else
        {
            this.savedPasswords.Add(accountName, password);
        }

        Save();
    }

    public void DeletePassword(string accountName)
    {
        this.savedPasswords.Remove(accountName);

        Save();
    }

    private void Save()
    {
        File.WriteAllText(this.configFile.FullName, JsonSerializer.Serialize(this.savedPasswords));
    }
}
