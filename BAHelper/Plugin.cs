using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Singletons;
namespace BAHelper;

public sealed class Plugin : IDalamudPlugin
{
    private readonly WindowSystem WindowSystem;
    private readonly MainWindow MainWindow;

    public static Configuration Config { get; private set; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        EzConfig.Migrate<Configuration>();
        Config = EzConfig.Init<Configuration>();

        SingletonServiceManager.Initialize(typeof(Singletons));

        MainWindow = new();
        WindowSystem = new("BAHelper");
        WindowSystem.AddWindow(MainWindow);

        Svc.PluginInterface.UiBuilder.Draw += DrawUI;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
    }

    [Cmd("/bahelper", "开关主窗口")]
    private void ToggleMainWindow(string command, string argument) => MainWindow.IsOpen ^= true;

    private void DrawUI() => WindowSystem.Draw();

    private void OpenMainUi() => MainWindow.IsOpen = true;

    public static void PrintMessage(SeString message, XivChatType type = XivChatType.Echo)
    {
        var sb = new SeStringBuilder()
            .AddUiForeground("[兵武塔助手] ", 60)
            .Append(message);

        Svc.Chat.Print(new()
        {
            Type = type,
            Message = sb.BuiltString
        });
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
    }
}