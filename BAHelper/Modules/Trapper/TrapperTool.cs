using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BAHelper.Modules.Trapper;

public class TrapperTool()
{
    public static bool IsRunning => TaskManager.IsBusy;

    private static readonly TaskManager TaskManager = new() { ShowDebug = true};

    private static Configuration Config => Plugin.Config;

    public static void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    public static void Start()
    {
        if (IsRunning) return;
        TaskManager.Enqueue(DoNextCast, "DoNextCast");
    }

    public static void Stop() => TaskManager.Abort();

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

    private static IGameObject? NextShieldTarget(bool protect, bool shell)
    {
        var timeThreshold = Config.ShieldRemainingTimeThreshold * 60;
        bool check(IPlayerCharacter player)
        {
            float protectRemainingTime = 0f;
            float shellRemainingTime = 0f;
            bool protectFound = false;
            bool shellFound = false;
            foreach (var status in player.StatusList)
            {
                if (protect && !protectFound || shell && !shellFound)
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
                }
                else break;
            }
            return protect && protectRemainingTime <= timeThreshold
                || shell && shellRemainingTime <= timeThreshold;
        }
        if (protect || shell)
        {
            var current = Svc.Targets.Target as IPlayerCharacter;
            // 排除当前目标, 允许在给当前目标上盾完毕之前就切换目标
            var target = Svc.Objects.OfType<IPlayerCharacter>()
                .Where(p => (current is null || p.GameObjectId != current.GameObjectId) && p.IsTargetable && p.Position.Distance(Common.MeWorldPos) < 25.0f && check(p))
                .OrderBy(p => p.Position.Distance(Common.MeWorldPos))
                .FirstOrDefault();
            return target;
        }
        return null;
    }

    private static bool? DoNextCast()
    {
        var (action1, action2) = Player.Object.CarriedLogoActions();
        var hasProtect = action1 == 12 || action2 == 12;
        var hasShell = action1 == 13 || action2 == 13;

        if (!hasProtect && !hasShell)
            return null;

        var target = NextShieldTarget(hasProtect, hasShell);
        if (target is null)
            return null;

        Svc.Targets.Target = target;
        if (hasProtect)
        {
            TaskManager.Enqueue(() => ExecuteActionSafe(ActionType.Action, 12969, target.GameObjectId), "CastProtect");
            TaskManager.DelayNext(1000);
        }
        if (hasShell)
        {
            TaskManager.Enqueue(() => ExecuteActionSafe(ActionType.Action, 12970, target.GameObjectId), "CastShell");
            TaskManager.DelayNext(1000);
        }
        TaskManager.Enqueue(DoNextCast, "DoNextCast");
        return true;
    }

}
