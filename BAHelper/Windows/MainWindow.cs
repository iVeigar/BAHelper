using System.Collections.Generic;
using System.Linq;
using BAHelper.Modules;
using BAHelper.Modules.General;
using BAHelper.Modules.Party;
using BAHelper.Modules.Trapper;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;


namespace BAHelper.Windows;

public sealed class MainWindow() : Window("兵武塔助手", ImGuiWindowFlags.AlwaysAutoResize)
{
    private static Configuration Config => Plugin.Config;
    private static DashboardService DashboardService => Singletons.DashboardService;
    private static TrapperService TrapperService => Singletons.TrapperService;
    private static PartyService PartyService => Singletons.PartyService;

    public override void Draw()
    {
        if (ImGui.BeginTabBar("BAHelperTabBar"))
        {
            if (ImGui.BeginTabItem("仪表盘"))
            {
                DrawDashboardTab();
                ImGui.EndTabItem();
            }
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
            //if (ImGui.BeginTabItem("Debug"))
            //{
            //    DrawDebugTab();
            //    ImGui.EndTabItem();
            //}
            ImGui.EndTabBar();
        }
    }

    //private void DrawDebugTab()
    //{
    //    var save = false;
    //    save |= ImGui.Checkbox("显示区域边框(调试用)", ref Config.DrawAreaBorder);
    //    if (save)
    //    {
    //        Config.Save();
    //    }
    //    ImGui.Text("InHydatos: "); ImGui.SameLine(); ImGui.Text(Common.InHydatos.ToString());
    //    ImGui.Text("InBA: "); ImGui.SameLine(); ImGui.Text(Common.InBA.ToString());
    //    ImGui.Text("Area: "); ImGui.SameLine(); ImGui.Text(Common.MeCurrentArea.ToString());
    //}

    private void DrawDashboardTab()
    {
        ImGui.Text("坦克监控");
        ImGui.SameLine();
        var save = ImGui.Checkbox("只显示开盾姿的", ref Config.OnlyShowStanceOn);
        if (save)
            Config.Save();
        //if (!Common.InBA)
        //{
        //    ImGui.TextColored(Color.Red.ToVector4(), "仅在塔内生效");
        //    return;
        //}
        var entries = DashboardService.Tanks.Where(t => !Config.OnlyShowStanceOn || t.StanceActivated).SelectMany(t =>
            new List<ImGuiEx.EzTableEntry>(){
                new("玩家", ImGuiTableColumnFlags.WidthStretch, () => ImGui.Text(t.Name)),
                new("职业", ImGuiTableColumnFlags.WidthStretch, () => ImGui.Text(t.Job)),
                new("文理", ImGuiTableColumnFlags.WidthStretch, () => ImGui.Text(t.Logos)),
                new("盾姿", ImGuiTableColumnFlags.WidthFixed, () => {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(t.StanceActivated ? FontAwesomeIcon.Check.ToIconString() : "");
                    ImGui.PopFont();
                })
            }
        );
        if (!entries.Any())
        {
            ImGui.TextColored(Color.Red.ToVector4(), $"附近没有{(Config.OnlyShowStanceOn ? "开盾姿的" : "")}坦克");
            return;
        }
        ImGuiEx.EzTable("盾姿监控", ImGuiTableFlags.Borders, entries, true);
    }

    private void DrawPartyTab()
    {
        ImGui.TextWrapped("点击发送小队门坐标到:");
        ImGui.SameLine();
        var usePartyChannel = Config.UsePartyChannel;
        var save = false;
        if (ImGui.RadioButton("小队", usePartyChannel == true))
        {
            Config.UsePartyChannel = true;
            save = true;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("默语", usePartyChannel == false))
        {
            Config.UsePartyChannel = false;
            save = true;
        }
        save |= ImGui.Checkbox("使用国服莫古力区门图", ref Config.IsCNMoogleDCPlayer);
        if (save)
            Config.Save();

        using (ImRaii.Disabled(PartyService.IsBusy))
        {
            for (var i = 1; i < 7; i++)
            {
                if (ImGui.Button($"{i}队", ImGuiHelpers.ScaledVector2(40f, 40f)))
                {
                    PartyService.SendPortalsToChat(i);
                }
                if (i < 6) ImGui.SameLine();
            }
        }
        ImGui.Separator();
        using (ImRaii.Disabled(PartyService.IsBusy || Svc.Party.Length == 0))
        {
            if (ImGui.Button("一键检查队员补正数值"))
            {
                PartyService.CheckMembersEurekaBonus();
            }
        }
        if (Svc.Party.Length == 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("未组成小队");
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
        save |= ImGui.Checkbox("上护盾", ref Config.CheckStatusProtect);
        ImGui.SameLine();
        save |= ImGui.Checkbox("上魔盾", ref Config.CheckStatusShell);

        ImGui.Text("盾剩余时间阈值（分钟）");

        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScaleSafe);
        save |= ImGui.SliderInt("", ref Config.ShieldRemainingTimeThreshold, 1, 29);
        if (save)
            Config.Save();

        if (Config.AdvancedModeEnabled)
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
                    MacroManager.Execute($"/sh {TrapperService.ChestFoundAt.Description()}箱");
                }
            }
            ImGui.SameLine();
            var portalAt = TrapperService.PossibleAreasOfPortal.Count != 1 ? AreaTag.None : TrapperService.PossibleAreasOfPortal.First();
            using (ImRaii.Disabled(portalAt == AreaTag.None))
            {
                if (ImGui.Button($"{portalAt.Description()}门", bigButtonSize))
                {
                    MacroManager.Execute($"/sh {portalAt.Description()}门");
                }
            }
            if (portalAt == AreaTag.None)
            {
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.IceRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("冰无门", smallButtonSize))
                        MacroManager.Execute("/sh 冰无门");
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.LightningRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("雷无门", smallButtonSize))
                        MacroManager.Execute("/sh 雷无门");
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.FireRoom1) || TrapperService.PossibleAreasOfPortal.Contains(AreaTag.EarthRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("火土无门", smallButtonSize))
                        MacroManager.Execute("/sh 火土无门");
                }

                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.WaterRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("水无门", smallButtonSize))
                        MacroManager.Execute("/sh 水无门");
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(TrapperService.PossibleAreasOfPortal.Contains(AreaTag.WindRoom1) || portalAt != AreaTag.None))
                {
                    if (ImGui.Button("风无门", smallButtonSize))
                        MacroManager.Execute("/sh 风无门");
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
        if (ImGui.CollapsingHeader("工兵"))
        {
            save |= ImGui.Checkbox("启用扫描与绘图功能", ref Config.AdvancedModeEnabled);
            if (Config.AdvancedModeEnabled)
            {
                ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScaleSafe);
                save |= ImGui.SliderFloat("绘图范围", ref Config.TrapViewDistance, 15f, 2000f, Config.TrapViewDistance.ToString("##.0m"), ImGuiSliderFlags.Logarithmic);

                save |= ImGui.Checkbox("绘制所有已知的陷阱点位", ref Config.DrawRecordedTraps);
                if (Config.DrawRecordedTraps)
                {
                    using (ImRaii.PushIndent())
                    {
                        var bigBombColorV4 = Config.TrapBigBombColor.ToVector4();
                        if (ImGuiUtils.ColorPickerWithPalette(1, "", ref bigBombColorV4))
                        {
                            save = true;
                            Config.TrapBigBombColor = bigBombColorV4.ToUint();
                        }
                        ImGui.SameLine();
                        ImGui.Text("即死雷");

                        ImGui.SameLine();
                        ImGui.Spacing();
                        ImGui.SameLine();

                        var smallBombColorV4 = Config.TrapSmallBombColor.ToVector4();
                        if (ImGuiUtils.ColorPickerWithPalette(2, "", ref smallBombColorV4))
                        {
                            save = true;
                            Config.TrapSmallBombColor = smallBombColorV4.ToUint();
                        }
                        ImGui.SameLine();
                        ImGui.Text("易伤雷");

                        var portalColorV4 = Config.TrapPortalColor.ToVector4();
                        if (ImGuiUtils.ColorPickerWithPalette(3, "", ref portalColorV4))
                        {
                            save = true;
                            Config.TrapPortalColor = portalColorV4.ToUint();
                        }
                        ImGui.SameLine();
                        ImGui.Text("传送门");

                        ImGui.SameLine();
                        ImGui.Spacing();
                        ImGui.SameLine();

                        var discoveredTrapColorV4 = Config.RevealedTrapColor.ToVector4();
                        if (ImGuiUtils.ColorPickerWithPalette(4, "", ref discoveredTrapColorV4))
                        {
                            save = true;
                            Config.RevealedTrapColor = discoveredTrapColorV4.ToUint();
                        }
                        ImGui.SameLine();
                        ImGui.Text("已探明/踩到的");
                    }
                }

                save |= ImGui.Checkbox("绘制陷阱爆炸波及范围", ref Config.DrawTrapBlastCircle);
                if (Config.DrawTrapBlastCircle)
                {
                    using (ImRaii.PushIndent())
                    {
                        save |= ImGui.Checkbox("仅当接近陷阱时##blast", ref Config.DrawTrapBlastCircleOnlyWhenApproaching);
                    }
                }

                save |= ImGui.Checkbox("绘制陷阱15m半径提示圈", ref Config.DrawTrap15m);
                ImGui.SameLine();
                var trap15mCircleColorV4 = Config.Trap15mCircleColor.ToVector4();
                if (ImGuiUtils.ColorPickerWithPalette(5, "", ref trap15mCircleColorV4))
                {
                    save = true;
                    Config.Trap15mCircleColor = trap15mCircleColorV4.ToUint();
                }
                if (Config.DrawTrap15m)
                {
                    using (ImRaii.PushIndent())
                    {
                        save |= ImGui.Checkbox("仅当接近陷阱时##15m", ref Config.DrawTrap15mOnlyWhenApproaching);
                        save |= ImGui.Checkbox("已探出/踩过的陷阱除外##15m", ref Config.DrawTrap15mExceptRevealed);
                    }
                }

                save |= ImGui.Checkbox("绘制陷阱36m半径提示圈", ref Config.DrawTrap36m);
                ImGui.SameLine();
                var trap36mCircleColorV4 = Config.Trap36mCircleColor.ToVector4();
                if (ImGuiUtils.ColorPickerWithPalette(6, "", ref trap36mCircleColorV4))
                {
                    save = true;
                    Config.Trap36mCircleColor = trap36mCircleColorV4.ToUint();
                }
                if (Config.DrawTrap36m)
                {
                    using (ImRaii.PushIndent())
                    {
                        save |= ImGui.Checkbox("仅当接近陷阱时##36m", ref Config.DrawTrap36mOnlyWhenApproaching);
                        save |= ImGui.Checkbox("已探出/踩过的陷阱除外##36m", ref Config.DrawTrap36mExceptRevealed);
                    }
                }

                save |= ImGui.Checkbox("绘制探景推荐点位", ref Config.DrawRecommendedScanningSpots);
                ImGui.SameLine();
                var scanningSpotColorV4 = Config.ScanningSpotColor.ToVector4();
                if (ImGuiUtils.ColorPickerWithPalette(7, "", ref scanningSpotColorV4))
                {
                    save = true;
                    Config.ScanningSpotColor = scanningSpotColorV4.ToUint();
                }

                save |= ImGui.Checkbox("绘制探景推荐点位15m半径提示圈", ref Config.DrawScanningSpot15m);
                ImGui.SameLine();
                var scanningSpot15mCircleColorV4 = Config.ScanningSpot15mCircleColor.ToVector4();
                if (ImGuiUtils.ColorPickerWithPalette(8, "", ref scanningSpot15mCircleColorV4))
                {
                    save = true;
                    Config.ScanningSpot15mCircleColor = scanningSpot15mCircleColorV4.ToUint();
                }

                save |= ImGui.Checkbox("绘制探景推荐点位36m半径提示圈", ref Config.DrawScanningSpot36m);
                ImGui.SameLine();
                var scanningSpot36mCircleColorV4 = Config.ScanningSpot36mCircleColor.ToVector4();
                if (ImGuiUtils.ColorPickerWithPalette(9, "", ref scanningSpot15mCircleColorV4))
                {
                    save = true;
                    Config.ScanningSpot36mCircleColor = scanningSpot36mCircleColorV4.ToUint();
                }

                save |= ImGui.Checkbox("绘制怪物仇恨范围", ref Config.DrawMobViews);
                if (Config.DrawMobViews)
                {
                    using (ImRaii.PushIndent())
                    {
                        var normalColorV4 = Config.NormalAggroColor.ToVector4();
                        if (ImGuiUtils.ColorPickerWithPalette(10, "", ref normalColorV4))
                        {
                            save = true;
                            Config.NormalAggroColor = normalColorV4.ToUint();
                        }
                        ImGui.SameLine();
                        ImGui.Text("视觉仇恨");

                        ImGui.SameLine();
                        ImGui.Spacing();
                        ImGui.SameLine();

                        var soundColorV4 = Config.SoundAggroColor.ToVector4();
                        if (ImGuiUtils.ColorPickerWithPalette(11, "", ref soundColorV4))
                        {
                            save = true;
                            Config.SoundAggroColor = soundColorV4.ToUint();
                        }
                        ImGui.SameLine();
                        ImGui.Text("听觉/碰撞仇恨");
                    }
                }
            }
        }
        if (ImGui.CollapsingHeader("杂项"))
        {
            save |= ImGui.Checkbox("进入水岛时提示当前等级", ref Config.ElementLevelReminderEnabled);
        }
        if (save)
            Config.Save();
    }
}