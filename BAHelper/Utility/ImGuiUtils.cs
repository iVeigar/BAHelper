using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;

namespace BAHelper.Utility;

internal static partial class ImGuiUtils
{
    public static void TextURL(string name, string url, uint color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.Text(name);
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }

            DrawUnderline(ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            ImGui.BeginTooltip();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(FontAwesomeIcon.Link.ToIconString()); ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.PopFont();
            ImGui.Text(url);
            ImGui.EndTooltip();
        }
        else
        {
            DrawUnderline(ImGui.GetColorU32(ImGuiCol.Button));
        }
    }

    public static void DrawUnderline(uint color)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        min.Y = max.Y;
        ImGui.GetWindowDrawList().AddLine(min, max, color, 1.0f);
    }

    public static void SetTooltip(string message)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(message);
    }

    public static void CenterText(string text)
    {
        var result = Vector2.Subtract(ImGui.GetContentRegionAvail(), ImGui.CalcTextSize(text));
        ImGui.SetCursorPos(new Vector2(result.X / 2, result.Y / 2));
        ImGui.Text(text);
    }

    public static void RightAlignTextInColumn(string text, Vector4? color = null)
    {
        var posX = ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X - ImGui.GetScrollX();
        if (posX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(posX);

        if (color == null)
            ImGui.Text(text);
        else
            ImGui.TextColored((Vector4)color, text);
    }

    private const int CircleSegments = 90;
    private const float CircleSegmentFullRotation = 2 * MathF.PI / CircleSegments;

    public static bool ColorPickerWithPalette(int id, string description, ref Vector4 originalColor, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
    {
        var changed = false;
        Vector4 col = originalColor;
        List<Vector4> list = ImGuiHelpers.DefaultColorPalette();
        if (ImGui.ColorButton($"{description}###ColorPickerButton{id}", originalColor, flags))
        {
            ImGui.OpenPopup($"###ColorPickerPopup{id}");
        }

        if (ImGui.BeginPopup($"###ColorPickerPopup{id}"))
        {
            if (ImGui.ColorPicker4($"###ColorPicker{id}", ref col, flags))
            {
                originalColor = col;
                changed = true;
            }
            for (int i = 0; i < 4; i++)
            {
                ImGui.Spacing();
                for (int j = i * 8; j < i * 8 + 8; j++)
                {
                    if (ImGui.ColorButton($"###ColorPickerSwatch{id}{i}{j}", list[j]))
                    {
                        originalColor = list[j];
                        changed = true;
                        ImGui.CloseCurrentPopup();
                        ImGui.EndPopup();
                        return changed;
                    }
                    ImGui.SameLine();
                }
            }
            ImGui.EndPopup();
        }
        return changed;
    }

    private static void DrawCircleInternal(this ImDrawListPtr drawList, Vector3 center, float radius, float thickness, uint color, bool filled)
    {
        for (var i = 0; i <= CircleSegments; i++)
        {
            var currentRotation = i * CircleSegmentFullRotation;
            var segmentWorld = center + (radius * currentRotation.ToNormalizedVector2()).ToVector3();
            Svc.GameGui.WorldToScreen(segmentWorld, out var segmentScreen);
            drawList.PathLineTo(segmentScreen);
        }

        if (filled)
            drawList.PathFillConvex(color);
        else
            drawList.PathStroke(color, ImDrawFlags.RoundCornersDefault, thickness);
    }

    public static bool DrawRingWorld(this ImDrawListPtr drawList, Vector3 center, float radius, float thickness, uint color, bool drawOffScreen = false, bool filled = false)
    {
        Svc.GameGui.WorldToScreen(center, out var _, out var inView);
        if (inView || drawOffScreen)
        {
            drawList.DrawCircleInternal(center, radius, thickness, color, filled);
        }
        return inView;
    }

    public static bool DrawRingWorldWithText(this ImDrawListPtr drawList, Vector3 center, float radius, float thickness, uint color, string text, bool filled = false, Vector2 offset = default, bool drawOffScreen = false)
    {
        Svc.GameGui.WorldToScreen(center, out var screenPos, out var inView);
        if (inView || drawOffScreen)
        {
            drawList.DrawCircleInternal(center, radius, thickness, color, filled);
            if (text != string.Empty)
                drawList.DrawTextTag(screenPos + offset, text, color, true, true, Color.TransBlack, true);
        }
        return inView;
    }

    internal static bool IconButton(FontAwesomeIcon icon, string id)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        bool result = ImGui.Button(icon.ToIconString() + "##" + id);
        ImGui.PopFont();
        return result;
    }

    public static void DrawTextTag(this ImDrawListPtr drawList, Vector2 pos, string text, uint color = Color.White, bool centerAlignX = true, bool bg = false, uint bgcolor = Color.TransBlack, bool bordered = false)
    {
        Vector2 vector = ImGui.CalcTextSize(text) + new Vector2(ImGui.GetStyle().ItemSpacing.X, 0f);
        if (centerAlignX)
        {
            pos -= new Vector2(vector.X, 0f) / 2f;
        }
        if (bg)
            drawList.AddRectFilled(pos, pos + vector, bgcolor, 8f); // 圆角
        if (bordered)
            drawList.AddRect(pos, pos + vector, color, 10f);
        drawList.AddText(pos + new Vector2(ImGui.GetStyle().ItemSpacing.X / 2f, 0f), color, text);
    }

    public static void DrawRectWorld(this ImDrawListPtr drawList, Vector3 origin, Vector3 dims)
    {
        Svc.GameGui.WorldToScreen(origin, out Vector2 lt);
        Svc.GameGui.WorldToScreen(origin + new Vector3(dims.X, 0f, 0f), out Vector2 rt);
        Svc.GameGui.WorldToScreen(origin + dims, out Vector2 rb);
        Svc.GameGui.WorldToScreen(origin + new Vector3(0, 0f, dims.Z), out Vector2 lb);

        drawList.AddPolyline(ref (new Vector2[]
        {
            lt, rt, rb, lb, lt
        })[0], 5, Color.Cyan, ImDrawFlags.RoundCornersAll, 1.2f);
    }

    public static void DrawConeFromCenterPoint(this ImDrawListPtr drawList, Vector3 center, float rotation, float angleRadian, float radius, uint outlineColor)
    {
        rotation += MathF.PI / 4;
        var partialCircleSegmentRotation = angleRadian / CircleSegments;
        var coneColor = outlineColor.SetAlpha(0.2f);

        Svc.GameGui.WorldToScreen(center, out var originPositionOnScreen);
        drawList.PathLineTo(originPositionOnScreen);
        for (var i = 0; i <= CircleSegments; i++)
        {
            var currentRotation = rotation - i * partialCircleSegmentRotation;
            var segmentWorld = center + (radius * currentRotation.ToNormalizedVector2()).ToVector3();
            Svc.GameGui.WorldToScreen(segmentWorld, out var segmentScreen);

            drawList.PathLineTo(segmentScreen);
        }

        drawList.PathFillConvex(coneColor);
        drawList.PathClear();

        drawList.PathLineTo(originPositionOnScreen);
        for (var i = 0; i <= CircleSegments; i++)
        {
            var currentRotation = rotation - i * partialCircleSegmentRotation;
            var segmentWorld = center + (radius * currentRotation.ToNormalizedVector2()).ToVector3();
            Svc.GameGui.WorldToScreen(segmentWorld, out var segmentScreen);

            drawList.PathLineTo(segmentScreen);
        }
        drawList.PathLineTo(originPositionOnScreen);

        drawList.PathStroke(outlineColor);
    }
}
