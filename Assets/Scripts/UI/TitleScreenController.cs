using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TitleScreenController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loadGamePanel;
    [SerializeField] private GameObject defaultSelected;

    [Header("Load Game List")]
    [SerializeField] private Transform saveListContentParent;
    [SerializeField] private SaveSlotUI saveSlotPrefab;

    private readonly List<SaveSlotUI> _spawnedSlots = new List<SaveSlotUI>();

    private void Start()
    {
        loadGamePanel.SetActive(false);

        if (defaultSelected != null)
            EventSystem.current.SetSelectedGameObject(defaultSelected);
    }

    public void OnNewGamePressed()
    {
        ConfirmationDialogueController.Instance.Show("Start a new game?", StartNewGame);
    }

    private void StartNewGame()
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

    private void RefreshSaveList()
    {
        foreach (var slot in _spawnedSlots)
            Destroy(slot.gameObject);
        _spawnedSlots.Clear();

        var saves = SaveManager.Instance.GetAllSaves();
        foreach (var save in saves)
        {
            var slot = Instantiate(saveSlotPrefab, saveListContentParent);
            slot.Setup(save);
            _spawnedSlots.Add(slot);
        }
    }
}