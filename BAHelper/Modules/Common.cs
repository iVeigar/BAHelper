using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BAHelper.Modules.Trapper;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.EzEventManager;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using Lumina.Excel.GeneratedSheets;
namespace BAHelper.Modules;

public static class Common
{
    public static bool InHydatos => Player.Territory == 827U;
    public static bool InBA => InHydatos && MeWorldPos.Y < 200f && MeCurrentArea != AreaTag.Entry;
    public static Vector3 MeWorldPos { get; private set; } = Vector3.Zero;
    public static AreaTag MeCurrentArea { get; private set; } = AreaTag.None;
    public static List<string> LogoActionNames { get; } = Svc.Data.GetExcelSheet<EurekaMagiaAction>()!.Select(row => row.Action.Value!.Name.ToDalamudString().TextValue.Replace("文理", string.Empty).Replace("的记忆", string.Empty).Replace("的加护", string.Empty)).ToList();

    static Common()
    {
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
    }

    private static void OnFrameworkUpdate()
    {
        if (!InHydatos) return;
        if (!Player.Available) return;
        MeWorldPos = Player.Object.Position;
        MeCurrentArea = Area.Locate(MeWorldPos)?.Tag ?? AreaTag.None;
    }

    public static bool IsInArea(this Vector3 pos, Area area) => pos.IsInRect(area.Origin, area.Dims);

    public static bool IsInRect(this Vector3 pos, Vector3 origin, Vector3 dims)
    {
        return pos.X.InRange(origin.X, dims.X) && pos.Z.InRange(origin.Z, dims.Z);
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
