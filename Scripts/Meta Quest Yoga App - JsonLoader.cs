/*
Meta Quest (Android) app component.

Loads yoga courses from StreamingAssets/yoga_courses.json and renders them in a
scrollable UI. Each course button opens a video/details view. The script also
handles smooth horizontal scrolling and simple page navigation (content, video,
contact form, credits). On Quest (Android) it reads StreamingAssets via
UnityWebRequest; in Editor/PC it uses File IO.
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

[System.Serializable]
public class YogaCourse
{
    public string Title;
    public string Description;
    public string ThumbnailPath; // Resources path, e.g. "Thumbnails/pose1"
    public string Url;           // Video URL or local path for your player
}

[System.Serializable]
public class YogaCourseList
{
    public List<YogaCourse> Courses;
}

public class JsonLoader : MonoBehaviour
{
    [Header("Scroller")]
    public ScrollRect Scroller;
    public Button PrevButton;
    public Button NextButton;
    [Range(0.01f, 1f)] public float ScrollStep = 0.1f;
    [Range(0.05f, 2f)] public float ScrollDuration = 0.5f;
    private Coroutine _scrollCo;

    [Header("Course Buttons")]
    public Button[] CourseButtons;

    [Header("Pages")]
    public GameObject ContentPage;
    public VideoPart VideoPage;
    public Animator CanvasAnimator;
    public GameObject ContactForm;
    public GameObject CreditsPage;

    [Header("Nav Buttons")]
    public Button BackToContentButton;
    public Button ContactButton;
    public Button CreditsButton;
    public Button CloseCreditsButton;

    private YogaCourseList _courses = new YogaCourseList { Courses = new List<YogaCourse>() };

    private void Awake()
    {
        StartCoroutine(LoadCourses());
        SetupStaticNav();
    }

    private IEnumerator LoadCourses()
    {
        string fileName = "yoga_courses.json";
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var req = UnityWebRequest.Get(path))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load courses from StreamingAssets: {req.error}");
                yield break;
            }
            ParseCourses(req.downloadHandler.text);
        }
#else
        if (!File.Exists(path))
        {
            Debug.LogError("Courses JSON not found at: " + path);
            yield break;
        }
        ParseCourses(File.ReadAllText(path));
        yield return null;
#endif

        WireCourseButtons();
    }

    private void ParseCourses(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Empty courses JSON.");
            return;
        }

        try
        {
            _courses = JsonUtility.FromJson<YogaCourseList>(json) ?? new YogaCourseList { Courses = new List<YogaCourse>() };
        }
        catch (System.Exception e)
        {
            Debug.LogError("JSON parse error: " + e.Message);
            _courses = new YogaCourseList { Courses = new List<YogaCourse>() };
        }
    }

    private void SetupStaticNav()
    {
        NextButton.onClick.AddListener(ScrollNext);
        PrevButton.onClick.AddListener(ScrollPrev);

        BackToContentButton.onClick.AddListener(() => SwitchPage(ContentPage));
        ContactButton.onClick.AddListener(() => SwitchPage(ContactForm));
        CreditsButton.onClick.AddListener(() => SwitchPage(CreditsPage));
        CloseCreditsButton.onClick.AddListener(() => SwitchPage(ContentPage));
    }

    private void WireCourseButtons()
    {
        for (int i = 0; i < CourseButtons.Length; i++)
        {
            int idx = i;
            bool hasCourse = idx < _courses.Courses.Count;

            var btn = CourseButtons[i];
            btn.onClick.RemoveAllListeners();
            btn.interactable = hasCourse;

            if (hasCourse)
                btn.onClick.AddListener(() => OpenCourse(idx));
        }
    }

    private void OpenCourse(int index)
    {
        if (index < 0 || index >= _courses.Courses.Count) return;

        var c = _courses.Courses[index];
        var yoga = new Yoga
        {
            Title = c.Title,
            Caption = c.Description,
            Url = c.Url,
            Sprite = LoadSprite(c.ThumbnailPath),
            Free = true,
            Consumable = 0
        };

        StartCoroutine(PlayToVideo(yoga));
    }

    private IEnumerator PlayToVideo(Yoga yoga)
    {
        yield return HideContent();
        yield return ShowVideo(yoga);
    }

    private void SwitchPage(GameObject target)
    {
        ContentPage.SetActive(target == ContentPage);
        ContactForm.SetActive(target == ContactForm);
        CreditsPage.SetActive(target == CreditsPage);
        VideoPage.gameObject.SetActive(false);
    }

    private IEnumerator ShowVideo(Yoga yoga)
    {
        yield return new WaitForSeconds(0.35f);
        VideoPage.gameObject.SetActive(true);
        VideoPage.Setup(yoga);
        var anim = VideoPage.GetComponent<Animator>();
        if (anim) anim.Play("CanvasShow", 0, 0);
    }

    private IEnumerator HideContent()
    {
        if (CanvasAnimator) CanvasAnimator.Play("CanvasHide", 0, 0);
        yield return new WaitForSeconds(0.35f);
        ContentPage.SetActive(false);
    }

    private void ScrollNext() => StartScroll(Scroller.horizontalNormalizedPosition + ScrollStep);
    private void ScrollPrev() => StartScroll(Scroller.horizontalNormalizedPosition - ScrollStep);

    private void StartScroll(float target)
    {
        if (_scrollCo != null) StopCoroutine(_scrollCo);
        _scrollCo = StartCoroutine(ScrollTo(Mathf.Clamp01(target)));
    }

    private IEnumerator ScrollTo(float target)
    {
        float start = Scroller.horizontalNormalizedPosition;
        float t = 0f;

        while (t < ScrollDuration)
        {
            Scroller.horizontalNormalizedPosition = Mathf.Lerp(start, target, t / ScrollDuration);
            t += Time.deltaTime;
            yield return null;
        }

        Scroller.horizontalNormalizedPosition = target;
    }

    private Sprite LoadSprite(string path) =>
        string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
}
