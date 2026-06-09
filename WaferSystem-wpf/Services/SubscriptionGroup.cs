using System;
using System.Collections.Generic;

namespace WaferSystem.Wpf.Services
{
    public sealed class SubscriptionGroup : IDisposable
    {
        private readonly List<Action> _unbindActions = new List<Action>();

        public void Bind(Action bind, Action unbind)
        {
            bind?.Invoke();
            if (unbind != null)
                _unbindActions.Add(unbind);
        }

        public void Clear()
        {
            for (int i = _unbindActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _unbindActions[i]?.Invoke();
                }
                catch
                {
                }
            }

            _unbindActions.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
