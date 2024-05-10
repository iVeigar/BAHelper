using System;
using System.Runtime.InteropServices;
using BAHelper.Structures;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace BAHelper;

internal unsafe class Game
{
    public static UIModule* uiModule;
    // Macro Execution
    public delegate void ExecuteMacroDelegate(RaptureShellModule* raptureShellModule, nint macro);
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28", DetourName = nameof(ExecuteMacroDetour))]
    public static Hook<ExecuteMacroDelegate> ExecuteMacroHook;

    public static RaptureShellModule* raptureShellModule;

    public static nint numCopiedMacroLinesPtr = nint.Zero;
    public static byte NumCopiedMacroLines
    {
        get => *(byte*)numCopiedMacroLinesPtr;
        set
        {
            if (numCopiedMacroLinesPtr != nint.Zero)
                SafeMemory.Write(numCopiedMacroLinesPtr, value);
        }
    }

    public static nint numExecutedMacroLinesPtr = nint.Zero;
    public static byte NumExecutedMacroLines
    {
        get => *(byte*)numExecutedMacroLinesPtr;
        set
        {
            if (numExecutedMacroLinesPtr != nint.Zero)
                SafeMemory.Write(numExecutedMacroLinesPtr, value);
        }
    }
    public static void Initialize()
    {
        uiModule = Framework.Instance()->GetUiModule();
        raptureShellModule = uiModule->GetRaptureShellModule();
        DalamudApi.GameInteropProvider?.InitializeFromAttributes(new Game());
        numCopiedMacroLinesPtr = DalamudApi.SigScanner.ScanText("49 8D 5E 70 BF ?? 00 00 00") + 0x5;
        numExecutedMacroLinesPtr = DalamudApi.SigScanner.ScanText("41 83 F8 ?? 0F 8D ?? ?? ?? ?? 49 6B C8 68") + 0x3;
        ExecuteMacroHook.Enable();
    }

    public static void ExecuteMacroDetour(RaptureShellModule* raptureShellModule, nint macro)
    {
        NumCopiedMacroLines = 15;
        NumExecutedMacroLines = 15;
        ExecuteMacroHook.Original(raptureShellModule, macro);
    }

    public static void SendMessage(string message)
    {
        string[] macroQueue = [message + "\0"];
        var macroPtr = nint.Zero;
        try
        {
            macroPtr = Marshal.AllocHGlobal(ExtendedMacro.size);
            using var macro = new ExtendedMacro(macroPtr, string.Empty, macroQueue);
            Marshal.StructureToPtr(macro, macroPtr, false);

            NumCopiedMacroLines = 1;
            NumExecutedMacroLines = 1;
            ExecuteMacroHook.Original(raptureShellModule, macroPtr);
            NumCopiedMacroLines = 15;
        }
        catch (Exception ex)
        {
            DalamudApi.PluginLog.Error(ex, "failed injecting macro");
        }
        Marshal.FreeHGlobal(macroPtr);
    }
    public static void Dispose()
    {
        ExecuteMacroHook?.Dispose();
        NumCopiedMacroLines = 15;
        NumExecutedMacroLines = 15;
    }
}
