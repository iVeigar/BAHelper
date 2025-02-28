using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BAHelper.Modules.Trapper;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.Toast;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.EzEventManager;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using Lumina.Excel.Sheets;
namespace BAHelper.Modules;

public static class Common
{
    public static bool InHydatos => Player.Territory == 827U;
    public static bool InBA => InHydatos && MeWorldPos.Y < 200f && MeCurrentArea != AreaTag.Entry;
    public static Vector3 MeWorldPos { get; private set; } = Vector3.Zero;
    public static AreaTag MeCurrentArea { get; private set; } = AreaTag.None;
    public static Dictionary<uint, string> LogoActionNames { get; } = Svc.Data.GetExcelSheet<EurekaMagiaAction>().ToDictionary(row => row.RowId, row => row.Action.Value.Name.ToDalamudString().TextValue.Replace("文理", string.Empty).Replace("的记忆", string.Empty).Replace("的加护", string.Empty));
    private static bool reminded = false;
    static Common()
    {
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
    }

    private static unsafe void OnFrameworkUpdate()
    {
        if (!InHydatos)
        {
            if (reminded) reminded = false;
            return;
        }
        
        if (!Player.Available) return;
        MeWorldPos = Player.Object.Position;
        MeCurrentArea = Area.Locate(MeWorldPos)?.Tag ?? AreaTag.None;
        if (Plugin.Config.ElementLevelReminderEnabled && !reminded && !GenericHelpers.IsOccupied())
        {
            var note = $"当前等级：\xE03A \xE06A.{Player.BattleChara->ForayInfo.Level}";
            Svc.Toasts.ShowQuest($"BA助手提示您{note}", new QuestToastOptions { IconId = 65060, PlaySound = true });
            Plugin.PrintMessage(note);
            reminded = true;
        }
    }

    public static bool IsInArea(this Vector3 pos, Area area) => pos.IsInRect(area.Origin, area.Dims);

    public static bool IsInRect(this Vector3 pos, Vector3 origin, Vector3 dims)
    {
        return pos.X.InRange(origin.X, origin.X + dims.X) && pos.Z.InRange(origin.Z, origin.Z + dims.Z);
    }

    public static (uint, uint) CarriedLogoActions(this IBattleChara? player)
    {
        uint param = player?.StatusList.FirstOrDefault(status => status.StatusId == 1618, null)?.Param ?? 0;
        return (param >> 8, param & 0xFF);
    }

    public static string CarriedLogoActionsStr(this IBattleChara? player)
    {
        var (logo1, logo2) = player.CarriedLogoActions();
        return $"{LogoActionNames[logo1]} {LogoActionNames[logo2]}".Trim();
    }

    public static bool InCombat(this IBattleChara chara)
    {
        return (chara.StatusFlags & StatusFlags.InCombat) != 0;
    }

    public static bool IsDead(this IBattleChara chara)
    {
        return chara.IsDead || chara.CurrentHp <= 0;
    }
}
