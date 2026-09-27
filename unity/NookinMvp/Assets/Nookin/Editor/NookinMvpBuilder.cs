using System;
using System.IO;
using Kirurobo;
using Nookin;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class NookinMvpBuilder
{
    private const string CharacterAsset = "Assets/Nookin/Character/nookin-rigged-wave.glb";
    private const string BaseColorAsset = "Assets/Nookin/Character/nookin-basecolor.jpg";
    private const string SimpleMaterialAsset = "Assets/Nookin/Character/NookinBaseColor.mat";
    private const string SceneAsset = "Assets/Nookin/Scenes/Main.unity";
    private const string AnimationFolder = "Assets/Nookin/Animations";

    [MenuItem("Nookin/Create MVP Scene")]
    public static void CreateScene()
    {
        EnsureAssetFolder("Assets/Nookin/Scenes");
        EnsureAssetFolder(AnimationFolder);
        AssetDatabase.ImportAsset(CharacterAsset, ImportAssetOptions.ForceUpdate);
        var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAsset);
        if (characterPrefab == null)
            throw new InvalidOperationException("glTFast did not import " + CharacterAsset + " as a GameObject.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
        character.name = "NookinCharacter";
        character.transform.position = Vector3.zero;

        var skin = character.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin == null || skin.sharedMesh == null)
            throw new InvalidOperationException("The imported GLB has no usable skinned mesh.");
        Debug.Log($"Nookin imported skin: {skin.sharedMesh.vertexCount} vertices, {skin.bones.Length} bones, localBounds={skin.localBounds}, worldBounds={skin.bounds}, transform={skin.transform.position}, root={character.transform.rotation}.");
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorAsset);
        if (baseColor == null)
            throw new InvalidOperationException("Missing GLB base-color texture: " + BaseColorAsset);
        var simpleMaterial = AssetDatabase.LoadAssetAtPath<Material>(SimpleMaterialAsset);
        if (simpleMaterial == null)
        {
            simpleMaterial = new Material(Shader.Find("Unlit/Texture"));
            AssetDatabase.CreateAsset(simpleMaterial, SimpleMaterialAsset);
        }
        simpleMaterial.mainTexture = baseColor;
        EditorUtility.SetDirty(simpleMaterial);
        skin.sharedMaterial = simpleMaterial;

        var head = FindBone(character.transform, "mixamorig:Head");
        if (head == null)
            throw new InvalidOperationException("The model has no mixamorig:Head bone.");
        var arm = FindBone(character.transform, "mixamorig:LeftArm");
        var forearm = FindBone(character.transform, "mixamorig:LeftForeArm");
        var hand = FindBone(character.transform, "mixamorig:LeftHand");
        if (arm == null || forearm == null || hand == null)
            throw new InvalidOperationException("The model has no complete left-arm bone chain for waving.");

        var idleClip = SaveHeadClip(
            AnimationFolder + "/Idle.anim", character.transform, head,
            new[] { 0f, 0.7f, 1.4f }, new[] { 0f, 1.5f, 0f }, true);
        var idleTimes = new[] { 0f, 1.4f };
        var restAngles = new[] { 0f, 0f };
        SetBoneRotationCurves(idleClip, character.transform, arm, idleTimes, restAngles);
        SetBoneRotationCurves(idleClip, character.transform, forearm, idleTimes, restAngles);
        SetBoneRotationCurves(idleClip, character.transform, hand, idleTimes, restAngles);
        var waveClip = SaveWaveClip(character.transform, arm, forearm, hand);
        var controller = CreateAnimatorController(idleClip, waveClip);
        var animator = character.GetComponent<Animator>();
        if (animator == null)
            animator = character.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        var cameraObject = new GameObject("CharacterCamera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 0.7f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        cameraObject.transform.position = new Vector3(0f, 0.5f, 3f);
        cameraObject.transform.LookAt(new Vector3(0f, 0.5f, 0f));

        var lightObject = new GameObject("SoftLight");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;
        lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        var headHit = new GameObject("HeadHitTarget");
        headHit.transform.SetParent(head, false);
        var headCollider = headHit.AddComponent<SphereCollider>();
        headCollider.center = new Vector3(0f, 0.77f, 0f);
        headCollider.radius = 0.31f;

        var bodyHit = new GameObject("BodyHitTarget");
        bodyHit.transform.SetParent(character.transform, false);
        var bodyCollider = bodyHit.AddComponent<CapsuleCollider>();
        bodyCollider.center = new Vector3(0f, 0.34f, 0f);
        bodyCollider.height = 0.6f;
        bodyCollider.radius = 0.27f;

        var events = new GameObject("EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<StandaloneInputModule>();

        var desktop = new GameObject("NookinDesktop");
        var window = desktop.AddComponent<UniWindowController>();
        var interaction = desktop.AddComponent<NookinDesktop>();
        interaction.Window = window;
        interaction.CharacterCamera = camera;
        interaction.CharacterAnimator = animator;

        EditorSceneManager.SaveScene(scene, SceneAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("Nookin MVP scene created: " + SceneAsset);
    }

    [MenuItem("Nookin/Build macOS MVP")]
    public static void BuildMacApp()
    {
        CreateScene();
        ConfigurePlayer();
        BuildScene(SceneAsset, "NookinMvp.app");
    }

    [MenuItem("Nookin/Render wave previews")]
    public static void RenderWavePreviews()
    {
        CreateScene();
        var character = GameObject.Find("NookinCharacter");
        var arm = FindBone(character.transform, "mixamorig:LeftArm");
        var skin = character.GetComponentInChildren<SkinnedMeshRenderer>();
        var camera = Camera.main;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "/Wave.anim");
        if (character == null || camera == null || clip == null || skin == null)
            throw new InvalidOperationException("Wave preview scene is incomplete.");

        var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "WavePreviews");
        Directory.CreateDirectory(output);
        var renderTexture = new RenderTexture(460, 540, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(460, 540, TextureFormat.RGBA32, false);
        var bakedMesh = new Mesh();
        var bakedObject = new GameObject("WaveBakedPreview");
        bakedObject.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
        bakedObject.transform.localScale = skin.transform.lossyScale;
        bakedObject.AddComponent<MeshFilter>().sharedMesh = bakedMesh;
        bakedObject.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
        camera.targetTexture = renderTexture;
        var frames = new[] { 0f, 0.18f, 0.38f, 0.55f, 0.72f, 0.90f, 1.15f };
        for (var i = 0; i < frames.Length; i++)
        {
            clip.SampleAnimation(character, frames[i]);
            skin.BakeMesh(bakedMesh);
            skin.enabled = false;
            camera.Render();
            skin.enabled = true;
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0, 0, 460, 540), 0, 0);
            readback.Apply();
            File.WriteAllBytes(Path.Combine(output, $"wave-{i:D2}.png"), readback.EncodeToPNG());
        }
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(readback);
        UnityEngine.Object.DestroyImmediate(bakedObject);
        UnityEngine.Object.DestroyImmediate(bakedMesh);
        Debug.Log("Nookin wave previews written: " + output);
    }

    private static void ConfigurePlayer()
    {
        PlayerSettings.productName = "Nookin MVP";
        PlayerSettings.companyName = "Nookin";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "app.nookin.mvp");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.defaultScreenWidth = 460;
        PlayerSettings.defaultScreenHeight = 540;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsFullScreen = false;
        PlayerSettings.allowFullscreenSwitch = false;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.runInBackground = true;
    }

    private static void BuildScene(string sceneAsset, string appName)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var output = Path.Combine(projectRoot, "Builds", appName);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { sceneAsset },
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("macOS build failed: " + report.summary.result);
        Debug.Log("Nookin MVP built: " + output);
    }

    private static void EnsureAssetFolder(string path)
    {
        var segments = path.Split('/');
        var parent = segments[0];
        for (var i = 1; i < segments.Length; i++)
        {
            var child = parent + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(child))
                AssetDatabase.CreateFolder(parent, segments[i]);
            parent = child;
        }
    }

    private static Transform FindBone(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    private static AnimationClip SaveHeadClip(
        string path, Transform root, Transform head, float[] times, float[] angles, bool loop)
    {
        if (times.Length != angles.Length)
            throw new ArgumentException("Keyframe times and angles must match.");

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = 30f };
            AssetDatabase.CreateAsset(clip, path);
        }

        var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        var original = head.localRotation;
        for (var i = 0; i < times.Length; i++)
        {
            var rotation = original * Quaternion.Euler(angles[i], 0f, 0f);
            curves[0].AddKey(times[i], rotation.x);
            curves[1].AddKey(times[i], rotation.y);
            curves[2].AddKey(times[i], rotation.z);
            curves[3].AddKey(times[i], rotation.w);
        }

        var bonePath = AnimationUtility.CalculateTransformPath(head, root);
        var channels = new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        for (var i = 0; i < channels.Length; i++)
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), channels[i]), curves[i]);
        clip.EnsureQuaternionContinuity();

        var serialized = new SerializedObject(clip);
        var loopSetting = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopSetting != null)
        {
            loopSetting.boolValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip SaveWaveClip(Transform root, Transform arm, Transform forearm, Transform hand)
    {
        const string path = AnimationFolder + "/Wave.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = 30f };
            AssetDatabase.CreateAsset(clip, path);
        }

        var times = new[] { 0f, 0.18f, 0.38f, 0.55f, 0.72f, 0.90f, 1.15f, 1.35f };
        SetBoneRotationCurves(clip, root, arm, times,
            new[] { 0f, -60f, -64f, -64f, -64f, -60f, 0f, 0f });
        SetBoneRotationCurves(clip, root, forearm, times,
            new[] { 0f, -5f, -20f, 5f, -22f, 8f, 0f, 0f });
        SetBoneRotationCurves(clip, root, hand, times,
            new[] { 0f, 8f, -18f, 18f, -18f, 10f, 0f, 0f });
        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetBoneRotationCurves(
        AnimationClip clip, Transform root, Transform bone, float[] times, float[] angles)
    {
        if (times.Length != angles.Length)
            throw new ArgumentException("Keyframe times and angles must match.");

        var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        var original = bone.localRotation;
        for (var i = 0; i < times.Length; i++)
        {
            var rotation = original * Quaternion.Euler(0f, 0f, angles[i]);
            curves[0].AddKey(times[i], rotation.x);
            curves[1].AddKey(times[i], rotation.y);
            curves[2].AddKey(times[i], rotation.z);
            curves[3].AddKey(times[i], rotation.w);
        }

        var bonePath = AnimationUtility.CalculateTransformPath(bone, root);
        var channels = new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        for (var i = 0; i < channels.Length; i++)
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), channels[i]), curves[i]);
        EditorUtility.SetDirty(clip);
    }

    private static AnimatorController CreateAnimatorController(AnimationClip idle, AnimationClip wave)
    {
        const string path = AnimationFolder + "/Nookin.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null)
            AssetDatabase.DeleteAsset(path);
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
        var states = controller.layers[0].stateMachine;
        var idleState = states.AddState("Idle");
        idleState.motion = idle;
        states.defaultState = idleState;
        var waveState = states.AddState("Wave");
        waveState.motion = wave;

        var respond = idleState.AddTransition(waveState);
        respond.hasExitTime = false;
        respond.duration = 0.08f;
        respond.AddCondition(AnimatorConditionMode.If, 0f, "Wave");

        var returnToIdle = waveState.AddTransition(idleState);
        returnToIdle.hasExitTime = true;
        returnToIdle.exitTime = 0.98f;
        returnToIdle.duration = 0.12f;
        return controller;
    }
}
