using System;

namespace SimpleStack.Orm.Logging
{
    public class DummyLoggerFactory : ILoggerFactory
    {
        private class DummyLogger<T> : ILogger<T>, IDebugLogFilter
        {
            public bool IsDebugEnabled => false;

            public void LogError(Exception e, string message, params object[] p)
            {
            
            }

            public void LogWarning(string message, params object[] p)
            {
            
            }

            public void LogInfo(string message, params object[] p)
            {
            
            }

            public void LogDebug(string message, params object[] p)
            {
            
            }
        }
        public ILogger<T> CreateLogger<T>()
        {
            return new DummyLogger<T>();
        }
    }
}