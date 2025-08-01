using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Configuraci�n global")]
    [Range(0f, 1f)] public float shinyProbability = 0.01f;

    [Header("Equipo del jugador")]
    public List<PokemonInstance> playerTeam = new List<PokemonInstance>(6);

    [Header("UI del equipo")]
    public PokemonTeamUIManager teamUIManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ��� CARGAR equipo guardado al iniciar ���
        playerTeam = SaveSystem.LoadTeam();
        if (teamUIManager != null)
        {
            teamUIManager.currentTeam = playerTeam;
            teamUIManager.RefreshUI();
        }
        else
        {
            Debug.LogWarning("No se encontr� el PokemonTeamUIManager al cargar el equipo.");
        }
    }

    public void CapturePokemon(PokemonInstance wildInstance)
    {
        if (playerTeam.Count >= 6)
        {
            Debug.Log("El equipo est� lleno. No se puede capturar m�s Pok�mon.");
            return;
        }

        playerTeam.Add(wildInstance);
        Debug.Log($"A�adido {wildInstance.baseData.pokemonName} al equipo. Total: {playerTeam.Count}");

        // Actualizar UI de equipo
        if (teamUIManager != null)
        {
            teamUIManager.currentTeam = playerTeam;
            teamUIManager.RefreshUI();
        }
        else
        {
            Debug.LogWarning("No se encontr� el PokemonTeamUIManager.");
        }

        // Actualizar selector de objetos (Pok�balls y Pok�mon)
        if (FindObjectOfType<ItemSelectorUI>() is ItemSelectorUI selector)
        {
            selector.UpdateUI();
        }

        // ��� GUARDAR equipo tras captura ���
        SaveSystem.SaveTeam(playerTeam);
    }
}
