using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
namespace BAHelper.Modules.Party;

public class PartyService
{
    private static Configuration Config => Plugin.Config;
    private readonly TaskManager TaskManager = new();
    private static readonly HashSet<uint> HaveOffHandJobCategories = [2, 7, 8, 20];
    public bool IsBusy => TaskManager.IsBusy;

    // note: "Portal" means the unstable / stable portal, not the light-green one in the BA dungeon
    // Global server player
    private static readonly List<List<(float X, float Y)>> PortalsMapGlobal =
    [
        [(4.2f, 15.3f), (7.2f, 14.7f), (10.5f, 14.6f), (6.9f, 18.5f), (10.1f, 18.0f), (6.2f, 21.7f), (4.3f, 26.2f), (7.3f, 29.3f)],
        [(15.8f, 14.5f), (16.4f, 18.7f), (10.6f, 21.9f), (15.9f, 22.3f), (13.5f, 24.2f), (10.7f, 25.9f), (14.7f, 26.8f), (16.7f, 29.2f)],
        [(21.2f, 17.1f), (19.3f, 19.9f), (23.7f, 21.8f), (19.1f, 26.8f), (23.0f, 26.9f), (19.1f, 28.6f), (20.7f, 28.9f), (22.8f, 29.1f)],
        [(25.0f, 16.3f), (26.6f, 18.9f), (28.8f, 23.3f), (25.1f, 25.4f), (26.7f, 26.7f), (26.0f, 27.7f), (25.4f, 28.9f), (25.6f, 30.2f)],
        [(28.8f, 15.3f), (33.3f, 17.0f), (29.9f, 19.5f), (32.4f, 24.7f), (29.4f, 26.3f), (30.7f, 28.5f), (30.2f, 30.1f), (31.7f, 29.9f)],
        [(35.3f, 13.8f), (37.9f, 15.0f), (36.0f, 19.0f), (37.4f, 23.8f), (33.0f, 27.7f), (37.3f, 27.8f), (32.6f, 28.8f), (35.8f, 29.9f)]
    ];
    // CN server - Moogle Data Center player
    private static readonly List<List<(float X, float Y)>> PortalsMapMoogleDC =
    [
        [(4.2f, 15.3f), (7.2f, 14.7f), (6.9f, 18.5f), (6.2f, 21.7f), (10.6f, 21.9f), (4.3f, 26.2f), (10.7f, 25.9f), (7.3f, 29.3f)],
        [(10.5f, 14.6f), (10.1f, 18.0f), (15.9f, 22.3f), (13.5f, 24.2f), (14.7f, 26.8f), (19.1f, 26.8f), (16.7f, 29.2f), (19.1f, 28.6f)],
        [(15.8f, 14.5f), (16.4f, 18.7f), (21.2f, 17.1f), (25.0f, 16.3f), (28.8f, 15.3f), (19.3f, 19.9f), (26.6f, 18.9f), (23.7f, 21.8f)],
        [(20.7f, 28.9f), (23.0f, 26.9f), (25.1f, 25.4f), (22.8f, 29.1f), (26.7f, 26.7f), (26.0f, 27.7f), (25.4f, 28.9f), (25.6f, 30.2f)],
        [(35.3f, 13.8f), (37.9f, 15.0f), (33.3f, 17.0f), (36.0f, 19.0f), (29.9f, 19.5f), (37.4f, 23.8f), (28.8f, 23.3f), (32.4f, 24.7f)],
        [(29.4f, 26.3f), (33.0f, 27.7f), (37.3f, 27.8f), (30.7f, 28.5f), (32.6f, 28.8f), (35.8f, 29.9f), (30.2f, 30.1f), (31.7f, 29.9f)]
    ];


    public void SendPortalsToChat(int partyNumber)
    {
        var portals = Config.IsCNMoogleDCPlayer ? PortalsMapMoogleDC : PortalsMapGlobal;
        var channel = Config.UsePartyChannel ? "p" : "e";
        if (partyNumber < 1 || partyNumber > 6)
            return;
        TaskManager.Enqueue(() => MacroManager.Execute($"/{channel} 我们是{partyNumber}队，门图如下："));
        for (int i = 0; i < 8; i++)
        {
            TaskManager.DelayNext(100);
            var (x, y) = portals[partyNumber - 1][i];
            TaskManager.Enqueue(() => Utils.SetFlagMarker(827, 515, x, y));
            var portalNumber = i + 1;
            TaskManager.Enqueue(() => MacroManager.Execute($"/{channel} {partyNumber}{portalNumber} - <flag>"));
        }
    }

    public unsafe void CheckMembersEurekaBonus()
    {
        TaskManager.Enqueue(() =>
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CharacterInspect", out var addon) &&
                GenericHelpers.IsAddonReady(addon) && EzThrottler.Throttle("CheckMembersEurekaBonus"))
            {
                AgentInspect.Instance()->AgentInterface.Hide();
            }
        });
        foreach (var member in Svc.Party)
        {
            TaskManager.Enqueue(() =>
            {
                if (!EzThrottler.Throttle("CheckMembersEurekaBonus")) return false;
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("CharacterInspect", out var addon) ||
                    !GenericHelpers.IsAddonReady(addon))
                {
                    AgentInspect.Instance()->ExamineCharacter(member.ObjectId);
                    return false;
                }
                var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
                if (container == null)
                {
                    AgentInspect.Instance()->ExamineCharacter(member.ObjectId);
                    return false;
                }

                short totalEB = 0;
                var itemSlotAmount = 11;
                for (var i = 0; i < 13; i++)
                {
                    if (i == 0)
                    {
                        var mainHand = Svc.Data.GetExcelSheet<Item>().GetRow(container->GetInventorySlot(i)->ItemID);
                        var category = mainHand.ClassJobCategory.Row;
                        if (HaveOffHandJobCategories.Contains(category))
                            itemSlotAmount++;
                    }

                    if (i == 1 && itemSlotAmount != 12) continue;

                    // 腰带
                    if (i == 5) continue;

                    var slot = container->GetInventorySlot(i);
                    if (slot == null) continue;

                    var itemID = slot->ItemID;
                    var item = Svc.Data.GetExcelSheet<Item>().GetRow(itemID);

                    if (item.ItemSpecialBonus.Row == 7) // 优雷卡专用效果
                    {
                        totalEB += item.UnkData73.FirstOrDefault(b => b.BaseParamSpecial == 36)?.BaseParamValueSpecial ?? 0; // 元素加持
                    }
                }

                var ssb = new SeStringBuilder();
                ssb.AddUiForeground(25);
                ssb.Add(new PlayerPayload(member.Name.TextValue, member.World.Id));
                ssb.AddUiForegroundOff();
                ssb.Append($" ({member.ClassJob.GameData.Name.RawString})");
                ssb.Append($" 元素加持: ").AddUiForeground(totalEB.ToString(), (ushort)(totalEB > 0 ? 43 : 17));

                Svc.Chat.Print(ssb.Build());

                AgentInspect.Instance()->AgentInterface.Hide();
                return true;
            });
        }
    }
}
