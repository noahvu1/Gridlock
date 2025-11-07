using System.Collections.Generic;
using UnityEngine;

public class ItemSpawnRandomizer : MonoBehaviour
{
    public Transform itemsParent;   // scene parent with item INSTANCES
    public Transform spawnsParent;  // scene parent with spawn points
    public bool uniqueSpawns = true; // if true, each spawn used once (then we cycle)

    void Start()
    {
        if (!itemsParent || !spawnsParent) { Debug.LogError("[Randomizer] Assign parents."); return; }

        // collect items (scene objects)
        var items = new List<Transform>();
        foreach (Transform t in itemsParent) if (t.gameObject.scene.IsValid()) items.Add(t);

        // collect spawns
        var spawns = new List<Transform>();
        foreach (Transform s in spawnsParent) if (s.gameObject.scene.IsValid()) spawns.Add(s);

        if (items.Count == 0 || spawns.Count == 0) { Debug.LogWarning("[Randomizer] Need items and spawns."); return; }

        // shuffle spawns
        for (int i = spawns.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spawns[i], spawns[j]) = (spawns[j], spawns[i]);
        }

        int used = 0;
        for (int i = 0; i < items.Count; i++)
        {
            // pick spawn index
            int idx = uniqueSpawns ? Mathf.Min(i, spawns.Count - 1) : Random.Range(0, spawns.Count);
            var spot = spawns[idx];

            // place
            var it = items[i];
            it.SetPositionAndRotation(spot.position, spot.rotation);
            var rb = it.GetComponent<Rigidbody>();
            if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

            Debug.Log($"[Randomizer] {it.name} -> {spot.name}");

            // if unique and we ran out of spawns, start cycling
            if (uniqueSpawns && i >= spawns.Count - 1) used++;
        }

        if (uniqueSpawns && items.Count > spawns.Count)
            Debug.LogWarning($"[Randomizer] More items ({items.Count}) than spawns ({spawns.Count}). Last items reuse the final shuffled spawns.");
    }
}
