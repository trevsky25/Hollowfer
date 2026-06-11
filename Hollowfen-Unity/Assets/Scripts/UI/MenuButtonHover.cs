using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Color hoverColor = new Color32(0xD5, 0xBF, 0x78, 0xFF);
    public TMP_Text label;

    Color baseColor;
    string baseText;

    void Awake()
    {
        if (!label) label = GetComponentInChildren<TMP_Text>();
        if (label)
        {
            baseColor = label.color;
            baseText = label.text;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!label) return;
        label.color = hoverColor;
        label.text = $"<u>{baseText}</u>";
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!label) return;
        label.color = baseColor;
        label.text = baseText;
    }
}
