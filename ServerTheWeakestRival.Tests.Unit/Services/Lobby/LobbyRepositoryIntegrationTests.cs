using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Lobby;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Integration.Services.Lobby
{
    [TestClass]
    public sealed class LobbyRepositoryIntegrationTests
    {
        private const int MIN_PLAYERS = 2;
        private const int DEFAULT_MAX_PLAYERS = 8;

        private const byte LOBBY_STATUS_OPEN = 1;
        private const byte LOBBY_STATUS_CLOSED = 3;

        private const string SQL_COUNT_ACTIVE_MEMBERS = @"
            SELECT COUNT(1)
            FROM dbo.LobbyMembers
            WHERE lobby_id = @LobbyId AND is_active = 1;";

        private const string SQL_GET_OWNER_ID = @"
            SELECT owner_user_id
            FROM dbo.Lobbies
            WHERE lobby_id = @LobbyId;";

        private const string SQL_GET_STATUS = @"
            SELECT status
            FROM dbo.Lobbies
            WHERE lobby_id = @LobbyId;";

        private const string PARAM_LOBBY_ID = "@LobbyId";

        private const int INVALID_ID = 0;

        private string connectionString;
        private LobbyRepository lobbyRepository;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();
            DbTestCleaner.CleanupAll();

            connectionString = ResolveConnectionString(LobbyServiceConstants.MAIN_CONNECTION_STRING_NAME);
            lobbyRepository = new LobbyRepository(connectionString);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DbTestCleaner.CleanupAll();
        }

        [TestMethod]
        public void CreateLobby_WhenValid_CreatesLobbyAndOwnerMembershipActive()
        {
            int ownerUserId = CreateUser("Owner");

            CreateLobbyDbResult created = lobbyRepository.CreateLobby(ownerUserId, "Lobby", DEFAULT_MAX_PLAYERS);

            Assert.IsTrue(created.LobbyId > INVALID_ID);
            Assert.AreNotEqual(Guid.Empty, created.LobbyUid);
            Assert.IsFalse(string.IsNullOrWhiteSpace(created.AccessCode));

            int activeMembers = ExecuteScalarInt(
                SQL_COUNT_ACTIVE_MEMBERS,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = created.LobbyId });

            Assert.AreEqual(1, activeMembers);

            int ownerIdInDb = ExecuteScalarInt(
                SQL_GET_OWNER_ID,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = created.LobbyId });

            Assert.AreEqual(ownerUserId, ownerIdInDb);

            int status = ExecuteScalarInt(
                SQL_GET_STATUS,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = created.LobbyId });

            Assert.AreEqual(LOBBY_STATUS_OPEN, (byte)status);
        }

        [TestMethod]
        public void JoinByCode_WhenValid_LeavesPreviousLobbyAndJoinsTargetLobby()
        {
            int ownerAUserId = CreateUser("OwnerA");
            int ownerBUserId = CreateUser("OwnerB");
            int joinerUserId = CreateUser("Joiner");

            CreateLobbyDbResult lobbyA = lobbyRepository.CreateLobby(ownerAUserId, "A", DEFAULT_MAX_PLAYERS);
            CreateLobbyDbResult lobbyB = lobbyRepository.CreateLobby(ownerBUserId, "B", DEFAULT_MAX_PLAYERS);

            JoinLobbyDbResult joinedA = lobbyRepository.JoinByCode(joinerUserId, lobbyA.AccessCode);
            Assert.AreEqual(lobbyA.LobbyId, joinedA.LobbyId);

            JoinLobbyDbResult joinedB = lobbyRepository.JoinByCode(joinerUserId, lobbyB.AccessCode);
            Assert.AreEqual(lobbyB.LobbyId, joinedB.LobbyId);

            int activeInA = ExecuteScalarInt(
                SQL_COUNT_ACTIVE_MEMBERS,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = lobbyA.LobbyId });

            int activeInB = ExecuteScalarInt(
                SQL_COUNT_ACTIVE_MEMBERS,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = lobbyB.LobbyId });

            Assert.AreEqual(1, activeInA, "Joiner debió salir de A por usp_Lobby_LeaveAllByUser (queda solo OwnerA).");
            Assert.AreEqual(2, activeInB, "En B deben estar OwnerB + Joiner activos.");
        }


        [TestMethod]
        public void LeaveLobby_WhenOwnerLeavesWithOtherMembers_TransfersOwnership()
        {
            int ownerUserId = CreateUser("Owner");
            int joinerUserId = CreateUser("Joiner");

            CreateLobbyDbResult lobby = lobbyRepository.CreateLobby(ownerUserId, "X", DEFAULT_MAX_PLAYERS);
            _ = lobbyRepository.JoinByCode(joinerUserId, lobby.AccessCode);

            lobbyRepository.LeaveLobby(ownerUserId, lobby.LobbyId);

            int ownerIdInDb = ExecuteScalarInt(
                SQL_GET_OWNER_ID,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = lobby.LobbyId });

            Assert.AreEqual(joinerUserId, ownerIdInDb);

            int status = ExecuteScalarInt(
                SQL_GET_STATUS,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = lobby.LobbyId });

            Assert.AreEqual(LOBBY_STATUS_OPEN, (byte)status);
        }

        [TestMethod]
        public void LeaveLobby_WhenOwnerLeavesAndNoMembersLeft_ClosesLobby()
        {
            int ownerUserId = CreateUser("Owner");

            CreateLobbyDbResult lobby = lobbyRepository.CreateLobby(ownerUserId, "Solo", MIN_PLAYERS);

            lobbyRepository.LeaveLobby(ownerUserId, lobby.LobbyId);

            int status = ExecuteScalarInt(
                SQL_GET_STATUS,
                new SqlParameter(PARAM_LOBBY_ID, SqlDbType.Int) { Value = lobby.LobbyId });

            Assert.AreEqual(LOBBY_STATUS_CLOSED, (byte)status);
        }

        [TestMethod]
        public void JoinByCode_WhenLobbyIsClosed_ThrowsSqlException()
        {
            int ownerUserId = CreateUser("Owner");
            int joinerUserId = CreateUser("Joiner");

            CreateLobbyDbResult lobby = lobbyRepository.CreateLobby(ownerUserId, "Solo", MIN_PLAYERS);

            lobbyRepository.LeaveLobby(ownerUserId, lobby.LobbyId); 

            try
            {
                _ = lobbyRepository.JoinByCode(joinerUserId, lobby.AccessCode);
                Assert.Fail("Expected SqlException was not thrown.");
            }
            catch (SqlException ex)
            {
                StringAssert.Contains(ex.Message, "Lobby is not open.");
            }
        }

        [TestMethod]
        public void JoinByCode_WhenLobbyIsFull_ThrowsSqlException()
        {
            int ownerUserId = CreateUser("Owner");
            int joiner1UserId = CreateUser("Joiner1");
            int joiner2UserId = CreateUser("Joiner2");

            CreateLobbyDbResult lobby = lobbyRepository.CreateLobby(ownerUserId, "Full", MIN_PLAYERS);

            _ = lobbyRepository.JoinByCode(joiner1UserId, lobby.AccessCode);

            try
            {
                _ = lobbyRepository.JoinByCode(joiner2UserId, lobby.AccessCode);
                Assert.Fail("Expected SqlException was not thrown.");
            }
            catch (SqlException ex)
            {
                StringAssert.Contains(ex.Message, "Lobby is full.");
            }
        }


        private int CreateUser(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        private int ExecuteScalarInt(string sql, params SqlParameter[] parameters)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(sql, sqlConnection))
            {
                foreach (SqlParameter p in parameters)
                {
                    sqlCommand.Parameters.Add(p);
                }

                sqlConnection.Open();

                object result = sqlCommand.ExecuteScalar();
                Assert.IsNotNull(result);

                return Convert.ToInt32(result);
            }
        }

        private static string ResolveConnectionString(string name)
        {
            var setting = System.Configuration.ConfigurationManager.ConnectionStrings[name];

            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                Assert.Fail(string.Format("Missing connectionString '{0}' in test config.", name));
            }

            return setting.ConnectionString;
        }
    }
}
