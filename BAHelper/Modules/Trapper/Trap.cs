using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
public class Trap : IEquatable<Trap>
{
    public TrapType Type { get; init; }

    public Vector3 Location { get; init; }

    public AreaTag AreaTag { get; init; }

    public TrapState State { get; set; } = TrapState.NotScanned;

    private int _id = -1;

    public int ID
    {
        get
        {
            if (_id == -1)
            {
                _id = AllTraps.Values.FirstOrDefault(Equals)?._id ?? -2;
            }
            return _id;
        }
    }

    public bool IsInRecords => ID >= 0;

    public float BlastRadius => Type switch
    {
        TrapType.BigBomb => 7.0f, // verified
        TrapType.SmallBomb => 7.0f, // need verify
        TrapType.Portal => 1.0f,
        _ => 0.0f
    };

    public float HitBoxRadius => Type switch
    {
        TrapType.BigBomb => 5.0f,
        TrapType.SmallBomb => 3.0f,
        TrapType.Portal => 1.0f,
        _ => 0.0f
    };

    public Trap()
    {
    }

    private Trap(int id, TrapType type, Vector3 location, AreaTag areaTag)
    {
        _id = id;
        Type = type;
        Location = location;
        AreaTag = areaTag;
    }

    public bool Equals(Trap other)
    {
        return other is not null && Type == other.Type && Location.Distance2D(other.Location) <= 0.01;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Trap);
    }

    public override int GetHashCode()
    {
        return Location.ToVector2().GetHashCode();
    }


    public IEnumerable<int> GetComplementarySet()
    {
        if (!IsInRecords) return [];
        return GetComplementarySet([ID]);
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

    public static void ResetAll()
    {
        foreach (var (_, trap) in AllTraps)
            trap.State = TrapState.NotScanned;
    }

    public static void UpdateByScanResult(Vector3 center, ScanResult lastScanResult)
    {
        if (lastScanResult == ScanResult.None)
            return;

        List<int> trapsIn15y = [];
        List<int> trapsBetween15yAnd36y = [];
        foreach (var trap in AllTraps.Values.Where(t => t.State == TrapState.NotScanned))
        {
            var distance = trap.Location.Distance2D(center);
            if (distance <= 15.0f)
                trapsIn15y.Add(trap.ID);
            else if (distance <= 36.0f)
                trapsBetween15yAnd36y.Add(trap.ID);
        }

        // 36y内无陷阱
        if (lastScanResult == ScanResult.NotSense)
        {
            trapsIn15y.Each(t => AllTraps[t].State = TrapState.Disabled);
            trapsBetween15yAnd36y.Each(t => AllTraps[t].State = TrapState.Disabled);
        }
        // 15y内无陷阱; 15y-36y有陷阱
        else if (lastScanResult == ScanResult.Sense)
        {
            if (trapsIn15y.Count > 0)
            {
                if (TrapSets[0].IsSupersetOf(trapsIn15y))
                {
                    GetComplementarySet(Enumerable.Range(0, 3).Except(trapsIn15y.Select(id => id % 3).ToHashSet())).Each(id => AllTraps[id].State = TrapState.Disabled);
                }
                else
                {
                    trapsIn15y.Each(id => AllTraps[id].State = TrapState.Disabled);
                }
            }
            if (trapsBetween15yAnd36y.Count > 0)
            {
                GetComplementarySet(trapsBetween15yAnd36y).Each(id => AllTraps[id].State = TrapState.Disabled);
            }
        }
        else if (lastScanResult == ScanResult.Discover)
        {
            trapsIn15y.Each(id => AllTraps[id].State = TrapState.Disabled);
        }
    }

    public static Dictionary<int, Trap> AllTraps { get; } = new()
    {
        { 0, new(0, TrapType.SmallBomb, new(-18.91813f, 22.124254f, 598.7612f), AreaTag.CircularPlatform) },
        { 1, new(1, TrapType.SmallBomb, new(-12.3771305f, 22.124254f, 601.1392f), AreaTag.CircularPlatform) },
        { 2, new(2, TrapType.SmallBomb, new(-6.8851304f, 22.124254f, 603.7652f), AreaTag.CircularPlatform) },
        { 3, new(3, TrapType.SmallBomb, new(-18.79713f, 22.124252f, 626.1222f), AreaTag.CircularPlatform) },
        { 4, new(4, TrapType.SmallBomb, new(-12.37613f, 22.611395f, 628.7342f), AreaTag.CircularPlatform) },
        { 5, new(5, TrapType.SmallBomb, new(-6.8851304f, 22.611395f, 631.3652f), AreaTag.CircularPlatform) },
        { 6, new(6, TrapType.SmallBomb, new(5.10786f, 22.488823f, 597.8062f), AreaTag.CircularPlatform) },
        { 7, new(7, TrapType.SmallBomb, new(11.62486f, 22.124254f, 601.1272f), AreaTag.CircularPlatform) },
        { 8, new(8, TrapType.SmallBomb, new(17.11486f, 22.124252f, 603.7652f), AreaTag.CircularPlatform) },
        { 9, new(9, TrapType.SmallBomb, new(5.1228604f, 22.611397f, 625.1842f), AreaTag.CircularPlatform) },
        { 10, new(10, TrapType.SmallBomb, new(11.62486f, 22.611393f, 628.7262f), AreaTag.CircularPlatform) },
        { 11, new(11, TrapType.SmallBomb, new(17.11486f, 22.12425f, 631.3652f), AreaTag.CircularPlatform) },

        { 12, new(12, TrapType.BigBomb, new(-77.73215f, 7.0f, 608.9447f), AreaTag.CorridorFromArt) },
        { 13, new(13, TrapType.BigBomb, new(-77.73215f, 7.0f, 614.9432f), AreaTag.CorridorFromArt) },
        { 14, new(14, TrapType.BigBomb, new(-77.73215f, 7.0f, 620.8428f), AreaTag.CorridorFromArt) },

        { 15, new(15, TrapType.BigBomb, new(77.66785f, 7.0f, 609.0447f), AreaTag.CorridorFromOwain) },
        { 16, new(16, TrapType.BigBomb, new(77.66785f, 7.0f, 615.0432f), AreaTag.CorridorFromOwain) },
        { 17, new(17, TrapType.BigBomb, new(77.66785f, 7.0f, 620.8428f), AreaTag.CorridorFromOwain) },

        { 18, new(18, TrapType.BigBomb, new(104.368f, 80.0f, 289.4429f), AreaTag.OctagonRoomToRoomGroup1) },
        { 19, new(19, TrapType.BigBomb, new(111.968f, 80.0f, 292.4431f), AreaTag.OctagonRoomToRoomGroup1) },
        { 20, new(20, TrapType.BigBomb, new(119.5679f, 80.0f, 289.4429f), AreaTag.OctagonRoomToRoomGroup1) },

        { 21, new(21, TrapType.Portal, new(-94.79746f, 80.0f, 269.1593f), AreaTag.IceRoom1) },
        { 22, new(22, TrapType.Portal, new(-84.89747f, 80.0f, 269.1593f), AreaTag.IceRoom1) },
        { 23, new(23, TrapType.Portal, new(-74.99747f, 80.0f, 269.1593f), AreaTag.IceRoom1) },
        { 24, new(24, TrapType.Portal, new(-65.09753f, 80.0f, 269.1593f), AreaTag.IceRoom1) },
        { 25, new(25, TrapType.Portal, new(-32.59755f, 80.0f, 265.3596f), AreaTag.LightningRoom1) },
        { 26, new(26, TrapType.Portal, new(-0.6975479f, 80.0f, 265.3596f), AreaTag.LightningRoom1) },
        { 27, new(27, TrapType.Portal, new(48.00245f, 80.0f, 265.3596f), AreaTag.FireRoom1) }, // 真实y是80.614265f，为了画出来的圈贴着地调整一下
        { 28, new(28, TrapType.Portal, new(-94.89746f, 80.1745f, 358.6594f), AreaTag.WaterRoom1) },
        { 29, new(29, TrapType.Portal, new(-84.89747f, 80.1745f, 358.6594f), AreaTag.WaterRoom1) },
        { 30, new(30, TrapType.Portal, new(-74.89747f, 80.1745f, 358.6594f), AreaTag.WaterRoom1) },
        { 31, new(31, TrapType.Portal, new(-64.99754f, 80.1745f, 358.6594f), AreaTag.WaterRoom1) },
        { 32, new(32, TrapType.Portal, new(-32.69755f, 80.0f, 362.6596f), AreaTag.WindRoom1) },
        { 33, new(33, TrapType.Portal, new(-0.7975483f, 80.0f, 362.6596f), AreaTag.WindRoom1) },
        { 34, new(34, TrapType.Portal, new(47.90245f, 80.0f, 363.0596f), AreaTag.EarthRoom1) },

        { 35, new(35, TrapType.BigBomb, new(-99.832f, 80.0f, 272.7431f), AreaTag.IceRoom1) },
        { 36, new(36, TrapType.BigBomb, new(-89.832f, 80.0f, 272.7431f), AreaTag.IceRoom1) },
        { 37, new(37, TrapType.BigBomb, new(-79.932f, 80.0f, 272.7431f), AreaTag.IceRoom1) },
        { 38, new(38, TrapType.BigBomb, new(-70.032f, 80.0f, 272.7431f), AreaTag.IceRoom1) },
        { 39, new(39, TrapType.BigBomb, new(-60.132f, 80.13398f, 272.7431f), AreaTag.IceRoom1) },
        { 40, new(40, TrapType.BigBomb, new(-99.832f, 80.521225f, 284.6432f), AreaTag.IceRoom1) },
        { 41, new(41, TrapType.BigBomb, new(-89.932f, 80.0f, 284.6432f), AreaTag.IceRoom1) },
        { 42, new(42, TrapType.BigBomb, new(-79.932f, 80.0f, 284.6432f), AreaTag.IceRoom1) },
        { 43, new(43, TrapType.BigBomb, new(-69.932f, 80.0f, 284.6432f), AreaTag.IceRoom1) },
        { 44, new(44, TrapType.BigBomb, new(-60.032f, 80.0f, 284.6432f), AreaTag.IceRoom1) },

        { 45, new(45, TrapType.BigBomb, new(-21.932f, 80.0f, 272.543f), AreaTag.LightningRoom1) },
        { 46, new(46, TrapType.BigBomb, new(-16.0328f, 80.0f, 280.6431f), AreaTag.LightningRoom1) },
        { 47, new(47, TrapType.BigBomb, new(-10.432f, 80.0f, 272.443f), AreaTag.LightningRoom1) },

        { 48, new(48, TrapType.BigBomb, new(-99.832f, 80.0f, 343.2431f), AreaTag.WaterRoom1) },
        { 49, new(49, TrapType.BigBomb, new(-89.832f, 80.0f, 343.2431f), AreaTag.WaterRoom1) },
        { 50, new(50, TrapType.BigBomb, new(-79.932f, 80.0f, 343.2431f), AreaTag.WaterRoom1) },
        { 51, new(51, TrapType.BigBomb, new(-70.032f, 80.0f, 343.2431f), AreaTag.WaterRoom1) },
        { 52, new(52, TrapType.BigBomb, new(-60.132f, 80.0f, 343.2431f), AreaTag.WaterRoom1) },
        { 53, new(53, TrapType.BigBomb, new(-99.832f, 80.201355f, 355.2433f), AreaTag.WaterRoom1) },
        { 54, new(54, TrapType.BigBomb, new(-89.932f, 80.174484f, 355.2433f), AreaTag.WaterRoom1) },
        { 55, new(55, TrapType.BigBomb, new(-79.932f, 80.174484f, 355.2433f), AreaTag.WaterRoom1) },
        { 56, new(56, TrapType.BigBomb, new(-69.932f, 80.17449f, 355.2433f), AreaTag.WaterRoom1) },
        { 57, new(57, TrapType.BigBomb, new(-60.032f, 80.22714f, 355.2433f), AreaTag.WaterRoom1) },

        { 58, new(58, TrapType.BigBomb, new(-21.832f, 80.0f, 362.5435f), AreaTag.WindRoom1) },//80.918
        { 59, new(59, TrapType.BigBomb, new(-16.032f, 80.0f, 354.2436f), AreaTag.WindRoom1) },
        { 60, new(60, TrapType.BigBomb, new(-10.332f, 80.0f, 362.5435f), AreaTag.WindRoom1) },//80.9289

        { 61, new(61, TrapType.BigBomb, new(-224.142f, 48.0f, 414.7333f), AreaTag.IceRoom2) },
        { 62, new(62, TrapType.BigBomb, new(-224.042f, 48.0f, 429.0333f), AreaTag.IceRoom2) },
        { 63, new(63, TrapType.BigBomb, new(-217.1419f, 48.0f, 421.8332f), AreaTag.IceRoom2) },
        { 64, new(64, TrapType.BigBomb, new(-231.042f, 48.0f, 421.9332f), AreaTag.IceRoom2) },
        { 65, new(65, TrapType.BigBomb, new(-160.032f, 48.285446f, 414.6233f), AreaTag.LightningRoom2) },
        { 66, new(66, TrapType.BigBomb, new(-166.932f, 48.0f, 421.8232f), AreaTag.LightningRoom2) },
        { 67, new(67, TrapType.BigBomb, new(-159.932f, 48.0f, 428.9233f), AreaTag.LightningRoom2) },
        { 68, new(68, TrapType.BigBomb, new(-153.0319f, 48.0f, 421.7232f), AreaTag.LightningRoom2) },
        { 69, new(69, TrapType.BigBomb, new(-96.00196f, 48.0f, 414.7333f), AreaTag.FireRoom2) },
        { 70, new(70, TrapType.BigBomb, new(-89.00191f, 48.57847f, 421.8332f), AreaTag.FireRoom2) },
        { 71, new(71, TrapType.BigBomb, new(-95.90195f, 48.0f, 429.0333f), AreaTag.FireRoom2) },
        { 72, new(72, TrapType.BigBomb, new(-102.902f, 48.23551f, 421.9332f), AreaTag.FireRoom2) },
        { 73, new(73, TrapType.BigBomb, new(-231.042f, 48.0f, 494.0228f), AreaTag.WaterRoom2) },
        { 74, new(74, TrapType.BigBomb, new(-224.142f, 48.0f, 486.8229f), AreaTag.WaterRoom2) },
        { 75, new(75, TrapType.BigBomb, new(-224.042f, 48.174477f, 501.1229f), AreaTag.WaterRoom2) },
        { 76, new(76, TrapType.BigBomb, new(-217.1419f, 48.0f, 493.9228f), AreaTag.WaterRoom2) },
        { 77, new(77, TrapType.BigBomb, new(-166.9321f, 48.0f, 494.0228f), AreaTag.WindRoom2) },
        { 78, new(78, TrapType.BigBomb, new(-160.032f, 48.0f, 486.8229f), AreaTag.WindRoom2) },
        { 79, new(79, TrapType.BigBomb, new(-159.932f, 48.0f, 501.1229f), AreaTag.WindRoom2) },
        { 80, new(80, TrapType.BigBomb, new(-153.032f, 48.0f, 493.9228f), AreaTag.WindRoom2) },
        { 81, new(81, TrapType.BigBomb, new(-102.942f, 48.0f, 494.0228f), AreaTag.EarthRoom2) },
        { 82, new(82, TrapType.BigBomb, new(-96.04198f, 48.0f, 486.8229f), AreaTag.EarthRoom2) },
        { 83, new(83, TrapType.BigBomb, new(-89.04194f, 48.0f, 493.9228f), AreaTag.EarthRoom2) },
        { 84, new(84, TrapType.BigBomb, new(-95.94198f, 48.0f, 501.1229f), AreaTag.EarthRoom2) },
    
        #region OctagonRoomFromRaiden
        { 100, new(100, TrapType.SmallBomb, new(102.661514f, 56.0f, 462.2673f), AreaTag.OctagonRoomFromRaiden) },
        { 101, new(101, TrapType.SmallBomb, new(117.0615f, 56.0f, 447.7673f), AreaTag.OctagonRoomFromRaiden) },
        { 102, new(102, TrapType.SmallBomb, new(131.8615f, 56.0f, 462.2673f), AreaTag.OctagonRoomFromRaiden) },
        { 103, new(103, TrapType.SmallBomb, new(117.0615f, 56.0f, 476.7673f), AreaTag.OctagonRoomFromRaiden) },
        { 104, new(104, TrapType.SmallBomb, new(140.7815f, 56.0f, 458.6813f), AreaTag.OctagonRoomFromRaiden) },
        { 105, new(105, TrapType.SmallBomb, new(125.7615f, 56.0f, 477.8533f), AreaTag.OctagonRoomFromRaiden) },
        #endregion

        #region OctagonRoomToRoomGroup2
        { 110, new(110, TrapType.SmallBomb, new(-296.3142f, 48.0f, 462.3232f), AreaTag.OctagonRoomToRoomGroup2) },
        { 111, new(111, TrapType.SmallBomb, new(-281.7142f, 48.0f, 447.8232f), AreaTag.OctagonRoomToRoomGroup2) },
        { 112, new(112, TrapType.SmallBomb, new(-281.8142f, 48.0f, 476.8232f), AreaTag.OctagonRoomToRoomGroup2) },
        { 113, new(113, TrapType.SmallBomb, new(-267.3142f, 48.0f, 462.3232f), AreaTag.OctagonRoomToRoomGroup2) },
        { 114, new(114, TrapType.SmallBomb, new(-277.1022f, 48.0f, 482.4652f), AreaTag.OctagonRoomToRoomGroup2) },
        { 115, new(115, TrapType.SmallBomb, new(-291.3342f, 48.0f, 467.62622f), AreaTag.OctagonRoomToRoomGroup2) },
        { 116, new(116, TrapType.SmallBomb, new(-283.7812f, 48.0f, 454.6372f), AreaTag.OctagonRoomToRoomGroup2) },
        { 117, new(117, TrapType.SmallBomb, new(-262.2142f, 48.0f, 467.3882f), AreaTag.OctagonRoomToRoomGroup2) },
        #endregion
    };

    public static List<HashSet<int>> TrapSets { get; } =
    [
        Enumerable.Range(0, 12).ToHashSet(), // CircularPlatform
        Enumerable.Range(12, 3).ToHashSet(), // CorridorFromArt
        Enumerable.Range(15, 3).ToHashSet(), // CorridorFromOwain
        Enumerable.Range(18, 3).ToHashSet(), // OctagonRoomToRoomGroup1
        Enumerable.Range(21, 14).ToHashSet(), // RoomGroup1 Portals
        Enumerable.Range(35, 5).ToHashSet(), // IceRoom1 TrapSet1
        Enumerable.Range(40, 5).ToHashSet(), // IceRoom1 TrapSet2
        Enumerable.Range(45, 3).ToHashSet(), // LightningRoom1 Traps
        Enumerable.Range(48, 5).ToHashSet(), // WaterRoom1 TrapSet1
        Enumerable.Range(53, 5).ToHashSet(), // WaterRoom1 TrapSet2
        Enumerable.Range(58, 3).ToHashSet(), // WindRoom1 Traps
        Enumerable.Range(61, 4).ToHashSet(), // IceRoom2
        Enumerable.Range(65, 4).ToHashSet(), // LightningRoom2
        Enumerable.Range(69, 4).ToHashSet(), // FireRoom2
        Enumerable.Range(73, 4).ToHashSet(), // WaterRoom2
        Enumerable.Range(77, 4).ToHashSet(), // WindRoom2
        Enumerable.Range(81, 4).ToHashSet(), // EarthRoom2
        Enumerable.Range(100, 6).ToHashSet(), // OctagonRoomFromRaiden // todo add more
        Enumerable.Range(110, 8).ToHashSet(), // OctagonRoomToRoomGroup2 // todo add more
    ];
}
