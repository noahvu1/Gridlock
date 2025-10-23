using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class BingoBoardUI : MonoBehaviour
{
    [Header("Pool of possible item names (>=24 is best)")]
    // List of all possible items that can appear on the bingo board
    public List<string> itemPool = new List<string> {
        "babyrake","backpack","bitcoin","bottle","bigruler","bomb",
        "cup","crown","dynamite","frying_pan","goldstatue","hat",
        "headphones","mine","plus","pot","piggybank","smiley",
        "shoe","tape","watering_can","snorkel","smartphone","tire"
    };

    [Header("Board Settings")]
    public int rows = 5;
    public int cols = 5;

    [Tooltip("If true, the board shuffles on Awake using StartSeed or a random seed.")]
    public bool shuffleOnAwake = true;

    [Tooltip("If >= 0, this seed is used. If < 0, a random seed is generated on Awake.")]
    public int startSeed = -1;

    [Tooltip("Optional: a checkmark sprite to show when a cell is completed.")]
    public Sprite checkmarkSprite;

    [Tooltip("Color tint applied to completed cells (in addition to checkmark).")]
    public Color completedTint = new Color(0.75f, 1f, 0.75f, 0.35f);

    [Header("Label Style")]
    public Color textColor = new Color(0.12f, 0.12f, 0.18f, 1f);
    public int minFontSize = 16;
    public int maxFontSize = 36;

    // References to UI components
    private GridLayoutGroup _grid;
    private RectTransform _rt;

    // The generated bingo board contents
    private string[] _boardItems;     // item name in each cell
    private bool[] _completed;        // whether each cell is completed
    private int _seedUsed;            // random seed used to generate board

    // Quick lookup from item name -> cell index
    private Dictionary<string, int> _indexByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Public read-only access to board info
    public IReadOnlyList<string> CurrentBoard => _boardItems;
    public int SeedUsed => _seedUsed;

    void Awake()
    {
        // Cache layout + setup grid
        _rt = GetComponent<RectTransform>();
        _grid = GetComponent<GridLayoutGroup>();
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = cols;
        _grid.childAlignment = TextAnchor.MiddleCenter;

        // Build a new random board when game starts
        if (shuffleOnAwake)
        {
            int seed = (startSeed >= 0) ? startSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            BuildNewBoard(seed);
        }
        else
        {
            // If not shuffling, just ensure cells exist (for editor viewing)
            EnsureCellsExist(rows * cols);
        }
    }

    /// <summary>
    /// Builds a new randomized bingo board using a seed.
    /// </summary>
    public void BuildNewBoard(int seed)
    {
        _seedUsed = seed;
        EnsureCellsExist(rows * cols);

        // Copy item pool and shuffle it
        var pool = new List<string>(itemPool);
        if (pool.Count == 0)
        {
            Debug.LogWarning("[BingoBoardUI] Item pool is empty. Board will be blank.");
            pool.Add("");
        }

        Shuffle(pool, seed);

        _boardItems = new string[rows * cols];
        _completed = new bool[rows * cols];
        _indexByItem.Clear();

        int center = (rows / 2) * cols + (cols / 2); // middle cell
        int takeIdx = 0;

        // Fill the 25 cells (skip center)
        for (int i = 0; i < _boardItems.Length; i++)
        {
            if (i == center)
            {
                // Leave center blank for the cat paw
                _boardItems[i] = null;
                _completed[i] = false;
                ApplyCellUI(i, "", false);
                continue;
            }

            // Pick next item from the shuffled list
            string nameToUse = pool[takeIdx % pool.Count];
            takeIdx++;

            _boardItems[i] = nameToUse;
            _completed[i] = false;

            // Save lookup: item name -> index
            if (!string.IsNullOrEmpty(nameToUse) && !_indexByItem.ContainsKey(nameToUse))
                _indexByItem[nameToUse] = i;

            // Update text label in UI
            ApplyCellUI(i, nameToUse, false);
        }
    }

    /// <summary>
    /// Marks a cell as completed by grid coordinates.
    /// </summary>
    public void MarkFound(int row, int col) => MarkFound(Index(row, col));

    /// <summary>
    /// Marks a cell as completed by index.
    /// </summary>
    public void MarkFound(int index)
    {
        if (!ValidIndex(index)) return;
        if (_boardItems == null) return;
        if (_boardItems[index] == null) return; // skip center
        if (_completed[index]) return; // already completed

        _completed[index] = true;
        ApplyCompletedUI(index, true); // visually update
    }

    /// <summary>
    /// Marks an item as completed by its name (case-insensitive).
    /// </summary>
    public void MarkFound(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return;
        if (_indexByItem.TryGetValue(itemName, out int idx))
            MarkFound(idx);
        else
            Debug.LogWarning($"[BingoBoardUI] Item '{itemName}' is not on this board (seed {SeedUsed}).");
    }

    /// <summary>
    /// Returns true if every cell (except center) is completed.
    /// </summary>
    public bool IsBoardComplete()
    {
        if (_completed == null) return false;
        int center = (rows / 2) * cols + (cols / 2);
        for (int i = 0; i < _completed.Length; i++)
        {
            if (i == center) continue;
            if (!_completed[i]) return false;
        }
        return true;
    }

    // -------------------- Helper functions --------------------

    // Make sure the correct number of cells exist in the grid
    void EnsureCellsExist(int target)
    {
        for (int i = transform.childCount; i < target; i++)
            CreateCell(i);

        for (int i = transform.childCount - 1; i >= target; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    // Create one cell (background + label + checkmark)
    void CreateCell(int index)
    {
        var go = new GameObject($"Cell_{index}", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        // Background
        var bg = go.AddComponent<Image>();
        bg.color = new Color(1,1,1,0);

        // Label (item name)
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)labelGO.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        var text = labelGO.GetComponent<Text>();
        text.text = "";
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minFontSize;
        text.resizeTextMaxSize = maxFontSize;

        // Checkmark overlay (starts hidden)
        var checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(go.transform, false);
        var crt = (RectTransform)checkGO.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

        var img = checkGO.GetComponent<Image>();
        img.sprite = checkmarkSprite;
        img.preserveAspect = true;
        img.color = Color.white;
        checkGO.SetActive(false);
    }

    // Update the text and background color for a cell
    void ApplyCellUI(int index, string label, bool completed)
    {
        var cell = transform.GetChild(index);
        var text = cell.Find("Label")?.GetComponent<Text>();
        var check = cell.Find("Checkmark")?.gameObject;
        var bg = cell.GetComponent<Image>();

        if (text) text.text = label ?? "";
        if (check) check.SetActive(completed);
        if (bg) bg.color = completed ? completedTint : new Color(1,1,1,0);
    }

    // Visually apply the "completed" state
    void ApplyCompletedUI(int index, bool completed)
    {
        var cell = transform.GetChild(index);
        var check = cell.Find("Checkmark")?.gameObject;
        var bg = cell.GetComponent<Image>();

        if (check) check.SetActive(completed && (checkmarkSprite != null || true));
        if (bg) bg.color = completed ? completedTint : new Color(1,1,1,0);
    }

    // Convert row/col to 1D index
    int Index(int row, int col) => row * cols + col;

    // Check index range
    bool ValidIndex(int i) => i >= 0 && i < rows * cols;

    // Shuffle helper for randomizing the items
    static void Shuffle<T>(IList<T> list, int seedVal)
    {
        var rng = new System.Random(seedVal);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
