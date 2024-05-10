using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace BAHelper.Modules.Trapper;

public enum AreaTag
{
    None = 0,
    Entry,
    CorridorFromArt,
    CorridorFromOwain,
    CircularPlatform,
    OctagonRoomFromRaiden,
    OctagonRoomToRoomGroup1,
    OctagonRoomFromPortal,
    OctagonRoomToRoomGroup2,
    RoomGroup1,
    RoomGroup2,
    IceRoom1,
    IceRoom2,
    WaterRoom1,
    WaterRoom2,
    LightningRoom1,
    LightningRoom2,
    WindRoom1,
    WindRoom2,
    FireRoom1,
    FireRoom2,
    EarthRoom1,
    EarthRoom2,
}
public static class AreaExtension
{
    public static string Description(this AreaTag areaTag)
    {
        return areaTag switch
        {
            AreaTag.FireRoom1 => "火",
            AreaTag.EarthRoom1 => "土",
            AreaTag.LightningRoom1 => "雷",
            AreaTag.WindRoom1 => "风",
            AreaTag.IceRoom1 => "冰",
            AreaTag.WaterRoom1 => "水",
            _ => "未知"
        };
    }
}
public class Area
{
    public AreaTag Tag { get; init; }
    public string Name { get; init; }
    public Vector3 Origin { get; init; }
    public Vector3 Dims { get; init; }
    public List<(Vector3 Center, float Radius, string Tip)> ScanningSpots { get; init; }

    private List<Trap> _traps = null;
    public List<Trap> Traps => _traps ??= Trap.AllTraps.Values.Where(t => t.AreaTag == Tag).ToList();
    public bool ShowScanningSpot => Traps.Any(trap => trap.State == TrapState.NotScanned);
    public static bool TryGet(AreaTag areaTag, out Area area)
    {
        return AllAreas.TryGetValue(areaTag, out area);
    }
    public static Area? Locate(Vector3 position)
    {
        return AllAreas.FirstOrDefault(kv => position.IsInArea(kv.Value)).Value;
    }
    public static Dictionary<AreaTag, Area> AllAreas { get; } = new()
    {
        {
            AreaTag.Entry,
            new(){
                Tag = AreaTag.Entry,
                Name = "总部塔入口",
                Origin = new(-73f, -60f, 650f),
                Dims = new(132f, 0f, 203f),
                ScanningSpots = []
            }
        },
        {
            AreaTag.CorridorFromArt,
            new(){
                Tag = AreaTag.CorridorFromArt,
                Name = "黑枪后走廊",
                Origin = new(-95f, 7f, 602f),
                Dims = new(32f, 0f, 25f),
                ScanningSpots = new()
                {
                    (new(-89.7f, 6.992835f, 614.9432f), 1.2f, ""),
                }
            }
        },
        { 
            AreaTag.CorridorFromOwain, 
            new(){
                Tag = AreaTag.CorridorFromOwain,
                Name = "白枪后走廊",
                Origin = new(63f, 7f, 602f),
                Dims = new(32f, 0f, 25f),
                ScanningSpots = new()
                {
                    (new(89.7f, 6.992835f, 615.0432f), 1.2f, ""),
                }
            }
        },
        {
            AreaTag.CircularPlatform,
            new(){
                Tag = AreaTag.CircularPlatform,
                Name = "圆形平台房间",
                Origin = new(-32f, 22.124f, 580f),
                Dims = new(64f, 0f, 64f),
                ScanningSpots = new()
                {
                    (new(-26f, 22.12425f, 624f), 1f, "A"),
                    (new(-13f, 22.12425f, 635.2f), 1f, "B"),
                    (new(13f, 22.12425f, 635.2f), 1f, "D"),
                    (new(26f, 22.12425f, 624f), 1f, "C"),
                    (new(5.12f, 22.611395f, 611.08f), 1f, "F"),
                    (new(-5.28f, 22.611395f, 611.42f), 1f, "E"),
                }
            }
        },
        { 
            AreaTag.OctagonRoomFromRaiden,
            new(){
                Tag = AreaTag.OctagonRoomFromRaiden,
                Name = "莱汀后平台",
                Origin = new(80f, 56f, 426f),
                Dims = new(64f, 0f, 64f),
                ScanningSpots = new()
                {
                    (new(90.8f, 56.00004f, 458f), 1f, "0"),
                    (new(105.5f, 56.00004f, 470.5f), 1f, "A-1"),
                    (new(105.5f, 56.00004f, 445.5f), 1f, "A-2"),

                    (new(119.3f, 56.00004f, 470.5f), 1f, "B-1"),
                    (new(128.8f, 56.00004f, 458f), 1f, "B-2"),
                    (new(124.5f, 56.00004f, 450.7f), 1f, "B-3"),
                }
            }
        },
        {
            AreaTag.OctagonRoomToRoomGroup1,
            new(){
                Tag = AreaTag.OctagonRoomToRoomGroup1,
                Name = "一查前平台",
                Origin = new(80f, 80f, 282f),
                Dims = new(64f, 0f, 64f),
                ScanningSpots = new()
                {
                    (new(111.975f, 80f, 300f), 2.0f, ""),
                    (new(111.97f, 80f, 326.8f), 1.5f, "感知中心雷"),

                }
            }
        },
        //{ 
        //    AreaTag.OctagonRoomFromPortal,
        //    new(){
        //        Tag = AreaTag.OctagonRoomFromPortal,
        //        Name = "一查后平台",
        //        Origin = new(-319f, 80f, 282f),
        //        Dims = new(64f, 0f, 64f),
        //        ScanningSpots = new()
        //    }
        //},
        {
            AreaTag.OctagonRoomToRoomGroup2, 
            new(){
                Tag = AreaTag.OctagonRoomToRoomGroup2,
                Name = "二查前平台",
                Origin = new(-319f, 48f, 426f),
                Dims = new(64f, 0f, 64f),
                ScanningSpots = new()
                {
                    (new(-287f, 48.00002f, 436.8f), 1f, "0"),
                    (new(-299.5f, 48.00002f, 451f), 1f, "A-1"),
                    (new(-274.5f, 48.00002f, 451f), 1f, "A-2"),
                    (new(-299.5f, 48.00002f, 465.3f), 1f, "B-1"),
                    (new(-287f, 48.00002f, 474.8f), 1f, "B-2"),
                    (new(-279.69f, 48.00002f, 470.5f), 1f, "B-3"),
                }
            }
        },
        //{
        //    AreaTag.RoomGroup1,
        //    new(){
        //        Tag = AreaTag.RoomGroup1,
        //        Name = "一查",
        //        Origin = new(-109f, 80f, 253f),
        //        Dims = new(186f, 0f, 122f)
        //    }
        //},
        {
            AreaTag.IceRoom1,
            new(){
                Tag = AreaTag.IceRoom1,
                Name = "冰",
                Origin = new(-104f, 80f, 254f),
                Dims = new(48f, 0f, 57f),
                ScanningSpots = new()
                {
                    (new(-79.93199f, 80.00002f, 295.2f), 0.3f, "15m前排雷"),
                    (new(-91.4f, 80.00002f, 278.5f), 0.3f, "15m门+雷"),
                    (new(-68.58f, 80.00002f, 278.5f), 0.3f, "15m门+雷"),
                }
            }
        },
        {
            AreaTag.LightningRoom1,
            new(){
                Tag = AreaTag.LightningRoom1,
                Name = "雷",
                Origin = new(-40f, 80f, 254f),
                Dims = new(48f, 0f, 57f),
                ScanningSpots = new()
                {
                    (new(-64.66f, 80.00002f, 278.71f), 1f, "雷房左侧门"),
                    (new(-16.1f, 79.999954f, 295.2f), 0.3f, "15m脸雷"),
                    (new(-27.4f, 79.999954f, 278.5f), 0.3f, "15m门+雷"),
                    (new(-4.58f, 79.999954f, 278.5f), 0.3f, "15m门+雷"),
                }
            }
        },
        {
            AreaTag.FireRoom1,
            new(){
                Tag = AreaTag.FireRoom1,
                Name = "火",
                Origin = new(24f, 80f, 254f),
                Dims = new(48f, 0f, 57f),
                ScanningSpots = new()
                {
                    (new(48.00245f, 80.0f, 300.8596f), 0.5f, "36m门"),
                }
            }
        },
        {
            AreaTag.WaterRoom1,
            new(){
                Tag = AreaTag.WaterRoom1,
                Name = "水",
                Origin = new(-104f, 80f, 317f),
                Dims = new(48f, 0f, 57f),
                ScanningSpots = new()
                {
                    (new(-79.93199f, 80.00000f, 332.8f), 0.3f, "15m前排雷"),
                    (new(-91.4f, 80.00000f, 349.5f), 0.3f, "15m门+雷"),
                    (new(-68.58f, 80.00000f, 349.5f), 0.3f, "15m门+雷"),
                }
            }
        },
        {
            AreaTag.WindRoom1, 
            new(){
                Tag = AreaTag.WindRoom1,
                Name = "风",
                Origin = new(-40f, 80f, 317f),
                Dims = new(48f, 0f, 57f),
                ScanningSpots = new()
                {
                    (new(-64.66f, 80.00000f, 349.2432f), 1.2f, "风房右侧门"),
                    (new(-16.0f, 79.999954f, 343.93f), 0.3f, "中心雷"),
                    (new(-27.4f, 79.999954f, 349.5f), 0.3f, "15m门+侧雷"),
                    (new(-4.6f, 79.999954f, 349.5f), 0.3f, "15m门+侧雷"),

                }
            }
        },
        {
            AreaTag.EarthRoom1, 
            new(){
                Tag = AreaTag.EarthRoom1,
                Name = "土",
                Origin = new(24f, 80f, 317f),
                Dims = new(48f, 0f, 57f),
                ScanningSpots = new()
                {
                    (new(47.90245f, 80f, 327.5596f), 0.5f, "36m门"),
                }
            }
        },
        //{
        //    AreaTag.RoomGroup2,
        //    new(){
        //        Tag = AreaTag.RoomGroup2,
        //        Name = "二查",
        //        Origin = new(-253f, 48f, 397f),
        //        Dims = new(186f, 0f, 122f),
        //    }
        //},
        {
            AreaTag.IceRoom2,
            new(){
                Tag = AreaTag.IceRoom2,
                Name = "冰",
                Origin = new(-248f, 48f, 398f),
                Dims = new(48f, 0f, 50f),
                ScanningSpots = new()
                {
                    (new(-224.042f, 48.00005f, 443.53f), 0.5f, "15m脸雷"),
                }
            }
        },
        {
            AreaTag.LightningRoom2, 
            new(){
                Tag = AreaTag.LightningRoom2,
                Name = "雷",
                Origin = new(-184f, 48f, 398f),
                Dims = new(48f, 0f, 50f),
                ScanningSpots = new()
                {
                    (new(-159.932f, 47.999805f, 443.4233f), 0.5f, "15m脸雷"),
                }
            }
        },
        { 
            AreaTag.FireRoom2, 
            new(){
                Tag = AreaTag.FireRoom2,
                Name = "火",
                Origin = new(-120f, 48f, 398f),
                Dims = new(48f, 0f, 50f),
                ScanningSpots = new()
                {
                    (new(-95.90195f, 48.00012f, 443.5333f), 0.5f, "15m脸雷"),
                }
            }
        },
        {
            AreaTag.WaterRoom2, 
            new(){
                Tag = AreaTag.WaterRoom2,
                Name = "水",
                Origin = new(-248f, 48f, 468f),
                Dims = new(48f, 0f, 50f),
                ScanningSpots = new()
                {
                    (new(-224.142f, 47.999947f, 472.3229f), 0.5f, "15m脸雷"),
                }
            }
        },
        {
            AreaTag.WindRoom2, 
            new(){
                Tag = AreaTag.WindRoom2,
                Name = "风",
                Origin = new(-184f, 48f, 468f),
                Dims = new(48f, 0f, 50f),
                ScanningSpots = new()
                {
                    (new(-160.032f, 47.99987f, 472.3229f), 0.5f, "15m脸雷"),
                }
            }
        },
        {
            AreaTag.EarthRoom2, 
            new(){
                Tag = AreaTag.EarthRoom2,
                Name = "土",
                Origin = new(-120f, 48f, 468f),
                Dims = new(48f, 0f, 50f),
                ScanningSpots = new()
                {
                    (new(-96.04198f, 48.000122f, 472.3229f), 0.5f, "15m脸雷"),
                }
            }
        },
    };
}
