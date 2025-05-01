using BAHelper.Modules.General;
using BAHelper.Modules.Party;
using BAHelper.Modules.Trapper;

namespace BAHelper;

public static class Singletons
{
    public static DashboardService DashboardService { get; private set; }
    public static TrapperService TrapperService { get; set; }
    public static PartyService PartyService { get; set; }
}
