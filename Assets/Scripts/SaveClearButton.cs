using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SaveClearButton : MonoBehaviour
{
    public enum ButtonAction { Save, Clear }

    [SerializeField] private ButtonAction actionType = ButtonAction.Save;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (actionType == ButtonAction.Save)
            SaveManager.Instance?.ManualSave();
        else
            SaveManager.Instance?.ClearSave();
    }
}
