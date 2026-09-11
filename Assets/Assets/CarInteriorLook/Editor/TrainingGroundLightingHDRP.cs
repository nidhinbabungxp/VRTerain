// Unity 6000.4 / HDRP 17.4. Place this file in an Editor folder.
// Applies an outdoor daylight preset to the ACTIVE scene. No XR camera changes.
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public class TrainingGroundLightingHDRP : EditorWindow
{
    const string RootName = "Training Ground Daylight (HDRP)";
    const string Folder = "Assets/TrainingGroundLighting/Generated";
    [SerializeField] float sunLux = 65000f;
    [SerializeField] float sunElevation = 50f;
    [SerializeField] float sunAzimuth = -35f;
    [SerializeField] float temperature = 6000f;
    [SerializeField] float exposureEV = 13.5f;
    [SerializeField] float shadowDistance = 90f;
    [SerializeField] bool disableOtherSuns = true;

    [MenuItem("Tools/Car Training/Outdoor HDRP Lighting")]
    static void OpenWindow()
    {
        GetWindow<TrainingGroundLightingHDRP>("Training daylight").minSize = new Vector2(390, 360);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Outdoor driving-training daylight", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Applies to the open active scene. Adds a sun and a global HDRP Volume. " +
            "Preserves your XR rig, gameplay, materials and existing Volume assets. Scene changes support Undo.", MessageType.Info);
        sunLux = EditorGUILayout.Slider("Sun intensity (lux)", sunLux, 20000f, 130000f);
        sunElevation = EditorGUILayout.Slider("Sun elevation (degrees)", sunElevation, 25f, 75f);
        sunAzimuth = EditorGUILayout.Slider("Sun azimuth (degrees)", sunAzimuth, -180f, 180f);
        temperature = EditorGUILayout.Slider("Sun temperature (K)", temperature, 5000f, 7000f);
        exposureEV = EditorGUILayout.Slider("Fixed exposure (EV)", exposureEV, 11f, 16f);
        shadowDistance = EditorGUILayout.Slider("Shadow distance (metres)", shadowDistance, 30f, 200f);
        disableOtherSuns = EditorGUILayout.Toggle("Disable other directional lights", disableOtherSuns);
        EditorGUILayout.HelpBox("Lower EV = brighter. Start at 13.5. Adjust azimuth so shadows run diagonally " +
            "across the course. Other point/spot/area lights stay as they are.", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Apply daylight to current scene", GUILayout.Height(32))) Apply();
    }

    void Apply()
    {
        if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
        {
            Debug.LogError("This preset requires an active HDRP asset.");
            return;
        }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("Close Prefab Mode and open your training-ground scene first.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        EnsureFolder(Folder);

        // A new asset preserves any earlier preset profile and its manual edits.
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Training Ground Daylight";
        string profilePath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/DaylightProfile.asset");
        AssetDatabase.CreateAsset(profile, profilePath);
        var visual = Add<VisualEnvironment>(profile);
        visual.skyType.Override(SkySettings.GetUniqueID<PhysicallyBasedSky>());
        visual.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        Add<PhysicallyBasedSky>(profile).SetAllOverridesTo(true); // Earth defaults, independent of lower-priority skies.
        var exposure = Add<Exposure>(profile);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(exposureEV);
        exposure.compensation.Override(0f);
        Add<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
        Add<HDShadowSettings>(profile).maxShadowDistance.Override(shadowDistance);
        // Override these values to zero/off; active=false would allow lower Volumes through.
        Add<MotionBlur>(profile).intensity.Override(0f);
        Add<DepthOfField>(profile).focusMode.Override(DepthOfFieldMode.Off);
        Add<ChromaticAberration>(profile).intensity.Override(0f);
        Add<Vignette>(profile).intensity.Override(0f);
        Add<Bloom>(profile).intensity.Override(0f);
        Add<Fog>(profile).enabled.Override(false);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply training-ground daylight");
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create daylight preset");
        }
        else
        {
            Undo.RecordObject(root, "Enable daylight preset");
            root.SetActive(true);
        }

        Light sun = root.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null)
        {
            var sunObject = new GameObject("Training Sun");
            sunObject.transform.SetParent(root.transform, false);
            sunObject.AddHDLight(LightType.Directional);
            Undo.RegisterCreatedObjectUndo(sunObject, "Create training sun");
            sun = sunObject.GetComponent<Light>();
        }
        HDAdditionalLightData hd = sun.GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = Undo.AddComponent<HDAdditionalLightData>(sun.gameObject);
        Undo.RecordObjects(new Object[] { sun, hd, sun.transform, sun.gameObject }, "Set daylight sun");
        sun.gameObject.SetActive(true);
        sun.enabled = true;
        sun.transform.rotation = Quaternion.Euler(sunElevation, sunAzimuth, 0f);
        sun.color = Color.white;
        sun.useColorTemperature = true;
        sun.colorTemperature = temperature;
        sun.lightUnit = LightUnit.Lux;
        sun.intensity = sunLux; // Directional native intensity is lux.
        sun.lightmapBakeType = LightmapBakeType.Realtime;
        sun.shadows = LightShadows.Soft;
        hd.interactsWithSky = true;
        hd.EnableShadows(true);
        if (disableOtherSuns)
        {
            foreach (Light other in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)))
            {
                if (other == sun || other.type != LightType.Directional || !other.enabled) continue;
                Undo.RecordObject(other, "Disable duplicate sun");
                other.enabled = false;
            }
        }

        Volume volume = root.GetComponent<Volume>();
        if (volume == null) volume = Undo.AddComponent<Volume>(root);
        Undo.RecordObjects(new Object[] { volume, root }, "Set daylight Volume");
        root.layer = 0;
        volume.enabled = true;
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.priority = 10000f;
        volume.sharedProfile = profile;
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);

        foreach (Camera camera in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
        {
            var cameraData = camera.GetComponent<HDAdditionalCameraData>();
            if (cameraData != null && (cameraData.volumeLayerMask.value & 1) == 0)
                Debug.LogWarning("Camera '" + camera.name + "' excludes Default from its Volume Layer Mask. Include Default to see this preset.", camera);
        }
        // Refresh existing realtime probes once. Baked probes must be rebaked in Unity.
        foreach (var probe in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HDAdditionalReflectionData>(true)))
            if (probe.mode == ProbeSettings.Mode.Realtime) probe.RequestRenderNextUpdate();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        Selection.activeGameObject = root;
        Debug.Log("Daylight applied to '" + scene.name + "'. Review through your XR/Game camera, then save the scene. " +
            "Ctrl+Z undoes scene changes (generated profile asset remains). Rebake existing baked lighting/reflection probes if used.");
    }

    static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
