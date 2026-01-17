
namespace Stately
{
    public partial class StateMachine<T>
    {

        public bool GoBack()
        {
            return GoBack(1);
        }

        public bool GoBack(int steps)
        {
            if (steps < 1)
            {
                logger.LogWarning("GoBack steps must be at least 1");
                return false;
            }

            if (currentState?.IsLocked() ?? true)
            {
                logger.LogWarning("Cannot go back: current state is locked");
                return false;
            }

            if (history.CurrentSize < steps)
            {
                logger.LogWarning($"Cannot go back {steps} steps: only {history.CurrentSize} entries in history");
                return false;
            }

            int targetIndex = history.CurrentSize - steps;
            if (targetIndex < 0)
                return false;
        
            var targetEntry = history.GetEntry(targetIndex);

            if (!states.TryGetValue(targetEntry.StateId, out var state))
            {
                logger.LogWarning($"Cannot go back: target state {targetEntry.StateId} no longer exists");
                return false;
            }

            history.RemoveRange(targetIndex, history.CurrentSize - targetIndex);

            PerformTransition(targetEntry.StateId, bypassHistory: true);
            return true;
        }

        public bool CanGoBack()
        {
            return history.CurrentSize > 0 && !(currentState?.IsLocked() ?? true);
        }

        public bool CanGoBack(int steps)
        {
            return history.CurrentSize >= steps && !(currentState?.IsLocked() ?? true);
        }

        public T PeekBackState()
        {
            return PeekBackState(1);
        }

        public T PeekBackState(int steps)
        {
            if (steps < 1 || history.CurrentSize < steps)
                return default;
        
            int targetIndex = history.CurrentSize - steps;
            return history.GetEntry(targetIndex).StateId;
        }

        public int FindInHistory(T stateId)
        {
            for (int i = history.CurrentSize - 1; i >= 0; i--)
            {
                if (history.GetEntry(i).StateId.Equals(stateId))
                    return history.CurrentSize - i;
            }

            return -1;
        }

        public bool GoBackToState(T id)
        {
            int steps = FindInHistory(id);
            if (steps < 0)
            {
                logger.LogWarning($"State {id} not found in history");
                return false;
            }

            return GoBack(steps);
        }

        public void SetHistoryActive(bool active)
        {
            history.SetActive(active);
        }
    }
}