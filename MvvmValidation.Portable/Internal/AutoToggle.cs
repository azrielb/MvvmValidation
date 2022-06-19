using System;

namespace MvvmValidation.Internal
{
    internal class AutoToggle
    {
        private readonly bool defaultValue;
        private int refCount = 0;

        public AutoToggle(bool defaultValue)
        {
            this.defaultValue = defaultValue;
        }

        public bool Value
        {
            get
            {
                return refCount > 0 ? !defaultValue : defaultValue;
            }
        }

        public IDisposable Toggle()
        {
            return new Lock(this);
        }

        private class Lock : IDisposable
        {
            private readonly AutoToggle toggle;

            public Lock(AutoToggle toggle)
            {
                this.toggle = toggle;
                System.Threading.Interlocked.Increment(ref toggle.refCount);
            }

            public void Dispose()
            {
                System.Threading.Interlocked.Decrement(ref toggle.refCount);
            }
        }
    }
}
