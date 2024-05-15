using BAHelper.Modules;
using BAHelper.Modules.Party;
using BAHelper.Modules.Trapper;
using BAHelper.Windows;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
namespace BAHelper;

public unsafe sealed class Plugin : IDalamudPlugin
{
    internal readonly WindowSystem WindowSystem;
    internal readonly MainWindow MainWindow;
    private readonly TrapperService TrapperService;
    private readonly PartyService PartyService;
    public Plugin(DalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        DalamudApi.Initialize(pluginInterface, this);
        Game.Initialize();
        Common.Initialize();
        TrapperService = new();
        TrapperTool.Initialize();
        PartyService = new();
        MainWindow = new(TrapperService, PartyService);
        WindowSystem = new("BAHelper");
        WindowSystem.AddWindow(MainWindow);

        DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
    }

    [Command("/bahelper")]
    [HelpMessage("开关主窗口")]
    private void ToggleMainWindow(string command, string argument) => MainWindow.IsOpen ^= true;

    private void DrawUI() => WindowSystem.Draw();
    private void OpenMainUi() => MainWindow.IsOpen = true;

    public static void PrintMessage(SeString message, XivChatType type=XivChatType.Echo)
    {
        var sb = new SeStringBuilder()
            .AddUiForeground("[兵武塔助手] ", 60)
            .Append(message);

        DalamudApi.ChatGui.Print(new XivChatEntry()
        {
            Type = type,
            Message = sb.BuiltString
        });
    }

    public void Dispose()
    {
        DalamudApi.PluginInterface.RemoveChatLinkHandler();
        DalamudApi.PluginInterface.UiBuilder.Draw -= DrawUI;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        WindowSystem.RemoveAllWindows();
        TrapperService.Dispose();
        TrapperTool.Dispose();
        Common.Dispose();
        Game.Dispose();
        DalamudApi.Dispose();
        ECommonsMain.Dispose();
    }
}