using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Gamejam menu.
//   1. Setup Character Animations: builds the animator controllers and puts the animated
//      models into the Player / BasicEnemy / RunawayEnemy prefabs. Safe to run again.
//   2. Build Dark Map Scene: generates Assets/Scenes/DarkMap.unity, a dark forest that uses
//      the same pieces as SuckTest (Player prefab, DarkAtmosphere, fir trees, fire pickups).
public static class GameSetupTool
{
    // ---------- assets ----------
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PlayerCharacterName = "PlayerCharacter";
    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/BasicEnemy.prefab",
        "Assets/Prefabs/RunawayEnemy.prefab",
    };

    private const string PlayerModelPath = "Assets/Prefabs/TallCreature.prefab";
    private const string EnemyModelPath = "Assets/CustomAssets/Models/Enemy1.fbx";

    private const string AnimFolder = "Assets/CustomAssets/Anims";
    private const string PlayerControllerPath = AnimFolder + "/PlayerGameplay.controller";
    private const string EnemyControllerPath = AnimFolder + "/EnemyGameplay.controller";
    private const string UpperBodyMaskPath = AnimFolder + "/UpperBody.mask";

    private const string TreePath = "Assets/CustomAssets/Models/FirTree.fbx";
    private const string RockFolder = "Assets/Prefabs/TerrainPrefabs";
    private const string FirePickupPath = "Assets/Prefabs/FirePickup.prefab";
    private const string GrassMaterialPath = "Assets/CustomAssets/Materials/Grass.mat";
    private const string GroundMaterialPath = "Assets/Materials/DarkMapGround.mat";
    private const string ScenePath = "Assets/Scenes/DarkMap.unity";

    // ---------- tuning ----------
    private const string ModelChildName = "Model";
    // Models whose height is outside this range get rescaled to the fit height
    private const float MinModelHeight = 1.6f;
    private const float MaxModelHeight = 3.2f;
    private const float PlayerFitHeight = 2.6f;
    private const float EnemyFitHeight = 2.1f;
    // How long the swing takes in game, whatever the clip length is
    private const float PlayerAttackDuration = 0.9f;
    private const float EnemyAttackDuration = 0.8f;

    private const float MapRadius = 110f;
    private const float StartClearingRadius = 12f;
    private const int TreeCount = 420;
    private const int RockCount = 70;
    private const int PickupCount = 30;
    private const int ClearingCount = 6;
    private const int Seed = 2026;

    // =====================================================================
    //  1. Animations
    // =====================================================================

    [MenuItem("Tools/Gamejam/1. Setup Character Animations")]
    public static void SetupCharacterAnimations()
    {
        AnimationClip idle = LoadClip(AnimFolder + "/player_idle.anim");
        AnimationClip walk = LoadClip(AnimFolder + "/player_walk.anim");
        AnimationClip swing = LoadClip(AnimFolder + "/player_swing.anim");
        AnimationClip sneak = LoadClip(AnimFolder + "/enemy_sneak.anim");
        GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
        GameObject enemyModel = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelPath);
        if (idle == null || walk == null || swing == null || sneak == null || playerModel == null || enemyModel == null)
        {
            Debug.LogError("[GameSetup] Missing animation clips or models, see errors above. Nothing changed.");
            return;
        }

        AvatarMask upperBody = CreateUpperBodyMask();

        float playerSpeed = ReadMaxSpeed(PlayerPrefabPath, PlayerCharacterName, 5f);
        AnimatorController playerController = BuildController(PlayerControllerPath, idle, walk, swing, playerSpeed, PlayerAttackDuration, upperBody);
        SetupCharacter(PlayerPrefabPath, PlayerCharacterName, playerModel, playerController, PlayerFitHeight);

        // All rigs are humanoid, so the enemy can reuse the player's idle and swing
        float enemySpeed = ReadMaxSpeed(EnemyPrefabPaths[0], null, 3f);
        AnimatorController enemyController = BuildController(EnemyControllerPath, idle, sneak, swing, enemySpeed, EnemyAttackDuration, upperBody);
        foreach (string enemyPath in EnemyPrefabPaths)
        {
            SetupCharacter(enemyPath, null, enemyModel, enemyController, EnemyFitHeight);
        }

        WirePlayerPrefab();

        AssetDatabase.SaveAssets();
        Debug.Log("[GameSetup] Character animations set up. Press play in any scene with the Player prefab to check.");
    }

    // The Player prefab holds the menu text, the spawners and the lantern, but GameController's and
    // LanternController's references to them were left empty, so the menu never went away.
    // Only fills in references that are empty, anything wired by hand is kept.
    private static void WirePlayerPrefab()
    {
        using (var scope = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;
            GameController controller = root.GetComponentInChildren<GameController>(true);
            if (controller == null) return;

            var serialized = new SerializedObject(controller);
            EnemySpawner[] spawners = root.GetComponentsInChildren<EnemySpawner>(true);
            SetIfEmpty(serialized, "basicSpawn", spawners.FirstOrDefault(s => s.name.Contains("Basic")));
            SetIfEmpty(serialized, "runSpawn", spawners.FirstOrDefault(s => s.name.Contains("Runaway")));

            Transform menuText = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Text"));
            if (menuText != null) SetIfEmpty(serialized, "text", menuText.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (LanternController lantern in root.GetComponentsInChildren<LanternController>(true))
            {
                var serializedLantern = new SerializedObject(lantern);
                SetIfEmpty(serializedLantern, "gameController", controller);
                serializedLantern.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void SetIfEmpty(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null && value != null) property.objectReferenceValue = value;
    }

    private static AnimationClip LoadClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) Debug.LogError("[GameSetup] Animation clip not found: " + path);
        return clip;
    }

    private static AvatarMask CreateUpperBodyMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        }

        var upper = new HashSet<AvatarMaskBodyPart>
        {
            AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
        };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var part = (AvatarMaskBodyPart)i;
            mask.SetHumanoidBodyPartActive(part, upper.Contains(part));
        }
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // Base layer: idle <-> walk blended by the "Speed" float.
    // Attack layer (upper body only, so legs keep walking): plays the swing on the "Attack" trigger.
    private static AnimatorController BuildController(string path, AnimationClip idle, AnimationClip walk, AnimationClip attack,
        float walkSpeed, float attackDuration, AvatarMask upperBody)
    {
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, Mathf.Max(walkSpeed, 0.1f));

        var attackMachine = new AnimatorStateMachine { name = "Attack", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(attackMachine, controller);
        controller.AddLayer(new AnimatorControllerLayer
        {
            name = "Attack",
            defaultWeight = 1f,
            avatarMask = upperBody,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = attackMachine,
        });

        AnimatorState none = attackMachine.AddState("None");
        AnimatorState swing = attackMachine.AddState("Swing");
        swing.motion = attack;
        swing.speed = attack.length / attackDuration;
        attackMachine.defaultState = none;

        AnimatorStateTransition start = attackMachine.AddAnyStateTransition(swing);
        start.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        start.hasExitTime = false;
        start.duration = 0.08f;
        start.canTransitionToSelf = true;

        AnimatorStateTransition end = swing.AddTransition(none);
        end.hasExitTime = true;
        end.exitTime = 0.85f;
        end.duration = 0.15f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static float ReadMaxSpeed(string prefabPath, string characterName, float fallback)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return fallback;
        Transform character = FindCharacter(prefab.transform, characterName);
        if (character == null) return fallback;

        foreach (MonoBehaviour behaviour in character.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null) continue;
            SerializedProperty property = new SerializedObject(behaviour).FindProperty("maxSpeed");
            if (property != null && property.floatValue > 0) return property.floatValue;
        }
        return fallback;
    }

    private static Transform FindCharacter(Transform root, string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return root;
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == characterName);
    }

    // Puts the animated model under the character, hides the placeholder capsule and wires up CharacterAnimator
    private static void SetupCharacter(string prefabPath, string characterName, GameObject model, AnimatorController controller, float fitHeight)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogError("[GameSetup] Prefab not found: " + prefabPath);
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            Transform character = FindCharacter(scope.prefabContentsRoot.transform, characterName);
            if (character == null)
            {
                Debug.LogError("[GameSetup] No object named " + characterName + " in " + prefabPath);
                return;
            }

            Transform old = character.Find(ModelChildName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, character);
            instance.name = ModelChildName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            FitModel(instance.transform, character, fitHeight);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // The capsule stays as the collider, it just isn't drawn anymore
            MeshRenderer placeholder = character.GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;

            CharacterAnimator characterAnimator = character.GetComponent<CharacterAnimator>();
            if (characterAnimator == null) characterAnimator = character.gameObject.AddComponent<CharacterAnimator>();
            var serialized = new SerializedObject(characterAnimator);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Stands the model on the bottom of the character's capsule, rescaling it if it's a very different size
    private static void FitModel(Transform model, Transform character, float fitHeight)
    {
        float feetY = character.position.y;
        CapsuleCollider capsule = character.GetComponent<CapsuleCollider>();
        if (capsule != null) feetY = character.TransformPoint(capsule.center + Vector3.down * capsule.height * 0.5f).y;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        float height = bounds.size.y;
        if (height < 0.01f)
        {
            model.position = new Vector3(model.position.x, feetY, model.position.z);
            Debug.LogWarning("[GameSetup] Couldn't measure " + model.root.name + "'s model. Check the Model child's position and scale by hand.");
            return;
        }

        float scale = height < MinModelHeight || height > MaxModelHeight ? fitHeight / height : 1f;
        float feetOffset = (bounds.min.y - model.position.y) * scale;
        model.localScale = Vector3.one * scale;
        model.position = new Vector3(model.position.x, feetY - feetOffset, model.position.z);
        Debug.Log($"[GameSetup] {character.name}: model height {height:0.00}, scale {scale:0.00}");
    }

    // =====================================================================
    //  2. Dark map
    // =====================================================================

    [MenuItem("Tools/Gamejam/2. Build Dark Map Scene")]
    public static void BuildDarkMapScene()
    {
        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Build Dark Map", ScenePath + " already exists. Regenerate it? Hand-made changes in that scene are lost.", "Regenerate", "Cancel"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePath);
        GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePickupPath);
        GameObject[] rockPrefabs = AssetDatabase.FindAssets("Rock t:Prefab", new[] { RockFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(p => p != null).ToArray();
        if (playerPrefab == null || treePrefab == null)
        {
            Debug.LogError("[GameSetup] Player prefab or fir tree not found. Nothing built.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Random.State previousRandom = Random.state;
        Random.InitState(Seed);

        BuildGround();
        BuildMoon();
        new GameObject("DarkAtmosphere").AddComponent<DarkAtmosphere>();

        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = new Vector3(0f, 2.6f, 0f);

        Transform environment = new GameObject("Environment").transform;
        List<Vector3> clearings = PickClearings();
        BuildTrees(treePrefab, Child(environment, "Trees"), clearings);
        BuildBorder(treePrefab, Child(environment, "Border"));
        if (rockPrefabs.Length > 0) BuildRocks(rockPrefabs, Child(environment, "Rocks"), clearings);
        if (pickupPrefab != null) BuildPickups(pickupPrefab, new GameObject("Pickups").transform, clearings);

        Random.state = previousRandom;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[GameSetup] Built " + ScenePath + ". It's in the build list too, so dying reloads it correctly.");
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void BuildGround()
    {
        float size = (MapRadius + 40f) * 2f;
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(size, 1f, size);
        ground.isStatic = true;

        // Same grass as SuckTest, tiled for the much bigger ground
        Material grass = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        if (grass == null) return;
        Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (groundMaterial == null)
        {
            groundMaterial = new Material(grass);
            AssetDatabase.CreateAsset(groundMaterial, GroundMaterialPath);
        }
        groundMaterial.CopyPropertiesFromMaterial(grass);
        Vector2 tiling = Vector2.one * (size / 8f);
        groundMaterial.mainTextureScale = tiling;
        if (groundMaterial.HasProperty("_BaseMap")) groundMaterial.SetTextureScale("_BaseMap", tiling);
        EditorUtility.SetDirty(groundMaterial);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
    }

    // DarkAtmosphere recolours and dims every directional light at runtime
    private static void BuildMoon()
    {
        Light moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.shadows = LightShadows.Soft;
        moon.color = new Color(0.45f, 0.55f, 0.85f);
        moon.intensity = 0.2f;
        moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
    }

    private static List<Vector3> PickClearings()
    {
        var clearings = new List<Vector3>();
        int attempts = 0;
        while (clearings.Count < ClearingCount && attempts++ < 500)
        {
            Vector3 candidate = RandomPoint(35f, MapRadius - 20f);
            if (clearings.All(c => Vector3.Distance(c, candidate) > 40f)) clearings.Add(candidate);
        }
        return clearings;
    }

    private static Vector3 RandomPoint(float minRadius, float maxRadius)
    {
        // Square root keeps the points evenly spread instead of bunched in the middle
        float radius = Mathf.Sqrt(Random.Range(minRadius * minRadius, maxRadius * maxRadius));
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private static bool InClearing(Vector3 point, List<Vector3> clearings, float radius)
    {
        return clearings.Any(c => Vector3.Distance(c, point) < radius);
    }

    private static void BuildTrees(GameObject treePrefab, Transform parent, List<Vector3> clearings)
    {
        var placed = new List<Vector3>();
        int attempts = 0;
        while (placed.Count < TreeCount && attempts++ < TreeCount * 30)
        {
            Vector3 point = RandomPoint(StartClearingRadius, MapRadius);
            if (InClearing(point, clearings, 9f)) continue;
            // Noise makes thick patches with open ground winding between them
            float density = Mathf.PerlinNoise(point.x * 0.035f + 50f, point.z * 0.035f + 50f);
            if (Random.value > Mathf.InverseLerp(0.3f, 0.65f, density)) continue;
            if (placed.Any(p => (p - point).sqrMagnitude < 3.5f * 3.5f)) continue;

            placed.Add(point);
            PlaceTree(treePrefab, parent, point, Random.Range(0.85f, 1.5f));
        }
    }

    // Wall of big trees plus invisible colliders so nobody walks off the map
    private static void BuildBorder(GameObject treePrefab, Transform parent)
    {
        const int borderTrees = 150;
        for (int i = 0; i < borderTrees; i++)
        {
            float angle = i * Mathf.PI * 2f / borderTrees;
            float radius = MapRadius + 3f + Random.Range(0f, 9f);
            Vector3 point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            PlaceTree(treePrefab, parent, point, Random.Range(1.4f, 2.1f));
        }

        const int wallSegments = 36;
        float wallRadius = MapRadius + 2f;
        float segmentLength = 2f * wallRadius * Mathf.Tan(Mathf.PI / wallSegments) + 0.5f;
        for (int i = 0; i < wallSegments; i++)
        {
            float angle = i * 360f / wallSegments;
            GameObject wall = new GameObject("Wall");
            wall.transform.SetParent(parent, false);
            wall.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            wall.transform.position = wall.transform.forward * wallRadius + Vector3.up * 5f;
            wall.AddComponent<BoxCollider>().size = new Vector3(segmentLength, 10f, 1f);
            wall.isStatic = true;
        }
    }

    private static void PlaceTree(GameObject treePrefab, Transform parent, Vector3 point, float scale)
    {
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, parent);
        tree.transform.position = point;
        tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * treePrefab.transform.rotation;
        tree.transform.localScale *= scale;
        SitOnGround(tree, 0.15f);

        // Trunk collider: blocks walking, the lamp beam and sucking (see LampSuck.IsBlocked)
        if (tree.GetComponentInChildren<Collider>() == null)
        {
            // On its own upright child, because model roots can carry an import rotation
            Transform trunkObject = Child(tree.transform, "TrunkCollider");
            trunkObject.rotation = Quaternion.identity;
            trunkObject.position = new Vector3(point.x, 3f * scale, point.z);
            float worldScale = Mathf.Max(trunkObject.lossyScale.y, 0.0001f);
            CapsuleCollider trunk = trunkObject.gameObject.AddComponent<CapsuleCollider>();
            trunk.radius = 0.35f * scale / worldScale;
            trunk.height = 6f * scale / worldScale;
        }
        SetStatic(tree);
    }

    private static void BuildRocks(GameObject[] rockPrefabs, Transform parent, List<Vector3> clearings)
    {
        // A loose ring of rocks marks each clearing
        foreach (Vector3 clearing in clearings)
        {
            int count = Random.Range(4, 7);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
                Vector3 point = clearing + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(6f, 8f);
                PlaceRock(rockPrefabs, parent, point);
            }
        }
        for (int i = 0; i < RockCount; i++)
        {
            PlaceRock(rockPrefabs, parent, RandomPoint(StartClearingRadius, MapRadius));
        }
    }

    private static void PlaceRock(GameObject[] rockPrefabs, Transform parent, Vector3 point)
    {
        GameObject rockPrefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
        var rock = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, parent);
        rock.transform.position = point;
        rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * rockPrefab.transform.rotation;
        rock.transform.localScale *= Random.Range(0.7f, 1.6f);
        SitOnGround(rock, 0.2f);

        if (rock.GetComponentInChildren<Collider>() == null)
        {
            foreach (MeshFilter filter in rock.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponent<LODGroup>() != null) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                break; // one collider is enough, the other meshes are LODs of the same rock
            }
        }
        SetStatic(rock);
    }

    private static void BuildPickups(GameObject pickupPrefab, Transform parent, List<Vector3> clearings)
    {
        // Every clearing is worth the trip
        foreach (Vector3 clearing in clearings)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 3f;
                PlacePickup(pickupPrefab, parent, clearing + new Vector3(offset.x, 0f, offset.y));
            }

            Light glow = new GameObject("ClearingGlow").AddComponent<Light>();
            glow.transform.SetParent(parent, false);
            glow.transform.position = clearing + Vector3.up * 2.5f;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.25f);
            glow.intensity = 6f;
            glow.range = 9f;
        }

        // A couple close to the start so the first minute isn't hopeless
        for (int i = 0; i < 3; i++) PlacePickup(pickupPrefab, parent, RandomPoint(6f, 14f));
        for (int i = 0; i < PickupCount; i++) PlacePickup(pickupPrefab, parent, RandomPoint(18f, MapRadius - 5f));
    }

    private static void PlacePickup(GameObject pickupPrefab, Transform parent, Vector3 point)
    {
        var pickup = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, parent);
        pickup.transform.position = point + Vector3.up * 1.7f;
    }

    // Moves the object so its lowest point is just below the ground surface (y = 0)
    private static void SitOnGround(GameObject go, float sink)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position += Vector3.up * (-sink - bounds.min.y);
    }

    private static void SetStatic(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
    }

    private static void AddToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
