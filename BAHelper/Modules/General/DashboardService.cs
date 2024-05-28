using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.EzEventManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Lumina.Excel.GeneratedSheets;
namespace BAHelper.Modules.General;

public sealed class DashboardService
{
    public List<(uint ObjectId, string Name, string Job, string Logos, bool StanceActivated)> Tanks { get; } = [];

    public DashboardService()
    {
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
    }

    private void OnFrameworkUpdate()
    {
        if (Svc.Objects == null)
        {
            if (Tanks.Count > 0)
                Tanks.Clear();
            return;
        }
        if (EzThrottler.Throttle("LeaderService-Check"))
        {
            FindTanks();
        }
    }

    private void FindTanks()
    {
        Tanks.Clear();
        foreach (var p in Svc.Objects.OfType<PlayerCharacter>())
        {
            //剑术师 斧术师 骑士 战士 暗黑骑士 绝枪战士
            if (!Svc.Data.GetExcelSheet<ClassJobCategory>().GetRow(59)!.IsJobInCategory(p.GetJob()))
                continue;
            Tanks.Add((
                p.ObjectId,
                p.Name.TextValue,
                p.ClassJob.GameData.Name.RawString,
                p.CarriedLogoActionsStr(),
                p.StatusList.Any(status => status.StatusId.EqualsAny(79u, 91u, 743u, 1833u))
            ));
        }
    }
}