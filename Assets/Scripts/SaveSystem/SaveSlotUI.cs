using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text saveNameText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button deleteButton;

    [Header("Completed Journey")]
    [SerializeField] private string completeFlag = SummitEnding.DefaultCompleteFlag;
    [SerializeField] private string completedSuffix = "";
    [SerializeField] [TextArea(1, 3)] private string completedMessage = "{0} has completed their journey.";
    [SerializeField] private string completedButtonText = "Okay";

    public void Setup(SaveData save, Action<SaveData> onDeleteRequested)
    {
        bool complete = !string.IsNullOrEmpty(completeFlag) && save.GetFlag(completeFlag);
        saveNameText.text = complete ? save.saveName + completedSuffix : save.saveName;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() =>
        {
            if (complete)
                ShowCompleted(save);
            else
                GameManager.Instance.LoadGame(save.saveId);
        });

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDeleteRequested?.Invoke(save));
        }
    }

    private void ShowCompleted(SaveData save)
    {
        string characterName = save.character != null && !string.IsNullOrEmpty(save.character.characterName)
            ? save.character.characterName
            : save.saveName;

        var returnTo = selectButton.gameObject;
        ConfirmationDialogueController.Instance.Show(
            string.Format(completedMessage, characterName),
            null,
            () =>
            {
                if (returnTo != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(returnTo);
            },
            allowConfirm: false,
            cancelText: completedButtonText);
    }
}