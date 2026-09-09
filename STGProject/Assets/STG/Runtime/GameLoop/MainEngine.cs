using UnityEngine;
using UnityEngine.InputSystem;


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
            m_inputActions = new InputActions();
            m_inputActions.Enable();
            m_gameSceneContext = new GameSceneContext(m_gameSetting, m_inputActions);
            m_gameSceneFSM = new GameSceneFSM(m_gameSceneContext);
        }

        private void Start()
        {
            m_gameSceneFSM.ChangeState(new StageState());
            m_frameCount = 0;
            m_gameSceneContext.frame = m_frameCount;
        }

        private void Update()
        {
            m_gameSceneContext.deltaTime = Time.deltaTime;
            FrameLoop();
        }

        private void FrameLoop()
        {
            if (m_gameSceneContext.deltaTime <= 0f)
            {
                return;
            }

            m_accumulator += m_gameSceneContext.deltaTime;
            int tickCount = 0;
            while (m_accumulator >= k_tickDelta && tickCount < k_maxTicksPerUpdate)
            {
                FrameTick();
                m_accumulator -= k_tickDelta;
                tickCount++;
                m_frameCount++;
                m_gameSceneContext.frame = m_frameCount;
            }
        }

        private void FrameTick()
        {
            m_gameSceneFSM?.Update();
        }
    }
}
