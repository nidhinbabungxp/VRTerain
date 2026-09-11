using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.IO;

public class CarSimVisualsImprover : EditorWindow
{
    [MenuItem("Tools/Improve Car Sim Visuals")]
    public static void ShowWindow()
    {
        GetWindow<CarSimVisualsImprover>("Visual Improver").Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Car Simulator Visual Realism Improver", EditorStyles.boldLabel);
        GUILayout.Label("1. This will duplicate the current scene.");
        GUILayout.Label("2. Adjusts Sun (100000 lux, 6500K).");
        GUILayout.Label("3. Duplicates and adjusts Volume Profiles (Exposure EV14, Neutral Tonemapping).");
        GUILayout.Label("4. Duplicates and adjusts Asphalt & Car materials.");
        GUILayout.Space(10);

        if (GUILayout.Button("Apply Improvements"))
        {
            ApplyImprovements();
        }
    }

    private static void ApplyImprovements()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("No active scene found!");
            return;
        }

        string originalScenePath = activeScene.path;
        if (string.IsNullOrEmpty(originalScenePath))
        {
            Debug.LogError("Please save the current scene first before applying improvements.");
            return;
        }

        string sceneDir = Path.GetDirectoryName(originalScenePath);
        string newSceneName = Path.GetFileNameWithoutExtension(originalScenePath) + "_Improved.unity";
        string newScenePath = Path.Combine(sceneDir, newSceneName).Replace('\\', '/');

        if (originalScenePath != newScenePath)
        {
            bool saved = EditorSceneManager.SaveScene(activeScene, newScenePath, true);
            if (!saved)
            {
                Debug.LogError("Failed to duplicate scene!");
                return;
            }
            Debug.Log($"Scene duplicated to: {newScenePath}");
        }

        // 1. Lighting and Exposure
        ApplyLightingAndVolumes(sceneDir);

        // 2 & 3. Asphalt and Car Materials
        ApplyMaterials(sceneDir);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Visual improvements applied successfully. Open 'Test scene 1_Improved.unity' to verify the results.");
    }

    private static void ApplyLightingAndVolumes(string saveDir)
    {
        // Find Sun
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional)
            {
                Undo.RecordObject(l, "Change Light Properties");
                l.useColorTemperature = true;
                l.colorTemperature = 6500f;
                l.shadows = LightShadows.Soft;

                HDAdditionalLightData hdLight = l.GetComponent<HDAdditionalLightData>();
                if (hdLight != null)
                {
                    Undo.RecordObject(hdLight, "Change HD Light Properties");
                    hdLight.intensity = 100000f;
                    hdLight.lightUnit = LightUnit.Lux;
                    hdLight.angularDiameter = 0.53f;
                    
                    SerializedObject so = new SerializedObject(hdLight);
                    SerializedProperty sp = so.FindProperty("m_UseContactShadow");
                    if (sp == null) sp = so.FindProperty("useContactShadow");
                    if (sp != null)
                    {
                        sp.boolValue = true;
                        so.ApplyModifiedProperties();
                    }
                }
                Debug.Log($"Updated Directional Light '{l.name}'.");
            }
        }

        // Find Volumes
        string profileSaveDir = Path.Combine(saveDir, "ImprovedProfiles");
        if (!AssetDatabase.IsValidFolder(profileSaveDir))
            AssetDatabase.CreateFolder(saveDir, "ImprovedProfiles");

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        foreach (Volume v in volumes)
        {
            if (v.profile != null)
            {
                string originalPath = AssetDatabase.GetAssetPath(v.profile);
                if (!string.IsNullOrEmpty(originalPath))
                {
                    string newProfileName = v.profile.name + "_Improved.asset";
                    string newProfilePath = Path.Combine(profileSaveDir, newProfileName).Replace('\\', '/');

                    AssetDatabase.CopyAsset(originalPath, newProfilePath);
                    VolumeProfile newProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(newProfilePath);

                    Undo.RecordObject(v, "Change Volume Profile");
                    v.profile = newProfile;
                    Debug.Log($"Duplicated Volume Profile for '{v.name}' to {newProfilePath}");

                    // Apply Overrides
                    Exposure exposure;
                    if (!newProfile.TryGet(out exposure)) exposure = newProfile.Add<Exposure>(false);
                    exposure.active = true;
                    exposure.mode.overrideState = true;
                    exposure.mode.value = ExposureMode.Fixed;
                    exposure.fixedExposure.overrideState = true;
                    exposure.fixedExposure.value = 14f;

                    Tonemapping tonemapping;
                    if (!newProfile.TryGet(out tonemapping)) tonemapping = newProfile.Add<Tonemapping>(false);
                    tonemapping.active = true;
                    tonemapping.mode.overrideState = true;
                    tonemapping.mode.value = TonemappingMode.Neutral;

                    ContactShadows contactShadows;
                    if (!newProfile.TryGet(out contactShadows)) contactShadows = newProfile.Add<ContactShadows>(false);
                    contactShadows.active = true;
                    contactShadows.enable.overrideState = true;
                    contactShadows.enable.value = true;

                    // SSGI for VR needs to be lightweight
                    GlobalIllumination gi;
                    if (!newProfile.TryGet(out gi)) gi = newProfile.Add<GlobalIllumination>(false);
                    gi.active = true;
                    gi.enable.overrideState = true;
                    gi.enable.value = true;
                    // Note: You can customize ray steps etc here, defaults are usually okay for testing
                }
            }
        }
    }

    private static void ApplyMaterials(string saveDir)
    {
        string matSaveDir = Path.Combine(saveDir, "ImprovedMaterials");
        if (!AssetDatabase.IsValidFolder(matSaveDir))
            AssetDatabase.CreateFolder(saveDir, "ImprovedMaterials");

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer r in renderers)
        {
            Material[] sharedMats = r.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material m = sharedMats[i];
                if (m == null) continue;

                string matName = m.name.ToLower();
                bool isAsphalt = matName.Contains("asphalt") || matName.Contains("road") || matName.Contains("tarmac") || matName.Contains("cut_dry");
                bool isCar = matName.Contains("car") || matName.Contains("paint") || matName.Contains("glass") || matName.Contains("rubber") || matName.Contains("plastic");

                if (isAsphalt || isCar)
                {
                    string originalPath = AssetDatabase.GetAssetPath(m);
                    if (string.IsNullOrEmpty(originalPath) || originalPath.Contains("ImprovedMaterials")) continue; // Avoid duplicating already improved or built-in materials

                    string newMatName = m.name + "_Improved.mat";
                    string newMatPath = Path.Combine(matSaveDir, newMatName).Replace('\\', '/');

                    if (!File.Exists(newMatPath))
                    {
                        AssetDatabase.CopyAsset(originalPath, newMatPath);
                    }
                    
                    Material newMat = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);
                    
                    if (isAsphalt)
                    {
                        if (newMat.HasProperty("_Smoothness"))
                        {
                            newMat.SetFloat("_Smoothness", 0.2f); // 0.15 - 0.3 range
                        }
                        else if (newMat.HasProperty("_SmoothnessRemapMax"))
                        {
                            newMat.SetFloat("_SmoothnessRemapMax", 0.2f);
                        }

                        if (newMat.HasProperty("_Metallic"))
                        {
                            newMat.SetFloat("_Metallic", 0f);
                        }
                        
                        if (newMat.HasProperty("_NormalScale"))
                        {
                            float currentNormal = newMat.GetFloat("_NormalScale");
                            newMat.SetFloat("_NormalScale", Mathf.Clamp(currentNormal * 0.5f, 0.1f, 1f)); // Reduce exaggerated normal detail
                        }
                    }

                    if (isCar)
                    {
                        // Example car material improvements
                        if (matName.Contains("paint") || matName.Contains("carbody"))
                        {
                            if (newMat.HasProperty("_ClearCoat")) newMat.SetFloat("_ClearCoat", 1f);
                            if (newMat.HasProperty("_ClearCoatSmoothness")) newMat.SetFloat("_ClearCoatSmoothness", 0.95f);
                        }
                    }

                    sharedMats[i] = newMat;
                    changed = true;
                    Debug.Log($"Replaced material {m.name} with {newMat.name} on {r.name}");
                }
            }

            if (changed)
            {
                Undo.RecordObject(r, "Change Materials");
                r.sharedMaterials = sharedMats;
            }
        }
    }
}
