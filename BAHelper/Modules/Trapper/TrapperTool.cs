using System.Linq;
using BAHelper.Helpers;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BAHelper.Modules.Trapper;

public class TrapperTool
{
    public static bool IsRunning { get; private set; } = false;

    private static bool protectMinded = false;
    private static bool shellMinded = false;
    private static bool canChangeTarget = true;
    private static bool protectCasted = false;
    private static bool shellCasted = false;

    private static readonly Throttle _action = new();
    private static readonly Throttle _changeTarget = new();
    private static unsafe bool ExecuteActionSafe(ActionType type, uint actionId, ulong targetId = GameObject.InvalidGameObjectId)
        => _action.Exec(() => ActionManager.Instance()->UseAction(type, actionId, targetId), ActionManager.GetAdjustedRecastTime(type, actionId) + 100);

    private static bool CastLogoProtect(GameObject? target)
        => ExecuteActionSafe(ActionType.Action, 12969, target?.ObjectId ?? GameObject.InvalidGameObjectId);
    
    private static bool CastLogoShell(GameObject? target)
        => ExecuteActionSafe(ActionType.Action, 12970, target?.ObjectId ?? GameObject.InvalidGameObjectId);

    public static PlayerCharacter? NextShieldTarget()
    {
        var checkStatusProtect = DalamudApi.Config.CheckStatusProtect; 
        var checkStatusShell = DalamudApi.Config.CheckStatusShell;
        var timeThreshold = DalamudApi.Config.ShieldRemainingTimeThreshold * 60;
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
            var current = DalamudApi.TargetManager.Target as PlayerCharacter;
            // 排除当前目标, 允许在给当前目标上盾完毕之前就切换目标
            var target = DalamudApi.ObjectTable
                .OfType<PlayerCharacter>()
                .Where(p => (current is null || p.ObjectId != current.ObjectId) && p.IsTargetable && p.Position.Distance(DalamudApi.ClientState.LocalPlayer.Position) < 25.0f)
                .FirstOrDefault(check, null);
            return target;
        }
        return null;
    }
    public static void Initialize()
    {
        DalamudApi.Framework.Update += Tick;
    }
    public static void Dispose()
    {
        DalamudApi.Framework.Update -= Tick;
    }
    public static void Tick(IFramework framework)
    {
        if (!IsRunning)
            return;

        var checkStatusProtect = DalamudApi.Config.CheckStatusProtect;
        var checkStatusShell = DalamudApi.Config.CheckStatusShell;

        var (action1, action2) = DalamudApi.ClientState.LocalPlayer.CarriedLogoActions();
        protectMinded = action1 == 26 || action2 == 26;
        shellMinded = action1 == 27 || action2 == 27;

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
            DalamudApi.TargetManager.Target = target;
            canChangeTarget = false;
            protectCasted = shellCasted = false;
        }

        if (checkStatusProtect && !protectCasted && CastLogoProtect(DalamudApi.TargetManager.Target))
        {
            protectCasted = true;
            _changeTarget.Exec(() => { }, 1000);
            return;
        }
        
        if (checkStatusShell && !shellCasted && CastLogoShell(DalamudApi.TargetManager.Target))
        {
            shellCasted = true;
            _changeTarget.Exec(() => { }, 1000);
            return;
        }

        if ((!checkStatusProtect || protectCasted) && (!checkStatusShell || shellCasted))
        {
            if(_changeTarget.Exec(() => { }, 1000)) // 延迟1s后切目标
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
        DalamudApi.TargetManager.Target = null;
        canChangeTarget = true;
        IsRunning = true;
    }

    public static void Stop()
    {
        IsRunning = false;
    }
}
