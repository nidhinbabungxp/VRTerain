// Replacement for the previous CarInteriorSceneBuilder.cs.
// Unity 6000.4 / HDRP 17.4. Keep exactly ONE copy, inside an Editor folder.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class CarInteriorSceneBuilder
{
    const string OutputFolder = "Assets/CarInteriorLook/GeneratedHDRP";

    [MenuItem("Tools/Car Interior/Create Preview Scene")]
    [MenuItem("Tools/Car Interior/Create HDRP Preview Scene")]
    public static void CreatePreview()
    {
        if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
        {
            Debug.LogError("This replacement is for HDRP. Assign your HDRP asset in Graphics/Quality settings before running it.");
            return;
        }

        GameObject source = ResolveCarAsset();
        if (source == null) return;
        // Standard Unity protection for the currently open scene.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(OutputFolder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject car = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (car == null) car = UnityEngine.Object.Instantiate(source);
        car.name = "Car - Refined Interior";

        Transform[] nodes = car.GetComponentsInChildren<Transform>(true);
        Transform fl = Find(nodes, "Wheel_FL_P"), fr = Find(nodes, "Wheel_FR_P");
        Transform bl = Find(nodes, "Wheel_BL_P"), br = Find(nodes, "Wheel_BR_P");
        Transform steering = Find(nodes, "Steering_P");
        Vector3 up = car.transform.up.normalized;
        Vector3 forward = car.transform.forward.normalized;
        Vector3 driverSide = -car.transform.right.normalized;
        Vector3 origin = car.transform.position;
        float scale = 1f;
        if (fl != null && fr != null && bl != null && br != null)
        {
            Vector3 front = (fl.position + fr.position) * .5f;
            Vector3 back = (bl.position + br.position) * .5f;
            float wheelbase = Vector3.Distance(front, back);
            if (wheelbase > .001f)
            {
                scale = wheelbase / 2.5768996f;
                forward = (front - back).normalized;
                origin = front - forward * (1.3722159f * scale) - up * (.2993074f * scale);
                driverSide = Vector3.Cross(up, forward).normalized;
                if (steering != null && Vector3.Dot(steering.position - origin, driverSide) < 0)
                    driverSide = -driverSide;
            }
        }
        else Debug.LogWarning("Original wheel anchors were not found. Lighting uses the model's root orientation; adjust the rig if necessary.");

        Vector3 P(float x, float y, float z) => origin + scale * (driverSide * x + up * y + forward * z);

        CreateEnvironment();
        var rig = new GameObject("HDRP Interior Lighting").transform;

        // Native intensity units: directional = lux, rectangular area = nits,
        // point = candela. Light.lightUnit controls the Inspector display unit.
        Light sun = AddLight("Cool daylight - 8000 lux", LightType.Directional,
            new Color(.84f, .91f, 1f), 8000f, LightUnit.Lux,
            P(-2, 3, 2), P(0, .8f, .5f), up, rig, true);
        RenderSettings.sun = sun;

        Light key = AddLight("Windshield softbox - 1800 nits", LightType.Rectangle,
            new Color(.87f, .94f, 1f), 1800f, LightUnit.Nits,
            P(-.10f, 1.23f, 1.04f), P(.06f, .73f, .36f), up, rig, true);
        key.areaSize = new Vector2(.70f, .25f) * scale;
        key.range = 2.5f * scale;

        Light fill = AddLight("Passenger window fill - 450 nits", LightType.Rectangle,
            new Color(.77f, .87f, 1f), 450f, LightUnit.Nits,
            P(-.57f, 1.12f, .34f), P(.15f, .80f, .42f), up, rig, false);
        fill.areaSize = new Vector2(.32f, .32f) * scale;
        fill.range = 1.6f * scale;

        Light bounce = AddLight("Lower cabin bounce - 5 cd", LightType.Point,
            new Color(.97f, .92f, .85f), 5f * scale * scale, LightUnit.Candela,
            P(.02f, .59f, .12f), P(0, .8f, .5f), up, rig, false);
        bounce.range = 1.15f * scale;

        GameObject probeObject = new GameObject("HDRP Cabin Reflection Probe");
        probeObject.transform.SetPositionAndRotation(P(0, 1.02f, .16f), Quaternion.LookRotation(forward, up));
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(1.65f, 1.2f, 2.4f) * scale;
        probe.nearClipPlane = .03f * scale;
        probe.farClipPlane = 30f * scale;
        var hdProbe = probeObject.GetComponent<HDAdditionalReflectionData>();
        if (hdProbe == null) hdProbe = probeObject.AddComponent<HDAdditionalReflectionData>();
        hdProbe.mode = ProbeSettings.Mode.Realtime;
        hdProbe.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
        hdProbe.influenceVolume.shape = InfluenceShape.Box;
        hdProbe.influenceVolume.boxSize = new Vector3(1.65f, 1.2f, 2.4f) * scale;
        hdProbe.influenceVolume.boxBlendDistancePositive = Vector3.one * (.10f * scale);
        hdProbe.influenceVolume.boxBlendDistanceNegative = Vector3.one * (.10f * scale);
        hdProbe.multiplier = 1f;

        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        {
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            if (renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(
                m => m != null && m.name.Contains("WindShield")))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        GameObject cameraObject = new GameObject("HDRP Interior Preview Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(P(.31f, 1.16f, .05f),
            Quaternion.LookRotation(P(.03f, .91f, .81f) - P(.31f, 1.16f, .05f), up));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 66f;
        camera.nearClipPlane = .025f * scale;
        camera.farClipPlane = 100f * scale;
        camera.allowHDR = true;
        var hdCamera = cameraObject.GetComponent<HDAdditionalCameraData>();
        if (hdCamera == null) hdCamera = cameraObject.AddComponent<HDAdditionalCameraData>();
        hdCamera.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
        hdCamera.volumeLayerMask = 1; // Default layer; the generated global Volume is on layer 0.
        hdCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        hdCamera.xrRendering = false; // Desktop preview only; use your existing XR camera in the VR scene.
        cameraObject.AddComponent<AudioListener>();

        hdProbe.RequestRenderNextUpdate();
        string scenePath = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/CarInterior_HDRP.unity");
        if (!EditorSceneManager.SaveScene(scene, scenePath))
        {
            Debug.LogError("The HDRP scene was created but could not be saved. Save it manually with File > Save As.");
            return;
        }
        AssetDatabase.SaveAssets();
        // Wait until the camera and Volume have been registered before requesting capture again.
        EditorApplication.delayCall += () =>
        {
            if (hdProbe != null) hdProbe.RequestRenderNextUpdate();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        };
        Selection.activeGameObject = car;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.AlignViewToObject(cameraObject.transform);
        Debug.Log("Created HDRP preview: " + scenePath +
            ". Open Game view and press Play. Brightness: HDRP Interior Look Volume > Exposure > Fixed Exposure (default 7 EV; lower = brighter).");
    }

    static void CreateEnvironment()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Car Interior HDRP Look";
        string profilePath = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/CarInterior_HDRP_Volume.asset");
        AssetDatabase.CreateAsset(profile, profilePath);

        var environment = AddOverride<VisualEnvironment>(profile);
        environment.skyType.Override(SkySettings.GetUniqueID<GradientSky>());
        environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        var sky = AddOverride<GradientSky>(profile);
        sky.top.Override(new Color(.16f, .21f, .29f));
        sky.middle.Override(new Color(.28f, .32f, .39f));
        sky.bottom.Override(new Color(.035f, .040f, .050f));
        sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);
        sky.multiplier.Override(100f);
        var exposure = AddOverride<Exposure>(profile);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(7f);
        exposure.compensation.Override(0f);
        var tone = AddOverride<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        var volume = new GameObject("HDRP Interior Look Volume").AddComponent<Volume>();
        volume.gameObject.layer = 0;
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(profile);
    }

    static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static Light AddLight(string name, LightType type, Color color, float nativeIntensity,
        LightUnit displayUnit, Vector3 position, Vector3 target, Vector3 up, Transform parent, bool shadows)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, up));
        // HDRP's factory initializes both Light and HDAdditionalLightData.
        HDAdditionalLightData hd = obj.AddHDLight(type);
        Light light = obj.GetComponent<Light>();
        light.color = color;
        light.useColorTemperature = false;
        light.lightUnit = displayUnit;
        light.intensity = nativeIntensity;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        hd.EnableShadows(shadows);
        return light;
    }

    static GameObject ResolveCarAsset()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        GameObject selected = string.IsNullOrEmpty(selectedPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(selectedPath);
        if (selected != null) return selected;
        string[] candidates = AssetDatabase.FindAssets("CarModelVR_Final")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => string.Equals(Path.GetFileName(p), "CarModelVR_Final.glb", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length == 1)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(candidates[0]);
            if (model != null) return model;
        }
        Debug.LogError("Select the imported car GLB/prefab in the Project window, then run Tools > Car Interior > Create HDRP Preview Scene. If the GLB has no prefab, import/reimport it with an HDRP-capable glTF importer first.");
        return null;
    }

    static Transform Find(Transform[] nodes, string name) => nodes.FirstOrDefault(t => t.name == name);
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
