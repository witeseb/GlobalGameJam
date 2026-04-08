using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutsceneManager : MonoBehaviour
{
    [System.Serializable]
    public class Slide
    {
        public Sprite image;
        public AudioClip sound;
    }

    public Image slideImage;
    public Image blackOverlay;
    public CanvasGroup canvasGroup;
    public Slide[] slides;
    public string nextSceneName;

    private int currentIndex = 0;
    private AudioSource audioSource;
    private bool isTransitioning = false;
    public RectTransform iris;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        ShowSlide(0);
        PlaySound(0);
        canvasGroup.alpha = 1;
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && !isTransitioning)
        {
            NextSlide();
        }
    }

    void ShowSlide(int index)
    {
        slideImage.sprite = slides[index].image;
    }

    void PlaySound(int index)
    {
        if (slides[index].sound != null)
        {
            audioSource.PlayOneShot(slides[index].sound);
        }
    }

    public void NextSlide()
    {
        StartCoroutine(Transition());
    }

    IEnumerator Transition()
    {
        isTransitioning = true;

        // Fade to black
        yield return FadeOverlay(1, 0.4f);
        yield return new WaitForSeconds(0.2f);
        currentIndex++;

        if (currentIndex >= slides.Length)
        {
            yield return IrisOut();   
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }

        ShowSlide(currentIndex);
        PlaySound(currentIndex);

        // Fade back in
        yield return FadeOverlay(0, 0.4f);

        isTransitioning = false;
    }

    IEnumerator FadeOverlay(float target, float duration)
    {
        float start = blackOverlay.color.a;
        float time = 0;
        Color color = blackOverlay.color;

        while (time < duration)
        {
            time += Time.deltaTime;
            color.a = Mathf.Lerp(start, target, time / duration);
            blackOverlay.color = color;
            yield return null;
        }

        color.a = target;
        blackOverlay.color = color;
    }

    IEnumerator IrisOut()
    {
        float time = 0f;
        float duration = 0.8f;

        // START = small 
        Vector3 start = new Vector3(22f, 22f, 1f);

        // END = big 
        Vector3 end = Vector3.zero;

        while (time < duration)
        {
            time += Time.deltaTime; 
            float t = time / duration;
            t = t * t * (3f - 2f * t); 
            iris.localScale = Vector3.Lerp(start, end, t);
            yield return null;
        }

        iris.localScale = end;
    }
}