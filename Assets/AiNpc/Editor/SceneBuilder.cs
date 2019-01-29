using System.IO;
using UnityAiNpc.Gameplay;
using UnityAiNpc.Npc;
using UnityAiNpc.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnityAiNpc.Editor
{
    /// <summary>
    /// Generates the demo scene (village square, NPC, player, UI) from primitives so the
    /// whole thing is reproducible from code. Menu: AI NPC → Rebuild Demo Scene.
    /// Batch mode: <c>-executeMethod UnityAiNpc.Editor.SceneBuilder.BuildFromCommandLine</c>.
    /// </summary>
    public static class SceneBuilder
    {
        public const string Root = "Assets/AiNpc";
        public const string ScenePath = Root + "/Scenes/NpcDemo.unity";
        public const string PersonaPath = Root + "/Personas/BrennaIronhand.asset";
        private const string MaterialsDir = Root + "/Materials";

        private static Font _font;

        [MenuItem("AI NPC/Rebuild Demo Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            Build();
            Debug.Log("[AI NPC] Demo scene built at " + ScenePath);
        }

        public static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Personas");
            EnsureFolder(MaterialsDir);
            ConfigurePlayerSettings();

            // NewScene(Single) unloads unused assets, so create the scene before loading anything it references.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var persona = CreateOrUpdatePersona();

            BuildLighting();
            BuildEnvironment();
            var npc = BuildNpc(persona);
            var player = BuildPlayer(new Vector3(0f, 0f, 7f));
            var cam = BuildCamera(player.transform);
            var ui = BuildUi(cam.GetComponent<Camera>());

            var controller = player.GetComponent<PlayerController>();
            Set(controller, "cameraTransform", cam.transform);

            var interactor = player.AddComponent<PlayerInteractor>();
            Set(interactor, "dialogue", ui);
            Set(interactor, "player", controller);
            Set(interactor, "followCamera", cam.GetComponent<FollowCamera>());

            var demo = new GameObject("Demo Autoplay").AddComponent<DemoAutoplay>();
            Set(demo, "player", controller);
            Set(demo, "interactor", interactor);
            Set(demo, "npc", npc.GetComponent<NpcInteraction>());
            Set(demo, "dialogue", ui);
            Set(demo, "followCamera", cam.GetComponent<FollowCamera>());
            demo.Lines.AddRange(DemoPersonas.DemoLines);
            EditorUtility.SetDirty(demo);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- settings & assets

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "AI NPC Showcase";
            PlayerSettings.productName = "Unity AI NPC";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; // keep talking to the API while unfocused
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // Unity blocks plain-HTTP requests by default; allow them so a local server
            // (Ollama at http://localhost:11434) and the test fake server work.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        }

        private static NpcPersona CreateOrUpdatePersona()
        {
            var persona = AssetDatabase.LoadAssetAtPath<NpcPersona>(PersonaPath);
            if (persona == null)
            {
                persona = ScriptableObject.CreateInstance<NpcPersona>();
                AssetDatabase.CreateAsset(persona, PersonaPath);
            }
            DemoPersonas.ApplyBlacksmith(persona);
            EditorUtility.SetDirty(persona);
            AssetDatabase.SaveAssets();
            return persona;
        }

        private static Material Mat(string name, Color color, float smoothness = 0.15f, Color? emission = null)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- world

        private static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.72f, 0.82f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 70f;
            RenderSettings.sun = sun;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        }

        private static void BuildEnvironment()
        {
            var world = new GameObject("World").transform;

            var ground = Prim(PrimitiveType.Plane, "Ground", world, Vector3.zero, new Vector3(8f, 1f, 8f), Mat("Grass", new Color(0.33f, 0.52f, 0.25f)));
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cylinder, "Square", world, new Vector3(0f, 0.01f, 2.5f), new Vector3(13f, 0.01f, 13f), Mat("Cobble", new Color(0.55f, 0.52f, 0.48f)), collider: false);

            // The forge behind the blacksmith.
            var stone = Mat("Stone", new Color(0.4f, 0.38f, 0.36f));
            var wood = Mat("Wood", new Color(0.42f, 0.28f, 0.17f));
            var roof = Mat("Roof", new Color(0.55f, 0.2f, 0.15f));
            var iron = Mat("Iron", new Color(0.18f, 0.18f, 0.2f), 0.6f);
            var embers = Mat("Embers", new Color(1f, 0.4f, 0.1f), 0.3f, new Color(2.5f, 0.8f, 0.15f));

            var forge = new GameObject("Forge").transform;
            forge.SetParent(world);
            forge.position = new Vector3(0f, 0f, -3.2f);
            Prim(PrimitiveType.Cube, "Back Wall", forge, new Vector3(0f, 1.6f, -1.2f), new Vector3(6f, 3.2f, 0.4f), stone);
            Prim(PrimitiveType.Cube, "Side Wall L", forge, new Vector3(-2.8f, 1.6f, 0.2f), new Vector3(0.4f, 3.2f, 3f), stone);
            Prim(PrimitiveType.Cube, "Post R", forge, new Vector3(2.8f, 1.6f, 1.5f), new Vector3(0.3f, 3.2f, 0.3f), wood);
            var roofL = Prim(PrimitiveType.Cube, "Roof", forge, new Vector3(0f, 3.55f, 0.15f), new Vector3(6.6f, 0.2f, 3.6f), roof);
            roofL.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Hearth", forge, new Vector3(-1.6f, 0.55f, -0.4f), new Vector3(1.6f, 1.1f, 1.2f), stone);
            Prim(PrimitiveType.Cube, "Coals", forge, new Vector3(-1.6f, 1.12f, -0.4f), new Vector3(1.2f, 0.08f, 0.8f), embers);
            Prim(PrimitiveType.Cube, "Chimney", forge, new Vector3(-1.6f, 2.6f, -0.8f), new Vector3(0.9f, 3f, 0.6f), stone);

            var glow = new GameObject("Forge Glow").AddComponent<Light>();
            glow.transform.SetParent(forge);
            glow.transform.localPosition = new Vector3(-1.6f, 1.6f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.2f);
            glow.range = 6f;
            glow.intensity = 2.2f;

            // Anvil in front of the forge.
            var anvil = new GameObject("Anvil").transform;
            anvil.SetParent(world);
            anvil.position = new Vector3(1.6f, 0f, -1.2f);
            Prim(PrimitiveType.Cylinder, "Stump", anvil, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.7f), wood);
            Prim(PrimitiveType.Cube, "Anvil Body", anvil, new Vector3(0f, 0.75f, 0f), new Vector3(0.35f, 0.3f, 0.3f), iron);
            Prim(PrimitiveType.Cube, "Anvil Top", anvil, new Vector3(0.05f, 0.95f, 0f), new Vector3(0.8f, 0.14f, 0.32f), iron);

            // Well, barrels and a weapon rack around the square.
            var well = new GameObject("Well").transform;
            well.SetParent(world);
            well.position = new Vector3(-4.5f, 0f, 3f);
            Prim(PrimitiveType.Cylinder, "Well Ring", well, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.45f, 1.6f), stone);
            Prim(PrimitiveType.Cylinder, "Water", well, new Vector3(0f, 0.91f, 0f), new Vector3(1.3f, 0.01f, 1.3f), Mat("Water", new Color(0.15f, 0.3f, 0.45f), 0.9f), collider: false);
            Prim(PrimitiveType.Cube, "Well Post L", well, new Vector3(-0.75f, 1.3f, 0f), new Vector3(0.12f, 1.7f, 0.12f), wood);
            Prim(PrimitiveType.Cube, "Well Post R", well, new Vector3(0.75f, 1.3f, 0f), new Vector3(0.12f, 1.7f, 0.12f), wood);
            Prim(PrimitiveType.Cube, "Well Roof", well, new Vector3(0f, 2.2f, 0f), new Vector3(1.9f, 0.12f, 1.1f), roof);

            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Barrel", world, new Vector3(3.6f + i * 0.75f, 0.5f, -2.2f + (i % 2) * 0.6f), new Vector3(0.65f, 0.5f, 0.65f), wood);

            var rack = new GameObject("Weapon Rack").transform;
            rack.SetParent(world);
            rack.position = new Vector3(-2.2f, 0f, -1.4f);
            Prim(PrimitiveType.Cube, "Rack Bar", rack, new Vector3(0f, 1.2f, 0f), new Vector3(1.6f, 0.08f, 0.08f), wood);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Blade", rack, new Vector3(-0.6f + i * 0.4f, 0.75f, 0.06f), new Vector3(0.06f, 1.1f, 0.02f), iron);

            // A ring of trees so the square doesn't end in a void.
            var trunk = Mat("Trunk", new Color(0.35f, 0.24f, 0.15f));
            var leaves = Mat("Leaves", new Color(0.2f, 0.42f, 0.2f));
            var rng = new System.Random(7);
            for (int i = 0; i < 22; i++)
            {
                float angle = i / 22f * Mathf.PI * 2f;
                float radius = 13f + (float)rng.NextDouble() * 7f;
                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius + 2f);
                float h = 1.8f + (float)rng.NextDouble() * 1.5f;
                var tree = new GameObject("Tree").transform;
                tree.SetParent(world);
                tree.position = pos;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.35f, h * 0.5f, 0.35f), trunk);
                float s = 2.2f + (float)rng.NextDouble() * 1.4f;
                Prim(PrimitiveType.Sphere, "Canopy", tree, new Vector3(0f, h + s * 0.35f, 0f), Vector3.one * s, leaves, collider: false);
            }
        }

        private static GameObject BuildNpc(NpcPersona persona)
        {
            var npc = new GameObject("Brenna (NPC)");
            npc.transform.position = Vector3.zero;

            var skin = Mat("Skin", new Color(0.86f, 0.66f, 0.52f));
            var shirt = Mat("Shirt", new Color(0.55f, 0.35f, 0.25f));
            var apron = Mat("Apron", new Color(0.25f, 0.2f, 0.17f));
            var hair = Mat("Hair", new Color(0.55f, 0.22f, 0.1f));
            var dark = Mat("Eyes", new Color(0.05f, 0.05f, 0.05f), 0.8f);
            var orbMat = Mat("Mood Orb", Color.white, 0.6f, Color.white);

            // The body keeps its collider so the player can't walk through her.
            Prim(PrimitiveType.Capsule, "Body", npc.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.85f, 0.9f, 0.7f), shirt);
            Prim(PrimitiveType.Cube, "Apron", npc.transform, new Vector3(0f, 0.75f, 0.33f), new Vector3(0.62f, 0.95f, 0.06f), apron, collider: false);
            Prim(PrimitiveType.Capsule, "Arm L", npc.transform, new Vector3(-0.5f, 1.05f, 0.05f), new Vector3(0.24f, 0.45f, 0.24f), skin, collider: false);
            Prim(PrimitiveType.Capsule, "Arm R", npc.transform, new Vector3(0.5f, 1.05f, 0.05f), new Vector3(0.24f, 0.45f, 0.24f), skin, collider: false);

            var head = new GameObject("Head").transform;
            head.SetParent(npc.transform, false);
            head.localPosition = new Vector3(0f, 1.98f, 0f);
            Prim(PrimitiveType.Sphere, "Face", head, Vector3.zero, new Vector3(0.52f, 0.56f, 0.52f), skin, collider: false);
            Prim(PrimitiveType.Sphere, "Hair", head, new Vector3(0f, 0.1f, -0.06f), new Vector3(0.56f, 0.46f, 0.56f), hair, collider: false);
            Prim(PrimitiveType.Sphere, "Eye L", head, new Vector3(-0.1f, 0.03f, 0.23f), new Vector3(0.07f, 0.09f, 0.05f), dark, collider: false);
            Prim(PrimitiveType.Sphere, "Eye R", head, new Vector3(0.1f, 0.03f, 0.23f), new Vector3(0.07f, 0.09f, 0.05f), dark, collider: false);
            Prim(PrimitiveType.Cube, "Brow L", head, new Vector3(-0.1f, 0.12f, 0.24f), new Vector3(0.11f, 0.025f, 0.03f), hair, collider: false);
            Prim(PrimitiveType.Cube, "Brow R", head, new Vector3(0.1f, 0.12f, 0.24f), new Vector3(0.11f, 0.025f, 0.03f), hair, collider: false);

            var orb = Prim(PrimitiveType.Sphere, "Mood Orb", npc.transform, new Vector3(0f, 2.65f, 0f), Vector3.one * 0.2f, orbMat, collider: false);
            orb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var conversation = npc.AddComponent<NpcConversation>();
            Set(conversation, "persona", persona);
            npc.AddComponent<NpcInteraction>();
            var visuals = npc.AddComponent<NpcMoodVisuals>();
            Set(visuals, "conversation", conversation);
            Set(visuals, "head", head);
            Set(visuals, "moodOrb", orb.GetComponent<Renderer>());
            return npc;
        }

        private static GameObject BuildPlayer(Vector3 position)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var cloak = Mat("Player Cloak", new Color(0.2f, 0.35f, 0.6f));
            Prim(PrimitiveType.Capsule, "Body", player.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), cloak, collider: false);
            Prim(PrimitiveType.Sphere, "Head", player.transform, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.45f, Mat("Player Skin", new Color(0.8f, 0.62f, 0.5f)), collider: false);
            Prim(PrimitiveType.Cube, "Pack", player.transform, new Vector3(0f, 1.1f, -0.38f), new Vector3(0.5f, 0.6f, 0.2f), Mat("Leather", new Color(0.45f, 0.3f, 0.2f)), collider: false);
            Prim(PrimitiveType.Cube, "Sword", player.transform, new Vector3(0.3f, 1.1f, -0.45f), new Vector3(0.06f, 1.1f, 0.04f), Mat("Iron", new Color(0.18f, 0.18f, 0.2f), 0.6f), collider: false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 25f);

            player.AddComponent<PlayerController>();
            return player;
        }

        private static GameObject BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.Skybox;
            go.AddComponent<AudioListener>();
            var follow = go.AddComponent<FollowCamera>();
            follow.Target = target;
            follow.SnapToTarget();
            return go;
        }

        // ---------------------------------------------------------------- UI

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.88f);
        private static readonly Color FieldColor = new Color(0.16f, 0.14f, 0.12f, 1f);
        private static readonly Color AccentColor = new Color(0.86f, 0.5f, 0.18f, 1f);

        private static DialogueUI BuildUi(Camera camera)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("UI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // so screenshots and camera renders include the UI
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            // HUD
            var title = Label(root, "Title", "Emberfold Forge  ·  AI NPC demo", 22, FontStyle.Bold, TextAnchor.UpperLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(600f, 30f));
            AddShadow(title.gameObject);
            var help = Label(root, "Controls", "WASD move  ·  E talk  ·  Enter send  ·  Esc leave", 15, FontStyle.Normal, TextAnchor.UpperLeft);
            help.color = new Color(1f, 1f, 1f, 0.85f);
            Anchor(help.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -48f), new Vector2(600f, 24f));
            AddShadow(help.gameObject);

            // "Press E" prompt
            var prompt = Panel(root, "Prompt", PanelColor);
            Anchor(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(420f, 48f));
            var promptLabel = Label(prompt.transform, "Prompt Label", "Press E to talk", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(promptLabel.rectTransform, 0f);

            // Dialogue panel
            var panel = Panel(root, "Dialogue Panel", PanelColor);
            Anchor(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-64f, 300f));
            var accent = Panel(panel.transform, "Accent", AccentColor);
            Anchor(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f));

            var nameLabel = Label(panel.transform, "NPC Name", "NPC", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            Anchor(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(0f, 34f));
            var backend = Label(panel.transform, "Backend", "", 14, FontStyle.Italic, TextAnchor.MiddleRight);
            backend.color = new Color(1f, 1f, 1f, 0.55f);
            Anchor(backend.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-62f, -8f), new Vector2(0f, 34f));

            var close = ButtonWithLabel(panel.transform, "Close Button", "×", 26, new Color(1f, 1f, 1f, 0.08f));
            Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(36f, 34f));

            // Transcript
            var scrollGo = new GameObject("Transcript", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(20f, 68f);
            scrollRect.offsetMax = new Vector2(-20f, -48f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch((RectTransform)viewport.transform, 0f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;

            // Input row
            var inputField = BuildInputField(panel.transform);
            Anchor((RectTransform)inputField.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 14f), new Vector2(-150f, 44f));
            var send = ButtonWithLabel(panel.transform, "Send Button", "Send", 20, AccentColor);
            Anchor((RectTransform)send.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(110f, 44f));

            var ui = canvasGo.AddComponent<DialogueUI>();
            Set(ui, "panel", panel.gameObject);
            Set(ui, "npcNameLabel", nameLabel);
            Set(ui, "backendLabel", backend);
            Set(ui, "transcriptScroll", scroll);
            Set(ui, "transcriptContent", contentRect);
            Set(ui, "input", inputField);
            Set(ui, "sendButton", send);
            Set(ui, "closeButton", close);
            Set(ui, "promptRoot", prompt.gameObject);
            Set(ui, "promptLabel", promptLabel);
            Set(ui, "font", _font);
            return ui;
        }

        private static InputField BuildInputField(Transform parent)
        {
            var bg = Panel(parent, "Input", FieldColor);
            var field = bg.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;

            var text = Label(bg.transform, "Text", "", 19, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch(text.rectTransform, 12f);
            var placeholder = Label(bg.transform, "Placeholder", "Say something to the blacksmith…", 19, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            Stretch(placeholder.rectTransform, 12f);

            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.caretColor = AccentColor;
            field.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            return field;
        }

        private static Button ButtonWithLabel(Transform parent, string name, string label, int size, Color color)
        {
            var img = Panel(parent, name, color);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var text = Label(img.transform, "Label", label, size, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 0f);
            return button;
        }

        private static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        private static Text Label(Transform parent, string name, string value, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void AddShadow(GameObject go)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, 0f);
            rt.offsetMax = new Vector2(-inset, 0f);
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Assigns a private [SerializeField] so the reference is saved with the scene.</summary>
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'.");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

    public static object CalculateHash(object data = null)
    {
        if (data == null) return null;
        return data;
    }