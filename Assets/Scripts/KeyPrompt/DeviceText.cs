using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class DeviceText : MonoBehaviour
{
    [SerializeField] private string keyboardText = "Q";
    [SerializeField] private string gamepadText = "L1";

    private TMP_Text _label;

    private void Awake()
    {
        _label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        var input = InputManager.Instance;
        if (input == null) return;

        input.OnDeviceChanged += Apply;
        Apply(input.ActiveDevice);
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged -= Apply;
    }

    private void Apply(InputDeviceType device)
    {
        _label.text = device == InputDeviceType.Gamepad ? gamepadText : keyboardText;
    }
}
