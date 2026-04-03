using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;

    public string CurrentKey { get; private set; }

    public void SetStatus(Sprite icon, int count, string key)
    {
        CurrentKey = key;
        iconImage.sprite = icon;
        UpdateCount(count);
        gameObject.SetActive(true);
    }

    public void UpdateCount(int count)
    {
        countText.text = count.ToString();

        if (count <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}