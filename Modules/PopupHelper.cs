using HarmonyLib;
using TMPro;
using Twitch;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal static class PopupHelper
{
    private static GenericPopup InfoPopup;

    [HarmonyPrefix]
    private static void StartPrefix()
    {
        if (InfoPopup) return;

        InfoPopup = Object.Instantiate(TwitchManager.Instance.TwitchPopup);
        InfoPopup.name = "InfoPopup";
        InfoPopup.TextAreaTMP.GetComponent<RectTransform>().sizeDelta = new(2.5f, 2f);
    }

    public static void ShowPopup(string message, StringNames buttonText, bool showButton = false, bool buttonIsExit = true)
    {
        if (!InfoPopup) return;

        InfoPopup.Show(message);
        Transform button = InfoPopup.transform.Find("ExitGame");

        if (!button) return;

        button.gameObject.SetActive(showButton);
        var textTranslatorTMP = button.GetChild(0).GetComponent<TextTranslatorTMP>();
        textTranslatorTMP.TargetText = buttonText;
        textTranslatorTMP.ResetText();
        var passiveButton = button.GetComponent<PassiveButton>();
        passiveButton.OnClick = new();

        if (buttonIsExit)
            passiveButton.OnClick.AddListener(SplashLogoAnimatorPatch.SceneChanger.ExitGame);
        else
            passiveButton.OnClick.AddListener(() => InfoPopup.Close());
    }
}
