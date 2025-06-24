using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BAHelper.Modules.Trapper;

public static class TrapperTool
{
    public static bool IsRunning => TaskManager.IsBusy;

    private static readonly TaskManager TaskManager = new() { ShowDebug = true};

    private static Configuration Config => Plugin.Config;

    public static void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    private static unsafe bool ExecuteActionSafe(ActionType type, uint actionId, ulong targetId = 0xE0000000)
    {
        var total = ActionManager.GetAdjustedRecastTime(type, actionId);
        var elapsed = (int)(ActionManager.Instance()->GetRecastTimeElapsed(type, actionId) * 1000);
        if (elapsed == 0 && ActionManager.Instance()->GetActionStatus(type, actionId) == 0) 
        {
            ActionManager.Instance()->UseAction(type, actionId, targetId);
        }
        else if (total - elapsed < 500 && !ActionManager.Instance()->ActionQueued)
        {
            ActionManager.Instance()->UseAction(type, actionId, targetId, mode: ActionManager.UseActionMode.Queue);
        }
        else 
            return false;
        return true;
    }
    private static (bool, bool) Candidate(IPlayerCharacter player, bool protect, bool shell, int timeThreshold)
    {
        float protectRemainingTime = 0f;
        float shellRemainingTime = 0f;
        bool protectFound = false;
        bool shellFound = false;
        foreach (var status in player.StatusList)
        {
            switch (status.StatusId)
            {
                case 1642: // 文理护盾statusid 1642
                    protectFound = true;
                    protectRemainingTime = status.RemainingTime;
                    break;
                case 1643: // 文理魔盾statusid 1643
                    shellFound = true;
                    shellRemainingTime = status.RemainingTime;
                    break;
                default:
                    break;
            }

            if ((!protect || protectFound) && (!shell || shellFound))
                break;
        }
        return (protect && protectRemainingTime <= timeThreshold, shell && shellRemainingTime <= timeThreshold);
    }
    public static void Stop() => TaskManager.Abort();

    private static void Start()
    {
        var (action1, action2) = Player.Object.CarriedLogoActions();
        var hasProtect = action1 == 12 || action2 == 12;
        var hasShell = action1 == 13 || action2 == 13;

        if (!hasProtect && !hasShell)
            return;
        var timeThreshold = Config.ShieldRemainingTimeThreshold * 60;
        foreach (var player in Svc.Objects.OfType<IPlayerCharacter>().Where(p => !p.IsDead && p.IsTargetable && p.Position.Distance(Player.Position) < 25.0f).OrderBy(p => p.Position.Distance(Player.Position)))
        {
            var (needProtect, needShell) = Candidate(player, hasProtect, hasShell, timeThreshold);
            var id = player.GameObjectId;
            var name = player.Name;
            if (needProtect)
            {
                TaskManager.Enqueue(() => ExecuteActionSafe(ActionType.Action, 12969, id), $"Cast Protect to {name}");
                TaskManager.DelayNext(1000);
            }
            if (needShell)
            {
                TaskManager.Enqueue(() => ExecuteActionSafe(ActionType.Action, 12970, id), $"Cast Shell to {name}");
                TaskManager.DelayNext(1000);
            }
        }
    }
}
