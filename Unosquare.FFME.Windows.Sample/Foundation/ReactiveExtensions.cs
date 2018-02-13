﻿namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Primitives;

    internal static class ReactiveExtensions
    {
        /// <summary>
        /// Contains a list of subscriptions Subscriptions[Publisher][PropertyName].List of subscriber-action pairs
        /// </summary>
        private static readonly Dictionary<INotifyPropertyChanged, SubscriptionSet> Subscriptions
            = new Dictionary<INotifyPropertyChanged, SubscriptionSet>();

        private static readonly ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);

        private static readonly Dictionary<Action, bool> PinnedActions = new Dictionary<Action, bool>();

        internal static void WhenChanged(this Action callback, INotifyPropertyChanged publisher, params string[] propertyNames)
        {
            callback.WhenChanged(true, publisher, propertyNames);
        }

        internal static void WhenChanged(this Action callback, bool pinned, INotifyPropertyChanged publisher, params string[] propertyNames)
        {
            var bindPropertyChanged = false;

            using (Locker.AcquireWriterLock())
            {
                if (Subscriptions.ContainsKey(publisher) == false)
                {
                    Subscriptions[publisher] = new SubscriptionSet();
                    bindPropertyChanged = true;
                }

                // Save the Action reference so that the weak reference is not lost
                if (pinned) PinnedActions[callback] = true;

                foreach (var propertyName in propertyNames)
                {
                    if (Subscriptions[publisher].ContainsKey(propertyName) == false)
                        Subscriptions[publisher][propertyName] = new CallbackReferenceSet();

                    Subscriptions[publisher][propertyName].Add(new CallbackReference(callback));
                }
            }

            if (bindPropertyChanged == false) return;

            // Finally, bind to proety changed
            publisher.PropertyChanged += (s, e) =>
            {
                if (Subscriptions[publisher].ContainsKey(e.PropertyName) == false)
                    return;

                var deadCallbacks = new CallbackReferenceSet();
                var aliveCallbacks = new CallbackReferenceSet();

                using (Locker.AcquireReaderLock())
                {
                    aliveCallbacks.AddRange(Subscriptions[publisher][e.PropertyName]);
                }

                foreach (var aliveSubscription in aliveCallbacks)
                {
                    if (aliveSubscription.IsAlive == false)
                    {
                        deadCallbacks.Add(aliveSubscription);
                        continue;
                    }

                    aliveSubscription.Target?.Invoke();
                }

                if (deadCallbacks.Count == 0) return;

                using (Locker.AcquireWriterLock())
                {
                    foreach (var deadSubscriber in deadCallbacks)
                        Subscriptions[publisher][e.PropertyName].Remove(deadSubscriber);
                }
            };
        }

        internal sealed class SubscriptionSet : Dictionary<string, CallbackReferenceSet> { }

        internal sealed class CallbackReferenceSet : List<CallbackReference>
        {
            public CallbackReferenceSet()
                : base(32)
            {
                // placeholder
            }
        }

        internal sealed class CallbackReference : WeakReference
        {
            public CallbackReference(Action action)
                : base(action, false)
            {
                // placeholder
            }

            public new Action Target => IsAlive ? base.Target as Action : null;
        }
    }
}
