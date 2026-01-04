using log4net;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    public sealed class SanctionReconciler : IDisposable
    {
        private const string CONTEXT_START = "SanctionReconciler.Start";
        private const string CONTEXT_TICK = "SanctionReconciler.Tick";
        private const string CONTEXT_RECONCILE = "SanctionReconciler.Reconcile";
        private const string CONTEXT_DISPOSE = "SanctionReconciler.Dispose";

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string APPSETTING_INTERVAL_SECONDS = "SanctionReconcilerIntervalSeconds";
        private const int DEFAULT_INTERVAL_SECONDS = 30;
        private const int MIN_INTERVAL_SECONDS = 5;
        private const int MAX_INTERVAL_SECONDS = 3600;

        private const int COMMAND_TIMEOUT_SECONDS = 30;

        private const string SP_SANCTIONS_RECONCILE = "dbo.usp_sanctions_reconcile";

        private static readonly ILog DefaultLogger = LogManager.GetLogger(typeof(SanctionReconciler));

        private readonly string _connectionString;
        private readonly ILog _logger;

        private Timer _timer;
        private int _isRunning;
        private int _isDisposed;

        private readonly int _intervalSeconds;

        public SanctionReconciler(string connectionString, ILog logger = null)
        {
            _connectionString = ResolveConnectionString(connectionString);
            _logger = logger ?? DefaultLogger;

            _intervalSeconds = ClampIntervalSeconds(ReadIntervalSecondsFromConfigOrDefault());
        }

        public bool Start()
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return false;
            }

            if (_timer != null)
            {
                return true;
            }

            try
            {
                _timer = new Timer(
                    callback: _ => TickSafe(),
                    state: null,
                    dueTime: TimeSpan.Zero,
                    period: TimeSpan.FromSeconds(_intervalSeconds));

                _logger.InfoFormat(
                    "{0}: started. IntervalSeconds={1}",
                    CONTEXT_START,
                    _intervalSeconds);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(CONTEXT_START, ex);
                return false;
            }
        }

        private void TickSafe()
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            {
                return; // evita re-entradas si tarda más que el intervalo
            }

            try
            {
                ReconcileOnce();
            }
            catch (Exception ex)
            {
                _logger.Error(CONTEXT_TICK, ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private void ReconcileOnce()
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(SP_SANCTIONS_RECONCILE, connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;

                connection.Open();

                int rows = command.ExecuteNonQuery();

                _logger.DebugFormat(
                    "{0}: executed. Rows={1}",
                    CONTEXT_RECONCILE,
                    rows);
            }
        }

        private static int ReadIntervalSecondsFromConfigOrDefault()
        {
            string raw = ConfigurationManager.AppSettings[APPSETTING_INTERVAL_SECONDS];

            if (int.TryParse(raw, out int value))
            {
                return value;
            }

            return DEFAULT_INTERVAL_SECONDS;
        }

        private static int ClampIntervalSeconds(int seconds)
        {
            if (seconds < MIN_INTERVAL_SECONDS)
            {
                return MIN_INTERVAL_SECONDS;
            }

            if (seconds > MAX_INTERVAL_SECONDS)
            {
                return MAX_INTERVAL_SECONDS;
            }

            return seconds;
        }

        private static string ResolveConnectionString(string connectionString)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            var cs = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];
            return cs != null ? (cs.ConnectionString ?? string.Empty) : string.Empty;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            try
            {
                var timer = _timer;
                _timer = null;

                if (timer != null)
                {
                    timer.Dispose();
                }

                _logger.Info(CONTEXT_DISPOSE);
            }
            catch (Exception ex)
            {
                _logger.Error(CONTEXT_DISPOSE, ex);
            }
        }
    }
}
