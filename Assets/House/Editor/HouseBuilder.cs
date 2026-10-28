using DSH.Assistant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PaperHouse.EditorTools
{
    /// <summary>
    /// Собирает дом с интерьером, столом, бесконечной бумагой и FPS-игроком.
    /// Меню: Tools > Paper House > Пересобрать дом.
    /// </summary>
    public static class HouseBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string HouseFolder = "Assets/House";
        private const string MaterialsFolder = HouseFolder + "/Materials";
        private const string MeshesFolder = HouseFolder + "/Meshes";
        private const string AudioFolder = HouseFolder + "/Audio";
        private const string PrefabPath = HouseFolder + "/House.prefab";

        private static Material grass;
        private static Material pathMat;
        private static Material stone;
        private static Material plaster;
        private static Material roofMat;
        private static Material wood;
        private static Material darkWood;
        private static Material paperMat;
        private static Material felt;
        private static Material glassMat;
        private static Material brick;
        private static Material metal;
        private static Material floorMat;
        private static Material lampShade;
        private static Material bulbMat;
        private static Material bushMat;
        private static Material trim;
        private static Material townWallA;
        private static Material townWallB;
        private static Material townWallC;
        private static Material townRoof;
        private static Material townWindow;
        private static Material laptopMat;
        private static Material laptopScreen;
        private static Material trashMat;
        private static Material trashLidMat;
        private static Material wheelMat;
        private static Material ceilingBulb;
        private static Material nightSky;
        private static Material moonMat;
        private static Material starMat;

        [MenuItem("Tools/Paper House/Пересобрать дом", false, 1)]
        public static void BuildMenu()
        {
            Build();
        }

        /// <summary>Точка входа для Unity -executeMethod.</summary>
        public static void BuildBatch()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolders();
            CreateMaterials();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Camera camera = Camera.main;
            if (camera != null && camera.transform.parent != null &&
                camera.transform.parent.name == "Player")
            {
                camera.transform.SetParent(null, true);
            }

            RemoveRoot(scene, "House");
            RemoveRoot(scene, "Player");

            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }

            var house = new GameObject("House");
            PaperMatInteractable mat = BuildHouse(house.transform);

            var laptop = Object.FindAnyObjectByType<LaptopInteractable>();
            var chair = Object.FindAnyObjectByType<ChairInteractable>();
            var trash = Object.FindAnyObjectByType<TrashBinInteractable>();
            if (trash != null)
                trash.mat = mat;

            GameObject player = BuildPlayer(camera, mat, laptop, chair);
            var audioService = player.GetComponent<AudioService>();
            if (audioService != null)
                audioService.ambientSource = house.GetComponent<AudioSource>();

            ConfigureLighting(house.transform);
            HideAssistantOverlay();
            SavePrefab(house);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int objects = house.GetComponentsInChildren<Transform>(true).Length;
            Debug.Log("[PaperHouse] BUILD_OK houseObjects=" + objects +
                      " player=" + player.name +
                      " prefab=" + PrefabPath);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(HouseFolder))
                AssetDatabase.CreateFolder("Assets", "House");
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(HouseFolder, "Materials");
            if (!AssetDatabase.IsValidFolder(MeshesFolder))
                AssetDatabase.CreateFolder(HouseFolder, "Meshes");
            if (!AssetDatabase.IsValidFolder(HouseFolder + "/Runtime"))
                AssetDatabase.CreateFolder(HouseFolder, "Runtime");
            if (!AssetDatabase.IsValidFolder(AudioFolder))
                AssetDatabase.CreateFolder(HouseFolder, "Audio");
        }

        private static void CreateMaterials()
        {
            plaster = GetOrCreateMaterial("Plaster", new Color(0.88f, 0.82f, 0.69f), 0.12f, 0f);
            roofMat = GetOrCreateMaterial("Roof", new Color(0.34f, 0.10f, 0.08f), 0.22f, 0f);
            wood = GetOrCreateMaterial("Wood", new Color(0.34f, 0.19f, 0.09f), 0.20f, 0f);
            darkWood = GetOrCreateMaterial("WoodDark", new Color(0.20f, 0.10f, 0.04f), 0.18f, 0f);
            stone = GetOrCreateMaterial("Stone", new Color(0.46f, 0.44f, 0.40f), 0.10f, 0f);
            brick = GetOrCreateMaterial("Brick", new Color(0.42f, 0.16f, 0.10f), 0.10f, 0f);
            pathMat = GetOrCreateMaterial("Path", new Color(0.55f, 0.51f, 0.43f), 0.08f, 0f);
            grass = GetOrCreateMaterial("Grass", new Color(0.25f, 0.42f, 0.18f), 0.05f, 0f);
            floorMat = GetOrCreateMaterial("Floor", new Color(0.44f, 0.27f, 0.15f), 0.18f, 0f);
            paperMat = GetOrCreateMaterial("Paper", new Color(0.97f, 0.95f, 0.87f), 0.06f, 0f);
            felt = GetOrCreateMaterial("Felt", new Color(0.14f, 0.24f, 0.17f), 0.04f, 0f);
            metal = GetOrCreateMaterial("Metal", new Color(0.72f, 0.58f, 0.25f), 0.75f, 0.90f);
            lampShade = GetOrCreateMaterial("LampShade", new Color(0.16f, 0.23f, 0.17f), 0.25f, 0f);
            bushMat = GetOrCreateMaterial("Bush", new Color(0.16f, 0.31f, 0.12f), 0.05f, 0f);
            bulbMat = GetOrCreateMaterial("Bulb", new Color(1f, 0.88f, 0.62f), 0.4f, 0f,
                new Color(4f, 2.5f, 1.1f));
            glassMat = GetOrCreateGlassMaterial("WindowGlass",
                new Color(0.62f, 0.80f, 0.92f, 0.22f));
            trim = GetOrCreateMaterial("Trim", new Color(0.80f, 0.74f, 0.62f), 0.16f, 0f);
            townWallA = GetOrCreateMaterial("TownWallA", new Color(0.82f, 0.76f, 0.64f), 0.10f, 0f);
            townWallB = GetOrCreateMaterial("TownWallB", new Color(0.70f, 0.77f, 0.73f), 0.10f, 0f);
            townWallC = GetOrCreateMaterial("TownWallC", new Color(0.80f, 0.68f, 0.52f), 0.10f, 0f);
            townRoof = GetOrCreateMaterial("TownRoof", new Color(0.24f, 0.28f, 0.34f), 0.18f, 0f);
            townWindow = GetOrCreateUnlitMaterial("TownWindow", new Color(1f, 0.76f, 0.38f));
            laptopMat = GetOrCreateMaterial("Laptop", new Color(0.08f, 0.09f, 0.10f), 0.4f, 0.6f);
            laptopScreen = GetOrCreateUnlitMaterial("LaptopScreen", new Color(0.12f, 0.58f, 0.92f));
            trashMat = GetOrCreateMaterial("Trash", new Color(0.10f, 0.30f, 0.12f), 0.35f, 0.05f);
            trashLidMat = GetOrCreateMaterial("TrashLid", new Color(0.08f, 0.24f, 0.10f), 0.35f, 0.05f);
            wheelMat = GetOrCreateMaterial("Wheel", new Color(0.035f, 0.035f, 0.035f), 0.2f, 0.1f);
            ceilingBulb = GetOrCreateUnlitMaterial("CeilingBulb", new Color(1f, 0.91f, 0.72f));
            moonMat = GetOrCreateUnlitMaterial("Moon", new Color(0.72f, 0.80f, 1f));
            starMat = GetOrCreateUnlitMaterial("Star", new Color(0.82f, 0.90f, 1f));
            nightSky = GetOrCreateNightSky();
        }

        private static Material GetOrCreateNightSky()
        {
            const string path = MaterialsFolder + "/NightSky.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader == null) return null;
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_SkyTint"))
                material.SetColor("_SkyTint", new Color(0.045f, 0.065f, 0.15f));
            if (material.HasProperty("_GroundColor"))
                material.SetColor("_GroundColor", new Color(0.015f, 0.02f, 0.045f));
            if (material.HasProperty("_Exposure"))
                material.SetFloat("_Exposure", 0.55f);
            if (material.HasProperty("_AtmosphereThickness"))
                material.SetFloat("_AtmosphereThickness", 0.28f);
            if (material.HasProperty("_SunSize"))
                material.SetFloat("_SunSize", 0f);
            if (material.HasProperty("_SunDisk"))
                material.SetFloat("_SunDisk", 0f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness,
            float metallic, Color? emission = null)
        {
            string assetPath = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.name = name;
            SetColor(material, color);
            SetFloat(material, "_Smoothness", smoothness);
            SetFloat(material, "_Metallic", metallic);

            // Возвращаем материал в непрозрачный режим, если он был стеклом.
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            SetFloat(material, "_Cull", 2f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            material.SetShaderPassEnabled("ShadowCaster", true);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateUnlitMaterial(string name, Color color)
        {
            string assetPath = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.name = name;
            SetColor(material, color);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateGlassMaterial(string name, Color color)
        {
            Material material = GetOrCreateMaterial(name, color, 0.95f, 0f);
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            SetFloat(material, "_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);

            // URP Lit иногда нормализует blend-поля при смене Surface;
            // выставляем их в самом конце, чтобы стекло было именно Alpha.
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_SrcBlendAlpha", 5f);
            material.SetFloat("_DstBlendAlpha", 10f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static AudioClip LoadClip(string fileName)
        {
            string basePath = AudioFolder + "/" + fileName;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + ".wav");
            if (clip == null)
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + ".mp3");
            if (clip == null)
                Debug.LogWarning("[PaperHouse] Не найден звук: " + basePath + ".(wav|mp3)");
            return clip;
        }

        private static AudioSource AddSpatialAudio(GameObject target, AudioClip clip, float volume)
        {
            var source = target.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 10f;
            source.volume = volume;
            source.clip = clip;
            return source;
        }

        private static PaperMatInteractable BuildHouse(Transform root)
        {
            // Большой газон — по нему можно ходить, он же закрывает «пустоту» горизонта.
            Box(root, "Ground", new Vector3(0f, -0.06f, 0f), new Vector3(80f, 0.12f, 80f), grass);

            Box(root, "Foundation", new Vector3(0f, 0.1f, 0f), new Vector3(7.4f, 0.2f, 6.4f), stone);
            Box(root, "Floor", new Vector3(0f, 0.225f, 0f), new Vector3(6.8f, 0.05f, 5.8f), floorMat);
            Box(root, "Ceiling", new Vector3(0f, 3.25f, 0f), new Vector3(6.8f, 0.1f, 5.8f), plaster);

            BuildWalls(root);
            BuildWindows(root);
            BuildDoor(root);
            BuildRoof(root);
            BuildCornerTrim(root);

            PaperMatInteractable mat = BuildInterior(root);
            BuildExterior(root);
            BuildMiniTown(root);
            BuildGreenery(root);
            BuildNightSky(root);

            var ambient = root.gameObject.AddComponent<AudioSource>();
            ambient.clip = LoadClip("ambient_wind");
            ambient.loop = true;
            ambient.playOnAwake = true;
            ambient.spatialBlend = 0f;
            ambient.volume = 0.10f;

            return mat;
        }

        private static void BuildWalls(Transform root)
        {
            const float height = 3f;
            const float centerY = 1.7f;
            const float thickness = 0.2f;

            // Передняя стена: дверь по центру и два окна.
            Box(root, "Front Wall Left", new Vector3(-3.15f, centerY, -3f),
                new Vector3(0.7f, height, thickness), plaster);
            Box(root, "Front Window L Below", new Vector3(-2.2f, 0.75f, -3f),
                new Vector3(1.2f, 1.1f, thickness), plaster);
            Box(root, "Front Window L Above", new Vector3(-2.2f, 2.85f, -3f),
                new Vector3(1.2f, 0.7f, thickness), plaster);
            Box(root, "Front Wall Mid Left", new Vector3(-1.125f, centerY, -3f),
                new Vector3(0.95f, height, thickness), plaster);
            Box(root, "Front Door Above", new Vector3(0f, 2.875f, -3f),
                new Vector3(1.3f, 0.65f, thickness), plaster);
            Box(root, "Front Wall Mid Right", new Vector3(1.125f, centerY, -3f),
                new Vector3(0.95f, height, thickness), plaster);
            Box(root, "Front Window R Below", new Vector3(2.2f, 0.75f, -3f),
                new Vector3(1.2f, 1.1f, thickness), plaster);
            Box(root, "Front Window R Above", new Vector3(2.2f, 2.85f, -3f),
                new Vector3(1.2f, 0.7f, thickness), plaster);
            Box(root, "Front Wall Right", new Vector3(3.15f, centerY, -3f),
                new Vector3(0.7f, height, thickness), plaster);

            // Задняя стена без окон — к ней придвинут стол.
            Box(root, "Back Wall", new Vector3(0f, centerY, 3f),
                new Vector3(7f, height, thickness), plaster);

            // Левая стена с окном.
            Box(root, "Left Wall Front", new Vector3(-3.5f, centerY, -2.2f),
                new Vector3(thickness, height, 1.6f), plaster);
            Box(root, "Left Window Below", new Vector3(-3.5f, 0.75f, -0.8f),
                new Vector3(thickness, 1.1f, 1.2f), plaster);
            Box(root, "Left Window Above", new Vector3(-3.5f, 2.85f, -0.8f),
                new Vector3(thickness, 0.7f, 1.2f), plaster);
            Box(root, "Left Wall Back", new Vector3(-3.5f, centerY, 1.4f),
                new Vector3(thickness, height, 3.2f), plaster);

            // Правая стена с окном.
            Box(root, "Right Wall Front", new Vector3(3.5f, centerY, -1.5f),
                new Vector3(thickness, height, 3f), plaster);
            Box(root, "Right Window Below", new Vector3(3.5f, 0.75f, 0.6f),
                new Vector3(thickness, 1.1f, 1.2f), plaster);
            Box(root, "Right Window Above", new Vector3(3.5f, 2.85f, 0.6f),
                new Vector3(thickness, 0.7f, 1.2f), plaster);
            Box(root, "Right Wall Back", new Vector3(3.5f, centerY, 2.1f),
                new Vector3(thickness, height, 1.8f), plaster);
        }

        private static void BuildWindows(Transform root)
        {
            CreateWindow(root, "Window Front Left", new Vector3(-2.2f, 1.9f, -3f), Quaternion.identity);
            CreateWindow(root, "Window Front Right", new Vector3(2.2f, 1.9f, -3f), Quaternion.identity);
            CreateWindow(root, "Window Left", new Vector3(-3.5f, 1.9f, -0.8f), Quaternion.Euler(0f, 90f, 0f));
            CreateWindow(root, "Window Right", new Vector3(3.5f, 1.9f, 0.6f), Quaternion.Euler(0f, -90f, 0f));
        }

        private static void CreateWindow(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            const float width = 1.2f;
            const float height = 1.2f;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;

            Box(root.transform, "Frame Top", new Vector3(0f, height / 2f + 0.04f, 0f),
                new Vector3(width + 0.16f, 0.08f, 0.26f), plaster);
            Box(root.transform, "Frame Bottom", new Vector3(0f, -height / 2f - 0.04f, 0f),
                new Vector3(width + 0.16f, 0.08f, 0.26f), plaster);
            Box(root.transform, "Frame Left", new Vector3(-width / 2f - 0.04f, 0f, 0f),
                new Vector3(0.08f, height + 0.16f, 0.26f), plaster);
            Box(root.transform, "Frame Right", new Vector3(width / 2f + 0.04f, 0f, 0f),
                new Vector3(0.08f, height + 0.16f, 0.26f), plaster);

            GameObject glass = Box(root.transform, "Glass", Vector3.zero,
                new Vector3(width - 0.04f, height - 0.04f, 0.03f), glassMat);
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            Box(root.transform, "Mullion V", new Vector3(0f, 0f, -0.02f),
                new Vector3(0.05f, height - 0.06f, 0.04f), darkWood);
            Box(root.transform, "Mullion H", new Vector3(0f, 0f, -0.02f),
                new Vector3(width - 0.06f, 0.05f, 0.04f), darkWood);

            Box(root.transform, "Sill", new Vector3(0f, -height / 2f - 0.08f, 0.04f),
                new Vector3(width + 0.24f, 0.1f, 0.34f), stone);
        }

        private static void BuildDoor(Transform root)
        {
            var pivot = new GameObject("Door Pivot");
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(-0.65f, 0.25f, -3f);

            var doorRoot = new GameObject("Door Root");
            doorRoot.transform.SetParent(pivot.transform, false);

            // Дверь ровно в проём, с нахлёстом сверху — щели над дверью нет.
            Box(doorRoot.transform, "Door", new Vector3(0.65f, 1.175f, 0f),
                new Vector3(1.3f, 2.35f, 0.08f), wood);
            Box(doorRoot.transform, "Door Panel", new Vector3(0.65f, 1.175f, -0.05f),
                new Vector3(0.92f, 1.55f, 0.02f), darkWood);
            Sphere(doorRoot.transform, "Handle", new Vector3(1.15f, 1.02f, -0.08f),
                new Vector3(0.09f, 0.09f, 0.09f), metal);

            Box(root, "Door Frame Left", new Vector3(-0.72f, 1.45f, -3.02f),
                new Vector3(0.12f, 2.5f, 0.18f), plaster);
            Box(root, "Door Frame Right", new Vector3(0.72f, 1.45f, -3.02f),
                new Vector3(0.12f, 2.5f, 0.18f), plaster);
            Box(root, "Door Frame Top", new Vector3(0f, 2.64f, -3.02f),
                new Vector3(1.56f, 0.14f, 0.18f), plaster);
            Box(root, "Threshold", new Vector3(0f, 0.22f, -3f),
                new Vector3(1.4f, 0.06f, 0.3f), stone);

            var door = pivot.AddComponent<DoorInteractable>();
            door.pivot = pivot.transform;
            door.openAngle = -105f;
            door.audioSource = AddSpatialAudio(pivot, LoadClip("closed-wooden-door"), 0.9f);
            door.openClip = LoadClip("closed-wooden-door");
            door.closeClip = LoadClip("door_close");
        }

        private static void BuildRoof(Transform root)
        {
            Mesh mesh = BuildRoofMesh(3.9f, 3.4f, 3.3f, 5.2f, "HouseRoof");
            const string meshPath = MeshesFolder + "/HouseRoof.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var roofObject = new GameObject("Roof", typeof(MeshFilter), typeof(MeshRenderer));
            roofObject.transform.SetParent(root, false);
            roofObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            roofObject.GetComponent<MeshRenderer>().sharedMaterials = new[] { roofMat, plaster };

            // Лобовые доски по краям фронтонов — закрывают стык крыши и стены.
            AddBargeboard(root, "Barge Front Left", new Vector3(-1.95f, 4.25f, -3.44f), 26f);
            AddBargeboard(root, "Barge Front Right", new Vector3(1.95f, 4.25f, -3.44f), -26f);
            AddBargeboard(root, "Barge Back Left", new Vector3(-1.95f, 4.25f, 3.44f), 26f);
            AddBargeboard(root, "Barge Back Right", new Vector3(1.95f, 4.25f, 3.44f), -26f);
        }

        private static void AddBargeboard(Transform root, string name, Vector3 position, float angle)
        {
            GameObject board = Box(root, name, position, new Vector3(4.45f, 0.12f, 0.1f), trim);
            board.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static Mesh BuildRoofMesh(float halfWidth, float halfDepth, float baseY, float ridgeY,
            string meshName)
        {
            var vertices = new[]
            {
                new Vector3(-halfWidth, baseY, -halfDepth),
                new Vector3(halfWidth, baseY, -halfDepth),
                new Vector3(0f, ridgeY, -halfDepth),
                new Vector3(-halfWidth, baseY, halfDepth),
                new Vector3(halfWidth, baseY, halfDepth),
                new Vector3(0f, ridgeY, halfDepth)
            };

            var slopes = new[]
            {
                0, 3, 5, 0, 5, 2,
                1, 2, 5, 1, 5, 4,
                0, 1, 4, 0, 4, 3
            };
            var gables = new[] { 0, 2, 1, 3, 4, 5 };

            var mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(slopes, 0);
            mesh.SetTriangles(gables, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static PaperMatInteractable BuildInterior(Transform root)
        {
            var table = new GameObject("Table");
            table.transform.SetParent(root, false);

            Box(table.transform, "Table Top", new Vector3(0f, 1.12f, 2.5f),
                new Vector3(6f, 0.08f, 0.75f), wood);
            Box(table.transform, "Leg L1", new Vector3(-2.8f, 0.665f, 2.2f),
                new Vector3(0.1f, 0.83f, 0.1f), darkWood);
            Box(table.transform, "Leg L2", new Vector3(-2.8f, 0.665f, 2.8f),
                new Vector3(0.1f, 0.83f, 0.1f), darkWood);
            Box(table.transform, "Leg R1", new Vector3(2.8f, 0.665f, 2.2f),
                new Vector3(0.1f, 0.83f, 0.1f), darkWood);
            Box(table.transform, "Leg R2", new Vector3(2.8f, 0.665f, 2.8f),
                new Vector3(0.1f, 0.83f, 0.1f), darkWood);

            // Бесконечная стопка бумаги слева.
            var stack = new GameObject("Paper Stack");
            stack.transform.SetParent(table.transform, false);
            stack.transform.localPosition = new Vector3(-2f, 1.17f, 2.5f);
            var stackCollider = stack.AddComponent<BoxCollider>();
            stackCollider.size = new Vector3(0.7f, 0.3f, 0.9f);
            stackCollider.center = new Vector3(0f, 0.15f, 0f);
            stackCollider.isTrigger = true;
            var stackScript = stack.AddComponent<PaperStackInteractable>();
            AudioClip pickClip = LoadClip("paper_pick");
            stackScript.audioSource = AddSpatialAudio(stack, pickClip, 0.8f);
            stackScript.pickClip = pickClip;

            for (int i = 0; i < 24; i++)
            {
                GameObject sheet = Box(stack.transform, "Sheet " + (i + 1),
                    new Vector3(0f, 0.006f + i * 0.007f, 0f),
                    new Vector3(0.56f, 0.006f, 0.76f), paperMat);
                sheet.transform.localRotation = Quaternion.Euler(0f, (i % 3 - 1) * 1.5f, 0f);
                Object.DestroyImmediate(sheet.GetComponent<Collider>());
            }

            // Коврик справа от стопки.
            var matObject = Box(table.transform, "Paper Mat", new Vector3(-0.75f, 1.165f, 2.5f),
                new Vector3(1.05f, 0.02f, 0.62f), felt);
            var mat = matObject.AddComponent<PaperMatInteractable>();
            mat.paperMaterial = paperMat;
            AudioClip placeClip = LoadClip("paper_place");
            mat.audioSource = AddSpatialAudio(matObject, placeClip, 0.8f);
            mat.placeClip = placeClip;

            var sheets = new GameObject("Placed Sheets");
            sheets.transform.SetParent(table.transform, false);
            sheets.transform.localPosition = new Vector3(-0.75f, 1.18f, 2.5f);
            mat.sheetParent = sheets.transform;

            // Настольная лампа справа.
            var lamp = new GameObject("Desk Lamp");
            lamp.transform.SetParent(table.transform, false);
            lamp.transform.localPosition = new Vector3(2.6f, 1.16f, 2.5f);
            // Cylinder в Unity имеет высоту 2, поэтому scale.y — это половина высоты.
            Cylinder(lamp.transform, "Base", new Vector3(0f, 0.025f, 0f),
                new Vector3(0.22f, 0.025f, 0.22f), metal);
            Cylinder(lamp.transform, "Stem", new Vector3(0f, 0.24f, 0f),
                new Vector3(0.035f, 0.22f, 0.035f), metal);
            Cylinder(lamp.transform, "Shade", new Vector3(0f, 0.50f, 0f),
                new Vector3(0.29f, 0.14f, 0.29f), lampShade);
            GameObject bulb = Sphere(lamp.transform, "Bulb", new Vector3(0f, 0.43f, 0f),
                new Vector3(0.14f, 0.14f, 0.14f), bulbMat);

            var lampLightObject = new GameObject("Lamp Light");
            lampLightObject.transform.SetParent(lamp.transform, false);
            lampLightObject.transform.localPosition = new Vector3(0f, 0.43f, 0f);
            var lampLight = lampLightObject.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.range = 3.2f;
            lampLight.intensity = 1.6f;
            lampLight.color = new Color(1f, 0.72f, 0.42f);
            lampLight.shadows = LightShadows.Soft;

            var lampScript = lamp.AddComponent<LampInteractable>();
            lampScript.lampLight = lampLight;
            lampScript.bulbRenderer = bulb.GetComponent<Renderer>();
            AudioClip clickClip = LoadClip("lamp_click");
            lampScript.audioSource = AddSpatialAudio(lamp, clickClip, 0.8f);
            lampScript.clickClip = clickClip;

            BuildLaptop(table.transform);
            BuildChair(root);

            return mat;
        }

        private static void BuildExterior(Transform root)
        {
            Box(root, "Porch", new Vector3(0f, 0.15f, -3.65f), new Vector3(2.8f, 0.18f, 1.2f), stone);
            Box(root, "Step", new Vector3(0f, 0.06f, -4.32f), new Vector3(1.7f, 0.12f, 0.5f), stone);
            Box(root, "Path", new Vector3(0f, 0.03f, -6.8f), new Vector3(1.4f, 0.06f, 6.4f), pathMat);

            BuildTrashBin(root);
        }

        private static void BuildCornerTrim(Transform root)
        {
            var corners = new[]
            {
                new Vector3(-3.5f, 1.7f, -3f),
                new Vector3(3.5f, 1.7f, -3f),
                new Vector3(-3.5f, 1.7f, 3f),
                new Vector3(3.5f, 1.7f, 3f)
            };
            for (int i = 0; i < corners.Length; i++)
                Box(root, "Corner Trim " + (i + 1), corners[i], new Vector3(0.28f, 3f, 0.28f), trim);

            Box(root, "Fascia Front", new Vector3(0f, 3.24f, -3.06f),
                new Vector3(7.12f, 0.14f, 0.18f), trim);
            Box(root, "Fascia Back", new Vector3(0f, 3.24f, 3.06f),
                new Vector3(7.12f, 0.14f, 0.18f), trim);
            Box(root, "Fascia Left", new Vector3(-3.56f, 3.24f, 0f),
                new Vector3(0.18f, 0.14f, 6.12f), trim);
            Box(root, "Fascia Right", new Vector3(3.56f, 3.24f, 0f),
                new Vector3(0.18f, 0.14f, 6.12f), trim);
        }

        private static void BuildMiniTown(Transform root)
        {
            var town = new GameObject("Mini Town");
            town.transform.SetParent(root, false);

            Box(town.transform, "Village Road", new Vector3(0f, 0.02f, -7.3f),
                new Vector3(40f, 0.04f, 2.2f), pathMat);

            Mesh townRoofMesh = BuildRoofMesh(2f, 1.65f, 2.2f, 3.2f, "TownRoof");
            const string townRoofPath = MeshesFolder + "/TownRoof.asset";
            AssetDatabase.DeleteAsset(townRoofPath);
            AssetDatabase.CreateAsset(townRoofMesh, townRoofPath);

            var walls = new[] { townWallA, townWallB, townWallC };
            var positions = new[]
            {
                new Vector3(-12f, 0f, -4f),
                new Vector3(-18f, 0f, 3f),
                new Vector3(-10f, 0f, 11f),
                new Vector3(-22f, 0f, 11f),
                new Vector3(-7f, 0f, 18f),
                new Vector3(12f, 0f, -4f),
                new Vector3(18f, 0f, 3f),
                new Vector3(10f, 0f, 11f),
                new Vector3(22f, 0f, 11f),
                new Vector3(7f, 0f, 18f)
            };
            var rotations = new[] { 12f, 8f, -5f, -8f, 0f, -12f, -8f, 5f, 8f, 0f };

            for (int i = 0; i < positions.Length; i++)
            {
                BuildMiniHouse(town.transform, "Town House " + (i + 1), positions[i], rotations[i],
                    walls[i % walls.Length], townRoofMesh);
            }

            BuildStreetLamp(town.transform, new Vector3(-10f, 0f, -6.8f));
            BuildStreetLamp(town.transform, new Vector3(10f, 0f, -6.8f));
        }

        private static void BuildMiniHouse(Transform parent, string name, Vector3 position,
            float rotation, Material wallMaterial, Mesh roofMesh)
        {
            var house = new GameObject(name);
            house.transform.SetParent(parent, false);
            house.transform.localPosition = position;
            house.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);

            Box(house.transform, "Foundation", new Vector3(0f, 0.1f, 0f),
                new Vector3(3.5f, 0.2f, 2.9f), stone);
            Box(house.transform, "Walls", new Vector3(0f, 1.2f, 0f),
                new Vector3(3.2f, 2.2f, 2.6f), wallMaterial);

            Box(house.transform, "Door", new Vector3(0f, 0.9f, -1.32f),
                new Vector3(0.68f, 1.45f, 0.08f), darkWood);
            Box(house.transform, "Step", new Vector3(0f, 0.06f, -1.72f),
                new Vector3(1.1f, 0.12f, 0.55f), stone);

            CreateMiniWindow(house.transform, "Window L", new Vector3(-1.05f, 1.55f, -1.31f));
            CreateMiniWindow(house.transform, "Window R", new Vector3(1.05f, 1.55f, -1.31f));

            var roof = new GameObject("Roof", typeof(MeshFilter), typeof(MeshRenderer));
            roof.transform.SetParent(house.transform, false);
            roof.GetComponent<MeshFilter>().sharedMesh = roofMesh;
            // Тёмный фронтон читается как крыша даже на расстоянии.
            roof.GetComponent<MeshRenderer>().sharedMaterials = new[] { townRoof, townRoof };
        }

        private static void CreateMiniWindow(Transform parent, string name, Vector3 position)
        {
            var window = new GameObject(name);
            window.transform.SetParent(parent, false);
            window.transform.localPosition = position;

            Box(window.transform, "Frame", Vector3.zero, new Vector3(0.64f, 0.64f, 0.07f), trim);
            Box(window.transform, "Glass", new Vector3(0f, 0f, -0.05f),
                new Vector3(0.5f, 0.5f, 0.03f), townWindow);
            Box(window.transform, "Mullion V", new Vector3(0f, 0f, -0.08f),
                new Vector3(0.04f, 0.5f, 0.03f), darkWood);
            Box(window.transform, "Mullion H", new Vector3(0f, 0f, -0.08f),
                new Vector3(0.5f, 0.04f, 0.03f), darkWood);
        }

        private static void BuildGreenery(Transform root)
        {
            var greenery = new GameObject("Greenery");
            greenery.transform.SetParent(root, false);

            AddTree(greenery.transform, new Vector3(-7f, 0f, -1.5f), 1.05f);
            AddTree(greenery.transform, new Vector3(7f, 0f, -1.5f), 1.0f);
            AddTree(greenery.transform, new Vector3(-9f, 0f, 5f), 1.15f);
            AddTree(greenery.transform, new Vector3(9f, 0f, 5f), 1.1f);
            AddTree(greenery.transform, new Vector3(-15f, 0f, -1f), 0.95f);
            AddTree(greenery.transform, new Vector3(15f, 0f, -1f), 1.0f);
            AddTree(greenery.transform, new Vector3(-4f, 0f, 15f), 1.2f);
            AddTree(greenery.transform, new Vector3(4f, 0f, 15f), 1.15f);
            AddTree(greenery.transform, new Vector3(-20f, 0f, 17f), 1.0f);
            AddTree(greenery.transform, new Vector3(20f, 0f, 17f), 1.05f);
        }

        private static void AddTree(Transform parent, Vector3 position, float scale)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;

            Cylinder(tree.transform, "Trunk", new Vector3(0f, 0.9f * scale, 0f),
                new Vector3(0.17f * scale, 0.9f * scale, 0.17f * scale), darkWood);

            GameObject leaves = Sphere(tree.transform, "Leaves 1", new Vector3(0f, 2.05f * scale, 0f),
                new Vector3(1.5f * scale, 1.3f * scale, 1.5f * scale), bushMat);
            Object.DestroyImmediate(leaves.GetComponent<Collider>());

            leaves = Sphere(tree.transform, "Leaves 2", new Vector3(-0.55f * scale, 1.65f * scale, 0.15f * scale),
                new Vector3(0.95f * scale, 0.85f * scale, 0.95f * scale), bushMat);
            Object.DestroyImmediate(leaves.GetComponent<Collider>());

            leaves = Sphere(tree.transform, "Leaves 3", new Vector3(0.5f * scale, 1.7f * scale, -0.1f * scale),
                new Vector3(1f * scale, 0.9f * scale, 1f * scale), bushMat);
            Object.DestroyImmediate(leaves.GetComponent<Collider>());
        }

        private static void BuildLaptop(Transform table)
        {
            var laptop = new GameObject("Laptop");
            laptop.transform.SetParent(table, false);
            laptop.transform.localPosition = new Vector3(1.15f, 1.16f, 2.45f);

            Box(laptop.transform, "Base", new Vector3(0f, 0.02f, 0f),
                new Vector3(0.58f, 0.035f, 0.42f), laptopMat);
            Box(laptop.transform, "Keyboard", new Vector3(0f, 0.045f, -0.02f),
                new Vector3(0.50f, 0.008f, 0.32f), darkWood);

            var lid = new GameObject("Lid");
            lid.transform.SetParent(laptop.transform, false);
            lid.transform.localPosition = new Vector3(0f, 0.02f, 0.21f);
            lid.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            Box(lid.transform, "Lid Back", new Vector3(0f, 0.21f, 0.02f),
                new Vector3(0.60f, 0.40f, 0.03f), laptopMat);
            Box(lid.transform, "Screen", new Vector3(0f, 0.21f, -0.02f),
                new Vector3(0.52f, 0.32f, 0.012f), laptopScreen);
            Box(lid.transform, "Bezel Top", new Vector3(0f, 0.375f, -0.015f),
                new Vector3(0.58f, 0.03f, 0.02f), laptopMat);
            Box(lid.transform, "Bezel Bottom", new Vector3(0f, 0.045f, -0.015f),
                new Vector3(0.58f, 0.03f, 0.02f), laptopMat);
            Box(lid.transform, "Bezel Left", new Vector3(-0.275f, 0.21f, -0.015f),
                new Vector3(0.03f, 0.36f, 0.02f), laptopMat);
            Box(lid.transform, "Bezel Right", new Vector3(0.275f, 0.21f, -0.015f),
                new Vector3(0.03f, 0.36f, 0.02f), laptopMat);

            var interactable = laptop.AddComponent<LaptopInteractable>();
            AudioClip click = LoadClip("lamp_click");
            interactable.audioSource = AddSpatialAudio(laptop, click, 0.8f);
            interactable.clickClip = click;
        }

        private static void BuildChair(Transform root)
        {
            var chair = new GameObject("Chair");
            chair.transform.SetParent(root, false);
            chair.transform.localPosition = new Vector3(1.15f, 0.25f, 1.35f);

            Box(chair.transform, "Seat", new Vector3(0f, 0.30f, 0f),
                new Vector3(0.55f, 0.08f, 0.55f), wood);
            // Спинка со стороны входа: сидящий смотрит на стол и ноутбук.
            Box(chair.transform, "Back", new Vector3(0f, 0.68f, -0.24f),
                new Vector3(0.55f, 0.65f, 0.08f), darkWood);
            Box(chair.transform, "Leg FL", new Vector3(-0.22f, 0.15f, -0.22f),
                new Vector3(0.07f, 0.3f, 0.07f), darkWood);
            Box(chair.transform, "Leg FR", new Vector3(0.22f, 0.15f, -0.22f),
                new Vector3(0.07f, 0.3f, 0.07f), darkWood);
            Box(chair.transform, "Leg BL", new Vector3(-0.22f, 0.15f, 0.22f),
                new Vector3(0.07f, 0.3f, 0.07f), darkWood);
            Box(chair.transform, "Leg BR", new Vector3(0.22f, 0.15f, 0.22f),
                new Vector3(0.07f, 0.3f, 0.07f), darkWood);

            var seatPoint = new GameObject("Seat Point");
            seatPoint.transform.SetParent(chair.transform, false);
            seatPoint.transform.localPosition = Vector3.zero;

            var standPoint = new GameObject("Stand Point");
            standPoint.transform.SetParent(chair.transform, false);
            standPoint.transform.localPosition = new Vector3(0f, -0.20f, -0.78f);

            var interactable = chair.AddComponent<ChairInteractable>();
            interactable.seatPoint = seatPoint.transform;
            interactable.standPoint = standPoint.transform;
        }

        private static void BuildTrashBin(Transform root)
        {
            var bin = new GameObject("Trash Bin");
            bin.transform.SetParent(root, false);
            bin.transform.localPosition = new Vector3(0.85f, 0f, 3.72f);

            // Большой зелёный контейнер на колёсах, как уличный бак.
            Box(bin.transform, "Body", new Vector3(0f, 0.75f, 0f),
                new Vector3(1.3f, 1.1f, 1.0f), trashMat);
            Box(bin.transform, "Lid", new Vector3(0f, 1.36f, 0f),
                new Vector3(1.4f, 0.12f, 1.1f), trashLidMat);
            Box(bin.transform, "Lid Ridge", new Vector3(0f, 1.43f, 0f),
                new Vector3(1.18f, 0.05f, 0.8f), trashLidMat);
            Box(bin.transform, "Hinge", new Vector3(0f, 1.28f, 0.53f),
                new Vector3(1.1f, 0.07f, 0.07f), metal);

            // Ручка спереди.
            Box(bin.transform, "Handle", new Vector3(0f, 0.98f, -0.53f),
                new Vector3(1.12f, 0.06f, 0.06f), metal);
            Box(bin.transform, "Handle Left", new Vector3(-0.52f, 0.86f, -0.53f),
                new Vector3(0.06f, 0.3f, 0.06f), metal);
            Box(bin.transform, "Handle Right", new Vector3(0.52f, 0.86f, -0.53f),
                new Vector3(0.06f, 0.3f, 0.06f), metal);

            Cylinder(bin.transform, "Axle", new Vector3(0f, 0.16f, 0f),
                new Vector3(0.035f, 0.66f, 0.035f), metal);
            bin.transform.Find("Axle").localRotation = Quaternion.Euler(0f, 0f, 90f);

            AddBinWheel(bin.transform, new Vector3(-0.55f, 0.16f, -0.38f));
            AddBinWheel(bin.transform, new Vector3(0.55f, 0.16f, -0.38f));
            AddBinWheel(bin.transform, new Vector3(-0.55f, 0.16f, 0.38f));
            AddBinWheel(bin.transform, new Vector3(0.55f, 0.16f, 0.38f));

            var interactable = bin.AddComponent<TrashBinInteractable>();
            AudioClip coin = LoadClip("coin");
            interactable.audioSource = AddSpatialAudio(bin, coin, 0.9f);
            interactable.sellClip = coin;
        }

        private static void AddBinWheel(Transform parent, Vector3 position)
        {
            GameObject wheel = Cylinder(parent, "Wheel", position,
                new Vector3(0.16f, 0.06f, 0.16f), wheelMat);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void BuildNightSky(Transform root)
        {
            var sky = new GameObject("Night Sky");
            sky.transform.SetParent(root, false);

            GameObject moon = Sphere(sky.transform, "Moon", new Vector3(0f, 38f, 50f),
                new Vector3(7.5f, 7.5f, 7.5f), moonMat);
            Object.DestroyImmediate(moon.GetComponent<Collider>());

            for (int i = 0; i < 48; i++)
            {
                float angle = i * 2.39996f;
                float radius = 62f + (i % 6) * 3f;
                var position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    18f + (i * 37 % 46),
                    Mathf.Sin(angle) * radius + 18f);
                GameObject star = Sphere(sky.transform, "Star " + (i + 1), position,
                    new Vector3(0.18f, 0.18f, 0.18f), starMat);
                Object.DestroyImmediate(star.GetComponent<Collider>());
            }
        }

        private static void BuildStreetLamp(Transform parent, Vector3 position)
        {
            var lamp = new GameObject("Street Lamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = position;

            Cylinder(lamp.transform, "Post", new Vector3(0f, 1.5f, 0f),
                new Vector3(0.07f, 1.5f, 0.07f), metal);
            Sphere(lamp.transform, "Lamp", new Vector3(0f, 3.05f, 0f),
                new Vector3(0.3f, 0.3f, 0.3f), bulbMat);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.70f, 0.38f);
            light.shadows = LightShadows.Soft;
        }

        private static GameObject BuildPlayer(Camera camera, PaperMatInteractable mat,
            LaptopInteractable laptop, ChairInteractable chair)
        {
            var player = new GameObject("Player");
            player.layer = 2; // Ignore Raycast: луч взаимодействия не бьёт в самого игрока.
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.05f, -9.6f);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.45f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.03f;

            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;

            var fpc = player.AddComponent<FirstPersonController>();
            fpc.cameraTransform = camera.transform;

            var footstepSource = player.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.loop = true;
            footstepSource.spatialBlend = 0f;
            footstepSource.volume = 0.55f;
            footstepSource.clip = LoadClip("fast-walking-on-a-concrete-surface");
            fpc.footstepSource = footstepSource;

            var hud = player.AddComponent<PaperHUD>();

            var settings = player.AddComponent<SettingsMenu>();
            settings.player = fpc;
            settings.hud = hud;
            fpc.settingsMenu = settings;

            var skillTree = player.AddComponent<SkillTreeMenu>();
            skillTree.player = fpc;
            skillTree.hud = hud;
            fpc.skillTree = skillTree;

            if (laptop != null)
            {
                laptop.skillTree = skillTree;
                laptop.controller = fpc;
            }
            if (chair != null)
                chair.controller = fpc;

            var audioService = player.AddComponent<AudioService>();

            var musicObject = new GameObject("Music");
            musicObject.transform.SetParent(player.transform, false);
            var music = musicObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0.35f;
            music.clip = LoadClip("dream-whisper_95075");
            audioService.musicSource = music;

            var interactor = player.AddComponent<PlayerInteractor>();
            interactor.view = camera.transform;
            interactor.hud = hud;
            interactor.mat = mat;

            GameObject hand = Box(camera.transform, "Held Paper",
                new Vector3(0.34f, -0.28f, 0.55f), new Vector3(0.28f, 0.005f, 0.38f), paperMat);
            Object.DestroyImmediate(hand.GetComponent<Collider>());
            hand.transform.localRotation = Quaternion.Euler(0f, -12f, 8f);
            hand.SetActive(false);
            interactor.heldPaperVisual = hand;

            var thrower = player.AddComponent<PaperThrower>();
            thrower.moon = GameObject.Find("Moon") != null ? GameObject.Find("Moon").transform : null;
            thrower.interactor = interactor;
            thrower.hud = hud;
            thrower.controller = fpc;
            thrower.paperMaterial = paperMat;

            return player;
        }

        private static void BuildCeilingLamp(Transform houseRoot)
        {
            var lamp = new GameObject("Ceiling Lamp");
            lamp.transform.SetParent(houseRoot, false);

            Cylinder(lamp.transform, "Ceiling Plate", new Vector3(0f, 3.12f, 0.6f),
                new Vector3(0.30f, 0.035f, 0.30f), metal);
            Cylinder(lamp.transform, "Cord", new Vector3(0f, 2.95f, 0.6f),
                new Vector3(0.025f, 0.15f, 0.025f), metal);
            Cylinder(lamp.transform, "Shade", new Vector3(0f, 2.78f, 0.6f),
                new Vector3(0.38f, 0.12f, 0.38f), lampShade);
            Sphere(lamp.transform, "Bulb", new Vector3(0f, 2.64f, 0.6f),
                new Vector3(0.18f, 0.22f, 0.18f), ceilingBulb);
        }

        private static void ConfigureLighting(Transform houseRoot)
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    // Луна: холодный неяркий свет.
                    light.intensity = 0.42f;
                    light.color = new Color(0.52f, 0.62f, 0.92f);
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.8f;
                    light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                }
            }

            BuildCeilingLamp(houseRoot);

            var ceilingLightObject = new GameObject("Interior Light");
            ceilingLightObject.transform.SetParent(houseRoot, false);
            ceilingLightObject.transform.localPosition = new Vector3(0f, 2.62f, 0.6f);
            var ceilingLight = ceilingLightObject.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.range = 8f;
            ceilingLight.intensity = 2.1f;
            ceilingLight.color = new Color(1f, 0.74f, 0.48f);
            ceilingLight.shadows = LightShadows.Soft;

            if (nightSky != null)
                RenderSettings.skybox = nightSky;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.045f, 0.09f);
            RenderSettings.reflectionIntensity = 0.35f;
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 60f;
        }

        private static void HideAssistantOverlay()
        {
            DSHChatOverlay overlay = Object.FindFirstObjectByType<DSHChatOverlay>();
            if (overlay == null) return;

            var serialized = new SerializedObject(overlay);
            SerializedProperty visible = serialized.FindProperty("visibleOnStart");
            if (visible != null)
            {
                visible.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SavePrefab(GameObject root)
        {
            AssetDatabase.DeleteAsset(PrefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null)
                Debug.LogWarning("[PaperHouse] Не удалось сохранить префаб " + PrefabPath);
        }

        private static void RemoveRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    Object.DestroyImmediate(go);
                    return;
                }
            }
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
