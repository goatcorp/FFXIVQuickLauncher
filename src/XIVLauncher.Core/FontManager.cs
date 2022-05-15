using System.Runtime.InteropServices;
using ImGuiNET;

namespace XIVLauncher.Core;

public class FontManager
{
    private const float FONT_GAMMA = 1.4f;

    public static ImFontPtr TextFont { get; private set; }

    public static ImFontPtr IconFont { get; private set; }

    public unsafe void SetupFonts(float pxSize)
    {
        var ioFonts = ImGui.GetIO().Fonts;

        ImGui.GetIO().Fonts.Clear();

        ImFontConfigPtr fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig.PixelSnapH = true;

        var fontDataText = AppUtil.GetEmbeddedResourceBytes("NotoSansCJKjp-Regular.otf");
        var fontDataIcons = AppUtil.GetEmbeddedResourceBytes("FontAwesome5FreeSolid.otf");

        var fontDataTextPtr = Marshal.AllocHGlobal(fontDataText.Length);
        Marshal.Copy(fontDataText, 0, fontDataTextPtr, fontDataText.Length);

        var fontDataIconsPtr = Marshal.AllocHGlobal(fontDataIcons.Length);
        Marshal.Copy(fontDataIcons, 0, fontDataIconsPtr, fontDataIcons.Length);

        var japaneseRangeHandle = GCHandle.Alloc(GlyphRangesJapanese.GlyphRanges, GCHandleType.Pinned);

        TextFont = ioFonts.AddFontFromMemoryTTF(fontDataTextPtr, fontDataText.Length, pxSize, null, japaneseRangeHandle.AddrOfPinnedObject());

        var iconRangeHandle = GCHandle.Alloc(
            new ushort[]
            {
                0xE000,
                0xF8FF,
                0,
            },
            GCHandleType.Pinned);

        IconFont = ioFonts.AddFontFromMemoryTTF(fontDataIconsPtr, fontDataIcons.Length, pxSize, fontConfig, iconRangeHandle.AddrOfPinnedObject());

        ioFonts.Build();

        if (Math.Abs(FONT_GAMMA - 1.0f) >= 0.001)
        {
            // Gamma correction (stbtt/FreeType would output in linear space whereas most real world usages will apply 1.4 or 1.8 gamma; Windows/XIV prebaked uses 1.4)
            ioFonts.GetTexDataAsRGBA32(out byte* texPixels, out var texWidth, out var texHeight);
            for (int i = 3, j = texWidth * texHeight * 4; i < j; i += 4)
                texPixels[i] = (byte)(Math.Pow(texPixels[i] / 255.0f, 1.0f / FONT_GAMMA) * 255.0f);
        }

        fontConfig.Destroy();
        japaneseRangeHandle.Free();
        iconRangeHandle.Free();
    }
}