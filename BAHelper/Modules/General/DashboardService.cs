using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.EzEventManager;
using ECommons.Throttlers;
namespace BAHelper.Modules.General;

public sealed class DashboardService
{
    public List<(ulong ObjectId, string Name, string Job, string Logos, string Description)> Players { get; } = [];

    public DashboardService()
    {
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
    }

    private void OnFrameworkUpdate()
    {
        if (!Common.InHydatos)
            return;
        if (Svc.Objects == null)
        {
            if (Players.Count > 0)
                Players.Clear();
            return;
        }
        if (EzThrottler.Throttle("DashboardService-CheckPlayers"))
        {
            CheckPlayers();
        }
    }

    private void CheckPlayers()
    {
        Players.Clear();
        foreach (var p in Svc.Objects.OfType<IPlayerCharacter>())
        {
            if (Check(p, out var result, out var sticky))
            {
                if (!sticky)
                    Players.Add(result);
                else
                    Players.Insert(0, result);
            }
        }
    }

    private static bool Check(IPlayerCharacter player, out (ulong ObjectId, string Name, string Job, string Logos, string Description) result, out bool sticky)
    {
        var failed = false;
        sticky = false;
        var descriptions = new List<string>();
        var logos = player.CarriedLogoActions();
        var jobId = player.ClassJob.RowId;
        var job = (Job)jobId;
        do
        {
            // 初始职业
            if (jobId >= 1 && jobId <= 7 || jobId == 26 || jobId == 29)
            {
                failed = true;
                descriptions.Add("未转职");
                break;
            }

            // 英杰
            if (!player.IsSpiritOfTheRememberedActive())
            {
                failed = true;
                descriptions.Add("无英杰");
            }

            // 易伤
            if (player.HasVulnerabilityUp())
            {
                failed = true;
                descriptions.Add("易伤");
            }

            // 携带但没开记忆
            if (Common.Wisdoms.TryGetValue(logos.Item1, out var statusId))
            {
                if (!player.HasStatus(statusId)) //携带但没开记忆
                {
                    failed = true;
                    descriptions.Add("没开记忆");
                }
            }

            // 检查文理
            switch (job)
            {
                case Job.PLD:
                case Job.WAR:
                case Job.DRK:
                case Job.GNB:
                    // 列出非斗双T 和开盾的T
                    if (logos != (2, 49))
                    {
                        failed = true;
                        sticky = true;
                    }
                    if (player.IsTankStanceActive())
                    {
                        failed = true;
                        sticky = true;
                        descriptions.Insert(0, "盾姿");
                    }
                    break;
                case Job.MNK:
                case Job.DRG:
                case Job.NIN:
                case Job.SAM:
                case Job.RPR:
                case Job.VPR:
                    // 剑双
                    if (logos != (53, 49))
                        failed = true;
                    break;
                case Job.BRD:
                case Job.MCH:
                case Job.DNC:
                    // 弓扎
                    if (logos != (54, 50))
                        failed = true;
                    break;
                case Job.WHM:
                case Job.SCH:
                case Job.AST:
                case Job.SGE:
                    // 圣骑+醒神  圣骑+勇气
                    if (logos != (8, 40) && logos != (8, 45))
                        failed = true;
                    break;
                case Job.BLM:
                case Job.SMN:
                case Job.RDM:
                case Job.PCT:
                    // 贤爆
                    if (logos != (52, 48))
                        failed = true;
                    break;
                default:
                    break;
            }
        } while (false);

        if (failed)
        {
            result = (
                    player.GameObjectId,
                    player.Name.TextValue,
                    player.ClassJob.Value.Name.ToString(),
                    LogoActionsStr(player.CarriedLogoActions()),
                    descriptions.Join("; ")
                );
        }
        else
            result = default;

        return failed;
    }
    private static string LogoActionsStr((uint, uint) logos) => $"{Common.LogoActionNames[logos.Item1]} {Common.LogoActionNames[logos.Item2]}".Trim();
}