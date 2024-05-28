using BAHelper.Modules.General;
using BAHelper.Modules.Party;
using BAHelper.Modules.Trapper;
using BAHelper.System;

namespace BAHelper;

public static class Singletons
{
    public static DashboardService DashboardService { get; private set; }
    public static TrapperService TrapperService { get; set; }
    public static TrapperTool TrapperTool { get; set; }
    public static PartyService PartyService { get; set; }
    public static GameSound SoundManager { get; set; }
}
