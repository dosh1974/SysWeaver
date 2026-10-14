using System;
using System.Data;
using System.Data.Common;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using SimpleStack.Orm.Logging;

namespace SimpleStack.Orm
{
    internal class OrmCommand : DbCommand
    {
        private readonly ILogger<OrmCommand> _logger;

        /// <summary>
        /// The logger as a debug filter, null if it doesn't implement it (debug messages are always logged)
        /// </summary>
        private readonly IDebugLogFilter _debugFilter;

        private DbCommand _command;

        /// <param name="command">The provider command</param>
        /// <param name="logger">The logger to use, shared by all commands of a connection</param>
        internal OrmCommand(DbCommand command, ILogger<OrmCommand> logger)
        {
            _command = command;
            _logger = logger;
            _debugFilter = logger as IDebugLogFilter;
        }

        /// <summary>
        /// True if debug messages are logged, false if their arguments can be skipped
        /// </summary>
        private bool IsDebug
        {
            get
            {
                var f = _debugFilter;
                return f == null || f.IsDebugEnabled;
            }
        }

        public override string CommandText
        {
            get => _command.CommandText;
            set => _command.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _command.CommandTimeout;
            set => _command.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _command.CommandType;
            set => _command.CommandType = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _command.UpdatedRowSource;
            set => _command.UpdatedRowSource = value;
        }

        protected override DbConnection DbConnection
        {
            get => _command.Connection;
            set => _command.Connection = value;
        }

        protected override DbParameterCollection DbParameterCollection => _command.Parameters;

        protected override DbTransaction DbTransaction
        {
            get => _command.Transaction;
            set => _command.Transaction = value;
        }

        public override bool DesignTimeVisible
        {
            get => _command.DesignTimeVisible;
            set => _command.DesignTimeVisible = value;
        }

        public override void Prepare()
        {
            if (IsDebug)
                _logger.LogDebug("Preparing command");
            _command.Prepare();
        }

        public override void Cancel()
        {
            if (IsDebug)
                _logger.LogDebug("Cancelling command: {0}", CommandText);
            _command.Cancel();
        }

        protected override DbParameter CreateDbParameter()
        {
            return _command.CreateParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (IsDebug)
                _logger.LogDebug("Executing ExecuteReader : {0}", CommandText);
            var res = _command.ExecuteReader(behavior);
            if (IsDebug)
                _logger.LogDebug("ExecuteReader complete");
            return res;
        }

        public override int ExecuteNonQuery()
        {
            if (IsDebug)
                _logger.LogDebug("Executing ExecuteNonQuery : {0}", CommandText);
            var res = _command.ExecuteNonQuery();
            if (IsDebug)
                _logger.LogDebug("ExecuteNonQuery complete with result '{0}'", res);
            return res;
        }

        public override object ExecuteScalar()
        {
            if (IsDebug)
                _logger.LogDebug("Executing ExecuteScalar: {0}", CommandText);
            var res = _command.ExecuteScalar();
            if (IsDebug)
                _logger.LogDebug("ExecuteScalar complete with result '{0}'", res);
            return res;
        }

        // The async methods aren't async: a provider task that completed synchronously is returned as it is (no state machine and task),
        // the exceptions are returned in the task (like an async method does)

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            try
            {
                if (IsDebug)
                    _logger.LogDebug("Executing ExecuteReaderAsync : {0}", CommandText);
                var t = _command.ExecuteReaderAsync(behavior, cancellationToken);
                if (t.Status != TaskStatus.RanToCompletion)
                    return CompleteReaderAsync(t);
                if (IsDebug)
                    _logger.LogDebug("ExecuteReaderAsync complete");
                return t;
            }
            catch (Exception ex)
            {
                return Failed<DbDataReader>(ex);
            }
        }

        private async Task<DbDataReader> CompleteReaderAsync(Task<DbDataReader> t)
        {
            var res = await t.ConfigureAwait(false);
            if (IsDebug)
                _logger.LogDebug("ExecuteReaderAsync complete");
            return res;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (IsDebug)
                    _logger.LogDebug("Executing ExecuteNonQueryAsync : {0}", CommandText);
                var t = _command.ExecuteNonQueryAsync(cancellationToken);
                if (t.Status != TaskStatus.RanToCompletion)
                    return CompleteNonQueryAsync(t);
                if (IsDebug)
                    _logger.LogDebug("ExecuteNonQueryAsync complete with result '{0}'", t.Result);
                return t;
            }
            catch (Exception ex)
            {
                return Failed<int>(ex);
            }
        }

        private async Task<int> CompleteNonQueryAsync(Task<int> t)
        {
            var res = await t.ConfigureAwait(false);
            if (IsDebug)
                _logger.LogDebug("ExecuteNonQueryAsync complete with result '{0}'", res);
            return res;
        }

        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (IsDebug)
                    _logger.LogDebug("Executing ExecuteScalarAsync: {0}", CommandText);
                var t = _command.ExecuteScalarAsync(cancellationToken);
                if (t.Status != TaskStatus.RanToCompletion)
                    return CompleteScalarAsync(t);
                if (IsDebug)
                    _logger.LogDebug("ExecuteScalarAsync complete with result '{0}'", t.Result);
                return t;
            }
            catch (Exception ex)
            {
                return Failed<object>(ex);
            }
        }

        private async Task<object> CompleteScalarAsync(Task<object> t)
        {
            var res = await t.ConfigureAwait(false);
            if (IsDebug)
                _logger.LogDebug("ExecuteScalarAsync complete with result '{0}'", res);
            return res;
        }

        /// <summary>
        /// A task that failed like an async method that threw the exception: faulted, or canceled for an OperationCanceledException (an await rethrows the same exception)
        /// </summary>
        private static Task<T> Failed<T>(Exception ex)
        {
            return ex is OperationCanceledException ? Rethrow<T>(ex) : Task.FromException<T>(ex);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators: the async method builder turns an OperationCanceledException into a canceled task (keeping the exception)
        private static async Task<T> Rethrow<T>(Exception ex)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
            return default(T);
        }
#pragma warning restore CS1998

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _command.Dispose();
                _command = null;
            }
            base.Dispose(disposing);
        }
    }
}