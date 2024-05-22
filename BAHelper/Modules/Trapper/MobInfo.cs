using System.Collections.Generic;
namespace BAHelper.Modules.Trapper;
public enum AggroType
{
    Sight,
    Sound,
    Proximity,
    Magic,
    Blood,
}
public class MobInfo
{
    public uint Id; // Character.NameId
    public AggroType AggroType;
    public float AggroDistance;
    public static Dictionary<uint, MobInfo> Mobs { get; } = new()
    {
        { 7985, new(){ Id = 7985, AggroType = AggroType.Sight, AggroDistance = 15f }}, // 冰小怪
        { 7998, new(){ Id = 7998, AggroType = AggroType.Sight, AggroDistance = 15f }}, // 水小怪
        { 7997, new(){ Id = 7997, AggroType = AggroType.Sight, AggroDistance = 15.6f }}, // 风小怪
        { 8000, new(){ Id = 8000, AggroType = AggroType.Sight, AggroDistance = 15.4f }}, // 雷小怪
        { 7999, new(){ Id = 7999, AggroType = AggroType.Sight, AggroDistance = 15f }}, // 火小怪
        { 7986, new(){ Id = 7986, AggroType = AggroType.Sight, AggroDistance = 15f }}, // 土小怪

        { 7988, new(){ Id = 7988, AggroType = AggroType.Sight, AggroDistance = 17f }}, // 比布鲁斯
        { 7989, new(){ Id = 7989, AggroType = AggroType.Sight, AggroDistance = 16f }}, // 人马
        { 7990, new(){ Id = 7990, AggroType = AggroType.Sight, AggroDistance = 16f }}, // 珍本
        { 7991, new(){ Id = 7991, AggroType = AggroType.Sight, AggroDistance = 16.6f }}, // 智蛙
        { 7992, new(){ Id = 7992, AggroType = AggroType.Sight, AggroDistance = 16f }}, // 大娃娃
        { 7987, new(){ Id = 7987, AggroType = AggroType.Sight, AggroDistance = 16f }}, // 抄写员
        { 7994, new(){ Id = 7994, AggroType = AggroType.Sight, AggroDistance = 16f }}, // 扇
        { 8002, new(){ Id = 8002, AggroType = AggroType.Sight, AggroDistance = 16f }}, // 眼
        { 7996, new(){ Id = 7996, AggroType = AggroType.Sound, AggroDistance = 14.8f }}, // 魔导书

        { 8003, new(){ Id = 8003, AggroType = AggroType.Magic, AggroDistance = 16f }}, // 元精
        { 8004, new(){ Id = 8004, AggroType = AggroType.Blood, AggroDistance = 16f }}, // 死魂
        { 7993, new(){ Id = 7993, AggroType = AggroType.Blood, AggroDistance = 16f }}, // 逻各斯
    };
}
