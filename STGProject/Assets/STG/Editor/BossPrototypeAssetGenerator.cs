using System;
using GenjitsuLAB.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace GenjitsuLAB.STG.EditorTools
{
    /// <summary>
    /// Imports and wires the generated three-part prototype boss assets.
    /// </summary>
    public static class BossPrototypeAssetGenerator
    {
        private const string k_centerTexturePath = "Assets/STG/Art/BossCenter.png";
        private const string k_wingTexturePath = "Assets/STG/Art/BossWing.png";
        private const string k_centerAnimationPackPath = "Assets/STG/Art/BossCenterAnimPack.asset";
        private const string k_wingAnimationPackPath = "Assets/STG/Art/BossWingAnimPack.asset";
        private const string k_bossPrefabPath = "Assets/STG/Prefab/Boss.prefab";
        private const string k_stageSettingPath = "Assets/STG/Prefab/StageSettingAsset.asset";
        private const string k_stageScenePath = "Assets/STG/Scene/Stage01.unity";
        private const string k_spawnerName = "BossSpawner_90";

        /// <summary>Imports generated atlases and creates the boss prefab and stage marker.</summary>
        [MenuItem("STG/Generate Boss Prototype Assets")]
        public static void Generate()
        {
            ConfigureAtlas(k_centerTexturePath, "BossCenter", 32);
            ConfigureAtlas(k_wingTexturePath, "BossWing", 24);
            AnimationPack centerPack = CreateAnimationPack(
                k_centerAnimationPackPath,
                k_centerTexturePath,
                "BossCenter");
            AnimationPack wingPack = CreateAnimationPack(
                k_wingAnimationPackPath,
                k_wingTexturePath,
                "BossWing");
            BossController boss = CreateBossPrefab(centerPack, wingPack);
            ConfigureStageSetting(boss);
            ConfigureStageScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated and wired the STG boss prototype assets.");
        }

        /// <summary>
        /// Runs a warmed-up 10,000-tick boss, shot-buffer, and active-projectile allocation probe.
        /// </summary>
        public static void ValidateSteadyStateAllocations()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_bossPrefabPath);
            Bullet bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/Bullet.prefab")
                .GetComponent<Bullet>();
            GameObject root = new GameObject("BossAllocationProbe");
            BossController boss = UnityEngine.Object.Instantiate(
                prefab.GetComponent<BossController>(),
                new Vector3(0f, 7f, 0f),
                Quaternion.identity,
                root.transform);
            ComponentPool<Bullet> bulletPool = new ComponentPool<Bullet>(bulletPrefab, 32, root.transform);
            BossShot[] shots = new BossShot[5];
            Bullet[] activeBullets = new Bullet[32];
            int activeBulletCount = 0;
            Rect recycleArea = new Rect(-6f, -1f, 12f, 11f);
            Rect collisionProbe = new Rect(-0.1f, 0f, 0.2f, 0.2f);
            boss.Spawn(new Vector3(0f, 7f, 0f));

            for (int tick = 0; tick < 512; tick++)
            {
                RunAllocationProbeTick(
                    boss,
                    bulletPool,
                    shots,
                    activeBullets,
                    ref activeBulletCount,
                    recycleArea,
                    collisionProbe);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 10000; tick++)
            {
                RunAllocationProbeTick(
                    boss,
                    bulletPool,
                    shots,
                    activeBullets,
                    ref activeBulletCount,
                    recycleArea,
                    collisionProbe);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            bulletPool.ReturnAll();
            UnityEngine.Object.DestroyImmediate(root);
            if (allocatedBytes != 0)
            {
                throw new InvalidOperationException(
                    $"Boss steady-state probe allocated {allocatedBytes} managed bytes.");
            }

            Debug.Log("Boss steady-state allocation probe passed: 10,000 ticks, 0 managed bytes.");
        }

        /// <summary>
        /// Runs a warmed-up 10,000-tick player entry, life mutation, and stage-flow HUD allocation probe.
        /// </summary>
        public static void ValidateStageFlowAllocations()
        {
            PlayerController playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/STG/Prefab/Player.prefab")
                .GetComponent<PlayerController>();
            GameObject root = new GameObject("StageFlowAllocationProbe");
            PlayerController player = UnityEngine.Object.Instantiate(playerPrefab, root.transform);
            GameObject canvasObject = new GameObject(
                "StageFlowAllocationCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            StageFlowHud hud = canvasObject.AddComponent<StageFlowHud>();
            PlayerLifeState lives = new PlayerLifeState(98, 99);
            Vector3 entryPosition = new Vector3(0f, -1.5f, 0f);
            Vector3 spawnPosition = new Vector3(0f, 1f, 0f);
            player.Initialize();
            player.BeginEntry(entryPosition, spawnPosition, 0.03f);
            hud.ShowStart();

            for (int tick = 0; tick < 512; tick++)
            {
                RunStageFlowAllocationProbeTick(player, hud, lives, entryPosition, spawnPosition);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 10000; tick++)
            {
                RunStageFlowAllocationProbeTick(player, hud, lives, entryPosition, spawnPosition);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            UnityEngine.Object.DestroyImmediate(root);
            if (allocatedBytes != 0)
            {
                throw new InvalidOperationException(
                    $"Stage-flow steady-state probe allocated {allocatedBytes} managed bytes.");
            }

            Debug.Log("Stage-flow allocation probe passed: 10,000 ticks, 0 managed bytes.");
        }

        private static void RunStageFlowAllocationProbeTick(
            PlayerController player,
            StageFlowHud hud,
            PlayerLifeState lives,
            Vector3 entryPosition,
            Vector3 spawnPosition)
        {
            if (!player.TickEntry())
            {
                return;
            }

            hud.Hide();
            player.DestroyByDamage();
            lives.TryConsumeLife();
            lives.TryAddLives(1);
            player.BeginEntry(entryPosition, spawnPosition, 0.03f);
            hud.ShowStart();
        }

        private static void RunAllocationProbeTick(
            BossController boss,
            ComponentPool<Bullet> bulletPool,
            BossShot[] shots,
            Bullet[] activeBullets,
            ref int activeBulletCount,
            Rect recycleArea,
            Rect collisionProbe)
        {
            boss.TickMovementAndAnimation();
            boss.OverlapsOperationalPart(collisionProbe);
            int shotCount = boss.CollectShots(Vector2.zero, shots);
            for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
            {
                if (!bulletPool.TryRent(out Bullet bullet))
                {
                    continue;
                }

                BossShot shot = shots[shotIndex];
                bullet.Spawn(shot.Position, shot.Direction);
                activeBullets[activeBulletCount++] = bullet;
            }

            int index = 0;
            while (index < activeBulletCount)
            {
                Bullet bullet = activeBullets[index];
                if (bullet.Tick(recycleArea))
                {
                    index++;
                    continue;
                }

                bulletPool.Return(bullet);
                int lastIndex = --activeBulletCount;
                activeBullets[index] = activeBullets[lastIndex];
                activeBullets[lastIndex] = null;
            }
        }

        private static void ConfigureAtlas(string path, string spritePrefix, int cellSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"Texture importer not found: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] spriteRects = new SpriteRect[8];
            for (int row = 0; row < 2; row++)
            {
                string stateName = row == 0 ? "Operational" : "Damaged";
                float y = row == 0 ? cellSize : 0f;
                for (int frame = 0; frame < 4; frame++)
                {
                    spriteRects[(row * 4) + frame] = new SpriteRect
                    {
                        name = $"{spritePrefix}_{stateName}_{frame}",
                        rect = new Rect(frame * cellSize, y, cellSize, cellSize),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = GUID.Generate()
                    };
                }
            }

            provider.SetSpriteRects(spriteRects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static AnimationPack CreateAnimationPack(
            string packPath,
            string texturePath,
            string spritePrefix)
        {
            AssetDatabase.DeleteAsset(packPath);
            AnimationPack pack = ScriptableObject.CreateInstance<AnimationPack>();
            pack.name = System.IO.Path.GetFileNameWithoutExtension(packPath);
            AssetDatabase.CreateAsset(pack, packPath);

            SerializedObject serializedPack = new SerializedObject(pack);
            SerializedProperty clips = serializedPack.FindProperty("m_clips");
            clips.arraySize = 2;
            for (int animationId = 0; animationId < 2; animationId++)
            {
                string stateName = animationId == 0 ? "Operational" : "Damaged";
                GenjitsuLAB.Animation.AnimationClip clip =
                    ScriptableObject.CreateInstance<GenjitsuLAB.Animation.AnimationClip>();
                clip.name = stateName.ToLowerInvariant();
                SerializedObject serializedClip = new SerializedObject(clip);
                serializedClip.FindProperty("m_animId").intValue = animationId;
                serializedClip.FindProperty("m_animName").stringValue = stateName.ToLowerInvariant();
                serializedClip.FindProperty("m_isLoop").boolValue = true;
                SerializedProperty elements = serializedClip.FindProperty("m_elements");
                elements.arraySize = 4;
                for (int frame = 0; frame < 4; frame++)
                {
                    Sprite sprite = FindSprite(
                        texturePath,
                        $"{spritePrefix}_{stateName}_{frame}");
                    SerializedProperty element = elements.GetArrayElementAtIndex(frame);
                    element.FindPropertyRelative("m_sprite").objectReferenceValue = sprite;
                    element.FindPropertyRelative("m_duration").intValue = 6;
                }

                serializedClip.FindProperty("m_totalTicks").intValue = 24;
                serializedClip.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(clip, pack);
                clips.GetArrayElementAtIndex(animationId).objectReferenceValue = clip;
            }

            serializedPack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pack);
            return pack;
        }

        private static Sprite FindSprite(string texturePath, string spriteName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            for (int index = 0; index < assets.Length; index++)
            {
                Sprite sprite = assets[index] as Sprite;
                if (sprite != null && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new MissingReferenceException($"Sprite not found: {spriteName}");
        }

        private static BossController CreateBossPrefab(AnimationPack centerPack, AnimationPack wingPack)
        {
            GameObject root = new GameObject("Boss");
            try
            {
                BossPartController left = CreatePart(
                    root.transform,
                    "Left",
                    new Vector3(-1.6f, 0f, 0f),
                    wingPack,
                    FindSprite(k_wingTexturePath, "BossWing_Operational_0"),
                    BossPartType.Left,
                    30,
                    new Rect(-0.625f, -0.5f, 1.25f, 1f),
                    30,
                    90,
                    false,
                    -0.65f);
                BossPartController center = CreatePart(
                    root.transform,
                    "Center",
                    Vector3.zero,
                    centerPack,
                    FindSprite(k_centerTexturePath, "BossCenter_Operational_0"),
                    BossPartType.Center,
                    50,
                    new Rect(-0.75f, -0.75f, 1.5f, 1.5f),
                    60,
                    120,
                    false,
                    -0.9f);
                BossPartController right = CreatePart(
                    root.transform,
                    "Right",
                    new Vector3(1.6f, 0f, 0f),
                    wingPack,
                    FindSprite(k_wingTexturePath, "BossWing_Operational_0"),
                    BossPartType.Right,
                    30,
                    new Rect(-0.625f, -0.5f, 1.25f, 1f),
                    75,
                    90,
                    true,
                    -0.65f);

                BossController controller = root.AddComponent<BossController>();
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("m_leftPart").objectReferenceValue = left;
                serialized.FindProperty("m_centerPart").objectReferenceValue = center;
                serialized.FindProperty("m_rightPart").objectReferenceValue = right;
                serialized.FindProperty("m_entrySpeedPerTick").floatValue = 0.03f;
                serialized.FindProperty("m_stopY").floatValue = 7f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, k_bossPrefabPath);
                return prefab.GetComponent<BossController>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static BossPartController CreatePart(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            AnimationPack animationPack,
            Sprite sprite,
            BossPartType partType,
            int maxHealth,
            Rect damageRect,
            int firstShotDelayTicks,
            int fireIntervalTicks,
            bool flipX,
            float firePointY)
        {
            GameObject partObject = new GameObject(objectName);
            Transform partTransform = partObject.transform;
            partTransform.SetParent(parent, false);
            partTransform.localPosition = localPosition;
            SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = flipX;
            renderer.sortingLayerName = "Object";
            renderer.sortingOrder = 0;

            Transform firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(partTransform, false);
            firePoint.localPosition = new Vector3(0f, firePointY, 0f);

            BossPartController part = partObject.AddComponent<BossPartController>();
            SerializedObject serialized = new SerializedObject(part);
            serialized.FindProperty("m_spriteRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("m_animationPack").objectReferenceValue = animationPack;
            serialized.FindProperty("m_operationalAnimationId").intValue = 0;
            serialized.FindProperty("m_damagedAnimationId").intValue = 1;
            serialized.FindProperty("m_partType").enumValueIndex = (int)partType;
            serialized.FindProperty("m_maxHealth").intValue = maxHealth;
            serialized.FindProperty("m_damageRect").rectValue = damageRect;
            serialized.FindProperty("m_firePoint").objectReferenceValue = firePoint;
            serialized.FindProperty("m_firstShotDelayTicks").intValue = firstShotDelayTicks;
            serialized.FindProperty("m_fireIntervalTicks").intValue = fireIntervalTicks;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return part;
        }

        private static void ConfigureStageSetting(BossController boss)
        {
            StageSetting setting = AssetDatabase.LoadAssetAtPath<StageSetting>(k_stageSettingPath);
            SerializedObject serialized = new SerializedObject(setting);
            serialized.FindProperty("m_bossPrefab").objectReferenceValue = boss;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
        }

        private static void ConfigureStageScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(k_stageScenePath);
            StageController controller = UnityEngine.Object.FindFirstObjectByType<StageController>(
                FindObjectsInactive.Include);
            SerializedObject serializedController = new SerializedObject(controller);
            StageScrollLayer gameplayLayer = serializedController.FindProperty("m_gameplay")
                .objectReferenceValue as StageScrollLayer;
            if (gameplayLayer == null)
            {
                throw new MissingReferenceException("StageController requires a gameplay scroll layer.");
            }

            Transform gameplay = gameplayLayer.transform;
            Transform marker = gameplay.Find(k_spawnerName);
            if (marker == null)
            {
                marker = new GameObject(k_spawnerName).transform;
                marker.SetParent(gameplay, false);
                marker.gameObject.AddComponent<StageBossSpawner>();
            }

            marker.localPosition = new Vector3(0f, 99f, 0f);
            SerializedObject serialized = new SerializedObject(marker.GetComponent<StageBossSpawner>());
            serialized.FindProperty("m_activationY").floatValue = 9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
