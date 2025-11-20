using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class LobbyInfo
    {
        [DataMember(Order = 1)] public Guid LobbyId { get; set; }
        [DataMember(Order = 2)] public string LobbyName { get; set; }
        [DataMember(Order = 3)] public int MaxPlayers { get; set; }

        [DataMember(Order = 4)]
        public List<AccountMini> Players { get; set; } = new List<AccountMini>();

        [DataMember(Order = 5)] public string AccessCode { get; set; }
    }

    [DataContract]
    public sealed class JoinLobbyRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string LobbyName { get; set; }
    }

    [DataContract]
    public sealed class JoinLobbyResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public LobbyInfo Lobby { get; set; }
    }

    [DataContract]
    public sealed class LeaveLobbyRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid LobbyId { get; set; }
    }

    [DataContract]
    public sealed class ListLobbiesRequest
    {
        [DataMember(Order = 1)] public int? Top { get; set; }
    }

    [DataContract]
    public sealed class ListLobbiesResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public List<LobbyInfo> Lobbies { get; set; } = new List<LobbyInfo>();

    }

    [DataContract]
    public sealed class SendLobbyMessageRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public Guid LobbyId { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public string Message { get; set; }
    }

    [DataContract]
    public sealed class ChatMessage
    {
        [DataMember(Order = 1)] public Guid FromPlayerId { get; set; }
        [DataMember(Order = 2)] public string FromPlayerName { get; set; }
        [DataMember(Order = 3)] public string Message { get; set; }
        [DataMember(Order = 4)] public DateTime SentAtUtc { get; set; }
    }

    [DataContract]
    public sealed class CreateLobbyRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2)] public string LobbyName { get; set; }
        [DataMember(Order = 3)] public byte? MaxPlayers { get; set; }
    }

    [DataContract]
    public sealed class CreateLobbyResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public LobbyInfo Lobby { get; set; }
    }

    [DataContract]
    public sealed class JoinByCodeRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string AccessCode { get; set; }
    }

    [DataContract]
    public sealed class JoinByCodeResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public LobbyInfo Lobby { get; set; }
    }

    [DataContract]
    public sealed class StartLobbyMatchRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string Token { get; set; }
    }

    [DataContract]
    public sealed class StartLobbyMatchResponse
    {
        [DataMember(Order = 1, IsRequired = true)]
        public MatchInfo Match { get; set; }
    }

}
