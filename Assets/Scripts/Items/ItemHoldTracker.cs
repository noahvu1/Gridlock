using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemHoldTracker : MonoBehaviour
{
    [Header("Refs")]
    public ItemHolder holder;
    public BingoBoardUI bingoBoard;

    [Header("Timing")]
    public float requiredHoldSeconds = 10f;

    [Header("HUD Progress Bar")]
    public RectTransform holdBarRect;   // drag your bar's RectTransform
    public GameObject holdBarRoot;      // optional parent for show/hide
    public bool countDown = true;       // if true, bar shrinks down over time

    [Header("Visuals")]
    public Color completedTextColor = Color.green;

    // runtime
    Transform current;
    float timer;
    float fullWidth;

    void Reset()
    {
        holder = GetComponent<ItemHolder>();
        if (!bingoBoard) bingoBoard = FindObjectOfType<BingoBoardUI>();
    }

    void Start()
    {
        // get the starting full width
        if (holdBarRect)
        {
            fullWidth = holdBarRect.rect.width > 0 ? holdBarRect.rect.width :
                        (holdBarRect.sizeDelta.x > 0 ? holdBarRect.sizeDelta.x : 200f);
        }
    }

    void OnEnable()
    {
        SetBarVisible(false);
        ResetBar();
    }

    void Update()
    {
        if (!holder) return;

        // detect item change
        if (holder.CurrentHeld != current)
        {
            current = holder.CurrentHeld;
            timer = 0f;
            SetBarVisible(current != null);
            ResetBar();

            // optional: position the bar near bottom-center
            if (holdBarRect)
            {
                holdBarRect.anchorMin = holdBarRect.anchorMax = new Vector2(0.5f, 0.1f);
                holdBarRect.anchoredPosition = Vector2.zero;
            }
        }

        if (!current) return;

        // advance timer
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / Mathf.Max(0.0001f, requiredHoldSeconds));

        // shrink or grow the width
        float ratio = countDown ? (1f - progress) : progress;
        SetBar(ratio);

        // finished holding
        if (timer >= requiredHoldSeconds)
        {
            string itemName = NormalizeName(current.name);

            if (bingoBoard) bingoBoard.MarkFound(itemName);
            TintBoardLabelText(bingoBoard, itemName, completedTextColor);

            Transform toDestroy = current;
            holder.Drop();
            if (toDestroy) Destroy(toDestroy.gameObject);

            current = null;
            timer = 0f;
            SetBarVisible(false);
            ResetBar();
        }
    }

    void ResetBar()
    {
        if (!holdBarRect) return;

        if (fullWidth <= 0f)
            fullWidth = holdBarRect.rect.width > 0 ? holdBarRect.rect.width : 200f;

        holdBarRect.sizeDelta = new Vector2(fullWidth, holdBarRect.sizeDelta.y);
        holdBarRect.localScale = Vector3.one;
    }

    void SetBar(float ratio)
    {
        if (!holdBarRect) return;
        float width = fullWidth * Mathf.Clamp01(ratio);
        holdBarRect.sizeDelta = new Vector2(width, holdBarRect.sizeDelta.y);
    }

    void SetBarVisible(bool visible)
    {
        if (!holdBarRect) return;

        if (visible)
        {
            // bring to front
            holdBarRect.SetAsLastSibling();

            // force visible (not faded or transparent)
            var cg = holdBarRect.GetComponentInParent<CanvasGroup>();
            if (cg) cg.alpha = 1f;

            var img = holdBarRect.GetComponent<Image>();
            if (img)
            {
                var col = img.color;
                col.a = 1f;
                img.color = col;
            }
        }

        if (holdBarRoot) holdBarRoot.SetActive(visible);
        else holdBarRect.gameObject.SetActive(visible);
    }

    static string NormalizeName(string n)
    {
        if (string.IsNullOrEmpty(n)) return n;
        n = n.Replace("(Clone)", "").Trim();
        return n.ToLowerInvariant();
    }

    static void TintBoardLabelText(BingoBoardUI board, string itemName, Color c)
    {
        if (!board || string.IsNullOrEmpty(itemName)) return;

        var root = board.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            var label = root.GetChild(i).Find("Label")?.GetComponent<Text>();
            if (!label) continue;

            if (string.Equals(label.text?.Trim(), itemName, StringComparison.OrdinalIgnoreCase))
            {
                label.color = c;
                break;
            }
        }
    }
}
