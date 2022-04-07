using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.ImageSharp;

namespace XIVLauncher.Core;

public class TextureWrap : IDisposable
{
    public IntPtr ImGuiHandle { get; }

    private readonly Texture deviceTexture;

    public uint Width => this.deviceTexture.Width;

    public uint Height => this.deviceTexture.Height;

    public Vector2 Size => new(this.Width, this.Height);

    protected TextureWrap(byte[] data)
    {
        var image = Image.Load<Rgba32>(data);
        var texture = new ImageSharpTexture(image, false);
        this.deviceTexture = texture.CreateDeviceTexture(Program.GraphicsDevice, Program.GraphicsDevice.ResourceFactory);

        this.ImGuiHandle = Program.ImGuiBindings.GetOrCreateImGuiBinding(Program.GraphicsDevice.ResourceFactory, this.deviceTexture);
    }

    public static TextureWrap Load(byte[] data) => new(data);

    public void Dispose()
    {
        this.deviceTexture.Dispose();
    }
}