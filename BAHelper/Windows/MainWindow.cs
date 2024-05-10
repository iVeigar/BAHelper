using System.Linq;
using BAHelper.Modules.Party;
using BAHelper.Modules.Trapper;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;


namespace BAHelper.Windows;

public sealed class MainWindow(TrapperService trapperService, PartyService portalService)
    : Window("兵武塔助手", ImGuiWindowFlags.AlwaysAutoResize)
{
    private readonly Configuration config = DalamudApi.Config;
    private readonly TrapperService TrapperService = trapperService;
    private readonly PartyService PortalService = portalService;

    public override void Draw()
    {
        if (ImGui.BeginTabBar("BAHelperTabBar"))
        {
            if (ImGui.BeginTabItem("小队"))
            {
                DrawPartyTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("工兵"))
            {
                DrawTrapperTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("设置"))
            {
                DrawConfigTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawPartyTab()
    {
        ImGui.TextWrapped("点击以发送队号对应的门坐标到小队频道");
        for(var i = 1; i < 7; i++)
        {
            if (ImGui.Button($"{i}队", new(40f, 40f)))
            {
                PortalService.SendPortalsToChat(i);
            }
            if (i < 6) ImGui.SameLine();
        }
    }

    private void DrawTrapperTab()
    {
        var save = false;
        ImGui.Text("自动上盾功能");
        if (ImGuiUtils.IconButton(TrapperTool.IsRunning ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play, "##autoshield"))
        {
            TrapperTool.Toggle();
        }
        ImGui.SameLine();
        save |= ImGui.Checkbox("上护盾", ref config.CheckStatusProtect);
        ImGui.SameLine();
        save |= ImGui.Checkbox("上魔盾", ref config.CheckStatusShell);
        
        ImGui.Text("盾剩余时间阈值（分钟）");
        
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScaleSafe);
        save |= ImGui.SliderInt("", ref config.ShieldRemainingTimeThreshold, 1, 29);
        if (save)
            DalamudApi.Config.Save();

        if (config.AdvancedModeEnabled)
        {
            ImGui.Separator();
            ImGui.Text("快捷喊话");
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("须启用扫描与绘图功能");
            var bigButtonSize = ImGuiHelpers.ScaledVector2(95f, 60f);
            var smallButtonSize = ImGuiHelpers.ScaledVector2(60f, 45f);
            using (ImRaii.Disabled(TrapperService.ChestFoundAt == AreaTag.None))
            {
                if (ImGui.Button($"{TrapperService.ChestFoundAt.Description()}箱", bigButtonSize))
                {
                    Game.SendMessage($"/sh {TrapperService.ChestFoundAt.Description()}箱");
                }
            }
            ImGui.SameLine();
            var portalAt = TrapperService.PossibleAreasOfPortal.Count != 1 ? AreaTag.None : TrapperService.PossibleAreasOfPortal.First();
            using (ImRaii.Disabled(portalAt == AreaTag.None))
            {
                if (ImGui.Button($"{portalAt.Description()}门", bigButtonSize))
                {
                    Game.SendMessage($"/sh {portalAt.Description()}门");
                }
            }
            if (portalAt == AreaTag.None)
            {
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.IceRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("冰无门", smallButtonSize))
                        Game.SendMessage("/sh 冰无门");
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.LightningRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("雷无门", smallButtonSize))
                        Game.SendMessage("/sh 雷无门");
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.FireRoom1) || TrapperService.PossibleAreasOfPortal.Contains(AreaTag.EarthRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("火土无门", smallButtonSize))
                        Game.SendMessage("/sh 火土无门");
                }

                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.WaterRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("水无门", smallButtonSize))
                        Game.SendMessage("/sh 水无门");
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.WindRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("风无门", smallButtonSize))
                        Game.SendMessage("/sh 风无门");
                }
            }
            ImGui.Separator();
            if (ImGui.Button("重置绘图器状态"))
            {
                TrapperService.Reset();
            }
        }
    }

    private void DrawConfigTab()
    {
        var save = false;
        save |= ImGui.Checkbox("启用扫描与绘图功能", ref config.AdvancedModeEnabled);
        if (config.AdvancedModeEnabled)
        {
            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScaleSafe);
            save |= ImGui.SliderFloat("绘图范围", ref config.TrapViewDistance, 15f, 2000f, config.TrapViewDistance.ToString("##.0m"), ImGuiSliderFlags.Logarithmic);

            save |= ImGui.Checkbox("绘制所有已知的陷阱点位", ref config.DrawRecordedTraps);
            if (config.DrawRecordedTraps)
            {
                using (ImRaii.PushIndent())
                {
                    var bigBombColorV4 = ImGui.ColorConvertU32ToFloat4(config.TrapBigBombColor);
                    if (ImGuiUtils.ColorPickerWithPalette(1, "", ref bigBombColorV4))
                    {
                        save = true;
                        config.TrapBigBombColor = ImGui.ColorConvertFloat4ToU32(bigBombColorV4);
                    }
                    ImGui.SameLine();
                    ImGui.Text("即死雷");

                    var smallBombColorV4 = ImGui.ColorConvertU32ToFloat4(config.TrapSmallBombColor);
                    if (ImGuiUtils.ColorPickerWithPalette(2, "", ref smallBombColorV4))
                    {
                        save = true;
                        config.TrapSmallBombColor = ImGui.ColorConvertFloat4ToU32(smallBombColorV4);
                    }
                    ImGui.SameLine();
                    ImGui.Text("易伤雷");

                    var portalColorV4 = ImGui.ColorConvertU32ToFloat4(config.TrapPortalColor);
                    if (ImGuiUtils.ColorPickerWithPalette(3, "", ref portalColorV4))
                    {
                        save = true;
                        config.TrapPortalColor = ImGui.ColorConvertFloat4ToU32(portalColorV4);
                    }
                    ImGui.SameLine();
                    ImGui.Text("传送门");
                }
            }

            save |= ImGui.Checkbox("绘制探出/踩过的陷阱", ref config.DrawDiscoveredTraps);
            ImGui.SameLine();
            var discoveredTrapColorV4 = ImGui.ColorConvertU32ToFloat4(config.DiscoveredTrapColor);
            if (ImGuiUtils.ColorPickerWithPalette(4, "", ref discoveredTrapColorV4))
            {
                save = true;
                config.DiscoveredTrapColor = ImGui.ColorConvertFloat4ToU32(discoveredTrapColorV4);
            }
                    
            save |= ImGui.Checkbox("绘制陷阱爆炸波及范围(靠近时)", ref config.DrawExplosionRange);
            
            save |= ImGui.Checkbox("绘制陷阱15m半径提示圈", ref config.DrawTrap15m);
            ImGui.SameLine();
            var trap15mCircleColorV4 = ImGui.ColorConvertU32ToFloat4(config.Trap15mCircleColor);
            if (ImGuiUtils.ColorPickerWithPalette(5, "", ref trap15mCircleColorV4))
            {
                save = true;
                config.Trap15mCircleColor = ImGui.ColorConvertFloat4ToU32(trap15mCircleColorV4);
            }

            save |= ImGui.Checkbox("绘制陷阱36m半径提示圈", ref config.DrawTrap36m);
            ImGui.SameLine();
            var trap36mCircleColorV4 = ImGui.ColorConvertU32ToFloat4(config.Trap36mCircleColor);
            if (ImGuiUtils.ColorPickerWithPalette(6, "", ref trap36mCircleColorV4))
            {
                save = true;
                config.Trap36mCircleColor = ImGui.ColorConvertFloat4ToU32(trap36mCircleColorV4);
            }

            save |= ImGui.Checkbox("绘制探景推荐点位", ref config.DrawRecommendedScanningSpots);
            ImGui.SameLine();
            var scanningSpotColorV4 = ImGui.ColorConvertU32ToFloat4(config.ScanningSpotColor);
            if (ImGuiUtils.ColorPickerWithPalette(7, "", ref scanningSpotColorV4))
            {
                save = true;
                config.ScanningSpotColor = ImGui.ColorConvertFloat4ToU32(scanningSpotColorV4);
            }

            save |= ImGui.Checkbox("绘制探景推荐点位15m半径提示圈", ref config.DrawScanningSpot15m);
            ImGui.SameLine();
            var scanningSpot15mCircleColorV4 = ImGui.ColorConvertU32ToFloat4(config.ScanningSpot15mCircleColor);
            if (ImGuiUtils.ColorPickerWithPalette(8, "", ref scanningSpot15mCircleColorV4))
            {
                save = true;
                config.ScanningSpot15mCircleColor = ImGui.ColorConvertFloat4ToU32(scanningSpot15mCircleColorV4);
            }

            save |= ImGui.Checkbox("绘制探景推荐点位36m半径提示圈", ref config.DrawScanningSpot36m);
            ImGui.SameLine();
            var scanningSpot36mCircleColorV4 = ImGui.ColorConvertU32ToFloat4(config.ScanningSpot36mCircleColor);
            if (ImGuiUtils.ColorPickerWithPalette(9, "", ref scanningSpot15mCircleColorV4))
            {
                save = true;
                config.ScanningSpot36mCircleColor = ImGui.ColorConvertFloat4ToU32(scanningSpot36mCircleColorV4);
            }

            save |= ImGui.Checkbox("绘制怪物仇恨范围", ref config.DrawMobViews);
            if (config.DrawMobViews)
            {
                using (ImRaii.PushIndent())
                {
                    var normalColorV4 = ImGui.ColorConvertU32ToFloat4(config.NormalAggroColor);
                    if (ImGuiUtils.ColorPickerWithPalette(10, "", ref normalColorV4))
                    {
                        save = true;
                        config.NormalAggroColor = ImGui.ColorConvertFloat4ToU32(normalColorV4);
                    }
                    ImGui.SameLine();
                    ImGui.Text("视觉仇恨范围颜色");

                    var soundColorV4 = ImGui.ColorConvertU32ToFloat4(config.SoundAggroColor);
                    if (ImGuiUtils.ColorPickerWithPalette(11, "", ref soundColorV4))
                    {
                        save = true;
                        config.SoundAggroColor = ImGui.ColorConvertFloat4ToU32(soundColorV4);
                    }
                    ImGui.SameLine();
                    ImGui.Text("听觉/碰撞仇恨范围颜色");
                }
            }
            save |= ImGui.Checkbox("显示区域边框(调试用)", ref config.DrawAreaBorder);
        }
        if (save)
            DalamudApi.Config.Save();
    }
}