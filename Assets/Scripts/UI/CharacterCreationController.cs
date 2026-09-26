using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[Serializable]
public class CharacterColourSlot
{
    public Image uiPreview;
    public SpriteRenderer spritePreview;
    public Button triggerButton;
    public Image buttonImage;

    [NonSerialized] public Color selectedColor = Color.white;
    private Color _buttonBaseColor;

    public void Initialize()
    {
        if (spritePreview != null)
            selectedColor = spritePreview.color;
        else if (uiPreview != null)
            selectedColor = uiPreview.color;

        if (buttonImage != null)
            _buttonBaseColor = buttonImage.color;
    }

    public void ApplyColour(Color colour)
    {
        selectedColor = colour;
        if (uiPreview != null)
            uiPreview.color = colour;
        if (spritePreview != null)
            spritePreview.color = colour;
    }

    public void SetHighlighted(bool highlighted, Color highlightColour)
    {
        if (triggerButton != null && triggerButton.TryGetComponent<SelectionOutline>(out var outline))
            outline.SetChosen(highlighted, highlightColour);

        if (buttonImage != null)
            buttonImage.color = highlighted ? highlightColour : _buttonBaseColor;
    }
}

public class CharacterCreationController : MonoBehaviour
{
    private const string DefaultName = "Player";

    [SerializeField] private TMP_Text nameDisplay;
    [SerializeField] private OnScreenKeyboardController keyboard;
    [SerializeField] private ColourSwatchPicker colourPicker;
    [SerializeField] private GameObject defaultSelected;

    [SerializeField] private CharacterColourSlot hairSlot;
    [SerializeField] private CharacterColourSlot skinSlot;
    [SerializeField] private CharacterColourSlot eyeSlot;
    [SerializeField] private CharacterColourSlot clothesTopSlot;
    [SerializeField] private CharacterColourSlot clothesBottomSlot;
    [SerializeField] private CharacterColourSlot shoesSlot;

    private CharacterColourSlot _activeSlot;
    private string _characterName = DefaultName;

    private void Start()
    {
        if (defaultSelected != null)
            EventSystem.current.SetSelectedGameObject(defaultSelected);

        colourPicker.OnClosed += () => SetActiveSlot(null);
        nameDisplay.text = _characterName;

        foreach (var slot in new[] { hairSlot, skinSlot, eyeSlot, clothesTopSlot, clothesBottomSlot, shoesSlot })
            slot.Initialize();
    }

    public void OnEditNamePressed()
    {
        keyboard.Open(_characterName, name =>
        {
            _characterName = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
            nameDisplay.text = _characterName;
        });
    }

    public void OnChooseHairColourPressed() => OpenPicker(hairSlot);
    public void OnChooseSkinColourPressed() => OpenPicker(skinSlot);
    public void OnChooseEyeColourPressed() => OpenPicker(eyeSlot);
    public void OnChooseClothesTopColourPressed() => OpenPicker(clothesTopSlot);
    public void OnChooseClothesBottomColourPressed() => OpenPicker(clothesBottomSlot);
    public void OnChooseShoesColourPressed() => OpenPicker(shoesSlot);

    private void OpenPicker(CharacterColourSlot slot)
    {
        SetActiveSlot(slot);
        colourPicker.Open(slot.ApplyColour);
    }

    private void SetActiveSlot(CharacterColourSlot slot)
    {
        var highlight = UIManager.Instance.SelectionActiveCategoryColor;

        _activeSlot?.SetHighlighted(false, highlight);
        _activeSlot = slot;
        _activeSlot?.SetHighlighted(true, highlight);
    }

    public void OnConfirmPressed()
    {
        ConfirmationDialogueController.Instance.Show(
            "You are about to create this character. Are you sure?",
            OnCharacterConfirmed);
    }

    private void OnCharacterConfirmed()
    {
        var character = new CharacterData
        {
            characterName = _characterName,
            appearanceId = 0,
            hairColor = hairSlot.selectedColor,
            skinColor = skinSlot.selectedColor,
            eyeColor = eyeSlot.selectedColor,
            clothesTopColor = clothesTopSlot.selectedColor,
            clothesBottomColor = clothesBottomSlot.selectedColor,
            shoesColor = shoesSlot.selectedColor
        };

        GameManager.Instance.CompleteCharacterCreation(character);
    }

    public void OnBackToTitlePressed()
    {
        GameManager.Instance.ReturnToTitle();
    }
}