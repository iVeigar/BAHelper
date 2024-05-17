using System;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;

namespace BAHelper.System;

public enum SoundEffect
{
    SE_0 = 0x24,
    SE_1 = 0x25,
    SE_2 = 0x26,
    SE_3 = 0x27,
    SE_4 = 0x28,
    SE_5 = 0x29,
    SE_6 = 0x2A,
    SE_7 = 0x2B,
    SE_8 = 0x2C,
    SE_9 = 0x2D,
    SE_10 = 0x2E,
    SE_11 = 0x2F,
    SE_12 = 0x30,
    SE_13 = 0x31,
    SE_14 = 0x32,
    SE_15 = 0x33,
    SE_16 = 0x34
}

public unsafe class GameSound
{
    [Signature("E8 ?? ?? ?? ?? 4D 39 BE ?? ?? ?? ??")]
    public readonly delegate* unmanaged<uint, IntPtr, IntPtr, byte, void> PlaySoundEffect = null;

    public GameSound()
    {
        Svc.Hook.InitializeFromAttributes(this);
    }
    public void Play(SoundEffect soundEffect)
    {
        PlaySoundEffect((uint)soundEffect, IntPtr.Zero, IntPtr.Zero, 0);
    }
}
