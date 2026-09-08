using UnityEngine;

namespace GenjitsuLAB.STG
{
    public class MainEngine : MonoBehaviour
    {
        private GameSceneFSM m_gameSceneFSM;
        private GameSceneContext m_gameSceneContext;

        private void Awake()
        {

        }

        private void Start()
        {
            m_gameSceneFSM = new GameSceneFSM(m_gameSceneContext = new GameSceneContext());
            m_gameSceneFSM.ChangeState(new StageState());
        }

        private void Update()
        {
            m_gameSceneFSM.Update();
        }
    }
}
