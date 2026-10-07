using System;
using System.Collections;
using AmongUs.Data;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpcImmediately))]
static class StartRpcImmediatelyPatch
{
    public static void Postfix(uint targetNetId, byte callId, SendOption option, int targetClientId = -1)
    {
        if (callId is 21 or 44 or 45 or 104) return;
        Logger.Info($"Starting RPC: {callId} ({RPC.GetRpcName(callId)}) as {Main.CachedAllPlayerControls().Find(x => x.NetId == targetNetId)?.GetRealName() ?? targetNetId.ToString()} with SendOption {option} to {Utils.GetClientById(targetClientId)?.Character?.GetRealName() ?? targetClientId.ToString()}", "StartRpcImmediately");
    }
}

[HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Start))]
internal static class SplashLogoAnimatorPatch
{
    public static SceneChanger SceneChanger;
    
    public static void Postfix(SplashManager __instance)
    {
        SceneChanger = __instance.sceneChanger;
        Main.Instance.StartCoroutine(Coroutine());
        return;

        IEnumerator Coroutine()
        {
            while (__instance && SceneChanger && SceneChanger.loadOp == null) yield return null;
                
            if (SceneChanger) SceneChanger.AllowFinishLoadingScene();
            if (__instance) __instance.startedSceneLoad = true;

            while (!ModManager.InstanceExists) yield return null;
            
            ModManager.Instance.ShowModStamp();
        }
    }
}

[HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
internal static class RunLoginPatch
{
    public const int ClickCount = 0;

    public static void Prefix(ref bool canOnline)
    {
        if (DebugModeManager.AmDebugger) canOnline = true;
        
        if (!Main.AckdConsentPopup.Value)
        {
            canOnline = true;
        }

    }
}

[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
internal static class BanMenuSetVisiblePatch
{
    public static bool Prefix(BanMenu __instance, bool show)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        show &= PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data != null;
        __instance.BanButton.gameObject.SetActive(AmongUsClient.Instance.CanBan());
        __instance.KickButton.gameObject.SetActive(AmongUsClient.Instance.CanKick());
        __instance.MenuButton.gameObject.SetActive(show);
        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CanBan))]
internal static class InnerNetClientCanBanPatch
{
    public static bool Prefix(InnerNetClient __instance, ref bool __result)
    {
        __result = __instance.AmHost;
        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
internal static class KickPlayerPatch
{
    public static bool Prefix( /*InnerNetClient __instance,*/ int clientId, bool ban)
    {
        if (!AmongUsClient.Instance.AmHost && !OnGameJoinedPatch.JoiningGame) return true;

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            Logger.SendInGame($"Game Attempting to {(ban ? "Ban" : "Kick")} Host, Blocked the attempt.", Color.red);
            return false;
        }

        if (ban) BanManager.AddBanPlayer(AmongUsClient.Instance.GetRecentClient(clientId));

        return true;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
internal static class InnerNetObjectSerializePatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost || GameOptionsSender.ActiveCoroutine != null) return;
        GameOptionsSender.ActiveCoroutine = Main.Instance.StartCoroutine(GameOptionsSender.SendDirtyGameOptionsContinuously());
    }
}

// https://github.com/Rabek009/MoreGamemodes/blob/master/Patches/ClientPatch.cs
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CheckOnlinePermissions))]
static class CheckOnlinePermissionsPatch
{
    public static void Prefix()
    {
        DataManager.Player.Ban.banPoints = 0f;
    }
}