using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BAHelper.Modules.Trapper;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;

namespace BAHelper.Modules;

public static class Common
{
    public static bool InHydatos => DalamudApi.ClientState.TerritoryType == 827U;
    public static bool InBA => InHydatos && MeWorldPos.Y < 200f && MeCurrentArea != AreaTag.Entry;
    public static Vector3 MeWorldPos { get; private set; } = Vector3.Zero;
    public static AreaTag MeCurrentArea { get; private set; } = AreaTag.None;
    public static List<string> LogoActionNames { get; } = Svc.Data.GetExcelSheet<EurekaMagiaAction>()!.Select(row => row.Action.Value!.Name.ToDalamudString().TextValue.Replace("文理", string.Empty).Replace("的记忆", string.Empty).Replace("的加护", string.Empty)).ToList();

    public static void Initialize()
    {
        DalamudApi.Framework.Update += OnFrameworkUpdate;
    }
    
    public static void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
    }
    
    private static void OnFrameworkUpdate(IFramework framework)
    {
        if (!InHydatos) return;
        if (DalamudApi.ObjectTable == null) return;
        MeWorldPos = DalamudApi.ClientState.LocalPlayer.Position;
        MeCurrentArea = Area.Locate(MeWorldPos)?.Tag ?? AreaTag.None;
    }
    
    public static bool IsInArea(this Vector3 pos, AreaTag areaTag) => Area.TryGet(areaTag, out var area) && pos.IsInArea(area);

    public static bool IsInArea(this Vector3 pos, Area area) => pos.IsInRect(area.Origin, area.Dims);

    public static bool IsInRect(this Vector3 pos, Vector3 origin, Vector3 dims)
    {
        var diff = pos - origin;
        return diff.X >= 0 && diff.Z >= 0 && diff.X <= dims.X && diff.Z <= dims.Z;
    }

    public static (uint, uint) CarriedLogoActions(this BattleChara? player)
    {
        uint param = player?.StatusList.FirstOrDefault(status => status.StatusId == 1618, null)?.Param ?? 0;
        return (param >> 8, param & 0xFF);
    }

    public static bool InCombat(this BattleChara chara)
    {
        return (chara.StatusFlags & StatusFlags.InCombat) != 0;
    }
    
    public static bool IsDead(this BattleChara chara)
    {
        return chara.IsDead || chara.CurrentHp <= 0;
    }
}
