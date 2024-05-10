using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace BAHelper.Modules.Trapper;

public class MobObject(BattleNpc obj, MobInfo? mobInfo = null)
{
    public float SightRadian = 1.5708f;
    public BattleNpc Bnpc = obj;
    public MobInfo? MobInfo = mobInfo;
    public float AggroDistance => MobInfo?.AggroDistance ?? 16f;
    public AggroType AggroType => MobInfo?.AggroType ?? AggroType.Sight;
    public Vector3 Position => Bnpc.Position;
    public float Rotation => Bnpc.Rotation;
}
