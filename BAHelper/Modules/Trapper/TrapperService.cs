using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using BAHelper.System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using ImGuiNET;
namespace BAHelper.Modules.Trapper;

public sealed partial class TrapperService : IDisposable
{
    [GeneratedRegex("^(?<result>.*?)隐藏的陷阱！$")] // 发现了 / 附近感觉到有 / 附近没感觉到
    private static partial Regex MyRegex();
    private readonly Configuration config;
    private readonly HashSet<Trap> TrapDiscovered = [];
    private readonly List<MobObject> MobObjects = [];
    private long LastEnumeratedAt = Environment.TickCount64;

    public AreaTag ChestFoundAt { get; private set; } = AreaTag.None;
    public ScanResult LastScanResult { get; private set; } = ScanResult.None;
    public HashSet<AreaTag> PossibleAreasOfPortal { get; } = [];

    public TrapperService()
    {
        config = DalamudApi.Config;
        DalamudApi.PluginInterface.UiBuilder.Draw += OnDraw;
        DalamudApi.Framework.Update += OnFrameworkUpdate;
        DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    }

    private static bool ShouldDraw()
    {
        return DalamudApi.Config.AdvancedModeEnabled &&
               !(DalamudApi.Condition[ConditionFlag.LoggingOut] ||
                 DalamudApi.Condition[ConditionFlag.BetweenAreas] ||
                 DalamudApi.Condition[ConditionFlag.BetweenAreas51]) &&
               Common.InHydatos && DalamudApi.ClientState.LocalPlayer != null &&
               DalamudApi.ObjectTable.Length > 0;
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!DalamudApi.Config.AdvancedModeEnabled || (XivChatType)((int)type & 0x7f) != XivChatType.SystemMessage)
            return;

        foreach (var payload in message.Payloads)
        {
            if (payload is TextPayload textPayload)
            {
                if (MyRegex().Match(textPayload.Text ?? string.Empty) is var match && match.Success)
                {
                    var result = match.Groups["result"].Value;
                    if (result.Contains("发现"))
                        LastScanResult = ScanResult.Discover;
                    else if (result.Contains("感觉到有"))
                        LastScanResult = ScanResult.Sense;
                    else
                        LastScanResult = ScanResult.NotSense;
                    return;
                }
            }
        }
    }

    public void OnTerritoryChanged(ushort t) => Reset();

    public void Reset()
    {
        Trap.ResetAll();
        TrapDiscovered.Clear();
        ChestFoundAt = AreaTag.None;
        CheckPortalStatus();
        Monitor.Enter(MobObjects);
        MobObjects.Clear();
        Monitor.Exit(MobObjects);
    }

    public void Dispose()
    {
        DalamudApi.PluginInterface.UiBuilder.Draw -= OnDraw;
        DalamudApi.ClientState.TerritoryChanged -= OnTerritoryChanged;
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    }
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Common.InHydatos) return;
        if (DalamudApi.ObjectTable == null) return;
        Trap.UpdateByScanResult(Common.MeWorldPos, LastScanResult);
        LastScanResult = ScanResult.None;
        var now = Environment.TickCount64;
        if (now - LastEnumeratedAt >= 200)
        {
            EnumerateAllObjects();
            CheckPortalStatus();
            LastEnumeratedAt = now;
        }
    }
    private void CheckPortalStatus()
    {
        PossibleAreasOfPortal.Clear();
        foreach (var portal in Trap.AllTraps.Select(kv => kv.Value).Where(p => p.Type == TrapType.Portal && p.ShouldDraw))
        {
            PossibleAreasOfPortal.Add(portal.AreaTag);
        }
    }
    public void OnDraw()
    {
        if (!ShouldDraw()) return;
        var drawList = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
        if (config.DrawRecordedTraps)
        {
            drawList.DrawTraps(Trap.AllTraps.Values);
            drawList.DrawTraps(TrapDiscovered, true);
        }
        if (config.DrawAreaBorder)
        {
            drawList.DrawAllAreasBorder();
        }
        if (config.DrawRecommendedScanningSpots)
        {
            drawList.DrawAllScanningSpots();
        }
        if (config.DrawMobViews)
        {
            if (!Monitor.TryEnter(MobObjects)) return;
            drawList.DrawMobs(MobObjects);
            Monitor.Exit(MobObjects);
        }
    }

    private void EnumerateAllObjects()
    {
        var entityList = new List<MobObject>();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            var areaTag = Area.Locate(obj.Position)?.Tag ?? AreaTag.None;
            if (CheckChestObject(obj, areaTag)) continue;
            if (CheckTrapObject(obj, areaTag)) continue;
            CheckMobObject(obj, entityList);
        }
        Monitor.Enter(MobObjects);
        MobObjects.Clear();
        MobObjects.AddRange(entityList);
        Monitor.Exit(MobObjects);
    }

    private bool CheckChestObject(GameObject obj, AreaTag areaTag)
    {
        if (ChestFoundAt == AreaTag.None && obj.ObjectKind == ObjectKind.Treasure)
        {
            if (areaTag == AreaTag.FireRoom1
                || areaTag == AreaTag.EarthRoom1
                || areaTag == AreaTag.LightningRoom1
                || areaTag == AreaTag.WindRoom1
                || areaTag == AreaTag.IceRoom1
                || areaTag == AreaTag.WaterRoom1)
            {
                ChestFoundAt = areaTag;
                ShowRoomGroup1Toast(areaTag);
                return true;
            }
        }
        return false;
    }
    
    private static void ShowRoomGroup1Toast(AreaTag areaTag, bool isChest = true)
    {
        var toast = $"{areaTag.Description()}{(isChest ? "箱" : "门")}！";
        if (!string.IsNullOrEmpty(toast))
        {
            var sb = new SeStringBuilder().AddText(toast);
            DalamudApi.ToastGui.ShowQuest(sb.BuiltString);
            Plugin.PrintMessage(sb.BuiltString);
            SoundManager.PlaySoundEffect(SoundEffect.SE_1);
        }
    }
    
    private bool CheckTrapObject(GameObject obj, AreaTag areaTag)
    {
        if (areaTag == AreaTag.None)
            return false;

        var trapType = obj.DataId switch
        {
            2009728U => TrapType.BigBomb,
            2009729U => TrapType.Portal,
            2009730U => TrapType.SmallBomb,
            _ => TrapType.None
        };
        if (trapType == TrapType.None && obj is BattleNpc { BattleNpcKind: BattleNpcSubKind.Enemy, NameId: 7958U})
        {
            trapType = (areaTag == AreaTag.CircularPlatform || areaTag == AreaTag.OctagonRoomFromRaiden || areaTag == AreaTag.OctagonRoomToRoomGroup2)
                ? TrapType.SmallBomb
                : TrapType.BigBomb;
        }
        if (trapType != TrapType.None)
        {
            OnTrapDiscovered(new()
            {
                Type = trapType,
                Location = obj.Position,
                ShouldDraw = true,
                AreaTag = areaTag,
            });
            return true;
        }
        return false;
    }
    
    private void OnTrapDiscovered(Trap discoveredTrap)
    {
        if (!TrapDiscovered.Add(discoveredTrap))
            return;

        if (discoveredTrap.IsInRecords)
        {
            Trap.AllTraps[discoveredTrap.ID].ShouldDraw = false;
            foreach (var id in discoveredTrap.GetComplementarySet())
                Trap.AllTraps[id].ShouldDraw = false;

            if (discoveredTrap.Type == TrapType.Portal)
                ShowRoomGroup1Toast(discoveredTrap.AreaTag, false);
        }
        else
        {
            var str = $"探出新的陷阱/门点位! {{{{id}}, new( {{id}}, TrapType.{discoveredTrap.Type}, new({discoveredTrap.Location.X}f, {discoveredTrap.Location.Y}f, {discoveredTrap.Location.Z}f), AreaTag.{discoveredTrap.AreaTag}) }},";
            DalamudApi.PluginLog.Info(str);
            Plugin.PrintMessage(str);
            // 新点位只可能在那两个只有一个易伤雷的房间
            if (Area.TryGet(discoveredTrap.AreaTag, out var area))
            {
                area.Traps.ForEach(trap => {trap.ShouldDraw = false;});
            }
        }
    }
    
    private static bool CheckMobObject(GameObject obj, List<MobObject> entityList)
    {
        if (!obj.IsValid() || obj is not BattleNpc bnpc || !bnpc.BattleNpcKind.Equals(BattleNpcSubKind.Enemy))
            return false;

        if (bnpc.IsDead() || bnpc.InCombat() || !MobInfo.Mobs.TryGetValue(bnpc.NameId, out var mobInfo))
            return true;

        entityList.Add(new(bnpc, mobInfo));
        return true;
    }
}

public static class DrawListExtension
{
    public static void DrawMobs(this ImDrawListPtr drawList, List<MobObject> mobObjects)
    {
        foreach(var mob in mobObjects)
            drawList.DrawMob(mob);
    }
    
    public static void DrawMob(this ImDrawListPtr drawList, MobObject mob)
    {
        if (mob.Position.Distance2D(Common.MeWorldPos) <= 50)
        {
            switch (mob.AggroType)
            {
                case AggroType.Sight:
                    drawList.DrawConeFromCenterPoint(mob.Position, mob.Rotation, mob.SightRadian, mob.AggroDistance, DalamudApi.Config.NormalAggroColor);
                    break;
                case AggroType.Sound:
                    drawList.DrawRingWorld(mob.Position, mob.AggroDistance, 1f, DalamudApi.Config.SoundAggroColor);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, DalamudApi.Config.SoundAggroColor.SetAlpha(0.4f), filled: true);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, DalamudApi.Config.SoundAggroColor);
                    break;
                case AggroType.Proximity:
                    drawList.DrawRingWorld(mob.Position, mob.AggroDistance, 1f, DalamudApi.Config.NormalAggroColor);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, DalamudApi.Config.NormalAggroColor.SetAlpha(0.4f), filled: true);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, DalamudApi.Config.NormalAggroColor);
                    break;
                case AggroType.Magic:
                case AggroType.Blood:
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, DalamudApi.Config.SoundAggroColor.SetAlpha(0.4f), filled: true);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, DalamudApi.Config.SoundAggroColor);
                    break;
                default:
                    break;
            }
        }
    }
    
    public static void DrawTrap(this ImDrawListPtr drawList, Trap trap, bool revealed)
    {
        var inView = DalamudApi.GameGui.WorldToScreen(trap.Location, out var screenPos);
        var distance = trap.Location.Distance2D(Common.MeWorldPos);

        uint color = trap.Type switch {
            TrapType.BigBomb => DalamudApi.Config.TrapBigBombColor,
            TrapType.SmallBomb => DalamudApi.Config.TrapSmallBombColor,
            TrapType.Portal => DalamudApi.Config.TrapPortalColor,
            _ => Color.White
        };

        if (DalamudApi.Config.DrawTrapBlastCircle || distance < trap.BlastRadius + 4.0f)
            drawList.DrawRingWorld(trap.Location, trap.BlastRadius, 1.3f, color.SetAlpha(0.4f));

        if (revealed)
            color = DalamudApi.Config.RevealedTrapColor;
        if (drawList.DrawRingWorld(trap.Location, trap.HitBoxRadius, 1.5f, color) && inView)
            drawList.AddCircleFilled(screenPos, 1.5f, color); // 画中心点

        if (DalamudApi.Config.DrawTrap15m || trap.ShouldDraw && distance < 19.0f)
            drawList.DrawRingWorld(trap.Location, 15f, 1f, DalamudApi.Config.Trap15mCircleColor);

        if (DalamudApi.Config.DrawTrap36m || (trap.AreaTag == AreaTag.EarthRoom1 || trap.AreaTag == AreaTag.FireRoom1) && distance < 40.0f)
            drawList.DrawRingWorld(trap.Location, 36f, 1f, DalamudApi.Config.Trap36mCircleColor, true);
    }
    
    public static void DrawTraps(this ImDrawListPtr drawList, IEnumerable<Trap> traps, bool revealed = false)
    {
        foreach(var trap in traps.Where(trap => trap.ShouldDraw && trap.Location.Distance2D(Common.MeWorldPos) < DalamudApi.Config.TrapViewDistance))
            drawList.DrawTrap(trap, revealed);
    }

    public static void DrawAreaBorder(this ImDrawListPtr drawList, AreaTag areaTag)
    {
        if (Area.TryGet(areaTag, out var area))
            drawList.DrawAreaBorder(area);
    }

    public static void DrawAreaBorder(this ImDrawListPtr drawList, Area area)
    {
        drawList.DrawRectWorld(area.Origin, area.Dims);
    }
    
    public static void DrawAllAreasBorder(this ImDrawListPtr drawList)
    {
        drawList.DrawAreaBorder(AreaTag.Entry);
        drawList.DrawAreaBorder(AreaTag.CorridorFromArt);
        drawList.DrawAreaBorder(AreaTag.CorridorFromOwain);
        drawList.DrawAreaBorder(AreaTag.CircularPlatform);
        drawList.DrawAreaBorder(AreaTag.OctagonRoomFromRaiden);
        drawList.DrawAreaBorder(AreaTag.OctagonRoomToRoomGroup1);
        drawList.DrawAreaBorder(AreaTag.RoomGroup1);
        drawList.DrawAreaBorder(AreaTag.IceRoom1);
        drawList.DrawAreaBorder(AreaTag.LightningRoom1);
        drawList.DrawAreaBorder(AreaTag.FireRoom1);
        drawList.DrawAreaBorder(AreaTag.WaterRoom1);
        drawList.DrawAreaBorder(AreaTag.WindRoom1);
        drawList.DrawAreaBorder(AreaTag.EarthRoom1);
        drawList.DrawAreaBorder(AreaTag.OctagonRoomToRoomGroup2);
        drawList.DrawAreaBorder(AreaTag.RoomGroup2);
        drawList.DrawAreaBorder(AreaTag.IceRoom2);
        drawList.DrawAreaBorder(AreaTag.LightningRoom2);
        drawList.DrawAreaBorder(AreaTag.FireRoom2);
        drawList.DrawAreaBorder(AreaTag.WaterRoom2);
        drawList.DrawAreaBorder(AreaTag.WindRoom2);
        drawList.DrawAreaBorder(AreaTag.EarthRoom2);
    }
    
    public static void DrawAreaScanningSpots(this ImDrawListPtr drawList, AreaTag areaTag)
    {
        if (Area.TryGet(areaTag, out var area) && area.ShowScanningSpot)
        {
            foreach(var (Center, Radius, Tip) in area.ScanningSpots.Where(p => p.Center.Distance2D(Common.MeWorldPos) < DalamudApi.Config.TrapViewDistance))
            {
                var filled = Center.Distance2D(Common.MeWorldPos) <= Radius;
                if (filled)
                    drawList.DrawRingWorld(Center, Radius, 1.5f, DalamudApi.Config.ScanningSpotColor.SetAlpha(0.20f), filled: filled);
                drawList.DrawRingWorldWithText(Center, Radius, 1.5f, DalamudApi.Config.ScanningSpotColor, Tip);
                if (DalamudApi.Config.DrawScanningSpot15m)
                {
                    drawList.DrawRingWorld(Center, 15.0f - Radius, 1.5f, DalamudApi.Config.ScanningSpot15mCircleColor);
                }
                if (DalamudApi.Config.DrawScanningSpot36m)
                {
                    drawList.DrawRingWorld(Center, 36.0f - Radius, 1.5f, DalamudApi.Config.ScanningSpot36mCircleColor);
                }
            }
        }
    }
    
    public static void DrawAllScanningSpots(this ImDrawListPtr drawList)
    {
        drawList.DrawAreaScanningSpots(AreaTag.CorridorFromArt);
        drawList.DrawAreaScanningSpots(AreaTag.CorridorFromOwain);
        drawList.DrawAreaScanningSpots(AreaTag.CircularPlatform);
        drawList.DrawAreaScanningSpots(AreaTag.OctagonRoomFromRaiden);
        drawList.DrawAreaScanningSpots(AreaTag.OctagonRoomToRoomGroup1);   
        drawList.DrawAreaScanningSpots(AreaTag.FireRoom1);
        drawList.DrawAreaScanningSpots(AreaTag.EarthRoom1);
        drawList.DrawAreaScanningSpots(AreaTag.LightningRoom1);
        drawList.DrawAreaScanningSpots(AreaTag.WindRoom1);
        drawList.DrawAreaScanningSpots(AreaTag.IceRoom1);
        drawList.DrawAreaScanningSpots(AreaTag.WaterRoom1);
        drawList.DrawAreaScanningSpots(AreaTag.OctagonRoomToRoomGroup2);
        drawList.DrawAreaScanningSpots(AreaTag.IceRoom2);
        drawList.DrawAreaScanningSpots(AreaTag.LightningRoom2);
        drawList.DrawAreaScanningSpots(AreaTag.FireRoom2);
        drawList.DrawAreaScanningSpots(AreaTag.WaterRoom2);
        drawList.DrawAreaScanningSpots(AreaTag.WindRoom2);
        drawList.DrawAreaScanningSpots(AreaTag.EarthRoom2);
    }
}