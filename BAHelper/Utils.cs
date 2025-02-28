using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Newtonsoft.Json;

namespace BAHelper;

public static class Utils
{
    public static SeString CreateItemLink(uint itemId, bool isHq = false, string? displayNameOverride = null)
    {
        var itemLink = SeString.CreateItemLink(itemId, isHq, displayNameOverride);
        return new SeStringBuilder()
            .AddText("物品链接: ")
            .Append(itemLink)
            .Add(RawPayload.LinkTerminator)
            .BuiltString;
    }

    public static SeString CreateMapLink(uint territoryId, uint mapId, Vector2 position)
    {
        return SeString.CreateMapLink(territoryId, mapId, position.X, position.Y);
    }

    public unsafe static void SetFlagMarker(uint territoryId, uint mapId, float x, float y)
    {
        var mapLinkPayload = new MapLinkPayload(territoryId, mapId, x, y);
        AgentMap.Instance()->IsFlagMarkerSet = 0;
        AgentMap.Instance()->SetFlagMapMarker(territoryId, mapId, mapLinkPayload.RawX / 1000f, mapLinkPayload.RawY / 1000f);
    }

    public static T GetSheetRow<T>(uint row) where T : struct, IExcelRow<T>
    {
        return Svc.Data.GetExcelSheet<T>()!.GetRow(row);
    }

    public static Vector2 ToVector2(this Vector3 v) => new(v.X, v.Z);

    public static Vector3 ToVector3(this Vector2 v) => new(v.X, 0f, v.Y);

    public static Vector3 ToVector3(this Vector2 v, float Y) => new(v.X, Y, v.Y);

    public static float Distance(this Vector3 v, Vector3 v2) => Vector3.Distance(v, v2);

    public static float Distance2D(this Vector3 v, Vector3 v2) => Vector2.Distance(v.ToVector2(), v2.ToVector2());

    public static uint SetAlpha(this uint color32, uint alpha) => (color32 << 8 >> 8) + (alpha << 24);

    public static uint SetAlpha(this uint color32, float alpha) => color32.SetAlpha((uint)(255 * alpha));

    public static uint Invert(this uint color32) => (uint.MaxValue ^ color32) & 0xffffff | color32 & 0xff000000;

    public static Vector2 Normalize(this Vector2 v) => Vector2.Normalize(v);

    public static Vector2 Zoom(this Vector2 vin, float zoom, Vector2 origin = default)
    {
        return origin + (vin - origin) * zoom;
    }

    public static Vector2 Rotate(this Vector2 vin, float rad, Vector2 pivot = default)
    {
        var rotation = rad.ToNormalizedVector2();
        var diff = vin - pivot;
        return pivot + new Vector2(rotation.Y * diff.X - rotation.X * diff.Y, rotation.Y * diff.Y + rotation.X * diff.X);
    }

    public static Vector2 Rotate(this Vector2 vin, Vector2 rotation, Vector2 pivot = default)
    {
        rotation = rotation.Normalize();
        var diff = vin - pivot;
        return pivot + new Vector2(rotation.Y * diff.X - rotation.X * diff.Y, rotation.Y * diff.Y + rotation.X * diff.X);
    }

    public static float ToArc(this Vector2 vin) => MathF.Sin(vin.X);

    public static void MassTranspose(Vector2[] vin, Vector2 rotation, Vector2 pivot = default)
    {
        for (int i = 0; i < vin.Length; i++)
        {
            vin[i] = vin[i].Rotate(rotation, pivot);
        }
    }

    public static void MassTranspose(Vector2[] vin, float rotation, Vector2 pivot = default)
    {
        for (int i = 0; i < vin.Length; i++)
        {
            vin[i] = vin[i].Rotate(rotation, pivot);
        }
    }

    public static Vector2 ToNormalizedVector2(this float rad) => new(MathF.Sin(rad), MathF.Cos(rad));

    public static string GetRelativeAddress(this nint i)
    {
        return (i.ToInt64() - Svc.SigScanner.Module.BaseAddress.ToInt64()).ToString("X");
    }

    public static string ToCompressedString<T>(this T obj)
    {
        return Compress(obj.ToJsonString());
    }

    public static T? DecompressStringToObject<T>(this string compressedString)
    {
        return Decompress(compressedString).JsonStringToObject<T>();
    }

    public static string ToJsonString(this object? obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public static T? JsonStringToObject<T>(this string str)
    {
        return JsonConvert.DeserializeObject<T>(str);
    }

    public static string Compress(string s)
    {
        string result;
        using (MemoryStream memoryStream2 = new(Encoding.Unicode.GetBytes(s)))
        {
            using MemoryStream memoryStream3 = new();
            using (GZipStream destination = new(memoryStream3, CompressionLevel.Optimal))
            {
                memoryStream2.CopyTo(destination);
            }
            result = Convert.ToBase64String(memoryStream3.ToArray());
        }
        return result;
    }

    public static string Decompress(string s)
    {
        string @string;
        using (MemoryStream stream = new(Convert.FromBase64String(s)))
        {
            using MemoryStream memoryStream = new();
            using (GZipStream gZipStream = new(stream, CompressionMode.Decompress))
            {
                gZipStream.CopyTo(memoryStream);
            }
            @string = Encoding.Unicode.GetString(memoryStream.ToArray());
        }
        return @string;
    }
}
