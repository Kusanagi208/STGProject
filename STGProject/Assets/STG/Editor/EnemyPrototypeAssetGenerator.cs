using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GenjitsuLAB.STG.EditorTools
{
    /// <summary>
    /// Creates and wires the prototype enemy and bullet assets.
    /// </summary>
    public static class EnemyPrototypeAssetGenerator
    {
        private const string k_straightTexturePath = "Assets/STG/Art/EnemyStraight.png";
        private const string k_shooterTexturePath = "Assets/STG/Art/EnemyShooter.png";
        private const string k_bulletTexturePath = "Assets/STG/Art/EnemyBullet.png";
        private const string k_playerIdleTexturePath = "Assets/STG/Art/PlayerIdle.png";
        private const string k_playerLeftTexturePath = "Assets/STG/Art/PlayerBankingLeft.png";
        private const string k_playerRightTexturePath = "Assets/STG/Art/PlayerBankingRight.png";
        private const string k_playerAnimationPackPath = "Assets/STG/Art/PlayerAnimPack.asset";
        private const string k_straightPrefabPath = "Assets/STG/Prefab/EnemyStraight.prefab";
        private const string k_shooterPrefabPath = "Assets/STG/Prefab/EnemyShooter.prefab";
        private const string k_bulletPrefabPath = "Assets/STG/Prefab/EnemyBullet.prefab";
        private const string k_stageSettingPath = "Assets/STG/Prefab/StageSettingAsset.asset";
        private const string k_gameSettingPath = "Assets/STG/Prefab/GameSettingAsset.asset";
        private const string k_stageScenePath = "Assets/STG/Scene/Stage01.unity";

        /// <summary>Generates all prototype combat assets and their serialized references.</summary>
        [MenuItem("STG/Generate Enemy Prototype Assets")]
        public static void Generate()
        {
            ConfigureTexture(k_straightTexturePath);
            ConfigureTexture(k_shooterTexturePath);
            ConfigureTexture(k_bulletTexturePath);
            ConfigureTexture(k_playerIdleTexturePath);
            ConfigureTexture(k_playerLeftTexturePath);
            ConfigureTexture(k_playerRightTexturePath);
            ConfigurePlayerAnimationPack();

            EnemyController straight = CreateEnemyPrefab(
                k_straightPrefabPath,
                "EnemyStraight",
                k_straightTexturePath,
                EnemyType.Straight,
                new Rect(-0.35f, -0.35f, 0.7f, 0.7f),
                0.04f,
                false);
            EnemyController shooter = CreateEnemyPrefab(
                k_shooterPrefabPath,
                "EnemyShooter",
                k_shooterTexturePath,
                EnemyType.Shooter,
                new Rect(-0.45f, -0.35f, 0.9f, 0.7f),
                0.03f,
                true);
            Bullet bullet = CreateBulletPrefab();

            ConfigureStageSetting(straight, shooter, bullet);
            ConfigureGameSetting();
            ConfigureStageScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated and wired the STG enemy prototype assets.");
        }

        private static void ConfigureTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"Texture importer not found: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static EnemyController CreateEnemyPrefab(
            string prefabPath,
            string objectName,
            string texturePath,
            EnemyType enemyType,
            Rect damageRect,
            float speedPerTick,
            bool hasFirePoint)
        {
            GameObject root = new GameObject(objectName);
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                renderer.sortingLayerName = "Object";
                renderer.sortingOrder = 0;

                Transform firePoint = null;
                if (hasFirePoint)
                {
                    firePoint = new GameObject("FirePoint").transform;
                    firePoint.SetParent(root.transform, false);
                    firePoint.localPosition = new Vector3(0f, -0.5f, 0f);
                }

                EnemyController controller = root.AddComponent<EnemyController>();
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("m_spriteRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("m_enemyType").enumValueIndex = (int)enemyType;
                serialized.FindProperty("m_damageRect").rectValue = damageRect;
                serialized.FindProperty("m_speedPerTick").floatValue = speedPerTick;
                serialized.FindProperty("m_shooterStopY").floatValue = 7f;
                serialized.FindProperty("m_firePoint").objectReferenceValue = firePoint;
                serialized.FindProperty("m_firstShotDelayTicks").intValue = 30;
                serialized.FindProperty("m_fireIntervalTicks").intValue = 90;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return prefab.GetComponent<EnemyController>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePlayerAnimationPack()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(k_playerAnimationPackPath);
            for (int index = 0; index < assets.Length; index++)
            {
                GenjitsuLAB.Animation.AnimationClip clip =
                    assets[index] as GenjitsuLAB.Animation.AnimationClip;
                if (clip == null)
                {
                    continue;
                }

                string texturePath = clip.AnimId switch
                {
                    1 => k_playerLeftTexturePath,
                    2 => k_playerRightTexturePath,
                    _ => k_playerIdleTexturePath
                };
                SerializedObject serialized = new SerializedObject(clip);
                SerializedProperty elements = serialized.FindProperty("m_elements");
                if (elements.arraySize == 0)
                {
                    elements.InsertArrayElementAtIndex(0);
                }

                elements.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("m_sprite")
                    .objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(clip);
            }
        }

        private static Bullet CreateBulletPrefab()
        {
            GameObject root = new GameObject("EnemyBullet");
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(k_bulletTexturePath);
                renderer.sortingLayerName = "Object";
                renderer.sortingOrder = 1;

                Bullet bullet = root.AddComponent<Bullet>();
                SerializedObject serialized = new SerializedObject(bullet);
                serialized.FindProperty("m_damageRect").rectValue = new Rect(-0.1f, -0.1f, 0.2f, 0.2f);
                serialized.FindProperty("m_speedPerTick").floatValue = 0.08f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, k_bulletPrefabPath);
                return prefab.GetComponent<Bullet>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureStageSetting(
            EnemyController straight,
            EnemyController shooter,
            Bullet bullet)
        {
            StageSetting setting = AssetDatabase.LoadAssetAtPath<StageSetting>(k_stageSettingPath);
            SerializedObject serialized = new SerializedObject(setting);
            serialized.FindProperty("m_straightEnemyPrefab").objectReferenceValue = straight;
            serialized.FindProperty("m_shooterEnemyPrefab").objectReferenceValue = shooter;
            serialized.FindProperty("m_bulletPrefab").objectReferenceValue = bullet;
            serialized.FindProperty("m_straightEnemyPoolCapacity").intValue = 8;
            serialized.FindProperty("m_shooterEnemyPoolCapacity").intValue = 4;
            serialized.FindProperty("m_bulletPoolCapacity").intValue = 32;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
        }

        private static void ConfigureGameSetting()
        {
            GameSetting setting = AssetDatabase.LoadAssetAtPath<GameSetting>(k_gameSettingPath);
            SerializedObject serialized = new SerializedObject(setting);
            Rect recycleArea = new Rect(-6f, -1f, 12f, 11f);
            serialized.FindProperty("m_enemyRecycleArea").rectValue = recycleArea;
            serialized.FindProperty("m_bulletRecycleArea").rectValue = recycleArea;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
        }

        private static void ConfigureStageScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(k_stageScenePath);
            StageEnemySpawner[] spawners = Object.FindObjectsByType<StageEnemySpawner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < spawners.Length; index++)
            {
                SerializedObject serialized = new SerializedObject(spawners[index]);
                serialized.FindProperty("m_enemyType").enumValueIndex =
                    spawners[index].name == "Spawner_60" ? (int)EnemyType.Shooter : (int)EnemyType.Straight;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
