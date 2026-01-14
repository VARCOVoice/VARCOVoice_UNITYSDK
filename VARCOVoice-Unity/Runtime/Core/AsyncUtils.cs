using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VARCOVoice
{
    /// <summary>
    /// Helper utilities for async operations in Unity without UniTask
    /// </summary>
    public static class AsyncUtils
    {
        /// <summary>
        /// Extension method to allow awaiting UnityWebRequestAsyncOperation
        /// </summary>
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }

        /// <summary>
        /// Simple awaiter for UnityWebRequest
        /// </summary>
        public struct UnityWebRequestAwaiter : INotifyCompletion
        {
            private readonly UnityWebRequestAsyncOperation _asyncOp;
            private Action _continuation;

            public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
            {
                _asyncOp = asyncOp;
                _continuation = null;
            }

            public bool IsCompleted => _asyncOp.isDone;

            public void OnCompleted(Action continuation)
            {
                _continuation = continuation;
                _asyncOp.completed += OnRequestCompleted;
            }

            private void OnRequestCompleted(AsyncOperation obj)
            {
                _continuation?.Invoke();
            }

            public void GetResult() { }
        }

        /// <summary>
        /// Wait while a condition is true
        /// </summary>
        public static async Task WaitWhile(Func<bool> condition)
        {
            while (condition())
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Wait until a condition becomes true
        /// </summary>
        public static async Task WaitUntil(Func<bool> condition)
        {
            while (!condition())
            {
                await Task.Yield();
            }
        }
    }
}
