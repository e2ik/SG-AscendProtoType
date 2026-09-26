using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleScreenController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loadGamePanel;
    [SerializeField] private GameObject defaultSelected;

    [Header("Load Game List")]
    [SerializeField] private Transform saveListContentParent;
    [SerializeField] private SaveSlotUI saveSlotPrefab;
    [SerializeField] private Button deleteAllButton;

    private readonly List<SaveSlotUI> _spawnedSlots = new List<SaveSlotUI>();

    private void Start()
    {
        loadGamePanel.SetActive(false);

        if (defaultSelected != null)
            EventSystem.current.SetSelectedGameObject(defaultSelected);
    }

    public void OnNewGamePressed()
    {
        GameManager.Instance.StartNewGame();
    }

    public void OnLoadGamePressed()
    {
        loadGamePanel.SetActive(true);
        RefreshSaveList();
    }

    public void OnBackPressed()
    {
        loadGamePanel.SetActive(false);
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