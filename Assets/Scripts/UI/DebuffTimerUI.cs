using UnityEngine;
using TMPro;

public class DebuffTimerUI : MonoBehaviour
{
    public PlayerDebuffReceiver receiver;

    public GameObject slowRoot;
    public TMP_Text slowText;

    public GameObject jumpRoot;
    public TMP_Text jumpText;

    public GameObject dashRoot;
    public TMP_Text dashText;

    private void Update()
    {
        if (receiver == null)
            return;

        UpdateDebuff(slowRoot, slowText, receiver.IsSlowActive, receiver.SlowTimeLeft);
        UpdateDebuff(jumpRoot, jumpText, receiver.IsJumpDebuffActive, receiver.JumpDebuffTimeLeft);
        UpdateDebuff(dashRoot, dashText, receiver.IsDashDebuffActive, receiver.DashDebuffTimeLeft);
    }

    private void UpdateDebuff(GameObject root, TMP_Text text, bool active, float timeLeft)
    {
        if (root != null)
            root.SetActive(active);

        if (text != null)
            text.text = Mathf.CeilToInt(timeLeft).ToString();
    }
}