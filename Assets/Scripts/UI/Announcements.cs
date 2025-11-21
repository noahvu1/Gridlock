using UnityEngine;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class AnnouncementsManager : MonoBehaviour
{
    public static AnnouncementsManager Instance;

    [Header("UI")]
    public TMP_Text announcementsText;  // drag Announcements_Text here
    public float clearAfterSeconds = 3f; // how long before fade starts
    public float fadeDuration = 1f;      // how long the fade lasts

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Announce(string message)
    {
        if (!announcementsText) return;

        announcementsText.text = message;
        announcementsText.alpha = 1f; // fully visible
        StopAllCoroutines();
        if (clearAfterSeconds > 0f) StartCoroutine(FadeOutRoutine());
    }

    IEnumerator FadeOutRoutine()
    {
        // wait before starting fade
        yield return new WaitForSeconds(clearAfterSeconds);

        float t = 0f;
        float startAlpha = announcementsText.alpha;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, 0f, t / fadeDuration);
            announcementsText.alpha = a;
            yield return null;
        }

        announcementsText.alpha = 0f;
        announcementsText.text = "";
    }
}