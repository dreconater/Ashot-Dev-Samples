/*
 * GoogleTTSManager
 * ----------------
 * Lightweight Text-to-Speech helper for Unity (Meta Quest ready).
 * - Sends text to Google Cloud Text-to-Speech and plays the returned audio.
 * - Auto-detects language by simple phrase matching and maps to a suitable voice.
 * - Uses WAV (LINEAR16) for easy playback via an AudioSource.
 */

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GoogleTTSManager : MonoBehaviour
{
    [Header("Google Cloud")]
    [SerializeField] private string apiKey; // Set from env or Inspector

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    private const string Endpoint = "https://texttospeech.googleapis.com/v1/text:synthesize";

    private void Awake()
    {
        if (!audioSource)
        {
            audioSource = GetComponent<AudioSource>();
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        StartCoroutine(SynthesizeAndPlay(text));
    }

    private IEnumerator SynthesizeAndPlay(string inputText)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError("GoogleTTS: API key not set.");
            yield break;
        }

        string languageCode = DetectLanguage(inputText);
        string voiceName = GetVoiceName(languageCode);

        var payload = $@"{{
  ""input"":{{""text"":""{EscapeJson(inputText)}""}},
  ""voice"":{{""languageCode"":""{languageCode}"",""name"":""{voiceName}""}},
  ""audioConfig"":{{""audioEncoding"":""LINEAR16""}}
}}";

        using var request = new UnityWebRequest($"{Endpoint}?key={apiKey}", UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 20
        };
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"GoogleTTS: request failed - {request.error}");
            yield break;
        }

        var json = request.downloadHandler.text;
        var resp = JsonUtility.FromJson<TTSResponse>(json);
        if (resp == null || string.IsNullOrEmpty(resp.audioContent))
        {
            Debug.LogError("GoogleTTS: empty response audioContent.");
            yield break;
        }

        byte[] wavBytes;
        try { wavBytes = Convert.FromBase64String(resp.audioContent); }
        catch (Exception e) { Debug.LogError($"GoogleTTS: base64 decode failed: {e.Message}"); yield break; }

        var clip = WavUtility.ToAudioClip(wavBytes, "GoogleTTS_Audio");
        if (!clip) { Debug.LogError("GoogleTTS: WAV decode failed."); yield break; }

        audioSource.clip = clip;
        audioSource.Play();
    }

    // --- Language detection (simple phrase heuristics) ---

    public string DetectLanguage(string text)
    {
        if (string.IsNullOrEmpty(text)) return "en-US";
        string t = text.ToLowerInvariant();

        // trigger substring -> BCP-47 languageCode
        (string trigger, string code)[] map =
        {
            ("你好啊", "zh-CN"), ("你好", "zh-TW"), ("bonjour", "fr-FR"), ("hallo", "de-DE"),
            ("hola", "es-ES"), ("ciao", "it-IT"), ("olá", "pt-BR"), ("مرحبا", "ar-XA"),
            ("sveiki", "lt-LT"), ("привіт", "uk-UA"), ("cześć", "pl-PL"), ("こんにちは", "ja-JP"),
            ("안녕하세요", "ko-KR"), ("привет", "ru-RU"), ("merhaba", "tr-TR"), ("hej", "sv-SE"),
            ("salam", "az-AZ"), ("سلام", "fa-IR"), ("नमस्ते", "hi-IN"), ("שלום", "he-IL"),
            ("สวัสดี", "th-TH"), ("hallo ", "nl-NL")
        };

        foreach (var (trigger, code) in map)
            if (t.Contains(trigger)) return code;

        return "en-US";
    }

    public string GetVoiceName(string languageCode) => languageCode switch
    {
        "en-US" => "en-US-Neural2-F",
        "fr-FR" => "fr-FR-Neural2-E",
        "de-DE" => "de-DE-Neural2-E",
        "es-ES" => "es-ES-Neural2-E",
        "zh-TW" => "cmn-TW-Wavenet-A",
        "zh-CN" => "cmn-CN-Wavenet-D",
        "ja-JP" => "ja-JP-Wavenet-C",
        "ko-KR" => "ko-KR-Wavenet-C",
        "ru-RU" => "ru-RU-Wavenet-D",
        "tr-TR" => "tr-TR-Wavenet-A",
        "sv-SE" => "sv-SE-Wavenet-A",
        "az-AZ" => "az-AZ-Standard-A",
        "fa-IR" => "fa-IR-Standard-A",
        "hi-IN" => "hi-IN-Wavenet-B",
        "he-IL" => "he-IL-Standard-B",
        "th-TH" => "th-TH-Wavenet-A",
        "it-IT" => "it-IT-Wavenet-C",
        "pt-BR" => "pt-BR-Wavenet-F",
        "ar-XA" => "ar-XA-Wavenet-B",
        "lt-LT" => "lt-LT-Wavenet-A",
        "nl-NL" => "nl-NL-Wavenet-D",
        "uk-UA" => "uk-UA-Wavenet-A",
        "pl-PL" => "pl-PL-Wavenet-D",
        _ => "en-US-Neural2-F"
    };

    private static string EscapeJson(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    [Serializable] private class TTSResponse { public string audioContent; }
}
