using UnityEngine;

public class VineTrap : MonoBehaviour
{
    [Header("Debuff")]
    public DebuffType debuffType = DebuffType.Slow;
    public float debuffAmount = 0.75f;
    public float debuffDuration = 3f;

    [Header("Trap Settings")]
    public bool disappearAfterTrigger = false;

    private void OnTriggerEnter(Collider other)
    {
        PlayerDebuffReceiver receiver = other.GetComponentInParent<PlayerDebuffReceiver>();

        if (receiver == null)
            return;

        receiver.ApplyDebuff(debuffType, debuffAmount, debuffDuration);

        if (disappearAfterTrigger)
            gameObject.SetActive(false);
    }
}