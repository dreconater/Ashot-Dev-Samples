/*
 * VideosLoader
 * ------------
 * Loads place/spot metadata from Resources/converted_spots.json, prepares a
 * "Content/<PlaceName>" directory per place under Application.persistentDataPath,
 * signals when at least one place folder contains files, and fires an "empty"
 * signal otherwise. Also removes the legacy "Content/Drone Point" folder.
 *
 * Events:
 *  - FilesLoaded(string[] folderNames, List<Place> places)
 *  - NoContent()
 *
 * Usage: drop on a boot scene object. Wire nothing—runs on Start.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class VideosLoader : MonoBehaviour
{
    public static VideosLoader Instance { get; private set; }

    public event Action<string[], List<Place>> FilesLoaded;
    public event Action NoContent;

    public string[] FolderNames { get; private set; }
    public List<Place> Places { get; private set; }

    const string JsonResourceName = "converted_spots";
    const string RootContentFolder = "Content";
    const string LegacyFolderToDelete = "Drone Point";

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (!TryLoadPlacesFromJson(out var places))
        {
            NoContent?.Invoke();
            return;
        }

        Places = places;
        FolderNames = Places.Select(p => p.name).ToArray();

        var contentRoot = Path.Combine(Application.persistentDataPath, RootContentFolder);
        Directory.CreateDirectory(contentRoot);

        foreach (var name in FolderNames)
            Directory.CreateDirectory(Path.Combine(contentRoot, name));

        bool anyNonEmpty = false;

        foreach (var name in FolderNames)
        {
            var dir = Path.Combine(contentRoot, name);
            if (!Directory.Exists(dir) || IsEmpty(dir))
            {
                NoContent?.Invoke();
            }
            else
            {
                anyNonEmpty = true;
            }
        }

        if (anyNonEmpty)
            FilesLoaded?.Invoke(FolderNames, Places);

        var legacyPath = Path.Combine(contentRoot, LegacyFolderToDelete);
        if (Directory.Exists(legacyPath))
            Directory.Delete(legacyPath, true);
    }

    static bool IsEmpty(string path) =>
        Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0;

    static bool TryLoadPlacesFromJson(out List<Place> places)
    {
        places = null;

        var jsonAsset = Resources.Load<TextAsset>(JsonResourceName);
        if (!jsonAsset) return false;

        try
        {
            var root = JObject.Parse(jsonAsset.text);
            var placesArray = (JArray)root["Places"];
            if (placesArray == null) return false;

            var result = new List<Place>();

            foreach (JObject jPlace in placesArray)
            {
                var place = new Place
                {
                    name = jPlace["name"]?.ToString() ?? string.Empty,
                    menu = jPlace["menu"]?.ToString() ?? string.Empty,
                    spots = new List<Spot>()
                };

                foreach (var prop in jPlace.Properties().Where(p => p.Name.StartsWith("spot", StringComparison.OrdinalIgnoreCase)))
                {
                    var spotObj = prop.Value as JObject;
                    if (spotObj == null) continue;

                    var loc = spotObj["location"] as JObject;
                    var spot = new Spot
                    {
                        goTo = spotObj["goTo"]?.ToString() ?? string.Empty,
                        location = new Location
                        {
                            x = (float?)loc?["x"] ?? 0f,
                            y = (float?)loc?["y"] ?? 0f
                        }
                    };
                    place.spots.Add(spot);
                }

                result.Add(place);
            }

            places = result;
            return places.Count > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"VideosLoader: JSON parse failed: {e.Message}");
            return false;
        }
    }
}
