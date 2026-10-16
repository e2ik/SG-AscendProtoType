using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[Serializable]
public class CharacterColourSlot
{
    public Image uiPreview;
    public SpriteRenderer spritePreview;
    public SpriteStyleSwapper styleSwapper;
    public Button triggerButton;
    public Image buttonImage;
    public TMP_Text buttonLabel;
    public string displayName;
    [TextArea(2, 5)] public string flavourText;
    public Sprite categoryImage;
    public SwatchPalette palette = SwatchPalette.Generated;
    public bool overrideLabelColour;
    public Color labelColour = Color.white;

    public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName : buttonLabel != null ? buttonLabel.text : string.Empty;

    [NonSerialized] public Color selectedColor = Color.white;
    [NonSerialized] public int selectedStyle;

    public AppearanceStyleSet StyleSet => styleSwapper != null ? styleSwapper.StyleSet : null;
    private Color _buttonBaseColor;
    private Color _labelBaseColor;
    private Color _normalTintBase = Color.white;

    public void Initialize()
    {
        if (spritePreview != null)
            selectedColor = spritePreview.color;
        else if (uiPreview != null)
            selectedColor = uiPreview.color;

        if (buttonImage != null)
            _buttonBaseColor = buttonImage.color;

        if (styleSwapper != null)
            selectedStyle = styleSwapper.StyleIndex;

        if (triggerButton != null)
            _normalTintBase = triggerButton.colors.normalColor;

        if (buttonLabel == null && triggerButton != null)
            buttonLabel = triggerButton.GetComponentInChildren<TMP_Text>(true);

        if (buttonLabel != null)
            _labelBaseColor = buttonLabel.color;
    }

    public void ApplyColour(Color colour)
    {
        selectedColor = colour;
        if (uiPreview != null)
            uiPreview.color = colour;
        if (spritePreview != null)
            spritePreview.color = colour;
    }

    public void ApplyStyle(int index)
    {
        selectedStyle = index;
        if (styleSwapper != null)
            styleSwapper.SetStyle(index);
    }

    private void SetNormalTint(Color colour)
    {
        if (triggerButton == null) return;

        var colours = triggerButton.colors;
        if (colours.normalColor == colour) return;

        colours.normalColor = colour;
        triggerButton.colors = colours;
    }

    public void SetHighlighted(bool highlighted)
    {
        SelectionOutline outline = null;
        if (triggerButton != null)
            triggerButton.TryGetComponent(out outline);

        var ui = UIManager.Instance;
        var style = outline != null && outline.OverrideCategoryStyle != null
            ? outline.OverrideCategoryStyle
            : ui != null ? ui.ActiveCategoryStyle : null;

        bool active = highlighted && style != null;

        if (outline != null)
            outline.SetChosen(active && style.changeOutline, active ? style.outlineColor : (Color?)null);

        bool useTint = active && style.UsesTint && triggerButton != null && triggerButton.transition == Selectable.Transition.ColorTint;
        SetNormalTint(useTint ? style.buttonColor : _normalTintBase);

        if (buttonImage != null)
            buttonImage.color = active && !useTint ? style.GetButtonColor(_buttonBaseColor) : _buttonBaseColor;

        if (buttonLabel != null)
            buttonLabel.color = active ? style.GetTextColor(_labelBaseColor) : _labelBaseColor;
    }
}

public enum FlavourColourMode
{
    MatchLabel,
    Pastel,
    Single
}

public class CharacterCreationController : MonoBehaviour
{
    private const string DefaultName = "Player";

    [SerializeField] private TMP_Text nameDisplay;
    [SerializeField] private string nameDisplayFormat = "Welcome {0}!";
    [SerializeField] private string blockedNameMessage = "That name isn't allowed. Please choose another one.";
    [SerializeField] private string blockedNameButtonText = "Sorry";

    [Header("Create Confirmation")]
    [SerializeField] [TextArea(2, 4)] private string createConfirmMessage = "Ready to set off on your adventure, {0}? The summit is waiting!";
    [SerializeField] private bool styleCreateConfirmation = true;
    [SerializeField] private ConfirmationStyle createConfirmationStyle = new ConfirmationStyle();
    [SerializeField] private OnScreenKeyboardController keyboard;
    [SerializeField] private ColourSwatchPicker colourPicker;
    [SerializeField] private StylePicker stylePicker;
    [SerializeField] private CategoryInfoPanel infoPanel;
    [SerializeField] private FlavourColourMode flavourColourMode = FlavourColourMode.MatchLabel;
    [SerializeField] private Color singleFlavourColour = Color.white;
    [SerializeField] private GameObject defaultSelected;

    [SerializeField] private CharacterColourSlot hairSlot;
    [SerializeField] private CharacterColourSlot skinSlot;
    [SerializeField] private CharacterColourSlot eyeSlot;
    [SerializeField] private CharacterColourSlot clothesTopSlot;
    [SerializeField] private CharacterColourSlot clothesBottomSlot;
    [SerializeField] private CharacterColourSlot shoesSlot;

    [Header("Category Navigation")]
    [SerializeField] private string nextCategoryActionPath = "UI/NextFilter";
    [SerializeField] private string previousCategoryActionPath = "UI/PrevFilter";
    [SerializeField] private bool categoryButtonsSelectable;
    [SerializeField] private string categorySwitchSound = "UIclick";

    private CharacterColourSlot _activeSlot;
    private CharacterColourSlot _lastSlot;
    private readonly List<CharacterColourSlot> _orderedSlots = new List<CharacterColourSlot>();
    private InputAction _nextCategoryAction;
    private InputAction _previousCategoryAction;
    private GameObject _lastNavigable;
    private string _characterName = DefaultName;
    private bool _nameSet;

    private void Start()
    {

        colourPicker.OnClosed += OnColourPickerClosed;
        if (stylePicker != null)
            stylePicker.OnClosed += OnStylePickerClosed;

        if (infoPanel != null)
            infoPanel.BackPressed += CloseCategory;

        foreach (var slot in new[] { hairSlot, skinSlot, eyeSlot, clothesTopSlot, clothesBottomSlot, shoesSlot })
            slot.Initialize();

        SetupCategoryNavigation();

        var first = IsCategoryButton(defaultSelected) ? FindFirstNavigable() : defaultSelected;
        if (first != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first);
    }

    private void LateUpdate()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || categoryButtonsSelectable) return;

        var current = eventSystem.currentSelectedGameObject;
        if (current == null) return;

        if (!IsCategoryButton(current))
        {
            _lastNavigable = current;
            return;
        }

        var target = _lastNavigable != null && _lastNavigable.activeInHierarchy ? _lastNavigable : FindFirstNavigable();
        eventSystem.SetSelectedGameObject(target);
    }

    private bool IsCategoryButton(GameObject target)
    {
        if (target == null || categoryButtonsSelectable) return false;

        foreach (var slot in _orderedSlots)
        {
            if (slot.triggerButton != null && slot.triggerButton.gameObject == target)
                return true;
        }
        return false;
    }

    private GameObject FindFirstNavigable()
    {
        foreach (var selectable in Selectable.allSelectablesArray)
        {
            if (!selectable.IsInteractable() || selectable.navigation.mode == Navigation.Mode.None) continue;
            if (IsCategoryButton(selectable.gameObject)) continue;
            if (colourPicker != null && selectable.transform.IsChildOf(colourPicker.PanelRoot)) continue;
            return selectable.gameObject;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (_nextCategoryAction != null)
            _nextCategoryAction.performed -= OnNextCategory;
        if (_previousCategoryAction != null)
            _previousCategoryAction.performed -= OnPreviousCategory;
    }

    private void SetupCategoryNavigation()
    {
        _orderedSlots.Clear();
        foreach (var slot in new[] { hairSlot, skinSlot, eyeSlot, clothesTopSlot, clothesBottomSlot, shoesSlot })
        {
            if (slot == null || slot.triggerButton == null) continue;
            _orderedSlots.Add(slot);

            if (!categoryButtonsSelectable)
            {
                var navigation = slot.triggerButton.navigation;
                navigation.mode = Navigation.Mode.None;
                slot.triggerButton.navigation = navigation;
            }
        }

        var input = InputManager.Instance;
        if (input == null) return;

        if (!string.IsNullOrEmpty(nextCategoryActionPath))
        {
            _nextCategoryAction = input.FindAction(nextCategoryActionPath, this);
            if (_nextCategoryAction != null)
                _nextCategoryAction.performed += OnNextCategory;
        }

        if (!string.IsNullOrEmpty(previousCategoryActionPath))
        {
            _previousCategoryAction = input.FindAction(previousCategoryActionPath, this);
            if (_previousCategoryAction != null)
                _previousCategoryAction.performed += OnPreviousCategory;
        }
    }

    private void OnNextCategory(InputAction.CallbackContext ctx) => StepCategory(1);
    private void OnPreviousCategory(InputAction.CallbackContext ctx) => StepCategory(-1);

    private void StepCategory(int direction)
    {
        if (_orderedSlots.Count == 0 || !CanSwitchCategory()) return;

        if (!string.IsNullOrEmpty(categorySwitchSound) && ASpawner.Instance != null)
            ASpawner.Play(categorySwitchSound);

        SortSlotsByScreenPosition();

        if (_activeSlot == null && _lastSlot != null && _orderedSlots.Contains(_lastSlot))
        {
            OpenPicker(_lastSlot);
            return;
        }

        int index = _activeSlot != null ? _orderedSlots.IndexOf(_activeSlot) : -1;
        index = index < 0
            ? (direction > 0 ? 0 : _orderedSlots.Count - 1)
            : (index + direction + _orderedSlots.Count) % _orderedSlots.Count;

        OpenPicker(_orderedSlots[index]);
    }

    private void SortSlotsByScreenPosition()
    {
        _orderedSlots.Sort((a, b) =>
        {
            var pa = a.triggerButton.transform.position;
            var pb = b.triggerButton.transform.position;
            if (Mathf.Abs(pa.y - pb.y) > 0.01f) return pb.y.CompareTo(pa.y);
            return pa.x.CompareTo(pb.x);
        });
    }

    private bool CanSwitchCategory()
    {
        var ui = UIManager.Instance;
        if (ui == null || !ui.IsAnyUIOpen) return true;

        var top = ui.TopLayerRoot;
        if (top == null) return false;

        bool pickerOnTop = (colourPicker != null && colourPicker.IsOpen && Related(top, colourPicker.PanelRoot))
                           || (stylePicker != null && stylePicker.IsOpen && Related(top, stylePicker.PanelRoot));
        bool infoOnTop = infoPanel != null && infoPanel.IsOpen && Related(top, infoPanel.PanelRoot);
        return pickerOnTop || infoOnTop;
    }

    private static bool Related(Transform a, Transform b) =>
        a != null && b != null && (a == b || a.IsChildOf(b) || b.IsChildOf(a));

    public void OnEditNamePressed()
    {
        CloseCategory();

        keyboard.Open(_nameSet ? _characterName : string.Empty, CensorName, name =>
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            if (IsBlocked(name))
            {
                ShowBlockedName();
                return;
            }

            _characterName = name.Trim();
            _nameSet = true;
            nameDisplay.text = string.IsNullOrEmpty(nameDisplayFormat) ? _characterName : string.Format(nameDisplayFormat, _characterName);
        });
    }

    private void ShowBlockedName()
    {
        var dialogue = ConfirmationDialogueController.Instance;
        if (dialogue == null) return;

        dialogue.Show(blockedNameMessage, null, OnEditNamePressed, allowConfirm: false, cancelText: blockedNameButtonText);
    }

    private static bool IsBlocked(string name) => NameFilter.IsBlocked(name);

    private static string CensorName(string name) => NameFilter.Censor(name);

    public void OnChooseHairColourPressed() => OpenPicker(hairSlot);
    public void OnChooseSkinColourPressed() => OpenPicker(skinSlot);
    public void OnChooseEyeColourPressed() => OpenPicker(eyeSlot);
    public void OnChooseClothesTopColourPressed() => OpenPicker(clothesTopSlot);
    public void OnChooseClothesBottomColourPressed() => OpenPicker(clothesBottomSlot);
    public void OnChooseShoesColourPressed() => OpenPicker(shoesSlot);

    private void OpenPicker(CharacterColourSlot slot)
    {
        SetActiveSlot(slot);

        if (infoPanel != null)
        {
            SortSlotsByScreenPosition();
            var colour = CategoryColourFor(slot);
            infoPanel.Show(slot.DisplayName, slot.flavourText, slot.categoryImage, colour, flavourColourMode, singleFlavourColour);
        }

        bool styled = stylePicker != null && slot.StyleSet != null && slot.StyleSet.Count > 1;

        if (styled)
        {
            colourPicker.Hide();
            stylePicker.Open(slot.StyleSet, slot.selectedStyle, index => OnStyleChosen(slot, index));
        }
        else
        {
            if (stylePicker != null)
                stylePicker.Hide();
            colourPicker.Open(slot.ApplyColour, slot.selectedColor, slot.palette);
        }
    }

    private void OnStyleChosen(CharacterColourSlot slot, int index)
    {
        slot.ApplyStyle(index);
        colourPicker.Open(slot.ApplyColour, slot.selectedColor, slot.palette);
    }

    private void OnColourPickerClosed()
    {
        if (stylePicker != null && stylePicker.IsOpen)
            return;

        SetActiveSlot(null);
    }

    private void OnStylePickerClosed()
    {
        colourPicker.Hide();
        SetActiveSlot(null);
    }

    private void CloseCategory()
    {
        if (colourPicker != null)
            colourPicker.Hide();
        if (stylePicker != null)
            stylePicker.Hide();
        SetActiveSlot(null);
    }

    private Color CategoryColourFor(CharacterColourSlot slot) =>
        slot.overrideLabelColour ? slot.labelColour : infoPanel.ColourForIndex(_orderedSlots.IndexOf(slot));

    private void SetActiveSlot(CharacterColourSlot slot)
    {
        _activeSlot?.SetHighlighted(false);
        _activeSlot = slot;
        _activeSlot?.SetHighlighted(true);

        if (slot != null)
            _lastSlot = slot;

        if (slot == null && infoPanel != null)
            infoPanel.Hide();
    }

    public void OnConfirmPressed()
    {
        ConfirmationDialogueController.Instance.Show(
            string.Format(createConfirmMessage, _characterName),
            OnCharacterConfirmed,
            style: styleCreateConfirmation ? createConfirmationStyle : null);
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
            shoesColor = shoesSlot.selectedColor,
            hairStyle = hairSlot.selectedStyle,
            clothesTopStyle = clothesTopSlot.selectedStyle,
            clothesBottomStyle = clothesBottomSlot.selectedStyle
        };

        GameManager.Instance.CompleteCharacterCreation(character);
    }

    public void OnBackToTitlePressed()
    {
        GameManager.Instance.ReturnToTitle();
    }
}