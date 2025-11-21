using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemHoldTracker : MonoBehaviour
{
    [Header("Refs")]
    public ItemHolder holder;            // must expose CurrentHeld
    public BingoBoardUI bingoBoard;      // to mark found on the board

    [Header("Timing")]
    public float requiredHoldSeconds = 10f;

    [Header("HUD Progress Bar")]
    public RectTransform holdBarRect;    // drag your bar's RectTransform
    public GameObject holdBarRoot;       // optional parent for show/hide
    public bool countDown = true;        // if true, bar shrinks down over time

    [Header("Visuals")]
    public Color completedTextColor = Color.green;

    // runtime
    Transform current;
    float timer;
    float fullWidth;

    void Reset()
    {
        // quick auto-refs
        holder = GetComponent<ItemHolder>();
        if (!bingoBoard) bingoBoard = FindObjectOfType<BingoBoardUI>();
    }

    void Start()
    {
        // cache starting width
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

        // detect change in held item
        if (holder.CurrentHeld != current)
        {
            current = holder.CurrentHeld;
            timer = 0f;
            SetBarVisible(current != null);
            ResetBar();

            // pin bar bottom-center
            if (holdBarRect)
            {
                holdBarRect.anchorMin = holdBarRect.anchorMax = new Vector2(0.5f, 0.1f);
                holdBarRect.anchoredPosition = Vector2.zero;
            }

            // announcement on pickup
            if (current)
            {
                AnnouncementsManager.Instance?.Announce($"{NormalizeName(current.name)} picked up.");
            }
        }

        if (!current) return;

        // advance timer
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / Mathf.Max(0.0001f, requiredHoldSeconds));

        // update width
        float ratio = countDown ? (1f - progress) : progress;
        SetBar(ratio);

        // claim when full
        if (timer >= requiredHoldSeconds)
        {
            string itemName = NormalizeName(current.name);

            if (bingoBoard) bingoBoard.MarkFound(itemName);
            TintBoardLabelText(bingoBoard, itemName, completedTextColor);

            Transform toDestroy = current;
            holder.Drop();
            if (toDestroy) Destroy(toDestroy.gameObject);

            AnnouncementsManager.Instance?.Announce($"{itemName} has been claimed!");

            current = null;
            timer = 0f;
            SetBarVisible(false);
            ResetBar();
        }
    }

    void ResetBar()
    {
        // set bar back to full width
        if (!holdBarRect) return;

        if (fullWidth <= 0f)
            fullWidth = holdBarRect.rect.width > 0 ? holdBarRect.rect.width : 200f;

        holdBarRect.sizeDelta = new Vector2(fullWidth, holdBarRect.sizeDelta.y);
        holdBarRect.localScale = Vector3.one;
    }

    void SetBar(float ratio)
    {
        // resize bar by ratio 0..1
        if (!holdBarRect) return;
        float width = fullWidth * Mathf.Clamp01(ratio);
        holdBarRect.sizeDelta = new Vector2(width, holdBarRect.sizeDelta.y);
    }

    void SetBarVisible(bool visible)
    {
        // show/hide bar object
        if (!holdBarRect) return;

        if (visible)
        {
            holdBarRect.SetAsLastSibling();

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
        // turn "frying_pan (Clone)" into "frying pan"
        if (string.IsNullOrEmpty(n)) return n;
        n = n.Replace("(Clone)", "").Trim();
        return n.Replace('_', ' ').ToLowerInvariant();
    }

    static void TintBoardLabelText(BingoBoardUI board, string itemName, Color c)
    {
        // color the label that matches itemName
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
