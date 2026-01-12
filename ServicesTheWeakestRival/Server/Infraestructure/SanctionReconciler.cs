using log4net;
using ServicesTheWeakestRival.Server.Infrastructure.Faults;
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

        private const string OPERATION_KEY_PREFIX_RECONCILE = "Sanction.Reconcile";

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private const string APPSETTING_INTERVAL_SECONDS = "SanctionReconcilerIntervalSeconds";
        private const int DEFAULT_INTERVAL_SECONDS = 30;
        private const int MIN_INTERVAL_SECONDS = 5;
        private const int MAX_INTERVAL_SECONDS = 3600;

        private const int COMMAND_TIMEOUT_SECONDS = 30;

        private const string SP_SANCTIONS_RECONCILE = "dbo.usp_sanctions_reconcile";

        private static readonly ILog DefaultLogger = LogManager.GetLogger(typeof(SanctionReconciler));

        private readonly string connectionString;
        private readonly ILog logger;

        private Timer timer;
        private int isRunning;
        private int isDisposed;

        private readonly int intervalSeconds;

        public SanctionReconciler(string connectionString, ILog logger = null)
        {
            this.connectionString = ResolveConnectionString(connectionString);
            this.logger = logger ?? DefaultLogger;

            intervalSeconds = ClampIntervalSeconds(ReadIntervalSecondsFromConfigOrDefault());
        }

        public bool Start()
        {
            if (Volatile.Read(ref isDisposed) == 1)
            {
                return false;
            }

            if (timer != null)
            {
                return true;
            }

            try
            {
                timer = new Timer(
                    callback: _ => TickSafe(),
                    state: null,
                    dueTime: TimeSpan.Zero,
                    period: TimeSpan.FromSeconds(intervalSeconds));

                this.logger.InfoFormat(
                    "{0}: started. IntervalSeconds={1}",
                    CONTEXT_START,
                    intervalSeconds);

                return true;
            }
            catch (Exception ex)
            {
                this.logger.Error(CONTEXT_START, ex);
                return false;
            }
        }

        private void TickSafe()
        {
            bool canRun = Volatile.Read(ref isDisposed) == 0;
            if (canRun)
            {
                bool acquired = Interlocked.Exchange(ref isRunning, 1) == 0;
                if (acquired)
                {
                    try
                    {
                        ReconcileOnce();
                    }
                    catch (SqlException ex)
                    {
                        LogSqlException(ex);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(CONTEXT_TICK, ex);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref isRunning, 0);
                    }
                }
            }
        }

        private void ReconcileOnce()
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(SP_SANCTIONS_RECONCILE, connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;

                connection.Open();

                int rows = command.ExecuteNonQuery();

                logger.DebugFormat(
                    "{0}: executed. Rows={1}",
                    CONTEXT_RECONCILE,
                    rows);
            }
        }

        private void LogSqlException(SqlException ex)
        {
            SqlFaultMapping mapping = SqlExceptionFaultMapper.Map(ex, OPERATION_KEY_PREFIX_RECONCILE);

            logger.ErrorFormat(
                "{0}: SqlException. Key={1}, Details={2}",
                CONTEXT_RECONCILE,
                mapping.MessageKey,
                mapping.Details);

            logger.Error(CONTEXT_RECONCILE, ex);
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
            bool shouldDispose = Interlocked.Exchange(ref isDisposed, 1) == 0;
            if (shouldDispose)
            {
                try
                {
                    Timer current = timer;
                    timer = null;

                    if (current != null)
                    {
                        current.Dispose();
                    }

                    logger.Info(CONTEXT_DISPOSE);
                }
                catch (Exception ex)
                {
                    logger.Error(CONTEXT_DISPOSE, ex);
                }
            }
        }
    }
}
