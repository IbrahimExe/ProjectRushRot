using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetScene = "ProceduralLoading";

    [Header("Skin Configurations by Column")]
    [SerializeField] private PlayerCharacterData[] skateboardSkins;
    [SerializeField] private PlayerCharacterData[] trolleySkins;
    [SerializeField] private PlayerCharacterData[] cheeseWheelSkins;

    [Header("Column Containers (Parent Transforms)")]
    [SerializeField] private Transform skateboardContainer;
    [SerializeField] private Transform trolleyContainer;
    [SerializeField] private Transform cheeseWheelContainer;

    [Header("UI Prefabs & Cards")]
    [SerializeField] private GameObject skinCardPrefab;
    [SerializeField] private SkinSelectCardUI[] manuallyAssignedCards;

    [Header("Header & Currency Display")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Button addCoinsDevButton;

    [Header("Selection & Preview Panel")]
    [SerializeField] private TMP_Text selectedSkinNameText;
    [SerializeField] private TMP_Text selectedSkinStatsText;
    [SerializeField] private Image selectedSkinPreviewImage;
    [SerializeField] private Button startButton;

    [Header("Legacy Support")]
    [SerializeField] private CharacterSelectButton[] characterButtons;

    private List<SkinSelectCardUI> allSpawnedCards = new List<SkinSelectCardUI>();
    private PlayerCharacterData currentlySelectedData;

    private void Start()
    {
        InitializeInventoryManager();
        SetupCoinsUI();
        BuildColumns();

        if (characterButtons != null)
        {
            foreach (CharacterSelectButton btn in characterButtons)
            {
                if (btn != null)
                    btn.OnCharacterSelected += OnCharacterSelected;
            }
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGameWithSelectedCharacter);
        }

        if (addCoinsDevButton != null)
        {
            addCoinsDevButton.onClick.AddListener(() => AddDevCoins(500));
        }

        // Auto-select first available unlocked skin
        SelectDefaultOrFirstSkin();
    }

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += UpdateCoinsUI;
        }
        UpdateCoinsUI();
        RefreshAllCards();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= UpdateCoinsUI;
        }
    }

    private void InitializeInventoryManager()
    {
        if (InventoryManager.Instance == null)
        {
            GameObject obj = new GameObject("InventoryManager");
            obj.AddComponent<InventoryManager>();
        }
    }

    public void SetTargetScene(string sceneName)
    {
        targetScene = sceneName;
    }

    public void AddDevCoins(int amount)
    {
        if (InventoryManager.Instance != null)
        {
            int current = InventoryManager.Instance.GetItemCount("Gold");
            InventoryManager.Instance.SetItemCount("Gold", current + amount);
        }
    }

    private void SetupCoinsUI()
    {
        UpdateCoinsUI();
    }

    public void UpdateCoinsUI()
    {
        int coins = 0;
        if (InventoryManager.Instance != null)
        {
            coins = InventoryManager.Instance.GetItemCount("Gold");
        }

        if (coinsText != null)
        {
            coinsText.text = $"💰 Coins: {coins:N0}";
        }
    }

    private void BuildColumns()
    {
        allSpawnedCards.Clear();

        // 1. Build Skateboard column
        BuildColumnCards(skateboardSkins, skateboardContainer);

        // 2. Build Trolley column
        BuildColumnCards(trolleySkins, trolleyContainer);

        // 3. Build Cheese Wheel column
        BuildColumnCards(cheeseWheelSkins, cheeseWheelContainer);

        // 4. Manually assigned cards (if any in inspector)
        if (manuallyAssignedCards != null)
        {
            foreach (var card in manuallyAssignedCards)
            {
                if (card != null)
                {
                    card.Setup(card.CharacterData, OnCardSelected);
                    allSpawnedCards.Add(card);
                }
            }
        }
    }

    private void BuildColumnCards(PlayerCharacterData[] skinArray, Transform container)
    {
        if (skinArray == null || container == null || skinCardPrefab == null) return;

        foreach (var skinData in skinArray)
        {
            if (skinData == null) continue;

            GameObject cardObj = Instantiate(skinCardPrefab, container);
            SkinSelectCardUI cardUI = cardObj.GetComponent<SkinSelectCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(skinData, OnCardSelected);
                allSpawnedCards.Add(cardUI);
            }
        }
    }

    private void OnCardSelected(SkinSelectCardUI clickedCard)
    {
        if (clickedCard == null || clickedCard.CharacterData == null) return;

        currentlySelectedData = clickedCard.CharacterData;

        // Persist selection
        if (CharacterDataPersistence.Instance != null)
        {
            CharacterDataPersistence.Instance.SetSelectedCharacter(currentlySelectedData);
        }

        RefreshAllCards();
        UpdatePreviewPanel(currentlySelectedData);
    }

    private void RefreshAllCards()
    {
        foreach (var card in allSpawnedCards)
        {
            if (card != null)
            {
                bool isThisSelected = (card.CharacterData == currentlySelectedData);
                card.RefreshState(isThisSelected);
            }
        }
    }

    private void SelectDefaultOrFirstSkin()
    {
        if (allSpawnedCards.Count > 0)
        {
            // Pick first unlocked card or first card
            SkinSelectCardUI target = allSpawnedCards.Find(c => c.IsUnlocked) ?? allSpawnedCards[0];
            OnCardSelected(target);
        }
    }

    private void UpdatePreviewPanel(PlayerCharacterData data)
    {
        if (data == null) return;

        if (selectedSkinNameText != null)
            selectedSkinNameText.text = !string.IsNullOrEmpty(data.skinName) ? data.skinName : data.name;

        if (selectedSkinPreviewImage != null && data.skinIcon != null)
        {
            selectedSkinPreviewImage.sprite = data.skinIcon;
            selectedSkinPreviewImage.gameObject.SetActive(true);
        }

        if (selectedSkinStatsText != null)
        {
            selectedSkinStatsText.text = $"<b>Speed:</b> {data.maxMoveSpeed}\n" +
                                         $"<b>Accel:</b> {data.acceleration}\n" +
                                         $"<b>Jump Force:</b> {data.jumpForce}\n" +
                                         $"<b>Jumps:</b> {data.numOfJumps}\n" +
                                         $"<b>Wall Run Speed:</b> x{data.wallRunSpeedMultiplier:F2}";
        }
    }

    public void StartGameWithSelectedCharacter()
    {
        if (currentlySelectedData == null)
            SelectDefaultOrFirstSkin();

        if (currentlySelectedData != null && CharacterDataPersistence.Instance != null)
        {
            CharacterDataPersistence.Instance.SetSelectedCharacter(currentlySelectedData);
        }

        SceneManager.LoadScene(targetScene);
    }

    private void OnCharacterSelected(PlayerCharacterData characterData)
    {
        if (characterData == null) return;

        if (CharacterDataPersistence.Instance != null)
            CharacterDataPersistence.Instance.SetSelectedCharacter(characterData);

        SceneManager.LoadScene(targetScene);
    }

    private void OnDestroy()
    {
        if (characterButtons != null)
        {
            foreach (CharacterSelectButton btn in characterButtons)
            {
                if (btn != null)
                    btn.OnCharacterSelected -= OnCharacterSelected;
            }
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGameWithSelectedCharacter);
        }
    }
}