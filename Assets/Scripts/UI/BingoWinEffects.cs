using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem; // new Input System

[DisallowMultipleComponent]
public class BingoWinEffects : MonoBehaviour
{
    [Header("Refs")]
    public BingoBoardUI board;
    public GameObject bingoPanel;
    public GameObject gameEndPanel;

    [Header("Shake Settings")]
    public float shakeDuration = 1.5f;
    public float posAmplitude = 6f;
    public float rotAmplitude = 7f;
    public float scaleAmplitude = 0.06f;
    public float frequency = 9f;
    public Color flashColor = new Color(0.6f, 1f, 0.6f, 1f);
    public float flashLerp = 0.55f;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip winnerSound;

    [Header("Slow Motion")]
    [Range(0.01f, 1f)] public float slowScale = 0.15f;

    [Header("Cursor on Win")]
    public bool showCursorOnWin = true;
    public CursorLockMode lockModeOnWin = CursorLockMode.None;

    [Header("Cheat")]
    public Key cheatKeyFallback = Key.P;
    public int cheatColumn = 0;
    public Color cheatTextColor = Color.green;

    bool _won;

    void Reset() { if (!board) board = GetComponentInChildren<BingoBoardUI>(); }

    void Awake()
    {
        if (bingoPanel) bingoPanel.SetActive(false);
        if (gameEndPanel) gameEndPanel.SetActive(false);
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    void Update()
    {
        bool cheatPressed = false;
        if (Keyboard.current != null) cheatPressed = Keyboard.current.pKey.wasPressedThisFrame;
        else cheatPressed = Input.GetKeyDown((KeyCode)cheatKeyFallback);

        if (!_won && cheatPressed)
        {
            CompleteColumnAndWin(cheatColumn);
            return;
        }

        if (_won || !board) return;

        for (int r = 0; r < board.rows; r++)
            if (IsFullRow(r)) { Win(CellsRow(r)); return; }

        for (int c = 0; c < board.cols; c++)
            if (IsFullCol(c)) { Win(CellsCol(c)); return; }

        if (IsFullMainDiag()) { Win(CellsMainDiag()); return; }
        if (IsFullAntiDiag()) { Win(CellsAntiDiag()); return; }
    }

    // --- cheat helper: mark cells as found + green ---
    void CompleteColumnAndWin(int col)
    {
        col = Mathf.Clamp(col, 0, board.cols - 1);
        for (int r = 0; r < board.rows; r++)
        {
            board.MarkFound(r, col);
            TintBoardLabelText(board, board.CurrentBoard[r * board.cols + col], cheatTextColor);
        }
        Win(CellsCol(col));
    }

    // tint helper copied from ItemHoldTracker (no dependency)
    static void TintBoardLabelText(BingoBoardUI board, string itemName, Color c)
    {
        if (!board || string.IsNullOrEmpty(itemName)) return;

        var root = board.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            var label = root.GetChild(i).Find("Label")?.GetComponent<Text>();
            if (!label) continue;

            if (string.Equals(label.text?.Trim(), itemName, System.StringComparison.OrdinalIgnoreCase))
            {
                label.color = c;
                break;
            }
        }
    }

    bool IsFullRow(int r)
    {
        for (int c = 0; c < board.cols; c++)
            if (!board.IsCellCompleted(r, c)) return false;
        return true;
    }
    bool IsFullCol(int c)
    {
        for (int r = 0; r < board.rows; r++)
            if (!board.IsCellCompleted(r, c)) return false;
        return true;
    }
    bool IsFullMainDiag()
    {
        int n = Mathf.Min(board.rows, board.cols);
        for (int i = 0; i < n; i++)
            if (!board.IsCellCompleted(i, i)) return false;
        return true;
    }
    bool IsFullAntiDiag()
    {
        int n = Mathf.Min(board.rows, board.cols);
        int last = board.cols - 1;
        for (int r = 0; r < n; r++)
            if (!board.IsCellCompleted(r, last - r)) return false;
        return true;
    }

    List<RectTransform> CellsRow(int r)
    {
        var list = new List<RectTransform>(board.cols);
        for (int c = 0; c < board.cols; c++) list.Add(board.GetCellRect(r, c));
        return list;
    }
    List<RectTransform> CellsCol(int c)
    {
        var list = new List<RectTransform>(board.rows);
        for (int r = 0; r < board.rows; r++) list.Add(board.GetCellRect(r, c));
        return list;
    }
    List<RectTransform> CellsMainDiag()
    {
        int n = Mathf.Min(board.rows, board.cols);
        var list = new List<RectTransform>(n);
        for (int i = 0; i < n; i++) list.Add(board.GetCellRect(i, i));
        return list;
    }
    List<RectTransform> CellsAntiDiag()
    {
        int n = Mathf.Min(board.rows, board.cols);
        int last = board.cols - 1;
        var list = new List<RectTransform>(n);
        for (int r = 0; r < n; r++) list.Add(board.GetCellRect(r, last - r));
        return list;
    }

    void Win(List<RectTransform> cells)
    {
        _won = true;
        if (bingoPanel) bingoPanel.SetActive(true);

        // slow motion
        Time.timeScale = slowScale;
        Time.fixedDeltaTime = 0.02f * slowScale;

        // play SFX
        if (sfxSource && winnerSound) sfxSource.PlayOneShot(winnerSound);

        // show mouse cursor for menus
        if (showCursorOnWin)
        {
            Cursor.lockState = lockModeOnWin;  // usually None
            Cursor.visible = true;
        }

        StartCoroutine(ShakeAndEnd(cells));
    }

    IEnumerator ShakeAndEnd(List<RectTransform> cells)
    {
        yield return ShakeCells(cells);
        yield return new WaitForSecondsRealtime(3f);
        if (bingoPanel) bingoPanel.SetActive(false);
        if (gameEndPanel) gameEndPanel.SetActive(true);
    }

    IEnumerator ShakeCells(List<RectTransform> cells)
    {
        var texts = new List<TMP_Text>(cells.Count);
        var pos0 = new List<Vector2>(cells.Count);
        var rot0 = new List<Quaternion>(cells.Count);
        var scale0 = new List<Vector3>(cells.Count);
        var color0 = new List<Color>(cells.Count);

        for (int i = 0; i < cells.Count; i++)
        {
            var rt = cells[i];
            if (!rt) continue;
            pos0.Add(rt.anchoredPosition);
            rot0.Add(rt.localRotation);
            scale0.Add(rt.localScale);
            TMP_Text t = rt.GetComponentInChildren<TMP_Text>(true);
            texts.Add(t);
            color0.Add(t ? t.color : Color.white);
        }

        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < shakeDuration)
        {
            float t = Time.unscaledTime - t0;
            float s = Mathf.Sin(t * Mathf.PI * 2f * frequency);
            for (int i = 0; i < cells.Count; i++)
            {
                var rt = cells[i];
                if (!rt) continue;
                rt.anchoredPosition = pos0[i] + new Vector2(s * posAmplitude, 0);
                rt.localRotation = rot0[i] * Quaternion.Euler(0, 0, s * rotAmplitude);
                rt.localScale = scale0[i] * (1f + s * scaleAmplitude);
                var txt = texts[i];
                if (txt)
                    txt.color = Color.Lerp(color0[i], flashColor, flashLerp * (0.5f + 0.5f * s));
            }
            yield return null;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            var rt = cells[i];
            if (!rt) continue;
            rt.anchoredPosition = pos0[i];
            rt.localRotation = rot0[i];
            rt.localScale = scale0[i];
            var txt = texts[i];
            if (txt) txt.color = color0[i];
        }
    }
}
