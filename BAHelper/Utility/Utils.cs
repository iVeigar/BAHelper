using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;

namespace BAHelper.Utility;

public static class Utils
{
    public unsafe static void SetFlagMarker(uint territoryId, uint mapId, float x, float y)
    {
        var mapLinkPayload = new MapLinkPayload(territoryId, mapId, x, y);
        AgentMap.Instance()->FlagMarkerCount = 0;
        AgentMap.Instance()->SetFlagMapMarker(territoryId, mapId, mapLinkPayload.RawX / 1000f, mapLinkPayload.RawY / 1000f);
    }

    public static T GetSheetRow<T>(uint row) where T : struct, IExcelRow<T> => Svc.Data.GetExcelSheet<T>()!.GetRow(row);

    public static Vector2 ToVector2(this Vector3 v) => new(v.X, v.Z);

    public static Vector3 ToVector3(this Vector2 v) => new(v.X, 0f, v.Y);

    public static float Distance(this Vector3 v, Vector3 v2) => Vector3.Distance(v, v2);

    public static float Distance2D(this Vector3 v, Vector3 v2) => (v - v2).ToVector2().Length();

    public static uint SetAlpha(this uint color32, uint alpha) => (color32 << 8 >> 8) + (alpha << 24);

    public static uint SetAlpha(this uint color32, float alpha) => color32.SetAlpha((uint)(255 * alpha));

    public static Vector2 ToNormalizedVector2(this float rad) => new(MathF.Sin(rad), MathF.Cos(rad));
}
