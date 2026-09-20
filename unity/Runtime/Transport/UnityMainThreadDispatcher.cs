using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Voxta.Unity.Transport
{
    /// <summary>Routes networking completions to the Unity player loop.</summary>
    internal sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();
        private static UnityMainThreadDispatcher instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance != null)
                return;

            var gameObject = new GameObject("Voxta Main Thread Dispatcher");
            DontDestroyOnLoad(gameObject);
            instance = gameObject.AddComponent<UnityMainThreadDispatcher>();
        }

        internal static void Post(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Pending.Enqueue(action);
        }

        internal static void DrainPending()
        {
            while (Pending.TryDequeue(out var action))
                action();
        }

        private void Update()
        {
            while (Pending.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
