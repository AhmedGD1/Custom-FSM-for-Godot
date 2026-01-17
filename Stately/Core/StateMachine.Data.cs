
namespace Stately
{
    public partial class StateMachine<T>
    {
        public void SetData<TData>(TData value)
        {
            globalData[typeof(TData)] = value;
        }

        public bool RemoveData<TData>()
        {
            return globalData.Remove(typeof(TData));
        }

        public bool TryGetData<TData>(out TData value)
        {
            if (globalData.TryGetValue(typeof(TData), out var result) && result is TData castValue)
            {
                value = castValue;
                return true;
            }
            value = default;
            return false;
        }

        public TData GetData<TData>()
        {
            if (globalData.TryGetValue(typeof(TData), out var result) && result is TData castValue)
                return castValue;
            return default;
        }

        public TData GetTransitionData<TData>()
        {
            if (transitionData is TData cast)
                return cast;
            return default;
        }

        public bool TryGetTransitionData<TData>(out TData value)
        {
            if (transitionData is TData cast)
            {
                value = cast;
                return true;
            }
            value = default;
            return false;
        }
    }
}