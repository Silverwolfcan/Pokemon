using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatLogItemUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image bg;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text txtName;
    [SerializeField] private TMP_Text txtAction;
    [SerializeField] private TMP_Text txtDamage;

    [Header("Colores")]
    [SerializeField] private Color allyColor = new Color32(0x37, 0x8B, 0xF6, 255);
    [SerializeField] private Color enemyColor = new Color32(0xE1, 0x54, 0x54, 255);

    public struct Data
    {
        public bool isAlly;
        public Sprite icon;
        public string monName;
        public string actionText;
        public string damageText;
    }

    public void Bind(Data d)
    {
        if (bg) bg.color = d.isAlly ? allyColor : enemyColor;
        if (icon) { icon.sprite = d.icon; icon.enabled = d.icon != null; }
        if (txtName) txtName.text = d.monName ?? "";
        if (txtAction) txtAction.text = d.actionText ?? "";
        if (txtDamage)
        {
            txtDamage.text = d.damageText ?? "";
            txtDamage.enabled = !string.IsNullOrEmpty(d.damageText);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
