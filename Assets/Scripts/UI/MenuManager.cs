using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RPG.UI
{
    public class MenuManager : MonoBehaviour
    {
        [Header("Tecla de menú")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        [Header("Contexto (opcional)")]
        [SerializeField] private GameObject combatCanvas;          // Bloquea abrir si está activo
        [SerializeField] private MonoBehaviour playerController;   // Debe exponer EnableControls(bool)
        [SerializeField] private bool pauseTimeScale = true;

        [Header("Cursor")]
        [SerializeField] private bool manageCursor = true;
        [SerializeField] private CursorLockMode menuCursorLock = CursorLockMode.None;
        [SerializeField] private bool menuCursorVisible = true;
        [SerializeField] private CursorLockMode gameplayCursorLock = CursorLockMode.Locked;
        [SerializeField] private bool gameplayCursorVisible = false;

        [Header("Menú principal")]
        [SerializeField] private GameObject mainMenuRoot;

        [Header("Paneles navegables")]
        [SerializeField] private PanelConfig[] panels;

        [Header("Botones del menú principal")]
        [SerializeField] private ButtonBinding[] mainButtons;

        [Header("Acciones globales")]
        [SerializeField] private UnityEvent onMenuOpened;
        [SerializeField] private UnityEvent onMenuClosed;

        [Serializable] public class PanelConfig { public string id; public GameObject root; }
        [Serializable] public class ButtonBinding { public Button button; public string openPanelId; public UnityEvent onClickAction; }

        private readonly Stack<GameObject> _stack = new Stack<GameObject>();
        private Dictionary<string, PanelConfig> _panelIndex;
        private bool _isOpen;

        private void Awake()
        {
            BuildIndex();
            WireButtons();
            SetActiveSafe(mainMenuRoot, false);
            foreach (var p in panels) SetActiveSafe(p.root, false);
        }

        private void Update()
        {
            if (!IsEscapePressed()) return;

            if (!_isOpen) { TryOpenMenu(); return; }

            if (IsAnySubPanelActive()) ShowMainMenuOnly();
            else CloseMenu();
        }

        // --- API pública ---
        public void OpenPanelById(string id)
        {
            if (!_isOpen) TryOpenMenu();
            if (!_panelIndex.TryGetValue(id, out var cfg) || cfg.root == null) return;

            if (_stack.Count == 0) _stack.Push(mainMenuRoot);
            if (_stack.Count > 0) SetActiveSafe(_stack.Peek(), false);
            SetActiveSafe(cfg.root, true);
            _stack.Push(cfg.root);
            Debug.Log($"[UI] Open -> {cfg.root.name}");
        }

        public void Back() => ShowMainMenuOnly();
        public void CloseAll() => CloseMenu();

        // --- Internos ---
        private void TryOpenMenu()
        {
            if (combatCanvas != null && combatCanvas.activeInHierarchy) { Debug.Log("[UI] Bloqueado por combate activo."); return; }
            if (mainMenuRoot == null) { Debug.LogWarning("[UI] mainMenuRoot no asignado."); return; }
            OpenMenu();
        }

        private void OpenMenu()
        {
            _isOpen = true;
            ApplyPause(true);
            SetCursorForMenu(true);
            _stack.Clear();
            ShowMainMenuOnly();
            onMenuOpened?.Invoke();
            Debug.Log("[UI] Menu Opened");
        }

        private void CloseMenu()
        {
            _isOpen = false;
            while (_stack.Count > 0) _stack.Pop();
            SetActiveSafe(mainMenuRoot, false);
            foreach (var p in panels) SetActiveSafe(p.root, false);
            ApplyPause(false);
            SetCursorForMenu(false);
            onMenuClosed?.Invoke();
            Debug.Log("[UI] Menu Closed");
        }

        private void ShowMainMenuOnly()
        {
            foreach (var p in panels) SetActiveSafe(p.root, false);
            SetActiveSafe(mainMenuRoot, true);
            _stack.Clear();
            _stack.Push(mainMenuRoot);
            Debug.Log("[UI] Back -> MainMenu");
        }

        private bool IsAnySubPanelActive()
        {
            foreach (var p in panels) if (p.root != null && p.root.activeInHierarchy) return true;
            return false;
        }

        private void ApplyPause(bool entering)
        {
            var method = playerController?.GetType().GetMethod("EnableControls");
            method?.Invoke(playerController, new object[] { !entering });
            if (pauseTimeScale) Time.timeScale = entering ? 0f : 1f;
        }

        private void SetCursorForMenu(bool entering)
        {
            if (!manageCursor) return;
            if (entering)
            {
                Cursor.lockState = menuCursorLock;
                Cursor.visible = menuCursorVisible;
            }
            else
            {
                Cursor.lockState = gameplayCursorLock;
                Cursor.visible = gameplayCursorVisible;
            }
        }

        private void WireButtons()
        {
            foreach (var b in mainButtons)
            {
                if (b?.button == null) continue;
                b.button.onClick.RemoveAllListeners();
                if (!string.IsNullOrEmpty(b.openPanelId))
                {
                    string id = b.openPanelId;
                    b.button.onClick.AddListener(() => OpenPanelById(id));
                }
                else b.button.onClick.AddListener(() => b.onClickAction?.Invoke());
            }
        }

        private void BuildIndex()
        {
            _panelIndex = new Dictionary<string, PanelConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in panels)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.id) || p.root == null) continue;
                if (!_panelIndex.ContainsKey(p.id)) _panelIndex.Add(p.id, p);
            }
        }

        private static void SetActiveSafe(GameObject go, bool state)
        {
            if (go != null && go.activeSelf != state) go.SetActive(state);
        }

        private bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
            return Input.GetKeyDown(toggleKey);
        }
    }
}
