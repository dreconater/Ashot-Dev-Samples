/*
 * VideoController
 * ---------------
 * Simple, robust controller for a Unity (Meta Quest) VideoPlayer.
 * - Play / Pause toggle with dedicated buttons
 * - 10-second forward/backward seeking
 * - Scrub with a normalized slider (0..1)
 * - Time label shows "MM:SS / MM:SS"
 * 
 * Wire the serialized fields in the Inspector.
 * Buttons and slider listeners are registered automatically on enable.
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class VideoController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private VideoPlayer mediaPlayer;

    [Header("UI")]
    [SerializeField] private Slider seekSlider;        // Normalized 0..1
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button forwardButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text timeLabel;

    private const double SeekStepSeconds = 10.0;

    private bool wasPlayingOnScrub;
    private bool isScrubbing;
    private float lastAppliedSliderValue = -1f;

    private void OnEnable()
    {
        if (playButton) playButton.onClick.AddListener(HandlePlay);
        if (pauseButton) pauseButton.onClick.AddListener(HandlePause);
        if (forwardButton) forwardButton.onClick.AddListener(HandleForward);
        if (backButton) backButton.onClick.AddListener(HandleBackward);

        if (seekSlider)
        {
            seekSlider.minValue = 0f;
            seekSlider.maxValue = 1f;
            seekSlider.onValueChanged.AddListener(HandleSeekSliderChanged);
        }

        SyncPlayPauseUI();
    }

    private void OnDisable()
    {
        if (playButton) playButton.onClick.RemoveListener(HandlePlay);
        if (pauseButton) pauseButton.onClick.RemoveListener(HandlePause);
        if (forwardButton) forwardButton.onClick.RemoveListener(HandleForward);
        if (backButton) backButton.onClick.RemoveListener(HandleBackward);

        if (seekSlider) seekSlider.onValueChanged.RemoveListener(HandleSeekSliderChanged);
    }

    private void Update()
    {
        if (!mediaPlayer || mediaPlayer.length <= 0d) return;

        if (!isScrubbing)
        {
            double t = mediaPlayer.time;
            double len = mediaPlayer.length;

            float normalized = Mathf.Clamp01((float)(t / len));
            lastAppliedSliderValue = normalized;

            if (seekSlider) seekSlider.value = normalized;
            if (timeLabel) timeLabel.text = $"{FormatMMSS(t)} / {FormatMMSS(len)}";
        }

        // Keep UI state in sync if user uses external controls
        SyncPlayPauseUI();
    }

    // --- Button handlers ---

    private void HandlePlay()
    {
        if (!mediaPlayer) return;

        mediaPlayer.Play();
        SyncPlayPauseUI();
    }

    private void HandlePause()
    {
        if (!mediaPlayer) return;

        mediaPlayer.Pause();
        SyncPlayPauseUI();
    }

    private void HandleForward()
    {
        if (!mediaPlayer) return;

        double newTime = Mathf.Min((float)(mediaPlayer.time + SeekStepSeconds), (float)mediaPlayer.length);
        mediaPlayer.time = newTime;
    }

    private void HandleBackward()
    {
        if (!mediaPlayer) return;

        double newTime = Mathf.Max((float)(mediaPlayer.time - SeekStepSeconds), 0f);
        mediaPlayer.time = newTime;
    }

    // --- Slider handlers (hook these from EventTrigger or UI events if needed) ---

    // Call from UI: OnPointerDown for the slider handle
    public void BeginScrub()
    {
        if (!mediaPlayer) return;

        isScrubbing = true;
        wasPlayingOnScrub = mediaPlayer.isPlaying;
        if (wasPlayingOnScrub) mediaPlayer.Pause();
    }

    // Called automatically via onValueChanged
    private void HandleSeekSliderChanged(float value)
    {
        if (!mediaPlayer || !seekSlider) return;

        if (Mathf.Approximately(value, lastAppliedSliderValue)) return;

        double target = value * mediaPlayer.length;
        mediaPlayer.time = target;
        lastAppliedSliderValue = value;

        if (timeLabel) timeLabel.text = $"{FormatMMSS(mediaPlayer.time)} / {FormatMMSS(mediaPlayer.length)}";
    }

    // Call from UI: OnPointerUp for the slider handle
    public void EndScrub()
    {
        if (!mediaPlayer) return;

        if (wasPlayingOnScrub) mediaPlayer.Play();
        wasPlayingOnScrub = false;
        isScrubbing = false;
        SyncPlayPauseUI();
    }

    // --- Helpers ---

    private void SyncPlayPauseUI()
    {
        bool playing = mediaPlayer && mediaPlayer.isPlaying;

        if (playButton) playButton.gameObject.SetActive(!playing);
        if (pauseButton) pauseButton.gameObject.SetActive(playing);
    }

    private static string FormatMMSS(double secondsTotal)
    {
        if (secondsTotal < 0d || double.IsInfinity(secondsTotal) || double.IsNaN(secondsTotal))
            return "00:00";

        int minutes = Mathf.FloorToInt((float)(secondsTotal / 60d));
        int seconds = Mathf.FloorToInt((float)(secondsTotal % 60d));
        return $"{minutes:D2}:{seconds:D2}";
    }
}
