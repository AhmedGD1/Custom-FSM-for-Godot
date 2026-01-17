using System;
using System.Collections.Generic;

namespace Stately
{
    public class StateTemplate<T> where T : Enum
    {
        public Action<float> Update { get; set; }
        public Action Enter { get; set; }
        public Action Exit { get; set; }
        public Action OnTimeout { get; set; }
        public Action OnTimeoutExpired { get; set; }
    
        public float MinTime { get; set; } = -1f;
        public float Timeout { get; set; } = -1f;
        public T TimeoutTargetId { get; set; }
    
        public FSMProcessMode? ProcessMode { get; set; }
        public FSMLockMode? LockMode { get; set; }
    
        public float CooldownDuration { get; set; } = -1f;
    
        public List<string> Tags { get; set; } = new();
        public Dictionary<Type, object> Data { get; set; } = new();
    
        public StateTemplate<T> WithUpdate(Action<float> update)
        {
            Update = update;
            return this;
        }
    
        public StateTemplate<T> WithEnter(Action enter)
        {
            Enter = enter;
            return this;
        }
    
        public StateTemplate<T> WithExit(Action exit)
        {
            Exit = exit;
            return this;
        }
    
        public StateTemplate<T> WithMinDuration(float duration)
        {
            MinTime = duration;
            return this;
        }
    
        public StateTemplate<T> WithTimeout(float duration, T targetId = default)
        {
            Timeout = duration;
            TimeoutTargetId = targetId;
            return this;
        }
    
        public StateTemplate<T> WithLock(FSMLockMode mode = FSMLockMode.Full)
        {
            LockMode = mode;
            return this;
        }
    
        public StateTemplate<T> WithProcessMode(FSMProcessMode mode)
        {
            ProcessMode = mode;
            return this;
        }
    
        public StateTemplate<T> WithCooldown(float duration)
        {
            CooldownDuration = duration;
            return this;
        }
    
        public StateTemplate<T> WithTags(params string[] tags)
        {
            Tags.AddRange(tags);
            return this;
        }
    
        public StateTemplate<T> WithData<TData>(TData value)
        {
            Data[typeof(TData)] = value;
            return this;
        }
    
        public void ApplyTo(State<T> state)
        {
            if (Update != null) state.OnUpdate(Update);
            if (Enter != null) state.OnEnter(Enter);
            if (Exit != null) state.OnExit(Exit);
            if (OnTimeout != null) state.OnTimeout(OnTimeout);
            if (OnTimeoutExpired != null) state.OnTimeoutExpired(OnTimeoutExpired);
        
            if (MinTime >= 0f) state.MinDuration(MinTime);
            if (Timeout >= 0f) state.TimeoutAfter(Timeout, TimeoutTargetId);
        
            if (ProcessMode.HasValue) state.ProcessIn(ProcessMode.Value);
            if (LockMode.HasValue) state.Lock(LockMode.Value);
        
            if (CooldownDuration >= 0f) state.SetCooldown(CooldownDuration);
        
            if (Tags.Count > 0) state.AddTags(Tags.ToArray());
        
            foreach (var kvp in Data)
                state.SetDataDirect(kvp.Key, kvp.Value);
        }
    }
}