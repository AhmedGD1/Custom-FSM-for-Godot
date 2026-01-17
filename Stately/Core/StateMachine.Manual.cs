using System;

namespace Stately
{
    public partial class StateMachine<T>
    {
        public bool TryTransitionTo(T to)
        {
            // Resolve to leaf (handles parent → default child)
            T targetLeaf = ResolveToLeaf(to);
        
            if (!CanTransitionTo(targetLeaf))
                return false;
        
            PerformTransition(targetLeaf);
            return true;
        }
    
        public bool TryTransitionTo<TData>(T to, TData data)
        {
            T targetLeaf = ResolveToLeaf(to);
        
            if (TryTransitionTo(targetLeaf))
            {
                transitionData = data;
                return true;
            }
            return false;
        }

        public void ForceTransitionTo(T to)
        {
            // Resolve to leaf
            T targetLeaf = ResolveToLeaf(to);
        
            if (!states.ContainsKey(targetLeaf))
            {
                logger.LogError($"Cannot force transition: state {targetLeaf} does not exist");
                return;
            }
        
            PerformTransition(targetLeaf);
        }

        public bool CanTransitionTo(T to)
        {
            return CanTransitionTo(to, out _);
        }

        public bool CanTransitionTo(T to, out string reason)
        {
            // Resolve to leaf first
            T targetLeaf = ResolveToLeaf(to);
        
            if (currentState?.IsLocked() ?? true)
            {
                reason = "Current state is locked";
                return false;
            }
        
            if (!MinTimeExceeded())
            {
                reason = "Minimum time not exceeded";
                return false;
            }
        
            if (!states.ContainsKey(targetLeaf))
            {
                reason = $"Target state {targetLeaf} does not exist";
                return false;
            }
        
            var targetState = GetState(targetLeaf);
        
            // Check if it's a parent without default child
            if (targetState.IsParent && targetState.DefaultChildId.Equals(default(T)))
            {
                reason = $"Parent state {to} has no default child set";
                return false;
            }
        
            if (targetState.IsOnCooldown())
            {
                reason = "Target state is on cooldown";
                return false;
            }
        
            reason = null;
            return true;
        }

        public bool TriggerTimeout()
        {
            if (currentState == null)
            {
                logger.LogError("No current state");
                return false;
            }
        
            if (currentState.Timeout <= 0f)
            {
                logger.LogWarning("Current state has no timeout configured");
                return false;
            }
        
            OnStateTimeoutTriggered();
            return true;
        }

        public void ResetStateTime()
        {
            stateTime = 0f;
        }

        public void SetStateTime(float time)
        {
            stateTime = MathF.Max(0f, time);
        }

        public void AddStateTime(float delta)
        {
            stateTime += delta;
        }

        public void TogglePaused(bool toggle) => paused = toggle;
        public void Pause() => paused = true;
        public void Resume() => paused = false;
    }

}
