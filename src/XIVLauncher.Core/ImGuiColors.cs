using System.Numerics;

namespace XIVLauncher.Core;

/// <summary>
/// Class containing frequently used colors for easier reference.
/// </summary>
public static class ImGuiColors
{
    /// <summary>
    /// Gets red used in dalamud.
    /// </summary>
    public static Vector4 DalamudRed { get; internal set; } = new(1f, 0f, 0f, 1f);

    /// <summary>
    /// Gets grey used in dalamud.
    /// </summary>
    public static Vector4 DalamudGrey { get; internal set; } = new(0.7f, 0.7f, 0.7f, 1f);

    /// <summary>
    /// Gets grey used in dalamud.
    /// </summary>
    public static Vector4 DalamudGrey2 { get; internal set; } = new(0.7f, 0.7f, 0.7f, 1f);

    /// <summary>
    /// Gets grey used in dalamud.
    /// </summary>
    public static Vector4 DalamudGrey3 { get; internal set; } = new(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>
    /// Gets white used in dalamud.
    /// </summary>
    public static Vector4 DalamudWhite { get; internal set; } = new(1f, 1f, 1f, 1f);

    /// <summary>
    /// Gets white used in dalamud.
    /// </summary>
    public static Vector4 DalamudWhite2 { get; internal set; } = new(0.878f, 0.878f, 0.878f, 1f);

    /// <summary>
    /// Gets orange used in dalamud.
    /// </summary>
    public static Vector4 DalamudOrange { get; internal set; } = new(1f, 0.709f, 0f, 1f);

    /// <summary>
    /// Gets yellow used in dalamud.
    /// </summary>
    public static Vector4 DalamudYellow { get; } = new(1f, 1f, .4f, 1f);

    /// <summary>
    /// Gets violet used in dalamud.
    /// </summary>
    public static Vector4 DalamudViolet { get; } = new(0.770f, 0.700f, 0.965f, 1.000f);

    /// <summary>
    /// Gets tank blue (UIColor37).
    /// </summary>
    public static Vector4 TankBlue { get; internal set; } = new(0f, 0.6f, 1f, 1f);

    /// <summary>
    /// Gets healer green (UIColor504).
    /// </summary>
    public static Vector4 HealerGreen { get; internal set; } = new(0f, 0.8f, 0.1333333f, 1f);

    /// <summary>
    /// Gets dps red (UIColor545).
    /// </summary>
    public static Vector4 DpsRed { get; internal set; } = new(0.7058824f, 0f, 0f, 1f);

    /// <summary>
    /// Gets parsed grey.
    /// </summary>
    public static Vector4 ParsedGrey { get; } = new(0.4f, 0.4f, 0.4f, 1f);

    /// <summary>
    /// Gets parsed green.
    /// </summary>
    public static Vector4 ParsedGreen { get; } = new(0.117f, 1f, 0f, 1f);

    /// <summary>
    /// Gets parsed blue.
    /// </summary>
    public static Vector4 ParsedBlue { get; } = new(0f, 0.439f, 1f, 1f);

    /// <summary>
    /// Gets parsed purple.
    /// </summary>
    public static Vector4 ParsedPurple { get; } = new(0.639f, 0.207f, 0.933f, 1f);

    /// <summary>
    /// Gets parsed orange.
    /// </summary>
    public static Vector4 ParsedOrange { get; } = new(1f, 0.501f, 0f, 1f);

    /// <summary>
    /// Gets parsed pink.
    /// </summary>
    public static Vector4 ParsedPink { get; } = new(0.886f, 0.407f, 0.658f, 1f);

    /// <summary>
    /// Gets parsed gold.
    /// </summary>
    public static Vector4 ParsedGold { get; } = new(0.898f, 0.8f, 0.501f, 1f);

    public static Vector4 Blue { get; } = new Vector4(0.3647058823529412f, 0.4549019607843137f, 0.6745098039215686f, 1f);
    public static Vector4 BlueShade0 { get; } = new Vector4(0.1686274509803922f, 0.1882352941176471f, 0.2313725490196078f, 1f);
    public static Vector4 BlueShade1 { get; } = new Vector4(0.2f, 0.2235294117647059f, 0.2784313725490196f, 1f);
    public static Vector4 BlueShade2 { get; } = new Vector4(0.2705882352941176f, 0.3019607843137255f, 0.3725490196078431f, 1f);
    public static Vector4 BlueShade3 { get; } = new Vector4(0.2980392156862745f, 0.3843137254901961f, 0.5803921568627451f, 1f);
    public static Vector4 Text { get; } = new Vector4(0.7843137254901961f, 0.796078431372549f, 0.8196078431372549f, 1f);
    public static Vector4 TextDisabled { get; } = new Vector4(Text.X, Text.Y, Text.Z, 0.5f);
}