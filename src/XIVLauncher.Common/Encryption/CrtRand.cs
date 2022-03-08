namespace XIVLauncher.Common.Encryption;

public class CrtRand
{
    private uint seed;

    public CrtRand(uint seed)
    {
        this.seed = seed;
    }

    public uint Next()
    {
        this.seed = 0x343FD * this.seed + 0x269EC3;
        return ((this.seed >> 16) & 0xFFFF) & 0x7FFF;
    }
}