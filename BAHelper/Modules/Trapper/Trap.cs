using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BAHelper.Utility;
using ECommons;

namespace BAHelper.Modules.Trapper;

public enum TrapState
{
    NotScanned,
    Revealed,
    Disabled
}
public enum TrapType
{
    None,
    BigBomb = 2009728,
    Portal = 2009729,
    SmallBomb = 2009730
}
public enum ScanResult
{
    None,
    Discover,
    Sense,
    NotSense
}
public class Trap
{
    public TrapType Type { get; init; }

    public Vector3 Location { get; init; }

    public AreaTag AreaTag { get; init; }

    public TrapState State { get; set; } = TrapState.NotScanned;

    public string Info { get; set; }

    public int Id { get; init; }

    public float BlastRadius => Type switch
    {
        TrapType.BigBomb => 7.0f,
        TrapType.SmallBomb => 8.0f,
        _ => 0.0f
    };

    public float HitBoxRadius => Type switch
    {
        TrapType.BigBomb => 5.0f,
        TrapType.SmallBomb => 3.0f,
        TrapType.Portal => 1.0f,
        _ => 0.0f
    };

    private Trap(int id, TrapType type, Vector3 location, AreaTag areaTag, string info)
    {
        Id = id;
        Type = type;
        Location = location;
        AreaTag = areaTag;
        Info = info;
    }

    public bool LocationEquals(Vector3 loc) => (Location - loc).ToVector2().LengthSquared() < 0.01f;

    public IEnumerable<int> GetComplementarySet() => GetComplementarySet([Id]);

    public static bool TryGetFromLocation(Vector3 loc, out Trap? trap)
    {
        trap = AllTraps.Values.FirstOrDefault(trap => trap.LocationEquals(loc));
        return trap != null;
    }

    public static IEnumerable<int> GetComplementarySet(IEnumerable<int> source)
    {
        if (source == null || !source.Any()) return [];
        if (TrapSets[0].IsSupersetOf(source))
        {
            var groupNums = source.Select(id => id % 3).ToHashSet();
            return TrapSets[0].Where(id => !groupNums.Contains(id % 3));
        }
        return TrapSets.Skip(1).FirstOrDefault(set => set.IsSupersetOf(source), []).Except(source);
    }

    public static void ResetAll() => AllTraps.Values.Each(trap => trap.State = TrapState.NotScanned);

    public static void UpdateByScanResult(Vector3 center, ScanResult lastScanResult)
    {
        if (lastScanResult == ScanResult.None)
            return;

        List<int> trapsBetween15yAnd36y = [];
        foreach (var trap in AllTraps.Values.Where(t => t.State == TrapState.NotScanned))
        {
            var distance = trap.Location.Distance2D(center);
            if (distance <= 15.0f)
            {
                // 即使画面上这里是雷, 也设为Disabled, 它的状态由GameObject扫描函数更新为Revealed
                trap.State = TrapState.Disabled;
                if (lastScanResult == ScanResult.Sense && trap.Id < 12) // 黑白枪平台后房间里每4颗雷为一组,一并同步更新状态为Disabled
                    Enumerable.Range(1, 3).Each(i => AllTraps[(trap.Id + i * 3) % 12].State = TrapState.Disabled);
            }
            else if (distance <= 36.0f)
            {
                trapsBetween15yAnd36y.Add(trap.Id);
                if (lastScanResult == ScanResult.NotSense) // 36y内无陷阱
                    trap.State = TrapState.Disabled;
            }
        }
        // 15y内无陷阱; 15y-36y有陷阱
        if (lastScanResult == ScanResult.Sense)
            GetComplementarySet(trapsBetween15yAnd36y).Each(id => AllTraps[id].State = TrapState.Disabled);
    }

    public static Dictionary<int, Trap> AllTraps { get; } = new()
    {
        { 0, new(0, TrapType.SmallBomb, new(-18.91813f, 22.124254f, 598.7612f), AreaTag.CircularPlatform, "左上易伤雷") },
        { 1, new(1, TrapType.SmallBomb, new(-12.3771305f, 22.124254f, 601.1392f), AreaTag.CircularPlatform, "左上易伤雷") },
        { 2, new(2, TrapType.SmallBomb, new(-6.8851304f, 22.124254f, 603.7652f), AreaTag.CircularPlatform, "左上易伤雷") },
        { 3, new(3, TrapType.SmallBomb, new(-18.79713f, 22.124252f, 626.1222f), AreaTag.CircularPlatform, "左下易伤雷") },
        { 4, new(4, TrapType.SmallBomb, new(-12.37613f, 22.611395f, 628.7342f), AreaTag.CircularPlatform, "左下易伤雷") },
        { 5, new(5, TrapType.SmallBomb, new(-6.8851304f, 22.611395f, 631.3652f), AreaTag.CircularPlatform, "左下易伤雷") },
        { 6, new(6, TrapType.SmallBomb, new(5.10786f, 22.488823f, 597.8062f), AreaTag.CircularPlatform, "右上易伤雷") },
        { 7, new(7, TrapType.SmallBomb, new(11.62486f, 22.124254f, 601.1272f), AreaTag.CircularPlatform, "右上易伤雷") },
        { 8, new(8, TrapType.SmallBomb, new(17.11486f, 22.124252f, 603.7652f), AreaTag.CircularPlatform, "右上易伤雷") },
        { 9, new(9, TrapType.SmallBomb, new(5.1228604f, 22.611397f, 625.1842f), AreaTag.CircularPlatform, "右下易伤雷") },
        { 10, new(10, TrapType.SmallBomb, new(11.62486f, 22.611393f, 628.7262f), AreaTag.CircularPlatform, "右下易伤雷") },
        { 11, new(11, TrapType.SmallBomb, new(17.11486f, 22.12425f, 631.3652f), AreaTag.CircularPlatform, "右下易伤雷") },

        { 12, new(12, TrapType.BigBomb, new(-77.73215f, 7.0f, 608.9447f), AreaTag.CorridorFromArt, "黑枪即死雷") },
        { 13, new(13, TrapType.BigBomb, new(-77.73215f, 7.0f, 614.9432f), AreaTag.CorridorFromArt, "黑枪即死雷") },
        { 14, new(14, TrapType.BigBomb, new(-77.73215f, 7.0f, 620.8428f), AreaTag.CorridorFromArt, "黑枪即死雷") },

        { 15, new(15, TrapType.BigBomb, new(77.66785f, 7.0f, 609.0447f), AreaTag.CorridorFromOwain, "白枪即死雷") },
        { 16, new(16, TrapType.BigBomb, new(77.66785f, 7.0f, 615.0432f), AreaTag.CorridorFromOwain, "白枪即死雷") },
        { 17, new(17, TrapType.BigBomb, new(77.66785f, 7.0f, 620.8428f), AreaTag.CorridorFromOwain, "白枪即死雷") },

        { 18, new(18, TrapType.BigBomb, new(104.368f, 80.0f, 289.4429f), AreaTag.OctagonRoomToRoomGroup1, "宝箱即死雷") },
        { 19, new(19, TrapType.BigBomb, new(111.968f, 80.0f, 292.4431f), AreaTag.OctagonRoomToRoomGroup1, "宝箱即死雷") },
        { 20, new(20, TrapType.BigBomb, new(119.5679f, 80.0f, 289.4429f), AreaTag.OctagonRoomToRoomGroup1, "宝箱即死雷") },

        { 21, new(21, TrapType.Portal, new(-94.79746f, 80.0f, 269.1593f), AreaTag.IceRoom1, "冰门") },
        { 22, new(22, TrapType.Portal, new(-84.89747f, 80.0f, 269.1593f), AreaTag.IceRoom1, "冰门") },
        { 23, new(23, TrapType.Portal, new(-74.99747f, 80.0f, 269.1593f), AreaTag.IceRoom1, "冰门") },
        { 24, new(24, TrapType.Portal, new(-65.09753f, 80.0f, 269.1593f), AreaTag.IceRoom1, "冰门") },
        { 25, new(25, TrapType.Portal, new(-32.59755f, 80.0f, 265.3596f), AreaTag.LightningRoom1, "雷门") },
        { 26, new(26, TrapType.Portal, new(-0.6975479f, 80.0f, 265.3596f), AreaTag.LightningRoom1, "雷门") },
        { 27, new(27, TrapType.Portal, new(48.00245f, 80.614265f, 265.3596f), AreaTag.FireRoom1, "火门") },
        { 28, new(28, TrapType.Portal, new(-94.89746f, 80.1745f, 358.6594f), AreaTag.WaterRoom1, "水门") },
        { 29, new(29, TrapType.Portal, new(-84.89747f, 80.1745f, 358.6594f), AreaTag.WaterRoom1, "水门") },
        { 30, new(30, TrapType.Portal, new(-74.89747f, 80.1745f, 358.6594f), AreaTag.WaterRoom1, "水门") },
        { 31, new(31, TrapType.Portal, new(-64.99754f, 80.1745f, 358.6594f), AreaTag.WaterRoom1, "水门") },
        { 32, new(32, TrapType.Portal, new(-32.69755f, 80.0f, 362.6596f), AreaTag.WindRoom1, "风门") },
        { 33, new(33, TrapType.Portal, new(-0.7975483f, 80.0f, 362.6596f), AreaTag.WindRoom1, "风门") },
        { 34, new(34, TrapType.Portal, new(47.90245f, 80.0f, 363.0596f), AreaTag.EarthRoom1, "土门") },

        { 35, new(35, TrapType.BigBomb, new(-99.832f, 80.0f, 272.7431f), AreaTag.IceRoom1, "冰后排雷") },
        { 36, new(36, TrapType.BigBomb, new(-89.832f, 80.0f, 272.7431f), AreaTag.IceRoom1, "冰后排雷") },
        { 37, new(37, TrapType.BigBomb, new(-79.932f, 80.0f, 272.7431f), AreaTag.IceRoom1, "冰后排雷") },
        { 38, new(38, TrapType.BigBomb, new(-70.032f, 80.0f, 272.7431f), AreaTag.IceRoom1, "冰后排雷") },
        { 39, new(39, TrapType.BigBomb, new(-60.132f, 80.13398f, 272.7431f), AreaTag.IceRoom1, "冰后排雷") },
        { 40, new(40, TrapType.BigBomb, new(-99.832f, 80.521225f, 284.6432f), AreaTag.IceRoom1, "冰前排雷") },
        { 41, new(41, TrapType.BigBomb, new(-89.932f, 80.0f, 284.6432f), AreaTag.IceRoom1, "冰前排雷") },
        { 42, new(42, TrapType.BigBomb, new(-79.932f, 80.0f, 284.6432f), AreaTag.IceRoom1, "冰前排雷") },
        { 43, new(43, TrapType.BigBomb, new(-69.932f, 80.0f, 284.6432f), AreaTag.IceRoom1, "冰前排雷") },
        { 44, new(44, TrapType.BigBomb, new(-60.032f, 80.0f, 284.6432f), AreaTag.IceRoom1, "冰前排雷") },

        { 45, new(45, TrapType.BigBomb, new(-21.932f, 80.0f, 272.543f), AreaTag.LightningRoom1, "雷即死雷") },
        { 46, new(46, TrapType.BigBomb, new(-16.0328f, 80.0f, 280.6431f), AreaTag.LightningRoom1, "雷即死雷") },
        { 47, new(47, TrapType.BigBomb, new(-10.432f, 80.0f, 272.443f), AreaTag.LightningRoom1, "雷即死雷") },

        { 48, new(48, TrapType.BigBomb, new(-99.832f, 80.0f, 343.2431f), AreaTag.WaterRoom1, "水前排雷") },
        { 49, new(49, TrapType.BigBomb, new(-89.832f, 80.0f, 343.2431f), AreaTag.WaterRoom1, "水前排雷") },
        { 50, new(50, TrapType.BigBomb, new(-79.932f, 80.0f, 343.2431f), AreaTag.WaterRoom1, "水前排雷") },
        { 51, new(51, TrapType.BigBomb, new(-70.032f, 80.0f, 343.2431f), AreaTag.WaterRoom1, "水前排雷") },
        { 52, new(52, TrapType.BigBomb, new(-60.132f, 80.0f, 343.2431f), AreaTag.WaterRoom1, "水前排雷") },
        { 53, new(53, TrapType.BigBomb, new(-99.832f, 80.201355f, 355.2433f), AreaTag.WaterRoom1, "水后排雷") },
        { 54, new(54, TrapType.BigBomb, new(-89.932f, 80.174484f, 355.2433f), AreaTag.WaterRoom1, "水后排雷") },
        { 55, new(55, TrapType.BigBomb, new(-79.932f, 80.174484f, 355.2433f), AreaTag.WaterRoom1, "水后排雷") },
        { 56, new(56, TrapType.BigBomb, new(-69.932f, 80.17449f, 355.2433f), AreaTag.WaterRoom1, "水后排雷") },
        { 57, new(57, TrapType.BigBomb, new(-60.032f, 80.22714f, 355.2433f), AreaTag.WaterRoom1, "水后排雷") },

        { 58, new(58, TrapType.BigBomb, new(-21.832f, 80.0f, 362.5435f), AreaTag.WindRoom1, "风即死雷") },//80.918
        { 59, new(59, TrapType.BigBomb, new(-16.032f, 80.0f, 354.2436f), AreaTag.WindRoom1, "风即死雷") },
        { 60, new(60, TrapType.BigBomb, new(-10.332f, 80.0f, 362.5435f), AreaTag.WindRoom1, "风即死雷") },//80.9289

        { 61, new(61, TrapType.BigBomb, new(-224.142f, 48.0f, 414.7333f), AreaTag.IceRoom2, "冰即死雷") },
        { 62, new(62, TrapType.BigBomb, new(-224.042f, 48.0f, 429.0333f), AreaTag.IceRoom2, "冰即死雷") },
        { 63, new(63, TrapType.BigBomb, new(-217.1419f, 48.0f, 421.8332f), AreaTag.IceRoom2, "冰即死雷") },
        { 64, new(64, TrapType.BigBomb, new(-231.042f, 48.0f, 421.9332f), AreaTag.IceRoom2, "冰即死雷") },
        { 65, new(65, TrapType.BigBomb, new(-160.032f, 48.285446f, 414.6233f), AreaTag.LightningRoom2, "雷即死雷") },
        { 66, new(66, TrapType.BigBomb, new(-166.932f, 48.0f, 421.8232f), AreaTag.LightningRoom2, "雷即死雷") },
        { 67, new(67, TrapType.BigBomb, new(-159.932f, 48.0f, 428.9233f), AreaTag.LightningRoom2, "雷即死雷") },
        { 68, new(68, TrapType.BigBomb, new(-153.0319f, 48.0f, 421.7232f), AreaTag.LightningRoom2, "雷即死雷") },
        { 69, new(69, TrapType.BigBomb, new(-96.00196f, 48.0f, 414.7333f), AreaTag.FireRoom2, "火即死雷") },
        { 70, new(70, TrapType.BigBomb, new(-89.00191f, 48.57847f, 421.8332f), AreaTag.FireRoom2, "火即死雷") },
        { 71, new(71, TrapType.BigBomb, new(-95.90195f, 48.0f, 429.0333f), AreaTag.FireRoom2, "火即死雷") },
        { 72, new(72, TrapType.BigBomb, new(-102.902f, 48.23551f, 421.9332f), AreaTag.FireRoom2, "火即死雷") },
        { 73, new(73, TrapType.BigBomb, new(-231.042f, 48.0f, 494.0228f), AreaTag.WaterRoom2, "水即死雷") },
        { 74, new(74, TrapType.BigBomb, new(-224.142f, 48.0f, 486.8229f), AreaTag.WaterRoom2, "水即死雷") },
        { 75, new(75, TrapType.BigBomb, new(-224.042f, 48.174477f, 501.1229f), AreaTag.WaterRoom2, "水即死雷") },
        { 76, new(76, TrapType.BigBomb, new(-217.1419f, 48.0f, 493.9228f), AreaTag.WaterRoom2, "水即死雷") },
        { 77, new(77, TrapType.BigBomb, new(-166.9321f, 48.0f, 494.0228f), AreaTag.WindRoom2, "风即死雷") },
        { 78, new(78, TrapType.BigBomb, new(-160.032f, 48.0f, 486.8229f), AreaTag.WindRoom2, "风即死雷") },
        { 79, new(79, TrapType.BigBomb, new(-159.932f, 48.0f, 501.1229f), AreaTag.WindRoom2, "风即死雷") },
        { 80, new(80, TrapType.BigBomb, new(-153.032f, 48.0f, 493.9228f), AreaTag.WindRoom2, "风即死雷") },
        { 81, new(81, TrapType.BigBomb, new(-102.942f, 48.0f, 494.0228f), AreaTag.EarthRoom2, "土即死雷") },
        { 82, new(82, TrapType.BigBomb, new(-96.04198f, 48.0f, 486.8229f), AreaTag.EarthRoom2, "土即死雷") },
        { 83, new(83, TrapType.BigBomb, new(-89.04194f, 48.0f, 493.9228f), AreaTag.EarthRoom2, "土即死雷") },
        { 84, new(84, TrapType.BigBomb, new(-95.94198f, 48.0f, 501.1229f), AreaTag.EarthRoom2, "土即死雷") },
    
        #region OctagonRoomFromRaiden
        { 100, new(100, TrapType.SmallBomb, new(102.661514f, 56.0f, 462.2673f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 101, new(101, TrapType.SmallBomb, new(117.0615f, 56.0f, 447.7673f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 102, new(102, TrapType.SmallBomb, new(131.8615f, 56.0f, 462.2673f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 103, new(103, TrapType.SmallBomb, new(117.0615f, 56.0f, 476.7673f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 104, new(104, TrapType.SmallBomb, new(140.7815f, 56.0f, 458.6813f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 105, new(105, TrapType.SmallBomb, new(125.7615f, 56.0f, 477.8533f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 106, new(106, TrapType.SmallBomb, new(126.099495f, 56.0f, 445.3333f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        { 107, new(107, TrapType.SmallBomb, new(124.3685f, 56.0f, 468.4923f), AreaTag.OctagonRoomFromRaiden, "平台易伤雷") },
        #endregion

        #region OctagonRoomToRoomGroup2
        { 110, new(110, TrapType.SmallBomb, new(-296.3142f, 48.0f, 462.3232f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 111, new(111, TrapType.SmallBomb, new(-281.7142f, 48.0f, 447.8232f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 112, new(112, TrapType.SmallBomb, new(-281.8142f, 48.0f, 476.8232f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 113, new(113, TrapType.SmallBomb, new(-267.3142f, 48.0f, 462.3232f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 114, new(114, TrapType.SmallBomb, new(-277.1022f, 48.0f, 482.4652f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 115, new(115, TrapType.SmallBomb, new(-291.3342f, 48.0f, 467.62622f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 116, new(116, TrapType.SmallBomb, new(-283.7812f, 48.0f, 454.6372f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        { 117, new(117, TrapType.SmallBomb, new(-262.2142f, 48.0f, 467.3882f), AreaTag.OctagonRoomToRoomGroup2, "平台易伤雷") },
        #endregion
    };

    public static List<HashSet<int>> TrapSets { get; } =
    [
        [..Enumerable.Range(0, 12)], // CircularPlatform
        [..Enumerable.Range(12, 3)], // CorridorFromArt
        [..Enumerable.Range(15, 3)], // CorridorFromOwain
        [..Enumerable.Range(18, 3)], // OctagonRoomToRoomGroup1
        [..Enumerable.Range(21, 14)], // RoomGroup1 Portals
        [..Enumerable.Range(35, 5)], // IceRoom1 TrapSet1
        [..Enumerable.Range(40, 5)], // IceRoom1 TrapSet2
        [..Enumerable.Range(45, 3)], // LightningRoom1 Traps
        [..Enumerable.Range(48, 5)], // WaterRoom1 TrapSet1
        [..Enumerable.Range(53, 5)], // WaterRoom1 TrapSet2
        [..Enumerable.Range(58, 3)], // WindRoom1 Traps
        [..Enumerable.Range(61, 4)], // IceRoom2
        [..Enumerable.Range(65, 4)], // LightningRoom2
        [..Enumerable.Range(69, 4)], // FireRoom2
        [..Enumerable.Range(73, 4)], // WaterRoom2
        [..Enumerable.Range(77, 4)], // WindRoom2
        [..Enumerable.Range(81, 4)], // EarthRoom2
        [..Enumerable.Range(100, 8)], // OctagonRoomFromRaiden // todo add more
        [..Enumerable.Range(110, 8)], // OctagonRoomToRoomGroup2 // todo add more
    ];
}
