using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.EzEventManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BAHelper.Modules.Trapper;

public class TrapperTool
{
    public static bool IsRunning { get; private set; } = false;
    private static Configuration Config => Plugin.Config;
    private static bool protectMinded = false;
    private static bool shellMinded = false;
    private static bool canChangeTarget = true;
    private static bool protectCasted = false;
    private static bool shellCasted = false;

    private static unsafe bool ExecuteActionSafe(ActionType type, uint actionId, ulong targetId = GameObject.InvalidGameObjectId)
    {
        if (!EzThrottler.Throttle("action", ActionManager.GetAdjustedRecastTime(type, actionId) + 100))
            return false;
        ActionManager.Instance()->UseAction(type, actionId, targetId);
        return true;
    }

    private static bool CastLogoProtect(GameObject? target)
        => ExecuteActionSafe(ActionType.Action, 12969, target?.ObjectId ?? GameObject.InvalidGameObjectId);

    private static bool CastLogoShell(GameObject? target)
        => ExecuteActionSafe(ActionType.Action, 12970, target?.ObjectId ?? GameObject.InvalidGameObjectId);

    public static PlayerCharacter? NextShieldTarget()
    {
        var checkStatusProtect = Config.CheckStatusProtect;
        var checkStatusShell = Config.CheckStatusShell;
        var timeThreshold = Config.ShieldRemainingTimeThreshold * 60;
        bool check(PlayerCharacter player)
        {
            float protectRemainingTime = 0f;
            float shellRemainingTime = 0f;
            bool protectFound = false;
            bool shellFound = false;
            foreach (var status in player.StatusList)
            {
                if (checkStatusProtect && !protectFound || checkStatusShell && !shellFound)
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
            return checkStatusProtect && protectRemainingTime <= timeThreshold
                || checkStatusShell && shellRemainingTime <= timeThreshold;
        }
        if (checkStatusProtect || checkStatusShell)
        {
            var current = Svc.Targets.Target as PlayerCharacter;
            // 排除当前目标, 允许在给当前目标上盾完毕之前就切换目标
            var target = Svc.Objects
                .OfType<PlayerCharacter>()
                .Where(p => (current is null || p.ObjectId != current.ObjectId) && p.IsTargetable && p.Position.Distance(Common.MeWorldPos) < 25.0f)
                .FirstOrDefault(check);
            return target;
        }
        return null;
    }
    public TrapperTool()
    {
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
    }
    public static void OnFrameworkUpdate()
    {
        if (!IsRunning)
            return;

        var checkStatusProtect = Config.CheckStatusProtect;
        var checkStatusShell = Config.CheckStatusShell;

        var (action1, action2) = Player.Object.CarriedLogoActions();
        protectMinded = action1 == 12 || action2 == 12;
        shellMinded = action1 == 13 || action2 == 13;

        if (!checkStatusProtect && !checkStatusShell
            || checkStatusProtect && !protectMinded
            || checkStatusShell && !shellMinded)
        {
            IsRunning = false;
            return;
        }

        if (canChangeTarget)
        {
            var target = NextShieldTarget();
            if (target is null)
            {
                IsRunning = false;
                return;
            }
            Svc.Targets.Target = target;
            canChangeTarget = false;
            protectCasted = shellCasted = false;
        }

        if (checkStatusProtect && !protectCasted && CastLogoProtect(Svc.Targets.Target))
        {
            protectCasted = true;
            EzThrottler.Throttle("changeTarget", 1000);
            return;
        }

        if (checkStatusShell && !shellCasted && CastLogoShell(Svc.Targets.Target))
        {
            shellCasted = true;
            EzThrottler.Throttle("changeTarget", 1000);
            return;
        }

        if ((!checkStatusProtect || protectCasted) && (!checkStatusShell || shellCasted))
        {
            if (EzThrottler.Throttle("changeTarget", 1000)) // 延迟1s后切目标
            {
                canChangeTarget = true;
            }
        }
    }

    public static void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    public static void Start()
    {
        Svc.Targets.Target = null;
        canChangeTarget = true;
        IsRunning = true;
    }

    public static void Stop() => IsRunning = false;
}
