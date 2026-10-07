using EHR.Gamemodes;
using EHR.Modules;

namespace EHR.Roles;

public class Sharp : RoleBase, IHideAndSeekRole
{
    public static bool On;

    private int UpdateCount;
    private byte SharpId;

    public Team Team => Team.Impostor;
    public int Chance => CustomRoles.Sharp.GetMode();
    public int Count => CustomRoles.Sharp.GetCount();
    public float RoleSpeed => Seeker.Speed.GetFloat();
    public float RoleVision => Seeker.Vision.GetFloat();

    public static string Name => "<voffset=7em><alpha=#00>.</alpha></voffset><size=150%><line-height=97%><cspace=0.16em><#0000>W</color><mark=#628d85>WWWWW</mark><#0000>WW</color>\n<mark=#586874>WW</mark><mark=#547a96>WW</mark><mark=#6894b6>WW</mark><mark=#586874>W</mark><#0000>W</color>\n<mark=#586874>WWW</mark><mark=#547a96>WW</mark><mark=#586874>WW</mark><mark=#f5ee2e>W</mark>\n<mark=#000000>W</mark><mark=#0d233f>W</mark><mark=#586874>WWW</mark><mark=#f5ee2e>W</mark><mark=#586874>W</mark><mark=#517a9a>W</mark>\n<mark=#000000>WW</mark><#0000>W</color><mark=#000000>W</mark><mark=#0d233f>W</mark><mark=#586874>W</mark><mark=#517a9a>W</mark><#0000>W\nWWW</color><mark=#000000>WW</mark><#0000>WWW";

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(69_211_208, TabGroup.ImpostorRoles, CustomRoles.Sharp, CustomGameMode.HideAndSeek);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        UpdateCount = 0;
        SharpId = playerId;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost || !pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        if (UpdateCount++ < 5) return;

        UpdateCount = 0;

        Vector2 pos = pc.Pos();

        if (FastVector2.TryGetClosestPlayerInRange(pos, 1.2f, out PlayerControl target, x =>
                x.PlayerId != pc.PlayerId &&
                CustomHnS.PlayerRoles.TryGetValue(x.PlayerId, out var targetRole) &&
                targetRole.Interface.Team != Team.Impostor))
            CustomHnS.OnCheckMurder(pc, target);
    }

    public override void AfterMeetingTasks()
    {
        var pc = SharpId.GetPlayer();
        if (!pc || !pc.IsAlive()) return;
        pc.RpcSetName(Name);
    }
}