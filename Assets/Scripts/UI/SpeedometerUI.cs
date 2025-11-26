using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class SpeedometerUI : MonoBehaviour
{
    [Header("Player Source")]
    public GameObject player; // scene player (drag here)
    [Tooltip("If you leave empty, the script will try to use Rigidbody, then fallback to a 'currentMoveSpeed' field on a component.")]
    public float maxSpeed = 80f; // used to normalize needle

    [Header("UI Elements")]
    public Image needleImage;    // assign the needle (pivot should be at base)
    public Image fillImage;      // optional radial fill image (Image.Type = Filled, Fill Method = Radial360)
    public TMP_Text speedText;

    [Header("Needle / Fill settings")]
    [Tooltip("Needle rotation angle at minimum speed (degrees)")]
    public float needleMinAngle = 105f;
    [Tooltip("Needle rotation angle at maxSpeed (degrees)")]
    public float needleMaxAngle = -105f;
    public float smoothing = 8f; // higher is snappier

    [Header("Format")]
    public string speedFormat = "F0"; // "F0" no decimals, "F1" one decimal

    private Rigidbody cachedRb;
    private Component cachedCustom;
    private float displayedValue = 0f;

    void Start()
    {
        CachePlayer();
    }

    void OnValidate()
    {
        CachePlayer();
    }

    void CachePlayer()
    {
        cachedRb = null;
        cachedCustom = null;

        if (player == null) return;

        cachedRb = player.GetComponent<Rigidbody>();

        if (cachedRb == null)
        {
            var monos = player.GetComponents<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m == null) continue;
                var t = m.GetType();
                var f = t.GetField("currentMoveSpeed");
                var p = t.GetProperty("currentMoveSpeed");
                if (f != null || p != null)
                {
                    cachedCustom = m;
                    break;
                }
            }
        }
    }

    void Update()
    {
        float speed = SamplePlayerSpeed();
        speed = Mathf.Max(0f, speed);

        displayedValue = Mathf.Lerp(displayedValue, speed, Time.deltaTime * smoothing);

        float t = (maxSpeed > 0f) ? displayedValue / maxSpeed : 0f;
        float clampedT = Mathf.Clamp01(t);

        if (needleImage != null)
        {
            float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, clampedT);
            needleImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = clampedT;
        }

        if (speedText != null)
        {
            speedText.text = displayedValue.ToString(speedFormat);
        }
    }

    private float SamplePlayerSpeed()
    {
        if (cachedRb != null)
            return cachedRb.linearVelocity.magnitude;

        if (cachedCustom != null)
        {
            var t = cachedCustom.GetType();

            var f = t.GetField(
                "currentMoveSpeed",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (f != null)
            {
                object val = f.GetValue(cachedCustom);
                if (val is float fval) return Mathf.Abs(fval);
                if (val is int ival) return Mathf.Abs(ival);
            }

            var p = t.GetProperty(
                "currentMoveSpeed",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (p != null)
            {
                object val = p.GetValue(cachedCustom);
                if (val is float fp) return Mathf.Abs(fp);
                if (val is int ip) return Mathf.Abs(ip);
            }
        }

        return 0f;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Sample")]
    private void DebugSample()
    {
        Debug.Log("Sample speed: " + SamplePlayerSpeed());
    }
#endif
}
