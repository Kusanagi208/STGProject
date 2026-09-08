
namespace GenjitsuLAB.Core
{
    public abstract class StateMachine<TState, TContext>
    where TState : class, IState<TContext>
    {
        protected TState m_currentState;
        protected readonly TContext m_ctx;

        public StateMachine(TContext ctx)
        {
            m_ctx = ctx;
        }

        public virtual void ChangeState(TState newState)
        {
            m_currentState?.Exit(m_ctx);
            m_currentState = newState;
            m_currentState?.Enter(m_ctx);
        }

        public virtual void Update()
        {
            m_currentState?.Update(m_ctx);
        }

        public virtual void Stop()
        {
            m_currentState?.Exit(m_ctx);
            m_currentState = null;
        }
    }
}
