using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using System;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Integration.Services.Friends
{
    [TestClass]
    public sealed class SendLobbyInviteEmailIntegrationTests
    {
        private const string DisplayNameMe = "Me";
        private const string DisplayNameTarget = "Target";

        private const string LobbyCodeValid = "ABC123";
        private const string LobbyCodeInvalid = "   ";

        private const int TokenLifetimeMinutes = 10;
        private const string TokenPrefix = "test-token-";

        private const string ExpectedInvalidTargetCode = "Invitación: jugador inválido";
        private const string ExpectedInvalidCode = "Invitación: código de lobby inválido";
        private const string ExpectedNotFriendCode = "Invitación: no es tu amigo";
        private const string ExpectedAccountNotFoundCode = "INVITE_ACCOUNT_NOT_FOUND";

        private const string SqlInsertAcceptedFriendRequest = @"
INSERT INTO dbo.FriendRequests (from_user_id, to_user_id, status, sent_at, responded_at)
VALUES (@Me, @Target, @Accepted, SYSUTCDATETIME(), SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS int);";

        private const string ParamMe = "@Me";
        private const string ParamTarget = "@Target";
        private const string ParamAccepted = "@Accepted";

        private const int FriendRequestAccepted = 1;

        private string connectionString;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();

            DbTestCleaner.CleanupAll();
            TokenStoreCleaner.Clean();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DbTestCleaner.CleanupAll();
            TokenStoreCleaner.Clean();
        }

        [TestMethod]
        public void SendLobbyInviteEmail_WhenTargetIsInvalid_ThrowsInvalidTargetFault()
        {
            int meId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meId);

            var service = new FriendService();

            FaultAssert.ThrowsServiceFault(
                () => service.SendLobbyInviteEmail(new SendLobbyInviteEmailRequest
                {
                    Token = meToken,
                    TargetAccountId = 0,
                    LobbyCode = LobbyCodeValid
                }),
                ExpectedInvalidTargetCode);
        }

        [TestMethod]
        public void SendLobbyInviteEmail_WhenTargetIsMe_ThrowsInvalidTargetFault()
        {
            int meId = CreateAccount(DisplayNameMe);
            string meToken = StoreTokenForUser(meId);

            var service = new FriendService();

            FaultAssert.ThrowsServiceFault(
                () => service.SendLobbyInviteEmail(new SendLobbyInviteEmailRequest
                {
                    Token = meToken,
                    TargetAccountId = meId,
                    LobbyCode = LobbyCodeValid
                }),
                ExpectedInvalidTargetCode);
        }

        [TestMethod]
        public void SendLobbyInviteEmail_WhenLobbyCodeIsInvalid_ThrowsInvalidCodeFault()
        {
            int meId = CreateAccount(DisplayNameMe);
            int targetId = CreateAccount(DisplayNameTarget);

            string meToken = StoreTokenForUser(meId);

            var service = new FriendService();

            FaultAssert.ThrowsServiceFault(
                () => service.SendLobbyInviteEmail(new SendLobbyInviteEmailRequest
                {
                    Token = meToken,
                    TargetAccountId = targetId,
                    LobbyCode = LobbyCodeInvalid
                }),
                ExpectedInvalidCode);
        }

        [TestMethod]
        public void SendLobbyInviteEmail_WhenNotFriends_ThrowsNotFriendFault()
        {
            int meId = CreateAccount(DisplayNameMe);
            int targetId = CreateAccount(DisplayNameTarget);

            string meToken = StoreTokenForUser(meId);

            var service = new FriendService();

            FaultAssert.ThrowsServiceFault(
                () => service.SendLobbyInviteEmail(new SendLobbyInviteEmailRequest
                {
                    Token = meToken,
                    TargetAccountId = targetId,
                    LobbyCode = LobbyCodeValid
                }),
                ExpectedNotFriendCode);
        }

        private int CreateAccount(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        

        private static string StoreTokenForUser(int userId)
        {
            string tokenValue = TokenPrefix + Guid.NewGuid().ToString("N");

            TokenStore.StoreToken(new AuthToken
            {
                Token = tokenValue,
                UserId = userId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes)
            });

            return tokenValue;
        }

        private static class TokenStoreCleaner
        {
            internal static void Clean()
            {
                TokenStore.Cache.Clear();
                TokenStore.ActiveTokenByUserId.Clear();
            }
        }

        internal static class FaultAssert
        {
            internal static void ThrowsServiceFault(Action action, string expectedCode)
            {
                if (action == null)
                {
                    Assert.Fail("action must be provided.");
                }

                try
                {
                    action();
                    Assert.Fail("Expected FaultException<ServiceFault> but no exception was thrown.");
                }
                catch (FaultException<ServiceFault> ex)
                {
                    Assert.IsNotNull(ex.Detail);
                    Assert.AreEqual(expectedCode, ex.Detail.Code);
                }
            }
        }
    }
}
