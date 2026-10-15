using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public static class ActionPresser
{
    public static InputAction Resolve(InputActionReference reference)
    {
        if (reference == null || reference.action == null) return null;

        var source = reference.action;
        var input = InputManager.Instance;
        if (input == null || input.PlayerInput == null) return source;

        string path = source.actionMap != null ? $"{source.actionMap.name}/{source.name}" : source.name;
        return input.PlayerInput.actions.FindAction(path) ?? source;
    }

    public static bool Press(InputAction action, Object context = null)
    {
        var button = FindButton(action);
        if (button == null)
        {
            Debug.LogWarning($"No pressable button found for action '{(action != null ? action.name : "null")}'", context);
            return false;
        }

        MonoBehaviour runner = InputManager.Instance;
        if (runner == null)
        {
            Debug.LogWarning("ActionPresser needs an InputManager in the scene", context);
            return false;
        }

        runner.StartCoroutine(PressRoutine(button));
        return true;
    }

    private static ButtonControl FindButton(InputAction action)
    {
        if (action == null) return null;

        ButtonControl gamepadButton = null;

        foreach (var control in action.controls)
        {
            if (!(control is ButtonControl button)) continue;

            if (button.device is Keyboard || button.device is Mouse)
                return button;

            if (gamepadButton == null && button.device is Gamepad)
                gamepadButton = button;
        }

        return gamepadButton;
    }

    private static IEnumerator PressRoutine(ButtonControl button)
    {
        Queue(button, 1f);
        yield return null;
        Queue(button, 0f);
    }

    private static void Queue(ButtonControl button, float value)
    {
        using (StateEvent.From(button.device, out var eventPtr))
        {
            button.WriteValueIntoEvent(value, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
    }
}
