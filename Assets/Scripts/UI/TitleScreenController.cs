using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleScreenController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loadGamePanel;
    [SerializeField] private GameObject loadGameBackground;
    [SerializeField] private GameObject defaultSelected;

    [Header("Load Game List")]
    [SerializeField] private Transform saveListContentParent;
    [SerializeField] private SaveSlotUI saveSlotPrefab;
    [SerializeField] private Button deleteAllButton;

    [Header("Save Limit")]
    [SerializeField] [Min(1)] private int maxSaves = 3;
    [SerializeField] [TextArea(2, 4)] private string saveLimitMessage = "You already have {0} adventures saved! Delete one from Load Game to start a new one.";
    [SerializeField] private string saveLimitButtonText = "Okay";

    private readonly List<SaveSlotUI> _spawnedSlots = new List<SaveSlotUI>();

    private void Start()
    {
        loadGamePanel.SetActive(false);
        if (loadGameBackground != null)
            loadGameBackground.SetActive(false);

        var uiPanel = loadGamePanel.GetComponent<UIPanel>();
        if (uiPanel != null)
            uiPanel.OnBack += HideBackground;

        if (defaultSelected != null)
            EventSystem.current.SetSelectedGameObject(defaultSelected);
    }

    public void OnNewGamePressed()
    {
        var saves = SaveManager.Instance != null ? SaveManager.Instance.GetAllSaves() : null;
        if (saves != null && saves.Count >= maxSaves)
        {
            ConfirmationDialogueController.Instance.Show(
                string.Format(saveLimitMessage, maxSaves),
                null,
                allowConfirm: false,
                cancelText: saveLimitButtonText);
            return;
        }

        GameManager.Instance.StartNewGame();
    }

    public void OnLoadGamePressed()
    {
        UIPanelAnimator.SetVisible(loadGameBackground, true);
        UIPanelAnimator.SetVisible(loadGamePanel, true);
        RefreshSaveList();
    }

    public void OnBackPressed()
    {
        UIPanelAnimator.SetVisible(loadGamePanel, false);
        HideBackground();
    }

    private void HideBackground()
    {
        UIPanelAnimator.SetVisible(loadGameBackground, false);
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }

    public void OnDeleteAllPressed()
    {
        ConfirmationDialogueController.Instance.Show(
            "Delete ALL saves? This can't be undone.",
            () =>
            {
                SaveManager.Instance.DeleteAllSaves();
                RefreshSaveList();
            },
            focusCancel: true);
    }

    private void OnDeleteSaveRequested(SaveData save)
    {
        ConfirmationDialogueController.Instance.Show(
            $"Delete \"{save.saveName}\"? This can't be undone.",
            () =>
            {
                SaveManager.Instance.DeleteSave(save.saveId);
                RefreshSaveList();
            },
            focusCancel: true);
    }

    private void RefreshSaveList()
    {
        foreach (var slot in _spawnedSlots)
        {
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }
        _spawnedSlots.Clear();

        var saves = SaveManager.Instance.GetAllSaves();
        foreach (var save in saves)
        {
            var slot = Instantiate(saveSlotPrefab, saveListContentParent);
            slot.Setup(save, OnDeleteSaveRequested);
            _spawnedSlots.Add(slot);
        }

        if (deleteAllButton != null)
            deleteAllButton.interactable = saves.Count > 0;
    }
}