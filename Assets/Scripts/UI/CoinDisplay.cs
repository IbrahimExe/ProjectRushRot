using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class CoinDisplay : MonoBehaviour
{
    private TMP_Text coinText;

    private void Awake()
    {
        coinText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        // Initial update
        UpdateCoinDisplay();
    }

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += UpdateCoinDisplay;
        }
        UpdateCoinDisplay();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= UpdateCoinDisplay;
        }
    }

    [Header("Formatting")]
    [Tooltip("Text to show before the number (e.g. 'Coins: ')")]
    [SerializeField] private string prefix = "Coins: ";

    private void UpdateCoinDisplay()
    {
        if (coinText != null && InventoryManager.Instance != null)
        {
            int currentCoins = InventoryManager.Instance.GetItemCount("Gold");
            coinText.text = $"{prefix}{currentCoins}";
        }
    }
}
