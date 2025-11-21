using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSpawnRandomizer : MonoBehaviour
{
    [Header("Scene Parents")]
    public Transform itemsParent;    // your Items parent (scene objects parked outside the map)
    public Transform spawnsParent;   // your ItemSpawns parent (spawn points)

    [Header("Rules")]
    public float unclaimedLifetime = 120f; // max time on the map
    public float respawnDelay = 2f;        // delay before next item after claim/timeout
    public bool parkSetInactive = false;   // optional: set parked items inactive when returned

    // runtime
    readonly List<GameObject> _items = new();   // scene items (some may get destroyed on claim)
    readonly List<Transform> _spawns = new();   // spawn points
    readonly Dictionary<GameObject, Pose> _parkPoses = new(); // original parked pose

    GameObject _active;  // currently spawned item

    void Start()
    {
        // need parents
        if (!itemsParent || !spawnsParent)
        {
            Debug.LogError("[Randomizer] Assign itemsParent and spawnsParent.");
            AnnouncementsManager.Instance?.Announce("Spawner not set up. Assign items & spawns.");
            return;
        }

        // collect scene items + remember their parked pose
        foreach (Transform t in itemsParent)
        {
            if (!t || !t.gameObject.scene.IsValid()) continue;
            var go = t.gameObject;
            _items.Add(go);
            _parkPoses[go] = new Pose(t.position, t.rotation);
        }

        // collect spawn points
        foreach (Transform s in spawnsParent)
        {
            if (!s || !s.gameObject.scene.IsValid()) continue;
            _spawns.Add(s);
        }

        // bail if nothing to do
        if (_items.Count == 0 || _spawns.Count == 0)
        {
            Debug.LogWarning("[Randomizer] Need items and spawns.");
            AnnouncementsManager.Instance?.Announce("No items or spawns found.");
            return;
        }

        // set parked active state
        if (parkSetInactive)
            foreach (var it in _items) if (it) it.SetActive(false);

        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            // keep list clean
            _items.RemoveAll(go => go == null);
            if (_items.Count == 0)
            {
                Debug.LogWarning("[Randomizer] No items left (likely all claimed/destroyed).");
                AnnouncementsManager.Instance?.Announce("All items claimed.");
                yield break;
            }

            // pick item and spawn
            var item = _items[Random.Range(0, _items.Count)];
            if (item == null) continue;

            var spawn = _spawns[Random.Range(0, _spawns.Count)];

            // activate if needed
            if (parkSetInactive && !item.activeSelf) item.SetActive(true);

            // place at spawn and reset rb
            item.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var rb = item.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            _active = item;
            string pretty = NormalizeName(_active.name);
            Debug.Log($"[Randomizer] 📣 Spawned {_active.name} @ {spawn.name}");
            AnnouncementsManager.Instance?.Announce($"{pretty} has spawned.");

            // wait for claim (destroy) or timeout
            float t = 0f;
            while (t < unclaimedLifetime && _active != null)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // timeout -> return to parked spot
            if (_active != null)
            {
                if (_parkPoses.TryGetValue(_active, out var pose))
                {
                    _active.transform.SetPositionAndRotation(pose.position, pose.rotation);
                }
                if (parkSetInactive) _active.SetActive(false);

                Debug.Log($"[Randomizer] ⏳ {_active.name} not claimed — returned to parked spot.");
                AnnouncementsManager.Instance?.Announce($"{pretty} despawned.");
            }
            else
            {
                // claimed (destroyed by ItemHoldTracker)
                Debug.Log("[Randomizer] ✅ Item claimed (destroyed).");
                AnnouncementsManager.Instance?.Announce($"{pretty} has been claimed!");
            }

            _active = null;

            // delay before next
            if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);
        }
    }

    static string NormalizeName(string n)
    {
        // turn "frying_pan (Clone)" into "frying pan"
        if (string.IsNullOrEmpty(n)) return n;
        n = n.Replace("(Clone)", "").Trim();
        return n.Replace('_', ' ').ToLowerInvariant();
    }
}
