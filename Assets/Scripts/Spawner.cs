using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PokemonSpawnEntry { public PokemonData pokemon; [Range(0f, 1f)] public float spawnChance = 1f; }

public class Spawner : MonoBehaviour
{
    public int numberOfCreatures = 1;
    public List<PokemonSpawnEntry> pokemonSpawnList;
    public LayerMask groundLayer;
    public float yOffset = 0.5f;

    [Header("Rango de niveles")]
    public int nivelMin = 3;
    public int nivelMax = 10;

    public float spawnRadiusMin = 1f;
    public float spawnRadiusMax = 3f;

    [Header("Aparición shiny")]
    [Range(0f, 1f)] public float shinyProbability = 0.01f;

    private void Start() { AdjustPositionToGround(); SpawnCreatures(); }

    void AdjustPositionToGround()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer))
            transform.position = hit.point + Vector3.up * yOffset;
    }

    public void SpawnCreatures() { for (int i = 0; i < numberOfCreatures; i++) SpawnCreature(); }

    void SpawnCreature()
    {
        Vector3 spawnPosition = GetRandomPositionWithinSpawnRadius();
        PokemonData selectedPokemon = GetRandomPokemon(); if (selectedPokemon == null) return;

        GameObject creatureInstance = Instantiate(selectedPokemon.prefab, spawnPosition, Quaternion.identity);
        CreatureBehavior creatureBehavior = creatureInstance.GetComponent<CreatureBehavior>() ?? creatureInstance.AddComponent<CreatureBehavior>();

        int level = Random.Range(nivelMin, nivelMax + 1);
        bool isShiny = Random.value < shinyProbability;

        // Crea la instancia (género se resuelve dentro por reglas de especie)
        PokemonInstance instance = new PokemonInstance(selectedPokemon, level, Gender.Unknown, isShiny);

        creatureBehavior.SetPokemon(instance);
        creatureBehavior.spawnPoint = transform.position;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) creatureBehavior.player = playerObject.transform;

        AdjustCreatureToGround(creatureInstance.transform);
    }

    Vector3 GetRandomPositionWithinSpawnRadius()
    {
        Vector3 randomDirection = Random.insideUnitSphere; randomDirection.y = 0;
        float randomDistance = Random.Range(spawnRadiusMin, spawnRadiusMax);
        return transform.position + randomDirection.normalized * randomDistance;
    }

    void AdjustCreatureToGround(Transform creatureTransform)
    {
        if (Physics.Raycast(creatureTransform.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer))
            creatureTransform.position = hit.point + Vector3.up * yOffset;
    }

    PokemonData GetRandomPokemon()
    {
        float totalWeight = 0f;
        foreach (var entry in pokemonSpawnList) totalWeight += entry.spawnChance;

        float roll = Random.Range(0f, totalWeight); float cumulative = 0f;
        foreach (var entry in pokemonSpawnList)
        {
            cumulative += entry.spawnChance;
            if (roll <= cumulative) return entry.pokemon;
        }
        return null;
    }
}
