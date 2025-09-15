/*
 * OpenAIManager
 * -------------
 * VR-friendly chat/voice flow for a Meta Quest Unity app:
 * - Starts/ends voice capture (Whisper) on button press (OVR Button.Two or 'R' in Editor).
 * - Blocks capture if in-app audio/video is already playing & shows a brief warning.
 * - Classifies intent (music / meditation / general) from the user's utterance.
 * - Sends a short prompt to OpenAI Chat Completions, speaks the reply via TTS,
 *   and optionally opens the meditation page.
 * - Toggles simple “Listening”/“Thinking” UI boxes.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenAI;
using UnityEngine.Video;

public class OpenAIManager : MonoBehaviour
{
    [Header("OpenAI")]
    [SerializeField] private string apiKey;    // Set from env or Inspector
    [SerializeField] private string orgId;     // optional
    [SerializeField] private string chatModel = "gpt-3.5-turbo";

    private OpenAIApi _openAI;
    private readonly List<ChatMessage> _messages = new List<ChatMessage>();

    [Header("Speech / STT / TTS")]
    public WhisperSTT WhisperModule;

    [Header("UI")]
    public GameObject ListeningBox;
    public GameObject ThinkingBox;
    public GameObject WarningMessage;   // shows when VideoPlayer is active
    public GameObject WarningMessage2;  // shows when YouTube is active

    [Header("Media Blocks")]
    public VideoPlayer MedAudioPlayer;  // meditation audio/video
    public YouTubePlayer YouTubePlayer; // project-specific, assumed to expose AvPro.Control.IsPlaying()

    [Header("Navigation")]
    public MenuController Menu;

    [Header("Prompts")]
    [TextArea] public string Hello = "Hey there! I’m SereniSphere, your chill AI buddy. Ask me anything about meditation and relaxation. Let’s chat and unwind!";

    private bool _listeningActive;

    // --- Phrase banks ---
    static readonly string[] MusicPhrases =
    {
        "play ", "i want to listen to ", "i want to play ", "can you play ", "put on ",
        "let me hear ", "some relax music", "some chill music", "some calm music",
        "some peaceful music", "some meditation music", "some soft music"
    };

    static readonly string[] MeditationPhrases =
    {
        "show meditation","play meditation","guided meditation","start meditation","guide meditation",
        "start a meditation session","launch meditation","meditation video","meditation audio","begin meditation"
    };

    // --- Lifecycle ---

    private void Awake()
    {
        if (WhisperModule) WhisperModule.DoneTalking += OnTranscription;
        _openAI = string.IsNullOrWhiteSpace(orgId)
            ? new OpenAIApi(apiKey)
            : new OpenAIApi(apiKey, orgId);
    }

    private void Start()
    {
        SetBoxes(listening: false, thinking: false);
    }

    private void OnDestroy()
    {
        if (WhisperModule) WhisperModule.DoneTalking -= OnTranscription;
    }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Two)) StartRecording();
#if UNITY_EDITOR
        if (Input.GetKeyUp(KeyCode.R)) StartRecording();
#endif
    }

    // --- Input / Recording ---

    private void StartRecording()
    {
        if (IsMediaPlaying())
        {
            if (MedAudioPlayer && MedAudioPlayer.isPlaying)
                StartCoroutine(Flash(WarningMessage, 2.5f));
            else
                StartCoroutine(Flash(WarningMessage2, 2.5f));
            return;
        }

        var audio = GetComponent<AudioSource>();
        if (audio) audio.Play();

        if (_listeningActive) return;

        if (WhisperModule)
        {
            WhisperModule.StartRecording();
            _listeningActive = true;
            SetBoxes(listening: true, thinking: false);
        }
    }

    private bool IsMediaPlaying()
    {
        bool videoPlaying = MedAudioPlayer && MedAudioPlayer.isPlaying;
        bool ytPlaying = YouTubePlayer && YouTubePlayer.AvPro != null && YouTubePlayer.AvPro.Control.IsPlaying();
        return videoPlaying || ytPlaying;
    }

    private static IEnumerator Flash(GameObject go, float seconds)
    {
        if (!go) yield break;
        go.SetActive(true);
        yield return new WaitForSeconds(seconds);
        go.SetActive(false);
    }

    // --- UI ---

    private void SetBoxes(bool listening, bool thinking)
    {
        if (ListeningBox) ListeningBox.SetActive(listening);
        if (ThinkingBox) ThinkingBox.SetActive(thinking);
    }

    public void ShowThinkingBox()
    {
        SetBoxes(listening: false, thinking: true);
    }

    public void HideAllBox()
    {
        SetBoxes(listening: false, thinking: false);
        _listeningActive = false;
    }

    private IEnumerator HideBoxesSoon(float delay = 1f)
    {
        yield return new WaitForSeconds(delay);
        HideAllBox();
    }

    // --- STT callback ---

    private void OnTranscription(string text)
    {
        _listeningActive = false;
        SetBoxes(listening: false, thinking: true);
        AskChat(text);
    }

    // --- Intent detection ---

    private enum Intent { General, Music, Meditation }

    private static Intent DetectIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return Intent.General;

        string m = message.ToLowerInvariant();

        foreach (var p in MusicPhrases)
            if (m.Contains(p)) return Intent.Music;

        foreach (var p in MeditationPhrases)
            if (m.Contains(p)) return Intent.Meditation;

        return Intent.General;
    }

    // --- OpenAI chat ---

    public async void AskChat(string userText)
    {
        if (_openAI == null) { Debug.LogWarning("OpenAI not configured."); return; }

        var intent = DetectIntent(userText);
        string prompt;

        switch (intent)
        {
            case Intent.Music:
                prompt = $"Confirm the request and say the song name. Mention it will start in a few seconds, in a short and casual way: {userText}";
                if (YouTubePlayer) _ = YouTubePlayer.SearchAsync(userText);
                break;

            case Intent.Meditation:
                prompt = $"A user asked: {userText}. Respond with a short, friendly confirmation that you’ll start a meditation session now.";
                break;

            default:
                prompt = $"Answer and help: {userText}";
                break;
        }

        var userMsg = new ChatMessage { Role = "user", Content = prompt };
        _messages.Add(userMsg);

        try
        {
            var req = new CreateChatCompletionRequest
            {
                Model = chatModel,
                Messages = _messages
            };

            var resp = await _openAI.CreateChatCompletion(req);

            if (resp?.Choices == null || resp.Choices.Count == 0) { StartCoroutine(HideBoxesSoon()); return; }

            var aiMsg = resp.Choices[0].Message;
            _messages.Add(aiMsg);

            Debug.Log($"AI: {aiMsg.Content}");

            if (WhisperModule?.tts != null)
                WhisperModule.tts.SpeakText(aiMsg.Content);

            if (intent == Intent.Meditation)
                StartCoroutine(OpenMeditation());

            StartCoroutine(HideBoxesSoon());
        }
        catch (Exception e)
        {
            Debug.LogError($"OpenAI chat failed: {e.Message}");
            StartCoroutine(HideBoxesSoon());
        }
    }

    // --- Navigation ---

    private IEnumerator OpenMeditation()
    {
        if (!Menu) yield break;
        Menu.gameObject.SetActive(true);
        var anim = Menu.GetComponent<Animator>();
        if (anim) anim.Play("MenuOpen", 0, 0);
        yield return new WaitForSeconds(0.5f);
        Menu.OpenMeditationPage();
    }

    // --- Misc ---

    public void SayHello()
    {
        if (WhisperModule?.tts != null)
            WhisperModule.tts.SpeakText(Hello);
    }
}
