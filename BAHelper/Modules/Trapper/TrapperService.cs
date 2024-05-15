using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly HashSet<Trap> TrapRevealed = [];
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

    private bool ShouldDraw()
    {
        return config.AdvancedModeEnabled &&
               !(DalamudApi.Condition[ConditionFlag.LoggingOut] ||
                 DalamudApi.Condition[ConditionFlag.BetweenAreas] ||
                 DalamudApi.Condition[ConditionFlag.BetweenAreas51]) &&
               Common.InHydatos && DalamudApi.ClientState.LocalPlayer != null &&
               DalamudApi.ObjectTable.Length > 0;
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!config.AdvancedModeEnabled || (XivChatType)((int)type & 0x7f) != XivChatType.SystemMessage)
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
        TrapRevealed.Clear();
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
        foreach (var portal in Trap.AllTraps.Select(kv => kv.Value).Where(p => p.Type == TrapType.Portal && p.Enabled))
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
            DrawTraps(drawList, Trap.AllTraps.Values);
            DrawTraps(drawList, TrapRevealed, true);
        }
        if (config.DrawAreaBorder)
        {
            DrawAllAreasBorder(drawList);
        }
        if (config.DrawRecommendedScanningSpots)
        {
            DrawAllScanningSpots(drawList);
        }
        if (config.DrawMobViews)
        {
            if (!Monitor.TryEnter(MobObjects)) return;
            DrawMobs(drawList, MobObjects);
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
            OnTrapRevealed(new()
            {
                Type = trapType,
                Location = obj.Position,
                Enabled = true,
                AreaTag = areaTag,
            });
            return true;
        }
        return false;
    }
    
    private void OnTrapRevealed(Trap revealedTrap)
    {
        if (!TrapRevealed.Add(revealedTrap))
            return;

        if (revealedTrap.IsInRecords)
        {
            Trap.AllTraps[revealedTrap.ID].Enabled = false;
            foreach (var id in revealedTrap.GetComplementarySet())
                Trap.AllTraps[id].Enabled = false;

            if (revealedTrap.Type == TrapType.Portal)
                ShowRoomGroup1Toast(revealedTrap.AreaTag, false);
        }
        else
        {
            var str = $"探出新的陷阱/门点位! {{{{id}}, new( {{id}}, TrapType.{revealedTrap.Type}, new({revealedTrap.Location.X}f, {revealedTrap.Location.Y}f, {revealedTrap.Location.Z}f), AreaTag.{revealedTrap.AreaTag}) }},";
            DalamudApi.PluginLog.Info(str);
            Plugin.PrintMessage(str);
            // 新点位只可能在那两个只有一个易伤雷的房间
            if (Area.TryGet(revealedTrap.AreaTag, out var area))
            {
                area.Traps.ForEach(trap => {trap.Enabled = false;});
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

    public void DrawMobs(ImDrawListPtr drawList, List<MobObject> mobObjects)
    {
        foreach(var mob in mobObjects)
            DrawMob(drawList, mob);
    }
    
    public void DrawMob(ImDrawListPtr drawList, MobObject mob)
    {
        if (mob.Position.Distance2D(Common.MeWorldPos) <= 50)
        {
            switch (mob.AggroType)
            {
                case AggroType.Sight:
                    drawList.DrawConeFromCenterPoint(mob.Position, mob.Rotation, mob.SightRadian, mob.AggroDistance, config.NormalAggroColor);
                    break;
                case AggroType.Sound:
                    drawList.DrawRingWorld(mob.Position, mob.AggroDistance, 1f, config.SoundAggroColor);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, config.SoundAggroColor.SetAlpha(0.4f), filled: true);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, config.SoundAggroColor);
                    break;
                case AggroType.Proximity:
                    drawList.DrawRingWorld(mob.Position, mob.AggroDistance, 1f, config.NormalAggroColor);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, config.NormalAggroColor.SetAlpha(0.4f), filled: true);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, config.NormalAggroColor);
                    break;
                case AggroType.Magic:
                case AggroType.Blood:
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, config.SoundAggroColor.SetAlpha(0.4f), filled: true);
                    drawList.DrawRingWorld(mob.Position, mob.Bnpc.HitboxRadius, 1f, config.SoundAggroColor);
                    break;
                default:
                    break;
            }
        }
    }
    
    public void DrawTrap(ImDrawListPtr drawList, Trap trap, bool revealed)
    {
        if (!trap.Enabled)
            return;

        var distance = trap.Location.Distance2D(Common.MeWorldPos);
        if (distance > config.TrapViewDistance)
            return;

        var inView = DalamudApi.GameGui.WorldToScreen(trap.Location, out var screenPos);
        uint color = trap.Type switch {
            TrapType.BigBomb => config.TrapBigBombColor,
            TrapType.SmallBomb => config.TrapSmallBombColor,
            TrapType.Portal => config.TrapPortalColor,
            _ => Color.White
        };

        if (config.DrawTrapBlastCircle
            && (!config.DrawTrapBlastCircleOnlyWhenApproaching || distance < trap.BlastRadius + 4.0f))
            drawList.DrawRingWorld(trap.Location, trap.BlastRadius, 1.3f, color.SetAlpha(0.4f));

        if (revealed)
            color = config.RevealedTrapColor;
        if (drawList.DrawRingWorld(trap.Location, trap.HitBoxRadius, 1.5f, color) && inView)
            drawList.AddCircleFilled(screenPos, 1.5f, color); // 画中心点

        if (config.DrawTrap15m
            && (!config.DrawTrap15mOnlyWhenApproaching || distance < 19.0f)
            && (!config.DrawTrap15mExceptRevealed || !revealed))
            drawList.DrawRingWorld(trap.Location, 15f, 1f, config.Trap15mCircleColor);

        if (config.DrawTrap36m
            && (!config.DrawTrap36mOnlyWhenApproaching || distance < 40.0f)
            && (!config.DrawTrap36mExceptRevealed || !revealed))
            drawList.DrawRingWorld(trap.Location, 36f, 1f, config.Trap36mCircleColor, true);
    }
    
    public void DrawTraps(ImDrawListPtr drawList, IEnumerable<Trap> traps, bool revealed = false)
    {
        foreach(var trap in traps)
            DrawTrap(drawList, trap, revealed);
    }

    public void DrawAreaBorder(ImDrawListPtr drawList, AreaTag areaTag)
    {
        if (Area.TryGet(areaTag, out var area))
            DrawAreaBorder(drawList, area);
    }

    public void DrawAreaBorder(ImDrawListPtr drawList, Area area)
    {
        drawList.DrawRectWorld(area.Origin, area.Dims);
    }
    
    public void DrawAllAreasBorder(ImDrawListPtr drawList)
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
    
    public void DrawAreaScanningSpots(ImDrawListPtr drawList, AreaTag areaTag)
    {
        if (Area.TryGet(areaTag, out var area) && area.ShowScanningSpot)
        {
            foreach(var (Center, Radius, Tip) in area.ScanningSpots.Where(p => p.Center.Distance2D(Common.MeWorldPos) < config.TrapViewDistance))
            {
                var filled = Center.Distance2D(Common.MeWorldPos) <= Radius;
                if (filled)
                    drawList.DrawRingWorld(Center, Radius, 1.5f, config.ScanningSpotColor.SetAlpha(0.20f), filled: filled);
                drawList.DrawRingWorldWithText(Center, Radius, 1.5f, config.ScanningSpotColor, Tip);
                if (config.DrawScanningSpot15m)
                {
                    drawList.DrawRingWorld(Center, 15.0f - Radius, 1.5f, config.ScanningSpot15mCircleColor);
                }
                if (config.DrawScanningSpot36m)
                {
                    drawList.DrawRingWorld(Center, 36.0f - Radius, 1.5f, config.ScanningSpot36mCircleColor);
                }
            }
        }
    }
    
    public void DrawAllScanningSpots(ImDrawListPtr drawList)
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