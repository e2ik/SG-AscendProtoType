using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    public event Action OnClosed;

    private Action<Color> _onSelected;
    private bool _built;
    private int _columns;
    private int _rows;
    private Button _chosenButton;

    private void Awake()
    {
        panel.SetActive(false);

        if (uiPanel != null)
            uiPanel.OnBack += () => OnClosed?.Invoke();
    }

    public void Open(Action<Color> onSelected)
    {
        _onSelected = onSelected;
        SetChosenButton(null);

        if (!_built)
            BuildSwatches();

        UIPanelAnimator.SetVisible(panel, true);
    }

    private void BuildSwatches()
    {
        ConfigureGrid();

        foreach (var color in GenerateColours())
        {
            var button = Instantiate(swatchButtonPrefab, contentParent);
            button.image.color = color;
            button.onClick.AddListener(() => Select(color, button));
        }

        _built = true;
    }

    private List<Color> GenerateColours()
    {
        var colours = new List<Color>(_columns * _rows);

        for (int r = 0; r < _rows; r++)
        {
            float t = _rows > 1 ? (float)r / (_rows - 1) : 0.5f;
            float s = t < 0.5f ? Mathf.Lerp(minTintSaturation, 1f, t * 2f) : 1f;
            float v = t < 0.5f ? 1f : Mathf.Lerp(1f, minShadeValue, (t - 0.5f) * 2f);

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