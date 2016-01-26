using System;
using System.Net.Sockets;
using System.Reflection;

namespace LumberjackClient.Tests
{
    public static class SocketAsyncEventArgsHelper
    {
        private static MethodInfo _methodForOnCompleted;
        private static MethodInfo _methodForSetResults;

        // Call "protected virtual void OnCompleted(SocketAsyncEventArgs e)"
        public static void InvokeOnCompleted(this SocketAsyncEventArgs e)
        {
            if (_methodForOnCompleted == null)
            {
                _methodForOnCompleted = typeof(SocketAsyncEventArgs).GetMethod("OnCompleted", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_methodForOnCompleted == null)
                    throw new InvalidOperationException("Cannot find SocketAsyncEventArgs.OnCompleted");
            }

            _methodForOnCompleted.Invoke(e, new object[1] { e });
        }

        // Call "internal void SetResults(SocketError socketError, int bytesTransferred, SocketFlags flags)"
        public static void InvokeSetResults(
            this SocketAsyncEventArgs e,
            SocketError socketError, int bytesTransferred, SocketFlags flags)
        {
            if (_methodForSetResults == null)
            {
                foreach (var method in typeof (SocketAsyncEventArgs).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.Name == "SetResults" && 
                        method.GetParameters().Length == 3 &&
                        method.GetParameters()[0].ParameterType == typeof (SocketError))
                    {
                        _methodForSetResults = method;
                        break;
                    }
                }
                if (_methodForSetResults == null)
                    throw new InvalidOperationException("Cannot find SocketAsyncEventArgs.SetResult");
            }
            _methodForSetResults.Invoke(e, new object[] {socketError, bytesTransferred, flags});
        }
    }
}
