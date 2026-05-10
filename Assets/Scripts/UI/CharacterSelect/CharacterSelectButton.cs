using UnityEngine;
using UnityEngine.UI;
using System;

public class CharacterSelectButton : MonoBehaviour
{
    [SerializeField] private PlayerCharacterData characterData;
    private Button button;

    public event Action<PlayerCharacterData> OnCharacterSelected;

    private void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(SelectCharacter);
        }
    }

    public void SelectCharacter()
    {
        if (characterData != null)
        {
            OnCharacterSelected?.Invoke(characterData);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectCharacter);
        }
    }
}