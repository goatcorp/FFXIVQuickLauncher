using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Encryption;

public class Ticket
{
    public string Text { get; }
    public int Length { get; }

    private Ticket(string text, int length)
    {
        this.Text = text;
        this.Length = length;
    }

    public static Ticket? Get(ISteam steam)
    {


        return null;
        //return new Ticket(data, data.Length - 2);
    }
}