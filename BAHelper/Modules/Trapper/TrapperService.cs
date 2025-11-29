using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using BAHelper.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.DalamudServices;
using ECommons.EzEventManager;
using ECommons.EzHookManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
namespace BAHelper.Modules.Trapper;

public sealed class TrapperService : IDisposable
{
    private static Configuration Config => Plugin.Config;
    private readonly HashSet<Vector3> TrapRevealed = [];
    private readonly List<MobObject> MobObjects = [];
    private bool prevInBA = false;
    public AreaTag ChestFoundAt { get; private set; } = AreaTag.None;
    public HashSet<AreaTag> PossibleAreasOfPortal { get; } = [];

#pragma warning disable CS0169
    private unsafe delegate void SystemLogMessageDelegate(uint entityId, uint logMessageId, int* args, byte argCount);
    [EzHook("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 0F B6 47 28", nameof(SystemLogMessageDetour))]
    private readonly EzHook<SystemLogMessageDelegate> SystemLogMessageHook;
#pragma warning restore CS0169

    public TrapperService()
    {
        EzSignatureHelper.Initialize(this);
        Svc.PluginInterface.UiBuilder.Draw += OnDraw;
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
        _ = new EzTerritoryChanged(OnTerritoryChanged);
    }

    private static bool ShouldDraw => 
        Config.AdvancedModeEnabled
        && Svc.Objects.Length > 0 && Player.Available && Common.InHydatos 
        && !(Svc.Condition[ConditionFlag.LoggingOut]
            || Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.BetweenAreas51]);

    private unsafe static void SystemLogMessageDetour(uint entityId, uint logId, int* args, byte argCount)
    {
        if (logId < 9103 || logId > 9105 || !Config.AdvancedModeEnabled || !Common.InBA)
            return;
        var scanResult = logId switch
        {
            9103 => ScanResult.Discover, // 发现了隐藏的陷阱！
            9104 => ScanResult.Sense, // 附近感觉到有隐藏的陷阱！
            9105 => ScanResult.NotSense, // 附近没感觉到隐藏的陷阱！
            _ => ScanResult.None
        };
        Trap.UpdateByScanResult(Player.Position, scanResult);
    }

    private void OnFrameworkUpdate()
    {
        if (!Common.InHydatos) return;
        if (Svc.Objects == null) return;
        if (!Common.InBA)
        {
            if (prevInBA)
                Reset();
            return;
        }
        else if (!prevInBA)
            prevInBA = true;

        if (EzThrottler.Throttle("TrapperService-Check", 200))
        {
            EnumerateAllObjects();
            CheckPortalStatus();
        }
    }

    private void OnTerritoryChanged(ushort t) => Reset();

    public void Reset()
    {
        Trap.ResetAll();
        TrapRevealed.Clear();
        ChestFoundAt = AreaTag.None;
        CheckPortalStatus();
        Monitor.Enter(MobObjects);
        MobObjects.Clear();
        Monitor.Exit(MobObjects);
        prevInBA = false;
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= OnDraw;
    }

    private void CheckPortalStatus()
    {
        PossibleAreasOfPortal.Clear();
        PossibleAreasOfPortal.UnionWith(Trap.AllTraps.Values.Where(t => t is { Type: TrapType.Portal, State: not TrapState.Disabled }).Select(t => t.AreaTag));
    }

    private void OnDraw()
    {
        if (!ShouldDraw) return;
        var drawList = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
        if (Config.DrawRecordedTraps)
        {
            DrawTraps(drawList, Trap.AllTraps.Values);
        }
        if (Config.DrawRecommendedScanningSpots)
        {
            DrawAllScanningSpots(drawList);
        }
        if (Config.DrawMobViews)
        {
            if (!Monitor.TryEnter(MobObjects)) return;
            DrawMobs(drawList, MobObjects);
            Monitor.Exit(MobObjects);
        }
    }

    private void EnumerateAllObjects()
    {
        var entityList = new List<MobObject>();
        foreach (var obj in Svc.Objects)
        {
            var areaTag = Area.Locate(obj.Position)?.Tag ?? AreaTag.None;
            if (CheckChestObject(obj, areaTag)) continue;
            if (CheckTrapObject(obj, areaTag)) continue;
            if (CheckMobObject(obj, out var mobObject) && mobObject is not null)
                entityList.Add(mobObject);
        }
        Monitor.Enter(MobObjects);
        MobObjects.Clear();
        MobObjects.AddRange(entityList);
        Monitor.Exit(MobObjects);
    }

    private bool CheckChestObject(IGameObject obj, AreaTag areaTag)
    {
        if (obj.ObjectKind != ObjectKind.Treasure)
            return false;

        if (ChestFoundAt == AreaTag.None && 
            (areaTag == AreaTag.FireRoom1 || areaTag == AreaTag.EarthRoom1 || areaTag == AreaTag.LightningRoom1
            || areaTag == AreaTag.WindRoom1 || areaTag == AreaTag.IceRoom1 || areaTag == AreaTag.WaterRoom1))
        {
            ChestFoundAt = areaTag;
            ShowChestAndPortalRevealed(areaTag);
        }
        return true;
    }

    private static void ShowChestAndPortalRevealed(AreaTag areaTag, bool isChest = true)
    {
        var toast = $"{areaTag.Description()}{(isChest ? "箱" : "门")}！";
        var sb = new SeStringBuilder().AddText(toast);
        Svc.Toasts.ShowQuest(sb.BuiltString, new QuestToastOptions() { PlaySound = true, DisplayCheckmark = true });
        Plugin.PrintMessage(sb.BuiltString);
    }

    private bool CheckTrapObject(IGameObject obj, AreaTag areaTag)
    {
        if (areaTag == AreaTag.None)
            return false;

        var trapType = obj switch
        {
            { BaseId: 2009728U } => TrapType.BigBomb,
            { BaseId: 2009729U } => TrapType.Portal,
            { BaseId: 2009730U } => TrapType.SmallBomb,
            IBattleNpc { NameId: 7958U } when (areaTag == AreaTag.CircularPlatform || areaTag == AreaTag.OctagonRoomFromRaiden || areaTag == AreaTag.OctagonRoomToRoomGroup2) => TrapType.SmallBomb,
            IBattleNpc { NameId: 7958U } => TrapType.BigBomb,
            _ => TrapType.None
        };
        
        if (trapType != TrapType.None)
        {
            OnTrapRevealed(obj.Position, trapType, areaTag, obj is IBattleNpc { NameId: 7958U });
            return true;
        }
        return false;
    }

    private void OnTrapRevealed(Vector3 location, TrapType type, AreaTag areaTag, bool isTriggered)
    {
        if (!TrapRevealed.Add(location))
            return;
        var logStr = "";
        var typeStr = type switch
        {
            TrapType.BigBomb => "即死雷",
            TrapType.SmallBomb => "易伤雷",
            TrapType.Portal => "传送门",
            _ => ""
        };

        if (Trap.TryGetFromLocation(location, out var trap))
        {
            logStr = $"{(isTriggered ? "陷阱被踩" : "探出陷阱")}：{trap.Info}";
            trap.State = TrapState.Revealed;
            trap.GetComplementarySet().Each(id => Trap.AllTraps[id].State = TrapState.Disabled);

            if (trap.Type == TrapType.Portal)
                ShowChestAndPortalRevealed(trap.AreaTag, false);
            // todo add config
            Plugin.PrintMessage(logStr);
        }
        else
        {
            logStr = $"发现新的陷阱点位! {typeStr} @ ({location.X}f, {location.Y}f, {location.Z}f) @ {areaTag}";
            
            if (Area.TryGet(areaTag, out var area))
            {
                // 新点位只可能在那两个只有一个易伤雷的房间，只有一个雷所以其他点位disabled
                area.Traps.Each(trap => trap.State = TrapState.Disabled);
            }
            // todo add config
            Plugin.PrintMessage(logStr);
        }
        Svc.Log.Debug(logStr);
    }

    private static bool CheckMobObject(IGameObject obj, out MobObject? mobObject)
    {
        mobObject = null;
        if (!obj.IsValid() || obj is not IBattleNpc bnpc || !bnpc.BattleNpcKind.Equals(BattleNpcSubKind.Enemy))
            return false;

        if (bnpc.IsDead() || bnpc.InCombat())
            return true;

        if (MobInfo.Mobs.TryGetValue(bnpc.NameId, out var mobInfo))
            mobObject = new(bnpc, mobInfo);

        return true;
    }

    public static void DrawMobs(ImDrawListPtr drawList, List<MobObject> mobObjects)
    {
        mobObjects.Each(mob => DrawMob(drawList, mob));
    }

    public static void DrawMob(ImDrawListPtr drawList, MobObject mob)
    {
        if (mob.Position.Distance2D(Player.Position) > Config.TrapViewDistance) // todo: change config var name
            return;

        switch (mob.AggroType)
        {
            case AggroType.Sight:
                drawList.DrawConeFromCenterPoint(mob.Position, mob.Rotation, mob.SightRadian, mob.AggroDistance, Config.NormalAggroColor);
                break;
            case AggroType.Sound:
                drawList.DrawRingWorld(mob.Position, mob.AggroDistance, 1f, Config.SoundAggroColor);
                drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, Config.SoundAggroColor.SetAlpha(0.4f), filled: true);
                drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, Config.SoundAggroColor);
                break;
            case AggroType.Proximity:
                drawList.DrawRingWorld(mob.Position, mob.AggroDistance, 1f, Config.NormalAggroColor);
                drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, Config.NormalAggroColor.SetAlpha(0.4f), filled: true);
                drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, Config.NormalAggroColor);
                break;
            case AggroType.Magic:
            case AggroType.Blood:
                drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, Config.SoundAggroColor.SetAlpha(0.4f), filled: true);
                drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, Config.SoundAggroColor);
                break;
            default:
                break;
        }
    }

    public static void DrawTrap(ImDrawListPtr drawList, Trap trap)
    {
        if (trap.State == TrapState.Disabled)
            return;

        var distance = trap.Location.Distance2D(Player.Position);
        if (distance > Config.TrapViewDistance)
            return;

        var inView = Svc.GameGui.WorldToScreen(trap.Location, out var screenPos);
        uint color = trap.Type switch
        {
            TrapType.BigBomb => Config.TrapBigBombColor,
            TrapType.SmallBomb => Config.TrapSmallBombColor,
            TrapType.Portal => Config.TrapPortalColor,
            _ => Color.White
        };

        if (trap.Type != TrapType.Portal && Config.DrawTrapBlastCircle
            && (!Config.DrawTrapBlastCircleOnlyWhenApproaching || distance < trap.BlastRadius + 4.0f))
            drawList.DrawRingWorld(trap.Location, trap.BlastRadius, 1.3f, color.SetAlpha(0.5f));

        if (trap.State == TrapState.Revealed)
            color = Config.RevealedTrapColor;
        if (drawList.DrawRingWorld(trap.Location, trap.HitBoxRadius, 1.5f, color) && inView)
            drawList.AddCircleFilled(screenPos, 1.5f, color); // 画中心点

        if (Config.DrawTrap15m
            && (!Config.DrawTrap15mOnlyWhenApproaching || distance < 19.0f)
            && (!Config.DrawTrap15mExceptRevealed || trap.State != TrapState.Revealed))
            drawList.DrawRingWorld(trap.Location, 15f, 1f, Config.Trap15mCircleColor);

        if (Config.DrawTrap36m
            && (!Config.DrawTrap36mOnlyWhenApproaching || distance < 40.0f)
            && (!Config.DrawTrap36mExceptRevealed || trap.State != TrapState.Revealed))
            drawList.DrawRingWorld(trap.Location, 36f, 1f, Config.Trap36mCircleColor, true);
    }

    public static void DrawTraps(ImDrawListPtr drawList, IEnumerable<Trap> traps)
    {
        traps.Each(trap => DrawTrap(drawList, trap));
    }

    public static void DrawAreaBorder(ImDrawListPtr drawList, AreaTag areaTag)
    {
        if (Area.TryGet(areaTag, out var area))
            DrawAreaBorder(drawList, area);
    }

    public static void DrawAreaBorder(ImDrawListPtr drawList, Area area)
    {
        drawList.DrawRectWorld(area.Origin, area.Dims);
    }

    public static void DrawAllAreasBorder(ImDrawListPtr drawList)
    {
        DrawAreaBorder(drawList, AreaTag.Entry);
        DrawAreaBorder(drawList, AreaTag.CorridorFromArt);
        DrawAreaBorder(drawList, AreaTag.CorridorFromOwain);
        DrawAreaBorder(drawList, AreaTag.CircularPlatform);
        DrawAreaBorder(drawList, AreaTag.OctagonRoomFromRaiden);
        DrawAreaBorder(drawList, AreaTag.OctagonRoomToRoomGroup1);
        DrawAreaBorder(drawList, AreaTag.RoomGroup1);
        DrawAreaBorder(drawList, AreaTag.IceRoom1);
        DrawAreaBorder(drawList, AreaTag.LightningRoom1);
        DrawAreaBorder(drawList, AreaTag.FireRoom1);
        DrawAreaBorder(drawList, AreaTag.WaterRoom1);
        DrawAreaBorder(drawList, AreaTag.WindRoom1);
        DrawAreaBorder(drawList, AreaTag.EarthRoom1);
        DrawAreaBorder(drawList, AreaTag.OctagonRoomToRoomGroup2);
        DrawAreaBorder(drawList, AreaTag.RoomGroup2);
        DrawAreaBorder(drawList, AreaTag.IceRoom2);
        DrawAreaBorder(drawList, AreaTag.LightningRoom2);
        DrawAreaBorder(drawList, AreaTag.FireRoom2);
        DrawAreaBorder(drawList, AreaTag.WaterRoom2);
        DrawAreaBorder(drawList, AreaTag.WindRoom2);
        DrawAreaBorder(drawList, AreaTag.EarthRoom2);
    }

    public static void DrawAreaScanningSpots(ImDrawListPtr drawList, AreaTag areaTag)
    {
        if (Area.TryGet(areaTag, out var area) && area.ShowScanningSpot)
        {
            foreach (var (Center, Radius, Tip) in area.ScanningSpots.Where(p => p.Center.Distance2D(Player.Position) < Config.TrapViewDistance))
            {
                var filled = Center.Distance2D(Player.Position) <= Radius;
                if (filled)
                    drawList.DrawRingWorld(Center, Radius, 1.5f, Config.ScanningSpotColor.SetAlpha(0.20f), filled: filled);
                drawList.DrawRingWorldWithText(Center, Radius, 1.5f, Config.ScanningSpotColor, Tip);
                if (Config.DrawScanningSpot15m)
                {
                    drawList.DrawRingWorld(Center, 15.0f - Radius, 1.5f, Config.ScanningSpot15mCircleColor);
                }
                if (Config.DrawScanningSpot36m)
                {
                    drawList.DrawRingWorld(Center, 36.0f - Radius, 1.5f, Config.ScanningSpot36mCircleColor);
                }
            }
        }
    }

    public static void DrawAllScanningSpots(ImDrawListPtr drawList)
    {
        DrawAreaScanningSpots(drawList, AreaTag.CorridorFromArt);
        DrawAreaScanningSpots(drawList, AreaTag.CorridorFromOwain);
        DrawAreaScanningSpots(drawList, AreaTag.CircularPlatform);
        DrawAreaScanningSpots(drawList, AreaTag.OctagonRoomFromRaiden);
        DrawAreaScanningSpots(drawList, AreaTag.OctagonRoomToRoomGroup1);
        DrawAreaScanningSpots(drawList, AreaTag.FireRoom1);
        DrawAreaScanningSpots(drawList, AreaTag.EarthRoom1);
        DrawAreaScanningSpots(drawList, AreaTag.LightningRoom1);
        DrawAreaScanningSpots(drawList, AreaTag.WindRoom1);
        DrawAreaScanningSpots(drawList, AreaTag.IceRoom1);
        DrawAreaScanningSpots(drawList, AreaTag.WaterRoom1);
        DrawAreaScanningSpots(drawList, AreaTag.OctagonRoomToRoomGroup2);
        DrawAreaScanningSpots(drawList, AreaTag.IceRoom2);
        DrawAreaScanningSpots(drawList, AreaTag.LightningRoom2);
        DrawAreaScanningSpots(drawList, AreaTag.FireRoom2);
        DrawAreaScanningSpots(drawList, AreaTag.WaterRoom2);
        DrawAreaScanningSpots(drawList, AreaTag.WindRoom2);
        DrawAreaScanningSpots(drawList, AreaTag.EarthRoom2);
    }
}