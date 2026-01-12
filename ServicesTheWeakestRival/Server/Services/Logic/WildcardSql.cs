namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public static class WildcardSql
    {
        public static class Text
        {
            public const string GET_WILDCARD_TYPES = @"
                SELECT wildcard_type_id,
                       code,
                       name,
                       description,
                       max_uses_per_match
                FROM dbo.WildcardTypes;";

            public const string GET_AVAILABLE_PLAYER_WILDCARDS = @"
                SELECT  pw.player_wildcard_id,
                        pw.match_id,
                        pw.user_id,
                        pw.wildcard_type_id,
                        wt.code,
                        wt.name,
                        wt.description,
                        wt.max_uses_per_match,
                        pw.granted_at,
                        pw.consumed_at,
                        pw.consumed_in_round
                FROM    dbo.PlayerWildcards pw
                        INNER JOIN dbo.WildcardTypes wt
                            ON pw.wildcard_type_id = wt.wildcard_type_id
                WHERE   pw.match_id = @MatchId
                  AND   pw.user_id = @UserId
                  AND   pw.consumed_at IS NULL;";

            public const string GET_ROUND_ID_BY_MATCH_AND_NUMBER = @"
                SELECT TOP (1) r.round_id
                FROM dbo.Rounds r
                WHERE r.match_id = @MatchId
                  AND r.round_number = @RoundNumber;";

            public const string CONSUME_AND_GET_PLAYER_WILDCARD_FOR_USE = @"
                UPDATE pw
                SET consumed_at = SYSUTCDATETIME(),
                    consumed_in_round = @RoundId
                OUTPUT  inserted.player_wildcard_id,
                        inserted.match_id,
                        inserted.user_id,
                        inserted.wildcard_type_id,
                        wt.code,
                        wt.name,
                        wt.description,
                        wt.max_uses_per_match,
                        inserted.granted_at,
                        inserted.consumed_at,
                        inserted.consumed_in_round
                FROM dbo.PlayerWildcards pw WITH (UPDLOCK, ROWLOCK)
                INNER JOIN dbo.WildcardTypes wt
                    ON pw.wildcard_type_id = wt.wildcard_type_id
                WHERE pw.player_wildcard_id = @PlayerWildcardId
                  AND pw.match_id = @MatchId
                  AND pw.user_id = @UserId
                  AND pw.consumed_at IS NULL;";

            public const string CONSUME_AND_GET_PLAYER_WILDCARD_FOR_USE_WITHOUT_ROUND = @"
                UPDATE pw
                SET consumed_at = SYSUTCDATETIME(),
                    consumed_in_round = NULL
                OUTPUT  inserted.player_wildcard_id,
                        inserted.match_id,
                        inserted.user_id,
                        inserted.wildcard_type_id,
                        wt.code,
                        wt.name,
                        wt.description,
                        wt.max_uses_per_match,
                        inserted.granted_at,
                        inserted.consumed_at,
                        inserted.consumed_in_round
                FROM dbo.PlayerWildcards pw WITH (UPDLOCK, ROWLOCK)
                INNER JOIN dbo.WildcardTypes wt
                    ON pw.wildcard_type_id = wt.wildcard_type_id
                WHERE pw.player_wildcard_id = @PlayerWildcardId
                  AND pw.match_id = @MatchId
                  AND pw.user_id = @UserId
                  AND pw.consumed_at IS NULL;";

            public const string UNCONSUME_PLAYER_WILDCARD = @"
                UPDATE dbo.PlayerWildcards
                SET consumed_at = NULL,
                    consumed_in_round = NULL
                WHERE player_wildcard_id = @PlayerWildcardId
                  AND match_id = @MatchId
                  AND user_id = @UserId
                  AND consumed_in_round = @RoundId;";

            public const string UNCONSUME_PLAYER_WILDCARD_WITHOUT_ROUND = @"
                UPDATE dbo.PlayerWildcards
                SET consumed_at = NULL,
                    consumed_in_round = NULL
                WHERE player_wildcard_id = @PlayerWildcardId
                  AND match_id = @MatchId
                  AND user_id = @UserId
                  AND consumed_in_round IS NULL;";

            public const string INSERT_PLAYER_WILDCARD = @"
                INSERT INTO dbo.PlayerWildcards
                        (match_id, user_id, wildcard_type_id, granted_at)
                VALUES  (@MatchId, @UserId, @WildcardTypeId, SYSUTCDATETIME());";

            public const string HAS_ANY_PLAYER_WILDCARDS = @"
                SELECT TOP (1) 1
                FROM dbo.PlayerWildcards pw
                WHERE pw.match_id = @MatchId
                  AND pw.user_id = @UserId;";
        }
    }
}
