using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SwatchPalette
{
    Generated,
    SkinTones
}

public class ColourSwatchPicker : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private Button swatchButtonPrefab;
    [SerializeField] private RectTransform contentParent;
    [SerializeField] private float cellSize = 50f;
    [SerializeField] private float minSpacing = 2f;
    [SerializeField] [Range(0f, 1f)] private float minTintSaturation = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float minShadeValue = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float pastelAmount = 0.35f;
    [SerializeField] private List<Color> skinTones = new List<Color>();

    public event Action OnClosed;

    public bool IsOpen => panel.activeSelf;
    public Transform PanelRoot => panel.transform;

    private Action<Color> _onSelected;
    private bool _built;
    private SwatchPalette _builtPalette;
    private int _columns;
    private int _rows;
    private Button _chosenButton;
    private readonly List<(Button button, Color colour)> _swatches = new List<(Button, Color)>();

    private const float ColourTolerance = 0.01f;

    private static readonly Color[] DefaultSkinTones =
    {
        new Color32(0xFF, 0xE4, 0xE6, 0xFF), new Color32(0xFF, 0xD9, 0xDC, 0xFF), new Color32(0xF7, 0xC6, 0xC7, 0xFF), new Color32(0xEF, 0xB5, 0xB9, 0xFF), new Color32(0xE2, 0xA0, 0xA6, 0xFF),
        new Color32(0xD4, 0x8C, 0x94, 0xFF), new Color32(0xC2, 0x7A, 0x82, 0xFF), new Color32(0xA8, 0x66, 0x70, 0xFF), new Color32(0x8C, 0x52, 0x5C, 0xFF), new Color32(0x6E, 0x3F, 0x48, 0xFF),
        new Color32(0xF9, 0xD0, 0xC4, 0xFF), new Color32(0xF0, 0xB8, 0xA8, 0xFF), new Color32(0xE3, 0xA0, 0x90, 0xFF), new Color32(0xD4, 0x8A, 0x78, 0xFF), new Color32(0xC0, 0x70, 0x60, 0xFF),
        new Color32(0xA8, 0x5A, 0x4A, 0xFF), new Color32(0x8E, 0x48, 0x3A, 0xFF), new Color32(0x76, 0x3A, 0x2E, 0xFF), new Color32(0x5E, 0x2E, 0x24, 0xFF), new Color32(0x47, 0x22, 0x1B, 0xFF),
        new Color32(0xFF, 0xE7, 0xD1, 0xFF), new Color32(0xFD, 0xDC, 0xC4, 0xFF), new Color32(0xF6, 0xD1, 0xB1, 0xFF), new Color32(0xF3, 0xC6, 0xA5, 0xFF), new Color32(0xEE, 0xBB, 0x99, 0xFF),
        new Color32(0xE8, 0xB0, 0x8B, 0xFF), new Color32(0xE0, 0xA4, 0x7D, 0xFF), new Color32(0xD6, 0x9A, 0x72, 0xFF), new Color32(0xC9, 0x8B, 0x63, 0xFF), new Color32(0xBD, 0x7E, 0x57, 0xFF),
        new Color32(0xFC, 0xE9, 0xC8, 0xFF), new Color32(0xF7, 0xDF, 0xB4, 0xFF), new Color32(0xF0, 0xD3, 0xA0, 0xFF), new Color32(0xE8, 0xC8, 0x8E, 0xFF), new Color32(0xDD, 0xBA, 0x7C, 0xFF),
        new Color32(0xD1, 0xAB, 0x6C, 0xFF), new Color32(0xC4, 0x9C, 0x5E, 0xFF), new Color32(0xB4, 0x8C, 0x51, 0xFF), new Color32(0xA2, 0x7C, 0x46, 0xFF), new Color32(0x8F, 0x6C, 0x3C, 0xFF),
        new Color32(0xE8, 0xD3, 0xAE, 0xFF), new Color32(0xD8, 0xC2, 0x9A, 0xFF), new Color32(0xC9, 0xB0, 0x86, 0xFF), new Color32(0xC8, 0xA2, 0x7C, 0xFF), new Color32(0xB8, 0x92, 0x70, 0xFF),
        new Color32(0xB4, 0x9C, 0x72, 0xFF), new Color32(0xA8, 0x84, 0x62, 0xFF), new Color32(0x9C, 0x87, 0x5F, 0xFF), new Color32(0x84, 0x72, 0x4F, 0xFF), new Color32(0x6E, 0x5E, 0x40, 0xFF),
        new Color32(0xB0, 0x71, 0x4C, 0xFF), new Color32(0xA2, 0x64, 0x43, 0xFF), new Color32(0x94, 0x58, 0x3A, 0xFF), new Color32(0x86, 0x4D, 0x32, 0xFF), new Color32(0x78, 0x43, 0x2B, 0xFF),
        new Color32(0x6A, 0x3A, 0x25, 0xFF), new Color32(0x5C, 0x31, 0x1F, 0xFF), new Color32(0x4E, 0x29, 0x1A, 0xFF), new Color32(0x3F, 0x22, 0x16, 0xFF), new Color32(0x2D, 0x1A, 0x12, 0xFF)
    };

    private static readonly Color[] DefaultFillerTones =
    {
        new Color32(0xFC, 0xE9, 0xC8, 0xFF), new Color32(0xF7, 0xDF, 0xB4, 0xFF), new Color32(0xF0, 0xD3, 0xA0, 0xFF),
        new Color32(0xE8, 0xC8, 0x8E, 0xFF), new Color32(0xDD, 0xBA, 0x7C, 0xFF), new Color32(0xD1, 0xAB, 0x6C, 0xFF),
        new Color32(0xC4, 0x9C, 0x5E, 0xFF), new Color32(0xB4, 0x8C, 0x51, 0xFF), new Color32(0xA2, 0x7C, 0x46, 0xFF),
        new Color32(0x8F, 0x6C, 0x3C, 0xFF)
    };

    private List<Color> SkinTones => skinTones != null && skinTones.Count > 0 ? skinTones : DefaultTones();

    private static List<Color> DefaultTones()
    {
        return new List<Color>(DefaultSkinTones);
    }

    [ContextMenu("Re-add Default Skin Tones")]
    private void FillDefaultSkinTones()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Re-add Default Skin Tones");
#endif
        skinTones = DefaultTones();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Clear Skin Tones")]
    private void ClearSkinTones()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Clear Skin Tones");
#endif
        skinTones = new List<Color>();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void Awake()
    {
        panel.SetActive(false);

        if (uiPanel == null)
            uiPanel = panel.GetComponent<UIPanel>();

        if (uiPanel != null)
            uiPanel.OnBack += () => OnClosed?.Invoke();
    }

    public void Open(Action<Color> onSelected, Color? current = null, SwatchPalette palette = SwatchPalette.Generated)
    {
        _onSelected = onSelected;

        if (!_built || _builtPalette != palette)
            BuildSwatches(palette);

        var match = current.HasValue ? FindSwatch(current.Value) : null;
        SetChosenButton(match);

        UIPanelAnimator.SetVisible(panel, true);

        if (match != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(match.gameObject);
    }

    private Button FindSwatch(Color colour)
    {
        foreach (var (button, swatch) in _swatches)
        {
            if (Mathf.Abs(swatch.r - colour.r) < ColourTolerance &&
                Mathf.Abs(swatch.g - colour.g) < ColourTolerance &&
                Mathf.Abs(swatch.b - colour.b) < ColourTolerance)
                return button;
        }
        return null;
    }

    public void Hide()
    {
        if (IsOpen)
            UIPanelAnimator.SetVisible(panel, false);
    }

    public void Close()
    {
        if (!IsOpen) return;

        UIPanelAnimator.SetVisible(panel, false);
        OnClosed?.Invoke();
    }

    private void BuildSwatches(SwatchPalette palette)
    {
        SetChosenButton(null);
        foreach (var (oldButton, _) in _swatches)
        {
            if (oldButton == null) continue;
            oldButton.gameObject.SetActive(false);
            Destroy(oldButton.gameObject);
        }
        _swatches.Clear();

        ConfigureGrid();

        var colours = palette == SwatchPalette.SkinTones ? FillRows(SkinTones) : GenerateColours();
        _builtPalette = palette;

        foreach (var color in colours)
        {
            var button = Instantiate(swatchButtonPrefab, contentParent);
            button.image.color = color;
            button.onClick.AddListener(() => Select(color, button));
            _swatches.Add((button, color));
        }

        _built = true;
    }

    private List<Color> FillRows(List<Color> colours)
    {
        if (_columns <= 0 || colours.Count == 0) return colours;

        var filled = new List<Color>(colours);
        int missing = (_columns - filled.Count % _columns) % _columns;
        for (int i = 0; i < missing; i++)
            filled.Add(DefaultFillerTones[i % DefaultFillerTones.Length]);

        return filled;
    }

    private List<Color> GenerateColours()
    {
        var colours = new List<Color>(_columns * _rows);

        for (int r = 0; r < _rows; r++)
        {
            float t = _rows > 1 ? (float)r / (_rows - 1) : 0.5f;
            float s = t < 0.5f ? Mathf.Lerp(minTintSaturation, 1f, t * 2f) : 1f;
            float v = t < 0.5f ? 1f : Mathf.Lerp(1f, minShadeValue, (t - 0.5f) * 2f);
            s *= 1f - pastelAmount * 0.5f;
            v += (1f - v) * pastelAmount * 0.2f;

            for (int c = 0; c < _columns; c++)
                colours.Add(Color.HSVToRGB((float)c / _columns, s, v));
        }

        if (colours.Count > 0)
        {
            colours[0] = Color.white;
            colours[colours.Count - 1] = Color.black;
        }

        return colours;
    }

    private void ConfigureGrid()
    {
        var grid = contentParent.GetComponent<GridLayoutGroup>();
        if (grid == null) return;

        grid.spacing = new Vector2(minSpacing, minSpacing);
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        float availableWidth = contentParent.rect.width - grid.padding.left - grid.padding.right;
        float availableHeight = contentParent.rect.height - grid.padding.top - grid.padding.bottom;

        _columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + minSpacing) / (cellSize + minSpacing)));
        _rows = Mathf.Max(2, Mathf.FloorToInt((availableHeight + minSpacing) / (cellSize + minSpacing)));

        grid.constraintCount = _columns;
    }

    private void Select(Color color, Button sourceButton)
    {
        SetChosenButton(sourceButton);
        _onSelected?.Invoke(color);
    }

    private void SetChosenButton(Button button)
    {
        SetOutlineChosen(_chosenButton, false);
        _chosenButton = button;
        SetOutlineChosen(_chosenButton, true);
    }

    private static void SetOutlineChosen(Button button, bool chosen)
    {
        if (button != null && button.TryGetComponent<SelectionOutline>(out var outline))
            outline.SetChosen(chosen);
    }
}