using System;
using System.Collections.Generic;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;
using static EHR.Options;

namespace EHR.Roles;

internal class Combimpostor : RoleBase
{
    private const int Id = 643520;

    private static readonly string[] SwitchModes =
    [
        "CombimpostorSwitchMode.Pet",
        "CombimpostorSwitchMode.AfterMeeting"
    ];

    private static readonly string[] BaseRoleModes =
    [
        "CombimpostorBaseRoles.AllThree",
        "CombimpostorBaseRoles.NoShapeshifter",
        "CombimpostorBaseRoles.NoPhantom",
        "CombimpostorBaseRoles.NoViper"
    ];

    private static readonly RoleTypes[] BaseRoleCycle =
    [
        RoleTypes.Shapeshifter,
        RoleTypes.Phantom,
        RoleTypes.Viper
    ];

    private static OptionItem SwitchMode;
    private static OptionItem BaseRoleMode;
    private static OptionItem ShapeshifterCooldown;
    private static OptionItem ShapeshifterDuration;
    private static OptionItem PhantomCooldown;
    private static OptionItem PhantomDuration;
    private static OptionItem ViperDissolveTime;

    private byte playerId;
    public static bool On;
    public override bool IsEnable => On;
    public static bool UsesPetSwitch => SwitchMode != null && SwitchMode.GetValue() == 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Combimpostor);
        OptionItem parent = CustomRoleSpawnChances[CustomRoles.Combimpostor];

        SwitchMode = new StringOptionItem(Id + 10, "CombimpostorSwitchMode", SwitchModes, 0, TabGroup.ImpostorRoles)
            .SetParent(parent);
        BaseRoleMode = new StringOptionItem(Id + 11, "CombimpostorBaseRoles", BaseRoleModes, 0, TabGroup.ImpostorRoles)
            .SetParent(parent);
        ShapeshifterCooldown = new FloatOptionItem(Id + 12, "CombimpostorShapeshifterCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshifterDuration = new FloatOptionItem(Id + 13, "CombimpostorShapeshifterDuration", new(1f, 60f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);
        PhantomCooldown = new FloatOptionItem(Id + 14, "CombimpostorPhantomCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);
        PhantomDuration = new FloatOptionItem(Id + 15, "CombimpostorPhantomDuration", new(1f, 60f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);
        ViperDissolveTime = new FloatOptionItem(Id + 16, "CombimpostorViperDissolveTime", new(0f, 60f, 0.5f), 10f, TabGroup.ImpostorRoles)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        CombimpostorRoleTimerPatch.ClearSnapshots();
    }

    public override void Add(byte id)
    {
        On = true;
        playerId = id;

        if (!UsesPetSwitch || !AmongUsClient.Instance.AmHost)
            return;

        foreach (PlayerControl player in Main.CachedAllPlayerControls())
        {
            if (player && string.IsNullOrEmpty(player.CurrentOutfit.PetId))
                PetsHelper.SetPet(player, PetsHelper.GetPetId());
        }
    }

    public override void OnPet(PlayerControl pc)
    {
        if (UsesPetSwitch)
            SwitchToNextBaseRole(pc);
    }

    public override void AfterMeetingTasks()
    {
        if (!UsesPetSwitch && AmongUsClient.Instance.AmHost)
            SwitchToNextBaseRole(playerId.GetPlayer());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        PlayerControl player = id.GetPlayer();
        if (!player)
            return;

        switch (player.GetRoleTypes())
        {
            case RoleTypes.Shapeshifter:
                AURoleOptions.ShapeshifterCooldown = ShapeshifterCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = ShapeshifterDuration.GetFloat();
                break;
            case RoleTypes.Phantom:
                AURoleOptions.PhantomCooldown = PhantomCooldown.GetFloat();
                AURoleOptions.PhantomDuration = PhantomDuration.GetFloat();
                break;
            case RoleTypes.Viper:
                AURoleOptions.ViperDissolveTime = ViperDissolveTime.GetFloat();
                break;
        }
    }

    public static bool IsAbilityBaseRole(RoleTypes roleType)
    {
        return roleType is RoleTypes.Shapeshifter or RoleTypes.Phantom or RoleTypes.Viper;
    }

    public static bool UsesPetSwitchFor(PlayerControl pc)
    {
        return pc && pc.GetCustomRole() == CustomRoles.Combimpostor && UsesPetSwitch;
    }

    public static float GetCooldown(RoleTypes roleType)
    {
        return roleType switch
        {
            RoleTypes.Shapeshifter => ShapeshifterCooldown.GetFloat(),
            RoleTypes.Phantom => PhantomCooldown.GetFloat(),
            _ => 0f
        };
    }

    public static float GetDuration(RoleTypes roleType)
    {
        return roleType switch
        {
            RoleTypes.Shapeshifter => ShapeshifterDuration.GetFloat(),
            RoleTypes.Phantom => PhantomDuration.GetFloat(),
            _ => 0f
        };
    }

    private static List<RoleTypes> GetEnabledBaseRoles()
    {
        List<RoleTypes> roles = [.. BaseRoleCycle];
        switch (BaseRoleMode.GetValue())
        {
            case 1:
                roles.Remove(RoleTypes.Shapeshifter);
                break;
            case 2:
                roles.Remove(RoleTypes.Phantom);
                break;
            case 3:
                roles.Remove(RoleTypes.Viper);
                break;
        }

        return roles;
    }

    private static void SwitchToNextBaseRole(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost || !pc || !pc.IsAlive() || !GameStates.IsInTask)
            return;

        List<RoleTypes> enabledRoles = GetEnabledBaseRoles();
        RoleTypes currentRole = pc.Data.Role.Role;
        int currentIndex = enabledRoles.IndexOf(currentRole);
        RoleTypes nextRole = enabledRoles[(currentIndex + 1) % enabledRoles.Count];

        CombimpostorRoleTimerPatch.SaveTimerBeforeSwitch(pc);
        pc.RpcSetRoleGlobal(nextRole, setRoleMap: true);
        LateTask.New(() =>
        {
            if (pc)
            {
                CombimpostorRoleTimerPatch.RestoreTimerAfterSwitch(pc, nextRole);
                pc.MarkDirtySettings();
            }
        }, 0.2f, log: false);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
internal static class CombimpostorRoleTimerPatch
{
    private readonly record struct TimerState(float Cooldown, float Duration);

    private static readonly Dictionary<(byte PlayerId, RoleTypes RoleType), TimerState> SavedTimers = [];
    private static readonly FieldInfo ShapeshifterCooldownField = GetField(typeof(ShapeshifterRole), "cooldownSecondsRemaining");
    private static readonly FieldInfo ShapeshifterDurationField = GetField(typeof(ShapeshifterRole), "durationSecondsRemaining");
    private static readonly FieldInfo PhantomCooldownField = GetField(typeof(PhantomRole), "cooldownSecondsRemaining");
    private static readonly FieldInfo PhantomDurationField = GetField(typeof(PhantomRole), "durationSecondsRemaining");

    private struct SwitchState
    {
        public bool IsCombimpostor;
        public RoleTypes PreviousRole;
        public TimerState PreviousTimer;
    }

    private static FieldInfo GetField(System.Type type, string name)
    {
        return AccessTools.Field(type, name)
               ?? throw new MissingFieldException(type.FullName, name);
    }

    public static void ClearSnapshots() => SavedTimers.Clear();

    private static void Prefix(PlayerControl __instance, RoleTypes roleType, out SwitchState __state)
    {
        __state = default;
        if (!__instance || __instance.GetCustomRole() != CustomRoles.Combimpostor)
            return;

        __state.IsCombimpostor = true;
        __state.PreviousRole = __instance.Data.Role.Role;
        __state.PreviousTimer = ReadTimer(__instance.Data.Role, __state.PreviousRole);
    }

    private static void Postfix(PlayerControl __instance, RoleTypes roleType, SwitchState __state)
    {
        if (!__state.IsCombimpostor || !__instance)
            return;

        if (Combimpostor.IsAbilityBaseRole(__state.PreviousRole))
            SavedTimers[(__instance.PlayerId, __state.PreviousRole)] = __state.PreviousTimer;

        RestoreTimerAfterSwitch(__instance, roleType);
    }

    public static void SaveTimerBeforeSwitch(PlayerControl player)
    {
        if (!player || player.GetCustomRole() != CustomRoles.Combimpostor)
            return;

        RoleTypes roleType = player.Data.Role.Role;
        if (Combimpostor.IsAbilityBaseRole(roleType))
            SavedTimers[(player.PlayerId, roleType)] = ReadTimer(player.Data.Role, roleType);
    }

    public static void RestoreTimerAfterSwitch(PlayerControl player, RoleTypes roleType)
    {
        if (!player || player.GetCustomRole() != CustomRoles.Combimpostor || !Combimpostor.IsAbilityBaseRole(roleType))
            return;

        TimerState timer = SavedTimers.TryGetValue((player.PlayerId, roleType), out TimerState saved)
            ? saved
            : new TimerState(Combimpostor.GetCooldown(roleType), Combimpostor.GetDuration(roleType));
        WriteTimer(player.Data.Role, roleType, timer);
    }

    private static TimerState ReadTimer(RoleBehaviour role, RoleTypes roleType)
    {
        return roleType switch
        {
            RoleTypes.Shapeshifter => new TimerState(
                (float)ShapeshifterCooldownField.GetValue(role),
                (float)ShapeshifterDurationField.GetValue(role)),
            RoleTypes.Phantom => new TimerState(
                (float)PhantomCooldownField.GetValue(role),
                (float)PhantomDurationField.GetValue(role)),
            _ => default
        };
    }

    private static void WriteTimer(RoleBehaviour role, RoleTypes roleType, TimerState timer)
    {
        switch (roleType)
        {
            case RoleTypes.Shapeshifter:
                ShapeshifterCooldownField.SetValue(role, timer.Cooldown);
                ShapeshifterDurationField.SetValue(role, timer.Duration);
                break;
            case RoleTypes.Phantom:
                PhantomCooldownField.SetValue(role, timer.Cooldown);
                PhantomDurationField.SetValue(role, timer.Duration);
                break;
        }
    }
}
