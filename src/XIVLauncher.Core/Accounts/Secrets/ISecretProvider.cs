namespace XIVLauncher.Core.Accounts.Secrets;

public interface ISecretProvider
{
    public string? GetPassword(string accountName);

    public void SavePassword(string accountName, string password);

    public void DeletePassword(string accountName);
}
