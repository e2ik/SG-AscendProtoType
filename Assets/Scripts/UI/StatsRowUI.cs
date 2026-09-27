using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatRowUI : MonoBehaviour
{
    [SerializeField] private StatType stat;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private Color pendingColor = new Color(0.5f, 1f, 0.4f);

    public StatType Stat => stat;
    public Button Plus => plusButton;
    public Button Minus => minusButton;

    public void Display(int current, int pending, int max, string effect)
    {
        string cap = max == int.MaxValue ? string.Empty : $" / {max}";
        string pendingPart = pending > 0 ? $" <color=#{ColorUtility.ToHtmlStringRGB(pendingColor)}>+{pending}</color>" : string.Empty;
        valueText.text = $"{current}{pendingPart}{cap}";

        if (effectText != null)
        {
            effectText.text = effect ?? string.Empty;
            effectText.gameObject.SetActive(!string.IsNullOrEmpty(effect));
        }
    }
}