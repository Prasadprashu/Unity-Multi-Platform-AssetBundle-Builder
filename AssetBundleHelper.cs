#if UNITY_EDITOR
/*
 * Asset Bundle Helper Pro - Optimized Edition
 * Author: Bhavani Prasad
 * Builds AssetBundles for multiple platforms with advanced control
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AssetBundleHelperPro : EditorWindow
{
    #region Data Structures
    
    [Serializable]
    private class PlatformConfig
    {
        public string displayName;
        public BuildTarget target;
        public string outputFolder;
        public ColorSpace defaultColorSpace;
        public ColorSpace currentColorSpace;
        public bool enabled;

        public PlatformConfig(string name, BuildTarget target, string folder, ColorSpace colorSpace)
        {
            this.displayName = name;
            this.target = target;
            this.outputFolder = folder;
            this.defaultColorSpace = colorSpace;
            this.currentColorSpace = colorSpace;
            this.enabled = false;
        }
    }

    private enum CompressionType
    {
        ChunkBasedLZ4,
        LZMA,
        Uncompressed
    }

    [Serializable]
    private class ToolSettings
    {
        public string outputRoot = DefaultOutputRoot;
        public bool clearBeforeBuild = true;
        public bool buildSelectedOnly = true;
        public int compressionMode = 0;
        public List<PlatformSettings> platforms = new List<PlatformSettings>();
        public SerializableDictionary bundleSelection = new SerializableDictionary();
    }

    [Serializable]
    private class PlatformSettings
    {
        public string displayName;
        public bool enabled;
        public int colorSpace;
    }

    [Serializable]
    private class SerializableDictionary
    {
        public List<string> keys = new List<string>();
        public List<bool> values = new List<bool>();
    }
    
    #endregion

    #region Constants & Prefs Keys
    
    private const string DefaultOutputRoot = "Assets/AssetBundles";
    private const int MaxBundleWarningThreshold = 5000;
    private static readonly Vector2 MinWindowSize = new Vector2(450f, 500f);
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
    
    private const string PrefsKey = "AssetBundleHelperPro_Settings";
    
    #endregion

    #region Fields
    
    private List<PlatformConfig> _platforms;
    private string _outputRoot = DefaultOutputRoot;
    private bool _clearBeforeBuild = true;
    private bool _buildSelectedOnly = true;
    private CompressionType _compression = CompressionType.ChunkBasedLZ4;
    
    private string[] _bundleNames = Array.Empty<string>();
    private Dictionary<string, bool> _bundleSelection = new Dictionary<string, bool>();
    private Vector2 _bundleScrollPos;
    private string _bundleSearchFilter = string.Empty;
    
    private ColorSpace _originalColorSpace;
    
    #endregion

    #region Unity Lifecycle
    
    [MenuItem("Tools/Multi Asset Bundle Helper Pro %#&a")]
    private static void ShowWindow()
    {
        var window = GetWindow<AssetBundleHelperPro>("AB Helper Pro");
        window.minSize = MinWindowSize;
        window.Show();
    }

    private void OnEnable()
    {
        InitializePlatforms();
        LoadSettings();
        RefreshBundleList();
        _originalColorSpace = PlayerSettings.colorSpace;
    }

    private void OnDisable()
    {
        SaveSettings();
        
        // Restore original color space if window is closed during build
        if (PlayerSettings.colorSpace != _originalColorSpace)
        {
            PlayerSettings.colorSpace = _originalColorSpace;
        }
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        
        DrawHeader();
        EditorGUILayout.Space(6);
        
        using (var scroll = new EditorGUILayout.ScrollViewScope(Vector2.zero))
        {
            DrawOutputSection();
            EditorGUILayout.Space(6);
            
            DrawOptionsSection();
            EditorGUILayout.Space(6);
            
            DrawPlatformsSection();
            EditorGUILayout.Space(6);
            
            DrawBundlesSection();
            EditorGUILayout.Space(8);
        }
        
        DrawBuildButton();
        
        if (EditorGUI.EndChangeCheck())
        {
            SaveSettings();
        }
    }
    
    #endregion

    #region Settings Persistence
    
    private void SaveSettings()
    {
        try
        {
            var settings = new ToolSettings
            {
                outputRoot = _outputRoot,
                clearBeforeBuild = _clearBeforeBuild,
                buildSelectedOnly = _buildSelectedOnly,
                compressionMode = (int)_compression,
                platforms = new List<PlatformSettings>(),
                bundleSelection = new SerializableDictionary()
            };

            // Save platform settings
            foreach (var platform in _platforms)
            {
                settings.platforms.Add(new PlatformSettings
                {
                    displayName = platform.displayName,
                    enabled = platform.enabled,
                    colorSpace = (int)platform.currentColorSpace
                });
            }

            // Save bundle selection
            foreach (var kvp in _bundleSelection)
            {
                settings.bundleSelection.keys.Add(kvp.Key);
                settings.bundleSelection.values.Add(kvp.Value);
            }

            string json = JsonUtility.ToJson(settings, true);
            EditorPrefs.SetString(PrefsKey, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save settings: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!EditorPrefs.HasKey(PrefsKey))
                return;

            string json = EditorPrefs.GetString(PrefsKey);
            if (string.IsNullOrEmpty(json))
                return;

            var settings = JsonUtility.FromJson<ToolSettings>(json);
            if (settings == null)
                return;

            // Load basic settings
            _outputRoot = settings.outputRoot ?? DefaultOutputRoot;
            _clearBeforeBuild = settings.clearBeforeBuild;
            _buildSelectedOnly = settings.buildSelectedOnly;
            _compression = (CompressionType)settings.compressionMode;

            // Load platform settings
            if (settings.platforms != null && _platforms != null)
            {
                foreach (var platformSetting in settings.platforms)
                {
                    var platform = _platforms.FirstOrDefault(p => p.displayName == platformSetting.displayName);
                    if (platform != null)
                    {
                        platform.enabled = platformSetting.enabled;
                        platform.currentColorSpace = (ColorSpace)platformSetting.colorSpace;
                    }
                }
            }

            // Load bundle selection
            if (settings.bundleSelection?.keys != null)
            {
                _bundleSelection.Clear();
                for (int i = 0; i < settings.bundleSelection.keys.Count; i++)
                {
                    if (i < settings.bundleSelection.values.Count)
                    {
                        _bundleSelection[settings.bundleSelection.keys[i]] = settings.bundleSelection.values[i];
                    }
                }
            }

            Debug.Log("Asset Bundle Helper Pro: Settings loaded successfully");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load settings: {ex.Message}. Using defaults.");
        }
    }
    
    #endregion

    #region UI Drawing
    
    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Multi Asset Bundle Helper Pro by Bhavani Prasad", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Reset", GUILayout.Width(50), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Reset all settings to defaults?", "Yes", "Cancel"))
                {
                    ResetToDefaults();
                }
            }
            
            if (GUILayout.Button("?", GUILayout.Width(24), GUILayout.Height(20)))
            {
                ShowHelp();
            }
        }
    }

    private void ResetToDefaults()
    {
        _outputRoot = DefaultOutputRoot;
        _clearBeforeBuild = true;
        _buildSelectedOnly = true;
        _compression = CompressionType.ChunkBasedLZ4;
        _bundleSearchFilter = string.Empty;
        
        foreach (var platform in _platforms)
        {
            platform.enabled = false;
            platform.currentColorSpace = platform.defaultColorSpace;
        }
        
        foreach (var key in _bundleSelection.Keys.ToList())
        {
            _bundleSelection[key] = true;
        }
        
        SaveSettings();
        Repaint();
        
        Debug.Log("Asset Bundle Helper Pro: Settings reset to defaults");
    }

    private void ShowHelp()
    {
        EditorUtility.DisplayDialog(
            "Multi Asset Bundle Helper Pro by Bhavani Prasad",
            "FEATURES:\n\n" +
            "• Multi-platform builds with one click\n" +
            "• Selective bundle building (ON by default)\n" +
            "• Per-platform color space control\n" +
            "• Auto folder clearing (ON by default)\n" +
            "• Chunk-based LZ4 compression (default)\n" +
            "• Android/Quest/Meta share output folder\n" +
            "• All settings are automatically saved\n\n" +
            "WORKFLOW:\n" +
            "1. Select platforms to build\n" +
            "2. Choose bundles (or build all)\n" +
            "3. Click 'Build AssetBundles'\n" +
            "4. Project color space resets to Gamma after build\n\n" +
            "TIP: Use search filter for large bundle lists\n" +
            "Your settings persist across Unity sessions!",
            "Got it");
    }

    private void DrawOutputSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Output", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Root", GUILayout.Width(40));
                _outputRoot = EditorGUILayout.TextField(_outputRoot);
                
                if (GUILayout.Button("···", GUILayout.Width(30)))
                {
                    BrowseOutputFolder();
                }
            }
            
            if (!IsValidPath(_outputRoot))
            {
                EditorGUILayout.HelpBox("Invalid path characters detected", MessageType.Warning);
            }
        }
    }

    private void DrawOptionsSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Options", EditorStyles.boldLabel);
            
            _clearBeforeBuild = EditorGUILayout.ToggleLeft(
                "Clear platform folders before build", _clearBeforeBuild);
            
            _compression = (CompressionType)EditorGUILayout.EnumPopup(
                "Compression", _compression);
        }
    }

    private void DrawPlatformsSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Platforms", EditorStyles.boldLabel);
            
            foreach (var platform in _platforms)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    platform.enabled = EditorGUILayout.ToggleLeft(
                        platform.displayName, platform.enabled, EditorStyles.boldLabel);
                    
                    using (new EditorGUI.DisabledScope(!platform.enabled))
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            platform.currentColorSpace = (ColorSpace)EditorGUILayout.EnumPopup(
                                "Color Space", platform.currentColorSpace);
                            
                            EditorGUILayout.LabelField(
                                $"→ {platform.outputFolder}", EditorStyles.miniLabel);
                        }
                    }
                }
            }
            
            int enabledCount = _platforms.Count(p => p.enabled);
            if (enabledCount == 0)
            {
                EditorGUILayout.HelpBox("Select at least one platform", MessageType.Info);
            }
        }
    }

    private void DrawBundlesSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("AssetBundles", EditorStyles.boldLabel);
            
            _buildSelectedOnly = EditorGUILayout.ToggleLeft(
                "Build selected bundles only", _buildSelectedOnly);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Height(22)))
                {
                    RefreshBundleList();
                }
                
                if (GUILayout.Button("Select All", GUILayout.Height(22)))
                {
                    SetAllBundles(true);
                }
                
                if (GUILayout.Button("Deselect All", GUILayout.Height(22)))
                {
                    SetAllBundles(false);
                }
            }
            
            _bundleSearchFilter = EditorGUILayout.TextField("Search", _bundleSearchFilter);
            
            if (_bundleNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No AssetBundles defined in project", MessageType.Info);
            }
            else
            {
                DrawBundleList();
            }
        }
    }

    private void DrawBundleList()
    {
        var filtered = GetFilteredBundles();
        int selectedCount = filtered.Count(b => _bundleSelection.ContainsKey(b) && _bundleSelection[b]);
        
        EditorGUILayout.LabelField(
            $"Showing {filtered.Length} / {_bundleNames.Length} | Selected: {selectedCount}",
            EditorStyles.miniLabel);
        
        _bundleScrollPos = EditorGUILayout.BeginScrollView(
            _bundleScrollPos, GUILayout.MinHeight(120), GUILayout.MaxHeight(200));
        
        foreach (string bundle in filtered)
        {
            bool selected = _bundleSelection.TryGetValue(bundle, out bool val) && val;
            bool newSelected = EditorGUILayout.ToggleLeft(bundle, selected);
            
            if (newSelected != selected)
            {
                _bundleSelection[bundle] = newSelected;
            }
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawBuildButton()
    {
        bool canBuild = _platforms.Any(p => p.enabled) && IsValidPath(_outputRoot);
        
        if (_buildSelectedOnly && GetSelectedBundles().Count == 0)
        {
            canBuild = false;
        }
        
        using (new EditorGUI.DisabledScope(!canBuild))
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = canBuild ? new Color(0.4f, 0.8f, 0.4f) : originalColor;
            
            if (GUILayout.Button("Build AssetBundles", GUILayout.Height(32)))
            {
                ExecuteBuild();
            }
            
            GUI.backgroundColor = originalColor;
        }
        
        if (!canBuild)
        {
            string reason = string.Empty;
            if (!_platforms.Any(p => p.enabled))
                reason = "Select at least one platform";
            else if (!IsValidPath(_outputRoot))
                reason = "Invalid output path";
            else if (_buildSelectedOnly && GetSelectedBundles().Count == 0)
                reason = "No bundles selected";
            
            if (!string.IsNullOrEmpty(reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            }
        }
    }
    
    #endregion

    #region Initialization
    
    private void InitializePlatforms()
    {
        if (_platforms != null) return;
        
        _platforms = new List<PlatformConfig>
        {
            new PlatformConfig("PC - Windows", BuildTarget.StandaloneWindows, 
                "StandaloneWindows", ColorSpace.Gamma),
            
            new PlatformConfig("Mac", BuildTarget.StandaloneOSX, 
                "StandaloneOSX", ColorSpace.Gamma),
            
            new PlatformConfig("iOS", BuildTarget.iOS, 
                "iOS", ColorSpace.Gamma),
            
            new PlatformConfig("HoloLens (WSA)", BuildTarget.WSAPlayer, 
                "WSAPlayer", ColorSpace.Gamma),
            
            new PlatformConfig("Android / Quest / Meta", BuildTarget.Android, 
                "QuestAndAndroid", ColorSpace.Linear)
        };
    }
    
    #endregion

    #region Bundle Management
    
    private void RefreshBundleList()
    {
        _bundleNames = AssetDatabase.GetAllAssetBundleNames() ?? Array.Empty<string>();
        Array.Sort(_bundleNames);
        
        // Sync selection dictionary
        var validBundles = new HashSet<string>(_bundleNames);
        
        // Add new bundles
        foreach (string bundle in _bundleNames)
        {
            if (!_bundleSelection.ContainsKey(bundle))
            {
                _bundleSelection[bundle] = true; // Selected by default
            }
        }
        
        // Remove deleted bundles
        var keysToRemove = _bundleSelection.Keys.Where(k => !validBundles.Contains(k)).ToList();
        foreach (string key in keysToRemove)
        {
            _bundleSelection.Remove(key);
        }
        
        SaveSettings();
        Repaint();
    }

    private string[] GetFilteredBundles()
    {
        if (string.IsNullOrWhiteSpace(_bundleSearchFilter))
            return _bundleNames;
        
        string filter = _bundleSearchFilter.ToLower();
        return _bundleNames.Where(b => b.ToLower().Contains(filter)).ToArray();
    }

    private List<string> GetSelectedBundles()
    {
        return _bundleSelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
    }

    private void SetAllBundles(bool selected)
    {
        var filtered = GetFilteredBundles();
        foreach (string bundle in filtered)
        {
            _bundleSelection[bundle] = selected;
        }
        SaveSettings();
        Repaint();
    }
    
    #endregion

    #region Build Execution
    
    private void ExecuteBuild()
    {
        if (!ValidatePreBuild()) return;
        
        List<string> selectedBundles = _buildSelectedOnly ? GetSelectedBundles() : null;
        var enabledPlatforms = _platforms.Where(p => p.enabled).ToList();
        
        _originalColorSpace = PlayerSettings.colorSpace;
        int successCount = 0;
        int totalPlatforms = enabledPlatforms.Count;
        
        try
        {
            AssetDatabase.SaveAssets();
            EnsureDirectoryExists(_outputRoot);
            
            for (int i = 0; i < enabledPlatforms.Count; i++)
            {
                var platform = enabledPlatforms[i];
                
                float progress = (float)i / totalPlatforms;
                EditorUtility.DisplayProgressBar(
                    "Building AssetBundles",
                    $"Building for {platform.displayName}... ({i + 1}/{totalPlatforms})",
                    progress);
                
                if (BuildPlatform(platform, selectedBundles))
                {
                    successCount++;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Build failed with exception: {ex}");
            EditorUtility.DisplayDialog("Build Error", 
                $"Build failed:\n{ex.Message}", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.Refresh();
        }
        
        ShowBuildResult(successCount, totalPlatforms);
    }

    private bool ValidatePreBuild()
    {
        if (string.IsNullOrWhiteSpace(_outputRoot))
        {
            EditorUtility.DisplayDialog("Invalid Path", 
                "Output root path is empty", "OK");
            return false;
        }
        
        if (!IsValidPath(_outputRoot))
        {
            EditorUtility.DisplayDialog("Invalid Path", 
                "Output path contains invalid characters", "OK");
            return false;
        }
        
        if (_buildSelectedOnly)
        {
            var selected = GetSelectedBundles();
            if (selected.Count == 0)
            {
                EditorUtility.DisplayDialog("No Selection", 
                    "No AssetBundles selected for build", "OK");
                return false;
            }
            
            if (selected.Count > MaxBundleWarningThreshold)
            {
                return EditorUtility.DisplayDialog("Large Build", 
                    $"Building {selected.Count} bundles. This may take a while.\n\nContinue?",
                    "Yes", "Cancel");
            }
        }
        
        return true;
    }

    private bool BuildPlatform(PlatformConfig platform, List<string> selectedBundles)
    {
        string outputPath = Path.Combine(_outputRoot, platform.outputFolder).Replace("\\", "/");
        
        try
        {
            EnsureDirectoryExists(outputPath);
            
            if (_clearBeforeBuild && !ClearDirectory(outputPath))
            {
                Debug.LogWarning($"Failed to clear directory: {outputPath}");
            }
            
            PlayerSettings.colorSpace = platform.currentColorSpace;
            
            BuildAssetBundleOptions options = GetBuildOptions();
            AssetBundleManifest manifest;
            
            if (_buildSelectedOnly && selectedBundles != null && selectedBundles.Count > 0)
            {
                var builds = CreateBuildMap(selectedBundles);
                if (builds.Length == 0)
                {
                    Debug.LogWarning($"No assets found for selected bundles on {platform.displayName}");
                    return false;
                }
                
                manifest = BuildPipeline.BuildAssetBundles(outputPath, builds, options, platform.target);
            }
            else
            {
                manifest = BuildPipeline.BuildAssetBundles(outputPath, options, platform.target);
            }
            
            if (manifest == null)
            {
                Debug.LogError($"Build failed for {platform.displayName}");
                return false;
            }
            
            Debug.Log($"✓ Built {platform.displayName} → {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception building {platform.displayName}: {ex}");
            return false;
        }
    }

    private AssetBundleBuild[] CreateBuildMap(List<string> bundleNames)
    {
        var builds = new List<AssetBundleBuild>();
        
        foreach (string bundleName in bundleNames)
        {
            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
            if (assets == null || assets.Length == 0)
                continue;
            
            builds.Add(new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetBundleVariant = string.Empty,
                assetNames = assets
            });
        }
        
        return builds.ToArray();
    }

    private BuildAssetBundleOptions GetBuildOptions()
    {
        switch (_compression)
        {
            case CompressionType.ChunkBasedLZ4:
                return BuildAssetBundleOptions.ChunkBasedCompression;
            case CompressionType.LZMA:
                return BuildAssetBundleOptions.None;
            case CompressionType.Uncompressed:
                return BuildAssetBundleOptions.UncompressedAssetBundle;
            default:
                return BuildAssetBundleOptions.ChunkBasedCompression;
        }
    }

    private void ShowBuildResult(int successCount, int totalCount)
    {
        if (successCount == totalCount)
        {
            EditorUtility.DisplayDialog("Build Complete", 
                $"Successfully built {successCount} platform(s)", "OK");
        }
        else if (successCount > 0)
        {
            EditorUtility.DisplayDialog("Build Completed with Issues", 
                $"Built {successCount}/{totalCount} platform(s)\nCheck console for errors", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Build Failed", 
                "All platform builds failed\nCheck console for errors", "OK");
        }
    }
    
    #endregion

    #region File Utilities
    
    private void BrowseOutputFolder()
    {
        string initial = string.IsNullOrEmpty(_outputRoot) ? Application.dataPath : _outputRoot;
        string selected = EditorUtility.OpenFolderPanel("Select Output Root", initial, "");
        
        if (string.IsNullOrEmpty(selected)) return;
        
        string dataPath = Application.dataPath.Replace("\\", "/");
        string normalized = selected.Replace("\\", "/");
        
        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            _outputRoot = "Assets" + normalized.Substring(dataPath.Length);
        }
        else
        {
            _outputRoot = normalized;
        }
        
        SaveSettings();
    }

    private static bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return path.IndexOfAny(InvalidPathChars) < 0;
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        try
        {
            string dir = Path.HasExtension(path) ? Path.GetDirectoryName(path) : path;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create directory '{path}': {ex.Message}");
        }
    }

    private static bool ClearDirectory(string path)
    {
        if (!Directory.Exists(path)) return true;
        
        try
        {
            foreach (string file in Directory.GetFiles(path))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            
            foreach (string dir in Directory.GetDirectories(path))
            {
                Directory.Delete(dir, true);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to clear directory '{path}': {ex.Message}");
            return false;
        }
    }
    
    #endregion
}
#endif