using System;

namespace Stately
{
    public class Cooldown
    {
        private float duration;
        private float remainingTime;
        private bool active;

        public float Duration => duration;
        public bool IsActive => active && remainingTime > 0f;

        public Cooldown(float cooldownDuration = 0f)
        {
            SetDuration(cooldownDuration);
        }

        public void SetDuration(float newDuration)
        {
            duration = MathF.Max(0f, newDuration);
        }

        public void Update(float delta)
        {
            if (!active)
                return;
        
            remainingTime -= delta;

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                active = false;
            }
        }

        public void Start()
        {
            if (duration <= 0f)
                return;
        
            remainingTime = duration;
            active = true;
        }

        public void Reset()
        {
            remainingTime = 0f;
            active = false;
        }

        public float GetRemaining()
        {
            return active ? MathF.Max(0f, remainingTime) : 0f;
        }

        public float GetProgress()
        {
            if (duration <= 0f || !active) 
                return 0f;
        
            float elapsed = duration - remainingTime;
            return Math.Clamp(elapsed / duration, 0f, 1f);
        }

        public float GetNormalizedRemaining()
        {
            if (duration <= 0f || !active)
                return 0f;

            return Math.Clamp(remainingTime / duration, 0f, 1f);
        }

        public bool IsComplete()
        {
            return !IsActive;
        }
    }

}
