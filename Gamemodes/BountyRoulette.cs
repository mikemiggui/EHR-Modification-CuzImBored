using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Gamemodes;

internal static class BountyRoulette
{
    private static readonly Dictionary<byte, int> WrongKillAttempts = [];
    private static readonly Dictionary<byte, int> MissedBounties = [];
    private static readonly Dictionary<byte, bool> KillMode = [];
    private static readonly Dictionary<byte, long> LastModeSwitch = [];
    private static readonly HashSet<byte> TargetGone = [];
    private static long LastFixedUpdate;
    private static (int Common, int Short, int Long)? OriginalTaskCounts;

    public static readonly Dictionary<byte, byte> Targets = [];
    public static readonly Dictionary<byte, float> Scores = [];
    public static int RoundTime;

    public static OptionItem GameTime;
    public static OptionItem KillCooldown;
    public static OptionItem TaskPoints;
    public static OptionItem TargetKillPoints;
    public static OptionItem HunterKillPoints;
    public static OptionItem MissedTargetPenalty;
    public static OptionItem RevealHunterName;
    public static OptionItem RevealHunterRoom;
    public static OptionItem CommonTasks;
    public static OptionItem ShortTasks;
    public static OptionItem LongTasks;
    public static OptionItem ModeSwitchCooldown;

    public static void SetupCustomOption()
    {
        GameTime = new IntegerOptionItem(67_224_001, "BountyRoulette_GameTime", new(30, 600, 10), 90, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);

        KillCooldown = new FloatOptionItem(67_224_002, "BountyRoulette_KillCooldown", new(1f, 60f, 1f), 10f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);

        TaskPoints = new FloatOptionItem(67_224_003, "BountyRoulette_TaskPoints", new(0f, 10f, 0.5f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        TargetKillPoints = new FloatOptionItem(67_224_004, "BountyRoulette_TargetKillPoints", new(0f, 10f, 0.5f), 2f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        HunterKillPoints = new FloatOptionItem(67_224_005, "BountyRoulette_HunterKillPoints", new(0f, 10f, 0.5f), 3f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        MissedTargetPenalty = new FloatOptionItem(67_224_008, "BountyRoulette_MissedTargetPenalty", new(0f, 10f, 0.5f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        RevealHunterName = new BooleanOptionItem(67_224_006, "BountyRoulette_RevealHunterName", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        RevealHunterRoom = new BooleanOptionItem(67_224_007, "BountyRoulette_RevealHunterRoom", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        CommonTasks = new IntegerOptionItem(67_224_009, "BountyRoulette_CommonTasks", new(0, 3, 1), 1, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        ShortTasks = new IntegerOptionItem(67_224_010, "BountyRoulette_ShortTasks", new(0, 10, 1), 3, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        LongTasks = new IntegerOptionItem(67_224_011, "BountyRoulette_LongTasks", new(0, 10, 1), 1, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));

        ModeSwitchCooldown = new IntegerOptionItem(67_224_012, "BountyRoulette_ModeSwitchCooldown", new(0, 30, 1), 3, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.BountyRoulette)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void ApplyTaskSettings()
    {
        if (Options.CurrentGameMode != CustomGameMode.BountyRoulette || Main.NormalOptions == null) return;

        OriginalTaskCounts ??= (Main.NormalOptions.NumCommonTasks, Main.NormalOptions.NumShortTasks, Main.NormalOptions.NumLongTasks);
        Main.NormalOptions.NumCommonTasks = CommonTasks.GetInt();
        Main.NormalOptions.NumShortTasks = ShortTasks.GetInt();
        Main.NormalOptions.NumLongTasks = LongTasks.GetInt();
    }

    public static void RestoreTaskSettings()
    {
        if (OriginalTaskCounts is not { } original || Main.NormalOptions == null) return;

        Main.NormalOptions.NumCommonTasks = original.Common;
        Main.NormalOptions.NumShortTasks = original.Short;
        Main.NormalOptions.NumLongTasks = original.Long;
        OriginalTaskCounts = null;
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.BountyRoulette) return;

        Targets.Clear();
        Scores.Clear();
        WrongKillAttempts.Clear();
        MissedBounties.Clear();
        KillMode.Clear();
        LastModeSwitch.Clear();
        TargetGone.Clear();
        LastFixedUpdate = 0;

        foreach (PlayerControl player in Main.EnumeratePlayerControls()
                     .Where(x => !x.Is(CustomRoles.GM) && !ChatCommands.Spectators.Contains(x.PlayerId)))
        {
            Scores[player.PlayerId] = 0;
            KillMode[player.PlayerId] = false;
            if (AmongUsClient.Instance.AmHost)
            {
                SyncScore(player.PlayerId);
                SyncKillMode(player.PlayerId, false);
                PetsHelper.SetPet(player, PetsHelper.GetPetId());
                player.RpcSetRoleGlobal(RoleTypes.Crewmate);
            }
        }

        if (AmongUsClient.Instance.AmHost)
        {
            StartRound(GameTime.GetInt());
        }
        else
            RoundTime = Math.Max(0, GameTime.GetInt());
    }

    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost || !IsKillMode(killer.PlayerId) || !killer.IsAlive() || !target.IsAlive() || target.inVent) return;

        byte killerId = killer.PlayerId;
        byte targetId = target.PlayerId;
        bool ownTarget = Targets.TryGetValue(killerId, out byte assignedTarget) && assignedTarget == targetId;
        bool counterBounty = Targets.TryGetValue(targetId, out byte counterTarget) && counterTarget == killerId;

        if (!ownTarget && !counterBounty)
        {
            int attempts = WrongKillAttempts.GetValueOrDefault(killerId) + 1;
            WrongKillAttempts[killerId] = attempts;
            if (attempts == 1)
            {
                killer.SetKillCooldown(KillCooldown.GetFloat());
                killer.Notify(GetString("BountyRoulette.WrongTarget"));
                return;
            }

            killer.Notify(GetString("BountyRoulette.WrongTargetSuicide"));
            killer.Kill(killer);
            ClearUnavailableTargets();
            return;
        }

        if (ownTarget)
        {
            AddScore(killerId, TargetKillPoints.GetFloat());
        }

        if (counterBounty)
            AddScore(killerId, HunterKillPoints.GetFloat());

        killer.SetKillCooldown(KillCooldown.GetFloat());
        killer.Kill(target);
        ClearUnavailableTargets();
        if (Main.CachedAlivePlayerControls().Count > 1 && AllLivingPlayersAreTargetless())
            StartRound(GameTime.GetInt());
    }

    public static void OnTaskCompleted(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !player.IsAlive()) return;
        if (!Scores.ContainsKey(player.PlayerId)) return;
        if (IsKillMode(player.PlayerId)) return;

        float points = TaskPoints.GetFloat();
        AddScore(player.PlayerId, points);
        player.Notify(string.Format(GetString("BountyRoulette.TaskPoint"), points));
    }

    public static string GetHudText(byte playerId)
    {
        string modeText = GetString(IsKillMode(playerId) ? "BountyRoulette.ModeKill" : "BountyRoulette.ModeTasks");
        PlayerControl player = Utils.GetPlayerById(playerId);
        string statusText = !player || !player.IsAlive()
            ? GetString("BountyRoulette.HudDead")
            : TargetGone.Contains(playerId)
                ? GetString("BountyRoulette.HudTargetGone")
                : GetString("BountyRoulette.HudNoTarget");
        if (player && player.IsAlive() && Targets.TryGetValue(playerId, out byte target) && target != byte.MaxValue)
            statusText = string.Format(GetString("BountyRoulette.HudTarget"), Utils.GetPlayerById(target)?.GetRealName() ?? GetString("Unknown"));

        bool hasActiveTarget = Targets.TryGetValue(playerId, out byte targetId) && targetId != byte.MaxValue;

        PlayerControl hunter = Targets
            .Where(entry => entry.Value == playerId)
            .Select(entry => Utils.GetPlayerById(entry.Key))
            .FirstOrDefault(player => player && player.IsAlive() && !player.Data.Disconnected);
        string hunterText = string.Empty;
        if (player && player.IsAlive() && !hasActiveTarget && hunter)
        {
            string hunterName = RevealHunterName.GetBool() ? hunter.GetRealName() : string.Empty;
            string hunterRoom = RevealHunterRoom.GetBool() ? hunter.GetPositionInfo().RoomName : string.Empty;
            if (hunterName.Length > 0 && hunterRoom.Length > 0)
                hunterText = string.Format(GetString("BountyRoulette.HudHunter"), hunterName, hunterRoom);
            else if (hunterName.Length > 0)
                hunterText = string.Format(GetString("BountyRoulette.HudHunterName"), hunterName);
            else if (hunterRoom.Length > 0)
                hunterText = string.Format(GetString("BountyRoulette.HudHunterRoom"), hunterRoom);
        }

        return $"{modeText}\n{RoundTime / 60:00}:{RoundTime % 60:00} | {string.Format(GetString("BountyRoulette.HudScore"), Scores.GetValueOrDefault(playerId, 0f))}\n{statusText}{(hunterText.Length > 0 ? $"\n{hunterText}" : string.Empty)}";
    }

    public static bool IsKillMode(byte playerId) => KillMode.GetValueOrDefault(playerId, true);

    public static void ToggleKillMode(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !player || !player.IsAlive()) return;

        long now = Utils.TimeStamp;
        if (LastModeSwitch.TryGetValue(player.PlayerId, out long lastSwitch) && now - lastSwitch < ModeSwitchCooldown.GetInt())
        {
            player.Notify(string.Format(GetString("BountyRoulette.ModeSwitchCooldownActive"), ModeSwitchCooldown.GetInt() - (now - lastSwitch)));
            return;
        }

        bool killMode = !IsKillMode(player.PlayerId);
        LastModeSwitch[player.PlayerId] = now;
        KillMode[player.PlayerId] = killMode;
        SyncKillMode(player.PlayerId, killMode);
        player.RpcSetRoleGlobal(killMode ? RoleTypes.Impostor : RoleTypes.Crewmate);
        if (killMode)
            player.SetKillCooldown(KillCooldown.GetFloat());
        Utils.NotifyRoles(SpecifySeer: player);
    }

    public static void ActivateInitialKillModes()
    {
        if (!AmongUsClient.Instance.AmHost || Options.CurrentGameMode != CustomGameMode.BountyRoulette) return;

        foreach (PlayerControl player in Main.EnumerateAlivePlayerControls().Where(x => Scores.ContainsKey(x.PlayerId)))
        {
            KillMode[player.PlayerId] = true;
            SyncKillMode(player.PlayerId, true);
            player.RpcSetRoleGlobal(RoleTypes.Impostor);
            player.SetKillCooldown(KillCooldown.GetFloat());
        }
    }

    private static void SyncKillMode(byte playerId, bool killMode)
    {
        Utils.SendRPC(CustomRPC.BountyRouletteSync, 5, playerId, killMode);
    }

    public static int GetRankFromScore(byte playerId)
    {
        if (!Scores.TryGetValue(playerId, out float score)) return Scores.Count + 1;
        int rank = 1 + Scores.Values.Count(value => value > score);
        rank += Scores.Where(entry => entry.Value == score).Select(entry => entry.Key).ToList().IndexOf(playerId);
        return rank;
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Targets[reader.ReadByte()] = reader.ReadByte();
                break;
            case 2:
                Scores[reader.ReadByte()] = reader.ReadSingle();
                break;
            case 3:
                RoundTime = reader.ReadPackedInt32();
                break;
            case 5:
                KillMode[reader.ReadByte()] = reader.ReadBoolean();
                break;
            case 6:
                byte targetlessPlayerId = reader.ReadByte();
                if (reader.ReadBoolean())
                    TargetGone.Add(targetlessPlayerId);
                else
                    TargetGone.Remove(targetlessPlayerId);
                break;
        }
    }

    private static void StartRound(int seconds)
    {
        RoundTime = Math.Max(0, seconds);
        SyncRoundTime();

        List<byte> alivePlayers = Main.EnumerateAlivePlayerControls()
            .Where(x => Scores.ContainsKey(x.PlayerId))
            .Select(x => x.PlayerId)
            .ToList();

        foreach (byte playerId in Scores.Keys)
        {
            SetTarget(playerId, byte.MaxValue);
            SetTargetGone(playerId, false);
        }

        if (alivePlayers.Count < 2) return;

        List<byte> shuffled = alivePlayers.Shuffle().ToList();
        for (int index = 0; index < shuffled.Count; index++)
        {
            byte playerId = shuffled[index];
            SetTarget(playerId, shuffled[(index + 1) % shuffled.Count]);
            PlayerControl player = Utils.GetPlayerById(playerId);
            PlayerControl target = Utils.GetPlayerById(Targets[playerId]);
            if (player && target)
                player.Notify(string.Format(GetString("BountyRoulette.TargetAssigned"), target.GetRealName()));
        }
    }

    private static void EndRound()
    {
        foreach (PlayerControl player in Main.EnumerateAlivePlayerControls())
        {
            if (!Scores.ContainsKey(player.PlayerId)) continue;
            if (!Targets.TryGetValue(player.PlayerId, out byte targetId) || targetId == byte.MaxValue) continue;
            PlayerControl target = Utils.GetPlayerById(targetId);
            if (!target || !target.IsAlive()) continue;

            float penalty = MissedTargetPenalty.GetFloat();
            AddScore(player.PlayerId, -penalty);
            int misses = MissedBounties.GetValueOrDefault(player.PlayerId) + 1;
            MissedBounties[player.PlayerId] = misses;
            player.Notify(misses == 1
                ? string.Format(GetString("BountyRoulette.MissedTarget"), penalty)
                : GetString("BountyRoulette.MissedTargetSuicide"));

            if (misses >= 2)
                player.Kill(player);
        }

        if (Main.CachedAlivePlayerControls().Count <= 1) return;
        StartRound(GameTime.GetInt());
    }

    private static void ClearUnavailableTargets()
    {
        KeyValuePair<byte, byte>[] unavailableAssignments = Targets
            .Where(assignment => assignment.Value != byte.MaxValue)
            .ToArray();

        foreach (KeyValuePair<byte, byte> assignment in unavailableAssignments)
        {
            PlayerControl hunter = Utils.GetPlayerById(assignment.Key);
            PlayerControl target = Utils.GetPlayerById(assignment.Value);
            if (!hunter || !hunter.IsAlive())
            {
                SetTarget(assignment.Key, byte.MaxValue);
                continue;
            }

            if (target && target.IsAlive() && !target.Data.Disconnected) continue;

            SetTarget(assignment.Key, byte.MaxValue);
            SetTargetGone(assignment.Key, true);
        }

        foreach (byte playerId in Targets.Keys.ToArray())
        {
            PlayerControl player = Utils.GetPlayerById(playerId);
            if (!player || !player.IsAlive())
                SetTarget(playerId, byte.MaxValue);
        }
    }

    private static bool AllLivingPlayersAreTargetless()
    {
        return Main.EnumerateAlivePlayerControls()
            .Where(player => Scores.ContainsKey(player.PlayerId))
            .All(player => !Targets.TryGetValue(player.PlayerId, out byte targetId) || targetId == byte.MaxValue);
    }

    private static void SetTargetGone(byte playerId, bool targetGone)
    {
        if (targetGone)
            TargetGone.Add(playerId);
        else
            TargetGone.Remove(playerId);

        Utils.SendRPC(CustomRPC.BountyRouletteSync, 6, playerId, targetGone);
    }

    private static void SetTarget(byte playerId, byte targetId)
    {
        Targets[playerId] = targetId;
        Utils.SendRPC(CustomRPC.BountyRouletteSync, 1, playerId, targetId);
    }

    private static void AddScore(byte playerId, float amount)
    {
        Scores[playerId] = Scores.GetValueOrDefault(playerId) + amount;
        SyncScore(playerId);
    }

    private static void SyncScore(byte playerId)
    {
        Utils.SendRPC(CustomRPC.BountyRouletteSync, 2, playerId, Scores.GetValueOrDefault(playerId, 0f));
    }

    private static void SyncRoundTime()
    {
        Utils.SendRPC(CustomRPC.BountyRouletteSync, 3, RoundTime);
    }

    public static class FixedUpdatePatch
    {
        public static void Postfix()
        {
            if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance ||
                Options.CurrentGameMode != CustomGameMode.BountyRoulette || !AmongUsClient.Instance.AmHost)
                return;

            long now = Utils.TimeStamp;
            if (IntroCutsceneDestroyPatch.IntroDestroyTS + 10 > now || LastFixedUpdate == now) return;
            LastFixedUpdate = now;

            ClearUnavailableTargets();
            if (Main.CachedAlivePlayerControls().Count > 1 && AllLivingPlayersAreTargetless())
            {
                StartRound(GameTime.GetInt());
                return;
            }

            RoundTime--;
            SyncRoundTime();
            RefreshVanillaNameStatus();
            if (RoundTime <= 0)
                EndRound();
        }
    }

    private static void RefreshVanillaNameStatus()
    {
        foreach (PlayerControl player in Main.EnumeratePlayerControls().Where(x => !x.Data.Disconnected && !x.IsModdedClient()))
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player, SendOption: SendOption.None);
    }
}
