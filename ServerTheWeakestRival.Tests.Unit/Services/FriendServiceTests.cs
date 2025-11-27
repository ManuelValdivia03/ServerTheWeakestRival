using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;

namespace ServerTheWeakestRival.Tests.Unit.Services
{
    [TestClass]
    public sealed class FriendServiceTests
    {
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_AUTH_REQUIRED = "AUTH_REQUIRED";
        private const string ERROR_AUTH_INVALID = "AUTH_INVALID";

        private static readonly PrivateType FriendServicePrivateType = new PrivateType(typeof(FriendService));

        private FriendService service;

        [TestInitialize]
        public void SetUp()
        {
            service = new FriendService();
        }

        private static ServiceFault AssertIsServiceFault(Exception ex, string expectedCode)
        {
            Assert.IsNotNull(ex);

            Exception actual = ex;

            if (ex is TargetInvocationException tie && tie.InnerException != null)
            {
                actual = tie.InnerException;
            }

            var detailProperty = actual.GetType().GetProperty("Detail");
            Assert.IsNotNull(detailProperty, "Exception does not have Detail property.");

            var detail = detailProperty.GetValue(actual) as ServiceFault;
            Assert.IsNotNull(detail, "Detail is not a ServiceFault.");

            Assert.AreEqual(expectedCode, detail.Code);

            return detail;
        }

        #region SendFriendRequest

        [TestMethod]
        public void SendFriendRequest_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.SendFriendRequest(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void SendFriendRequest_NullToken_ThrowsAuthRequired()
        {
            var request = new SendFriendRequestRequest
            {
                Token = null,
                TargetAccountId = 2
            };

            try
            {
                service.SendFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void SendFriendRequest_EmptyToken_ThrowsAuthRequired()
        {
            var request = new SendFriendRequestRequest
            {
                Token = string.Empty,
                TargetAccountId = 2
            };

            try
            {
                service.SendFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void SendFriendRequest_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new SendFriendRequestRequest
            {
                Token = "   ",
                TargetAccountId = 2
            };

            try
            {
                service.SendFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void SendFriendRequest_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new SendFriendRequestRequest
            {
                Token = "invalid",
                TargetAccountId = 2
            };

            try
            {
                service.SendFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region AcceptFriendRequest

        [TestMethod]
        public void AcceptFriendRequest_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.AcceptFriendRequest(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void AcceptFriendRequest_NullToken_ThrowsAuthRequired()
        {
            var request = new AcceptFriendRequestRequest
            {
                Token = null,
                FriendRequestId = 1
            };

            try
            {
                service.AcceptFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void AcceptFriendRequest_EmptyToken_ThrowsAuthRequired()
        {
            var request = new AcceptFriendRequestRequest
            {
                Token = string.Empty,
                FriendRequestId = 1
            };

            try
            {
                service.AcceptFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void AcceptFriendRequest_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new AcceptFriendRequestRequest
            {
                Token = "   ",
                FriendRequestId = 1
            };

            try
            {
                service.AcceptFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void AcceptFriendRequest_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new AcceptFriendRequestRequest
            {
                Token = "invalid",
                FriendRequestId = 1
            };

            try
            {
                service.AcceptFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region RejectFriendRequest

        [TestMethod]
        public void RejectFriendRequest_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.RejectFriendRequest(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void RejectFriendRequest_NullToken_ThrowsAuthRequired()
        {
            var request = new RejectFriendRequestRequest
            {
                Token = null,
                FriendRequestId = 1
            };

            try
            {
                service.RejectFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void RejectFriendRequest_EmptyToken_ThrowsAuthRequired()
        {
            var request = new RejectFriendRequestRequest
            {
                Token = string.Empty,
                FriendRequestId = 1
            };

            try
            {
                service.RejectFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void RejectFriendRequest_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new RejectFriendRequestRequest
            {
                Token = "   ",
                FriendRequestId = 1
            };

            try
            {
                service.RejectFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void RejectFriendRequest_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new RejectFriendRequestRequest
            {
                Token = "invalid",
                FriendRequestId = 1
            };

            try
            {
                service.RejectFriendRequest(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region RemoveFriend

        [TestMethod]
        public void RemoveFriend_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.RemoveFriend(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void RemoveFriend_NullToken_ThrowsAuthRequired()
        {
            var request = new RemoveFriendRequest
            {
                Token = null,
                FriendAccountId = 2
            };

            try
            {
                service.RemoveFriend(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void RemoveFriend_EmptyToken_ThrowsAuthRequired()
        {
            var request = new RemoveFriendRequest
            {
                Token = string.Empty,
                FriendAccountId = 2
            };

            try
            {
                service.RemoveFriend(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void RemoveFriend_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new RemoveFriendRequest
            {
                Token = "   ",
                FriendAccountId = 2
            };

            try
            {
                service.RemoveFriend(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void RemoveFriend_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new RemoveFriendRequest
            {
                Token = "invalid",
                FriendAccountId = 2
            };

            try
            {
                service.RemoveFriend(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region ListFriends

        [TestMethod]
        public void ListFriends_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.ListFriends(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void ListFriends_NullToken_ThrowsAuthRequired()
        {
            var request = new ListFriendsRequest
            {
                Token = null
            };

            try
            {
                service.ListFriends(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void ListFriends_EmptyToken_ThrowsAuthRequired()
        {
            var request = new ListFriendsRequest
            {
                Token = string.Empty
            };

            try
            {
                service.ListFriends(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void ListFriends_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new ListFriendsRequest
            {
                Token = "   "
            };

            try
            {
                service.ListFriends(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void ListFriends_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new ListFriendsRequest
            {
                Token = "invalid"
            };

            try
            {
                service.ListFriends(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region PresenceHeartbeat

        [TestMethod]
        public void PresenceHeartbeat_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.PresenceHeartbeat(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void PresenceHeartbeat_NullToken_ThrowsAuthRequired()
        {
            var request = new HeartbeatRequest
            {
                Token = null,
                Device = "PC"
            };

            try
            {
                service.PresenceHeartbeat(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void PresenceHeartbeat_EmptyToken_ThrowsAuthRequired()
        {
            var request = new HeartbeatRequest
            {
                Token = string.Empty,
                Device = "PC"
            };

            try
            {
                service.PresenceHeartbeat(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void PresenceHeartbeat_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new HeartbeatRequest
            {
                Token = "   ",
                Device = "PC"
            };

            try
            {
                service.PresenceHeartbeat(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void PresenceHeartbeat_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new HeartbeatRequest
            {
                Token = "invalid",
                Device = "PC"
            };

            try
            {
                service.PresenceHeartbeat(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region GetFriendsPresence

        [TestMethod]
        public void GetFriendsPresence_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.GetFriendsPresence(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void GetFriendsPresence_NullToken_ThrowsAuthRequired()
        {
            var request = new GetFriendsPresenceRequest
            {
                Token = null
            };

            try
            {
                service.GetFriendsPresence(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void GetFriendsPresence_EmptyToken_ThrowsAuthRequired()
        {
            var request = new GetFriendsPresenceRequest
            {
                Token = string.Empty
            };

            try
            {
                service.GetFriendsPresence(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void GetFriendsPresence_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new GetFriendsPresenceRequest
            {
                Token = "   "
            };

            try
            {
                service.GetFriendsPresence(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void GetFriendsPresence_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new GetFriendsPresenceRequest
            {
                Token = "invalid"
            };

            try
            {
                service.GetFriendsPresence(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region SearchAccounts

        [TestMethod]
        public void SearchAccounts_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.SearchAccounts(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void SearchAccounts_NullToken_ThrowsAuthRequired()
        {
            var request = new SearchAccountsRequest
            {
                Token = null,
                Query = "test"
            };

            try
            {
                service.SearchAccounts(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void SearchAccounts_EmptyToken_ThrowsAuthRequired()
        {
            var request = new SearchAccountsRequest
            {
                Token = string.Empty,
                Query = "test"
            };

            try
            {
                service.SearchAccounts(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void SearchAccounts_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new SearchAccountsRequest
            {
                Token = "   ",
                Query = "test"
            };

            try
            {
                service.SearchAccounts(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void SearchAccounts_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new SearchAccountsRequest
            {
                Token = "invalid",
                Query = "test"
            };

            try
            {
                service.SearchAccounts(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region GetAccountsByIds

        [TestMethod]
        public void GetAccountsByIds_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.GetAccountsByIds(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request is null.", detail.Message);
            }
        }

        [TestMethod]
        public void GetAccountsByIds_NullToken_ThrowsAuthRequired()
        {
            var request = new GetAccountsByIdsRequest
            {
                Token = null,
                AccountIds = new[] { 1, 2, 3 }
            };

            try
            {
                service.GetAccountsByIds(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void GetAccountsByIds_EmptyToken_ThrowsAuthRequired()
        {
            var request = new GetAccountsByIdsRequest
            {
                Token = string.Empty,
                AccountIds = new[] { 1, 2, 3 }
            };

            try
            {
                service.GetAccountsByIds(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void GetAccountsByIds_WhitespaceToken_ThrowsAuthRequired()
        {
            var request = new GetAccountsByIdsRequest
            {
                Token = "   ",
                AccountIds = new[] { 1, 2, 3 }
            };

            try
            {
                service.GetAccountsByIds(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void GetAccountsByIds_InvalidToken_ThrowsAuthInvalid()
        {
            var request = new GetAccountsByIdsRequest
            {
                Token = "invalid",
                AccountIds = new[] { 1, 2, 3 }
            };

            try
            {
                service.GetAccountsByIds(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        #endregion

        #region Helpers

        [TestMethod]
        public void Authenticate_NullToken_ThrowsAuthRequired()
        {
            try
            {
                FriendServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { null });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void Authenticate_EmptyToken_ThrowsAuthRequired()
        {
            try
            {
                FriendServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { string.Empty });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void Authenticate_WhitespaceToken_ThrowsAuthRequired()
        {
            try
            {
                FriendServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { "   " });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_REQUIRED);
                Assert.AreEqual("Missing token.", detail.Message);
            }
        }

        [TestMethod]
        public void Authenticate_InvalidToken_ThrowsAuthInvalid()
        {
            try
            {
                FriendServicePrivateType.InvokeStatic(
                    "Authenticate",
                    new object[] { "invalid" });

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_AUTH_INVALID);
                Assert.AreEqual("Invalid token.", detail.Message);
            }
        }

        [TestMethod]
        public void MapAvatar_NullEntity_ReturnsDefault()
        {
            var result = (AvatarAppearanceDto)FriendServicePrivateType.InvokeStatic(
                "MapAvatar",
                new object[] { null });

            Assert.AreEqual(AvatarBodyColor.Blue, result.BodyColor);
            Assert.AreEqual(AvatarPantsColor.Black, result.PantsColor);
            Assert.AreEqual(AvatarHatType.None, result.HatType);
            Assert.AreEqual(AvatarHatColor.Default, result.HatColor);
            Assert.AreEqual(AvatarFaceType.Default, result.FaceType);
            Assert.IsFalse(result.UseProfilePhotoAsFace);
        }

        [TestMethod]
        public void MapAvatar_ValidEntity_MapsValues()
        {
            var entity = new UserAvatarEntity
            {
                UserId = 1,
                BodyColor = (byte)AvatarBodyColor.Green,
                PantsColor = (byte)AvatarPantsColor.BlueJeans,
                HatType = (byte)AvatarHatType.Cap,
                HatColor = (byte)AvatarHatColor.Black,
                FaceType = (byte)AvatarFaceType.Happy,
                UseProfilePhoto = true
            };

            var result = (AvatarAppearanceDto)FriendServicePrivateType.InvokeStatic(
                "MapAvatar",
                new object[] { entity });

            Assert.AreEqual(AvatarBodyColor.Green, result.BodyColor);
            Assert.AreEqual(AvatarPantsColor.BlueJeans, result.PantsColor);
            Assert.AreEqual(AvatarHatType.Cap, result.HatType);
            Assert.AreEqual(AvatarHatColor.Black, result.HatColor);
            Assert.AreEqual(AvatarFaceType.Happy, result.FaceType);
            Assert.IsTrue(result.UseProfilePhotoAsFace);
        }

        #endregion
    }
}
