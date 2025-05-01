using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BAHelper.Modules.Trapper;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
    public static Dictionary<uint, uint> Wisdoms { get; } = new()
    {
        {1, 1631}, //术士的记忆
        {2, 1632}, //斗士的记忆
        {3, 1633}, //重骑兵的记忆
        {4, 1634}, //守护者的记忆
        {5, 1635}, //祭司的记忆
        {6, 1636}, //武人的记忆
        {7, 1637}, //斥候的记忆
        {8, 1638}, //圣骑士的记忆
        {9, 1639}, //狂战士的记忆
        {10, 1640}, //盗贼的记忆
        {52, 1739}, //贤者的记忆
        {53, 1740}, //剑豪的记忆
        {54, 1741}, //弓圣的记忆
        {55, 1742} //豪杰的记忆
    };

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

    public static (uint, uint) CarriedLogoActions(this IPlayerCharacter? player)
    {
        uint param = player?.StatusList.FirstOrDefault(status => status.StatusId == 1618, null)?.Param ?? 0;
        uint logo1 = param >> 8, logo2 = param & 0xFF;
        // 调整前后顺序,把记忆放在第一个
        if (!Wisdoms.ContainsKey(logo1) && Wisdoms.ContainsKey(logo2))
            return (logo2, logo1);
        return (logo1, logo2);
    }

    public static bool InCombat(this IBattleChara chara) => (chara.StatusFlags & StatusFlags.InCombat) != 0;

    public static bool IsDead(this IBattleChara chara) => chara.IsDead || chara.CurrentHp <= 0;

    public static bool HasStatus(this IPlayerCharacter player, params uint[] statusIds) => player.StatusList.Any(status => status.StatusId.EqualsAny(statusIds));
    
    public static bool IsTankStanceActive(this IPlayerCharacter player) => player.HasStatus(79u, 91u, 743u, 1833u);

    // normal vulnerability up
    public static bool HasVulnerabilityUp(this IPlayerCharacter player) => player.HasStatus(202u, 714u, 1412u, 1597u, 1789u, 2213u, 2912u, 3557u);

    public static bool IsSpiritOfTheRememberedActive(this IPlayerCharacter player) => player.HasStatus(1641u);
}
