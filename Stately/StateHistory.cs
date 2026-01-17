using System;
using System.Collections.Generic;

namespace Stately
{
    public class StateHistory<T> where T : Enum
    {
        public bool IsActive => active;
        public int Capacity => capacity;
        public int CurrentSize => entries.Count;

        private List<HistoryEntry<T>> entries = new();

        private int capacity = 20;
        private float totalElapsedTime;

        private bool active = true;

        public void SetActive(bool value)
        {
            active = value;
        }

        public void SetCapacity(int value)
        {
            capacity = Math.Max(0, value);
            Trim();
        }

        public void CreateNewEntry(T stateId, float timeSpent)
        {
            var entry = new HistoryEntry<T>(stateId, timeSpent, totalElapsedTime);
            entries.Add(entry);

            Trim();
        }

        public void UpdateElapsedTime(float delta)
        {
            totalElapsedTime += delta;
        }

        public IReadOnlyList<HistoryEntry<T>> GetHistory()
        {
            var reversed = new List<HistoryEntry<T>>(entries);
            reversed.Reverse();
        
            return reversed;
        }

        public List<HistoryEntry<T>> GetRecentHistory(int count)
        {
            int min = Math.Min(count, entries.Count);

            var recent = entries.GetRange(entries.Count - min, min);
            recent.Reverse();

            return recent;
        }

        public HistoryEntry<T> GetEntry(int index)
        {
            return entries[index];
        }

        public void RemoveRange(int startIndex, int count)
        {
            entries.RemoveRange(startIndex , count);
        }

        public void ClearHistory()
        {
            entries.Clear();
        }

        private void Trim()
        {
            if (capacity > 0 && entries.Count > capacity)
            {
                int removeCount = entries.Count - capacity;
                entries.RemoveRange(0, removeCount);
            }
        }
    }

    public struct HistoryEntry<T> where T : Enum
    {
        public T StateId { get; }
        public float TimeSpent { get; }
        public float TimeStamp { get; }

        public HistoryEntry(T stateId, float timeSpent, float timeStamp)
        {
            StateId = stateId;
            TimeSpent = timeSpent;
            TimeStamp = timeStamp;
        }
    }
}