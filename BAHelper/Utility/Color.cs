using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BAHelper.Utility;

public static class Color // ABGR
{
    private static readonly Random random = new();

    public static readonly Lazy<Vector4> RandomColor = new(delegate ()
    {
        float out_r = 0f, out_g = 0f, out_b = 0f;
        ImGui.ColorConvertHSVtoRGB((float)random.NextDouble(), 1f, 1f, ref out_r, ref out_g, ref out_b);
        return new(out_r, out_g, out_b, 1f);
    });

    public const uint Red = 0xFF0000FF;

    public const uint Magenta = 0xFFFF00FF;

    public const uint Yellow = 0xFF00FFFF;

    public const uint Green = 0xFF00FF00;

    public const uint GrassGreen = 0xFF00E000;

    public const uint Cyan = 0xFFFFFF00;

    public const uint DarkCyan = 0xFF909000;

    public const uint LightCyan = 0xFFFFFFA0;

    public const uint Blue = 0xFFFF0000;

    public const uint Black = 0xFF000000;

    public const uint TransBlack = 0x80000000;

    public const uint Grey = 0xFF808080;

    public const uint White = 0xFFFFFFFF;

    public const uint Orange = 0xFF0099FF;

    public const uint Brown = 0xFF2A2AA5;
}
