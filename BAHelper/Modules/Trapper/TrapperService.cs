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
using ECommons;
using ECommons.DalamudServices;
using ECommons.EzEventManager;
using ECommons.GameHelpers;
using ImGuiNET;
namespace BAHelper.Modules.Trapper;

public sealed partial class TrapperService : IDisposable
{
    [GeneratedRegex("^(?<result>.*?)隐藏的陷阱！$")] // 发现了 / 附近感觉到有 / 附近没感觉到
    private static partial Regex MyRegex();
    private static Configuration Config => Plugin.Config;
    private readonly HashSet<Vector3> TrapRevealed = [];
    private readonly List<MobObject> MobObjects = [];
    private long LastEnumeratedAt = Environment.TickCount64;

    public AreaTag ChestFoundAt { get; private set; } = AreaTag.None;
    public ScanResult LastScanResult { get; private set; } = ScanResult.None;
    public HashSet<AreaTag> PossibleAreasOfPortal { get; } = [];

    public TrapperService()
    {
        Svc.PluginInterface.UiBuilder.Draw += OnDraw;
        Svc.Chat.ChatMessage += OnChatMessage;
        _ = new EzFrameworkUpdate(OnFrameworkUpdate);
        _ = new EzTerritoryChanged(OnTerritoryChanged);
    }

    private static bool ShouldDraw => 
        Config.AdvancedModeEnabled
        && Svc.Objects.Length > 0 && Player.Available && Common.InHydatos 
        && !(Svc.Condition[ConditionFlag.LoggingOut]
            || Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.BetweenAreas51]);

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!Config.AdvancedModeEnabled || (XivChatType)((int)type & 0x7f) != XivChatType.SystemMessage)
            return;

        if (MyRegex().Match(message.ExtractText()) is var match && match.Success)
        {
            var result = match.Groups["result"].Value;
            if (result.Contains("发现"))
                LastScanResult = ScanResult.Discover;
            else if (result.Contains("感觉到有"))
                LastScanResult = ScanResult.Sense;
            else
                LastScanResult = ScanResult.NotSense;
        }
    }

    private void OnFrameworkUpdate()
    {
        if (!Common.InHydatos) return;
        if (Svc.Objects == null) return;
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
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= OnDraw;
        Svc.Chat.ChatMessage -= OnChatMessage;
    }

    private void CheckPortalStatus()
    {
        PossibleAreasOfPortal.Clear();
        PossibleAreasOfPortal.UnionWith(Trap.AllTraps.Values.Where(t => t.Type == TrapType.Portal && t.State == TrapState.NotScanned).Select(t => t.AreaTag));
    }

    private void OnDraw()
    {
        if (!ShouldDraw) return;
        var drawList = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
        if (Config.DrawRecordedTraps)
        {
            DrawTraps(drawList, Trap.AllTraps.Values);
        }
        if (Config.DrawAreaBorder)
        {
            DrawAllAreasBorder(drawList);
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

    private bool CheckChestObject(GameObject obj, AreaTag areaTag)
    {
        if (obj.ObjectKind != ObjectKind.Treasure)
            return false;

        if (ChestFoundAt == AreaTag.None && 
            (areaTag == AreaTag.FireRoom1 || areaTag == AreaTag.EarthRoom1 || areaTag == AreaTag.LightningRoom1
            || areaTag == AreaTag.WindRoom1 || areaTag == AreaTag.IceRoom1 || areaTag == AreaTag.WaterRoom1))
        {
            ChestFoundAt = areaTag;
            ShowRoomGroup1Toast(areaTag);
        }
        return true;
    }

    private static void ShowRoomGroup1Toast(AreaTag areaTag, bool isChest = true)
    {
        var toast = $"{areaTag.Description()}{(isChest ? "箱" : "门")}！";
        var sb = new SeStringBuilder().AddText(toast);
        Svc.Toasts.ShowQuest(sb.BuiltString);
        Plugin.PrintMessage(sb.BuiltString);
        Singletons.SoundManager.Play(SoundEffect.SE_1);
    }

    private bool CheckTrapObject(GameObject obj, AreaTag areaTag)
    {
        if (areaTag == AreaTag.None)
            return false;

        var trapType = obj switch
        {
            { DataId: 2009728U } => TrapType.BigBomb,
            { DataId: 2009729U } => TrapType.Portal,
            { DataId: 2009730U } => TrapType.SmallBomb,
            // todo test this pattern
            BattleNpc { NameId: 7958U } when (areaTag == AreaTag.CircularPlatform || areaTag == AreaTag.OctagonRoomFromRaiden || areaTag == AreaTag.OctagonRoomToRoomGroup2) => TrapType.SmallBomb,
            BattleNpc { NameId: 7958U } => TrapType.BigBomb,
            _ => TrapType.None
        };

        if (trapType != TrapType.None)
        {
            OnTrapRevealed(obj.Position, trapType, areaTag);
            return true;
        }
        return false;
    }

    private void OnTrapRevealed(Vector3 location, TrapType type, AreaTag areaTag)
    {
        if (!TrapRevealed.Add(location))
            return;
        var logStr = "";
        if (Trap.TryGetFromLocation(location, out var trap))
        {
            logStr = $"探出陷阱 #{trap.Id} {type} @ {areaTag}";
            trap.State = TrapState.Revealed;
            trap.GetComplementarySet().Each(id => Trap.AllTraps[id].State = TrapState.Disabled);

            if (trap.Type == TrapType.Portal)
                ShowRoomGroup1Toast(trap.AreaTag, false);
        }
        else
        {
            logStr = $"探出新的陷阱点位! {type} @ ({location.X}f, {location.Y}f, {location.Z}f) @ {areaTag}";
            // 新点位只可能在那两个只有一个易伤雷的房间
            if (Area.TryGet(areaTag, out var area))
            {
                area.Traps.Each(trap => trap.State = TrapState.Disabled);
            }
        }
        Svc.Log.Info(logStr);
        Plugin.PrintMessage(logStr);
    }

    private static bool CheckMobObject(GameObject obj, out MobObject? mobObject)
    {
        mobObject = null;
        if (!obj.IsValid() || obj is not BattleNpc bnpc || !bnpc.BattleNpcKind.Equals(BattleNpcSubKind.Enemy))
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
        if (mob.Position.Distance2D(Common.MeWorldPos) > Config.TrapViewDistance) // todo: change config var name
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

        var distance = trap.Location.Distance2D(Common.MeWorldPos);
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
            foreach (var (Center, Radius, Tip) in area.ScanningSpots.Where(p => p.Center.Distance2D(Common.MeWorldPos) < Config.TrapViewDistance))
            {
                var filled = Center.Distance2D(Common.MeWorldPos) <= Radius;
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