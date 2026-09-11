using UnityEngine;


namespace GenjitsuLAB.STG
{
    public class MainEngine : MonoBehaviour
    {
        private const int k_tickRate = 60;
        private const float k_tickDelta = 1f / k_tickRate;
        private const int k_maxTicksPerUpdate = 8;

        private static MainEngine s_instance;

        [SerializeField] private GameSetting m_gameSetting;

        private int m_frameCount;
        private double m_accumulator;

        private GameSceneFSM m_gameSceneFSM;
        private GameSceneContext m_gameSceneContext;
        private InputActions m_inputActions;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            if (!ValidateSettings())
            {
                enabled = false;
                return;
            }

            m_inputActions = new InputActions();
            m_gameSceneContext = new GameSceneContext(m_gameSetting, m_inputActions);
            m_gameSceneContext.deltaTime = k_tickDelta;
            m_gameSceneFSM = new GameSceneFSM(m_gameSceneContext);
        }

        private void Start()
        {
            if (m_gameSceneFSM == null)
            {
                return;
            }

            m_frameCount = 0;
            m_gameSceneContext.frame = m_frameCount;
            m_gameSceneFSM.ChangeState(new StageState(m_gameSetting.InitialStage));
        }

        private void Update()
        {
            FrameLoop(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (s_instance != this)
            {
                return;
            }

            m_gameSceneFSM?.Stop();
            m_gameSceneFSM = null;
            m_gameSceneContext = null;

            if (m_inputActions != null)
            {
                m_inputActions.Disable();
                m_inputActions.Dispose();
                m_inputActions = null;
            }

            s_instance = null;
        }

        private void FrameLoop(float frameDeltaTime)
        {
            if (frameDeltaTime <= 0f)
            {
                return;
            }

            double maxAccumulatedTime = k_tickDelta * k_maxTicksPerUpdate;
            m_accumulator = System.Math.Min(m_accumulator + frameDeltaTime, maxAccumulatedTime);

            int tickCount = 0;
            while (m_accumulator >= k_tickDelta && tickCount < k_maxTicksPerUpdate)
            {
                FrameTick();
                m_accumulator -= k_tickDelta;
                tickCount++;
                m_frameCount++;
                m_gameSceneContext.frame = m_frameCount;
            }

            if (tickCount == k_maxTicksPerUpdate)
            {
                m_accumulator = 0d;
            }
        }

        private void FrameTick()
        {
            m_gameSceneFSM?.Update();
        }

        private bool ValidateSettings()
        {
            if (m_gameSetting == null)
            {
                Debug.LogError("MainEngine requires a GameSetting asset.", this);
                return false;
            }

            if (m_gameSetting.PlayerPrefab == null)
            {
                Debug.LogError("GameSetting requires a player prefab.", m_gameSetting);
                return false;
            }

            if (m_gameSetting.InitialStage == null)
            {
                Debug.LogError("GameSetting requires an initial stage.", m_gameSetting);
                return false;
            }

            if (string.IsNullOrWhiteSpace(m_gameSetting.InitialStage.SceneName))
            {
                Debug.LogError("Initial StageSetting requires an additive scene name.", m_gameSetting.InitialStage);
                return false;
            }

            if (m_gameSetting.InitialStage.PickupPrefab == null)
            {
                Debug.LogError("Initial StageSetting requires a pickup prefab.", m_gameSetting.InitialStage);
                return false;
            }

            if (m_gameSetting.InitialStage.PickupPoolCapacity <= 0)
            {
                Debug.LogError("Initial StageSetting pickup pool capacity must be positive.", m_gameSetting.InitialStage);
                return false;
            }

            if (m_gameSetting.InitialStage.StraightEnemyPrefab == null ||
                m_gameSetting.InitialStage.ShooterEnemyPrefab == null ||
                m_gameSetting.InitialStage.BulletPrefab == null)
            {
                Debug.LogError("Initial StageSetting requires Straight, Shooter, and Bullet prefabs.", m_gameSetting.InitialStage);
                return false;
            }

            if (m_gameSetting.InitialStage.StraightEnemyPoolCapacity <= 0 ||
                m_gameSetting.InitialStage.ShooterEnemyPoolCapacity <= 0 ||
                m_gameSetting.InitialStage.BulletPoolCapacity <= 0)
            {
                Debug.LogError("Initial StageSetting enemy and bullet pool capacities must be positive.", m_gameSetting.InitialStage);
                return false;
            }

            Rect movementArea = m_gameSetting.PlayerMovementArea;
            if (movementArea.width <= 0f || movementArea.height <= 0f)
            {
                Debug.LogError("GameSetting player movement area must have a positive width and height.", m_gameSetting);
                return false;
            }

            Rect weaponRecycleArea = m_gameSetting.WeaponRecycleArea;
            if (weaponRecycleArea.width <= 0f || weaponRecycleArea.height <= 0f)
            {
                Debug.LogError("GameSetting weapon recycle area must have a positive width and height.", m_gameSetting);
                return false;
            }

            if (weaponRecycleArea.xMin > movementArea.xMin ||
                weaponRecycleArea.xMax < movementArea.xMax ||
                weaponRecycleArea.yMin > movementArea.yMin ||
                weaponRecycleArea.yMax < movementArea.yMax)
            {
                Debug.LogError("GameSetting weapon recycle area must contain the player movement area.", m_gameSetting);
                return false;
            }

            Rect pickupRecycleArea = m_gameSetting.PickupRecycleArea;
            if (pickupRecycleArea.width <= 0f || pickupRecycleArea.height <= 0f)
            {
                Debug.LogError("GameSetting pickup recycle area must have a positive width and height.", m_gameSetting);
                return false;
            }

            if (pickupRecycleArea.xMin > movementArea.xMin ||
                pickupRecycleArea.xMax < movementArea.xMax ||
                pickupRecycleArea.yMin > movementArea.yMin ||
                pickupRecycleArea.yMax < movementArea.yMax)
            {
                Debug.LogError("GameSetting pickup recycle area must contain the player movement area.", m_gameSetting);
                return false;
            }

            if (!ValidateContainingArea(m_gameSetting.EnemyRecycleArea, movementArea, "enemy recycle"))
            {
                return false;
            }

            if (!ValidateContainingArea(m_gameSetting.BulletRecycleArea, movementArea, "bullet recycle"))
            {
                return false;
            }

            return true;
        }

        private bool ValidateContainingArea(Rect area, Rect movementArea, string areaName)
        {
            if (area.width <= 0f || area.height <= 0f)
            {
                Debug.LogError($"GameSetting {areaName} area must have a positive width and height.", m_gameSetting);
                return false;
            }

            if (area.xMin > movementArea.xMin ||
                area.xMax < movementArea.xMax ||
                area.yMin > movementArea.yMin ||
                area.yMax < movementArea.yMax)
            {
                Debug.LogError($"GameSetting {areaName} area must contain the player movement area.", m_gameSetting);
                return false;
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
        }
    }
}
