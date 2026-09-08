

namespace GenjitsuLAB.Core
{
    public interface IState<T>
    {
        void Enter(T ctx);
        void Update(T ctx);
        void Exit(T ctx);
    }
}