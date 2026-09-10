using UnityEngine;


namespace GenjitsuLAB.STG
{
    public class MainEngine : MonoBehaviour
    {
        private const int k_tickRate = 60;
        private const float k_tickDelta = 1f / k_tickRate;
        private const int k_maxTicksPerUpdate = 8;

        [SerializeField] private GameSetting m_gameSetting;

        private int m_frameCount;
        private double m_accumulator;

        private GameSceneFSM m_gameSceneFSM;
        private GameSceneContext m_gameSceneContext;
        private InputActions m_inputActions;

        private void Awake()
        {
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
            m_gameSceneFSM?.Stop();
            m_gameSceneFSM = null;
            m_gameSceneContext = null;

            if (m_inputActions != null)
            {
                m_inputActions.Disable();
                m_inputActions.Dispose();
                m_inputActions = null;
            }
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

            if (m_gameSetting.InitialStage.BackgroundPrefab == null)
            {
                Debug.LogError("Initial StageSetting requires a background prefab.", m_gameSetting.InitialStage);
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

            return true;
        }
    }
}
