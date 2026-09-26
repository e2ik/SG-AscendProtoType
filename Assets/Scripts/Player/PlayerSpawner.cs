using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;

    public event Action<PlayerController> PlayerSpawned;
    public PlayerController Player { get; private set; }
    public GameObject PlayerObject { get; private set; }

    private void Start()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner has no Player Prefab assigned", this);
            return;
        }

        var point = spawnPoint != null ? spawnPoint : transform;

        PlayerObject = Instantiate(playerPrefab, point.position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(PlayerObject, gameObject.scene);

        Player = PlayerObject.GetComponent<PlayerController>();
        if (Player == null)
            Debug.LogWarning($"Spawned '{playerPrefab.name}' has no PlayerController on its root", PlayerObject);

        var appearance = Player != null && Player.Appearance != null
            ? Player.Appearance
            : PlayerObject.GetComponentInChildren<PlayerAppearance>();

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && appearance != null)
            appearance.Apply(save.character);

        if (Player != null)
            PlayerSpawned?.Invoke(Player);
    }
}