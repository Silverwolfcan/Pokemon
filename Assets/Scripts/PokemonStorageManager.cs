using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class PokemonStorageManager : MonoBehaviour
{
    public static PokemonStorageManager Instance { get; private set; }

    public PokemonParty PlayerParty { get; private set; }
    public PCStorage PcStorage { get; private set; }

    public bool HasCaughtFirstPokemon { get; private set; }

    public event Action OnPartyChanged;
    public event Action OnPcBoxChanged;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);

        PlayerParty = new PokemonParty();
        PcStorage = new PCStorage();
    }

    // ---- Save/Load bridge ----
    public PlayerSaveData GetSaveData(PlayerSaveData settingsAndInventory = null)
    {
        var save = settingsAndInventory ?? new PlayerSaveData();
        save.partyDTO = new PartyDTO();
        for (int i = 0; i < PlayerParty.MaxCapacity; i++)
        {
            var p = PlayerParty.GetAt(i);
            save.partyDTO.slots.Add(p);
            save.partyDTO.occupied.Add(p != null);
        }
        save.pcDTO = new PCDTO();
        for (int b = 0; b < PcStorage.UnlockedBoxCount; b++)
        {
            var box = PcStorage.Boxes[b];
            var dto = new BoxDTO();
            for (int i = 0; i < box.MaxCapacity; i++)
            {
                var p = box.GetAt(i);
                dto.slots.Add(p);
                dto.occupied.Add(p != null);
            }
            save.pcDTO.boxes.Add(dto);
        }
        save.party = PlayerParty.ToList();  // legado
        save.pcBoxes = PcStorage.ToSave();    // legado
        save.hasCaughtFirstPokemon = HasCaughtFirstPokemon;
        return save;
    }

    public void LoadFromSave(PlayerSaveData save)
    {
        PlayerParty = new PokemonParty();
        PcStorage = new PCStorage();
        HasCaughtFirstPokemon = save?.hasCaughtFirstPokemon ?? false;

        if (save?.partyDTO != null && save.partyDTO.slots != null && save.partyDTO.occupied != null && save.partyDTO.slots.Count > 0)
            PlayerParty.SetFromMasked(save.partyDTO.slots, save.partyDTO.occupied);
        else
            PlayerParty.SetFromList(save?.party ?? new List<PokemonInstance>());

        // **compactar al cargar** para garantizar huecos al final
        PlayerParty.Compact();

        if (save?.pcDTO != null && save.pcDTO.boxes != null && save.pcDTO.boxes.Count > 0)
        {
            var dtoBoxes = new List<(List<PokemonInstance>, List<bool>)>();
            foreach (var b in save.pcDTO.boxes)
                dtoBoxes.Add((b.slots ?? new List<PokemonInstance>(), b.occupied ?? new List<bool>()));
            PcStorage.SetFromMasked(dtoBoxes);
        }
        else
        {
            PcStorage.SetFromSave(save?.pcBoxes);
        }

        RecalcUnlocks();
        InvokeAllChanged();
    }

    private void RecalcUnlocks()
    {
        int total = PlayerParty.CountNonNull + PcStorage.Boxes.Sum(b => b.CountNonNull);
        PcStorage.UpdateUnlocks(total);
    }

    private void InvokeAllChanged()
    {
        OnPartyChanged?.Invoke();
        OnPcBoxChanged?.Invoke();
    }

    // ---- Public API ----
    public void CapturePokemon(PokemonInstance wild)
    {
        if (wild == null) return;

        bool added = PlayerParty.TryAdd(wild);
        if (!added)
        {
            if (!PcStorage.AddToFirstAvailable(wild))
                Debug.LogWarning("¡PC lleno! No hay hueco para el nuevo Pokémon.");
        }

        if (!HasCaughtFirstPokemon) HasCaughtFirstPokemon = true;

        // Party ya añade al primer hueco, pero compactar aquí no duele
        PlayerParty.Compact();

        RecalcUnlocks();
        InvokeAllChanged();
        SaveManager.Instance?.ManualSave();
    }

    public void MoveBetween(IPokemonStorage fromStorage, int fromIndex, IPokemonStorage toStorage, int toIndex)
    {
        if (fromStorage == null || toStorage == null) return;
        if (!fromStorage.IsIndexValid(fromIndex) || !toStorage.IsIndexValid(toIndex)) return;

        // Regla: mínimo 1 en party después del tutorial
        if (HasCaughtFirstPokemon && fromStorage is PokemonParty && PlayerParty.CountNonNull <= 1)
        {
            var pcheck = fromStorage.GetAt(fromIndex);
            if (pcheck != null)
            {
                Debug.Log("No puedes dejar la party vacía.");
                return;
            }
        }

        if (ReferenceEquals(fromStorage, toStorage))
        {
            if (fromStorage is PokemonParty pp) pp.Swap(fromIndex, toIndex);
            else if (fromStorage is PCBox pb) pb.Swap(fromIndex, toIndex);
        }
        else
        {
            var p = fromStorage.RemoveAt(fromIndex);
            if (p == null) return;

            if (toStorage.TryInsertAt(toIndex, p, out var displaced))
            {
                if (displaced != null)
                {
                    // devolver el desplazado al origen
                    if (!fromStorage.TryInsertAt(fromIndex, displaced, out _))
                    {
                        if (!fromStorage.TryAdd(displaced))
                        {
                            if (fromStorage is PokemonParty) PcStorage.AddToFirstAvailable(displaced);
                            else PlayerParty.TryAdd(displaced);
                        }
                    }
                }
            }
            else
            {
                // no se pudo colocar; revert
                fromStorage.TryInsertAt(fromIndex, p, out _);
                Debug.Log("No se pudo colocar en el destino.");
            }
        }

        // **compactar solo party** (origen y/o destino)
        if (fromStorage is PokemonParty p1) p1.Compact();
        if (toStorage is PokemonParty p2) p2.Compact();

        RecalcUnlocks();
        InvokeAllChanged();
        SaveManager.Instance?.ManualSave();
    }

    public void ReleaseFrom(IPokemonStorage storage, int index)
    {
        if (storage == null || !storage.IsIndexValid(index)) return;

        if (HasCaughtFirstPokemon && storage is PokemonParty && PlayerParty.CountNonNull <= 1)
        {
            Debug.Log("No puedes liberar tu único Pokémon del equipo.");
            return;
        }

        var p = storage.RemoveAt(index);
        if (p != null)
        {
            if (storage is PokemonParty party) party.Compact(); // **compactar tras liberar**
            Debug.Log($"{p.species.pokemonName} liberado.");
            RecalcUnlocks();
            InvokeAllChanged();
            SaveManager.Instance?.ManualSave();
        }
    }
}
