using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public static class WildcardSql
    {
        public static class Text
        {
            public const string GET_WILDCARD_TYPE_BY_CODE = @"
                SELECT wildcard_type_id,
                       code,
                       name,
                       description,
                       max_uses_per_match
                FROM dbo.WildcardTypes
                WHERE code = @Code;";

            public const string GET_WILDCARD_TYPES = @"
                SELECT wildcard_type_id,
                       code,
                       name,
                       description,
                       max_uses_per_match
                FROM dbo.WildcardTypes;";


            public const string COUNT_PLAYER_WILDCARDS_BY_TYPE = @"
                SELECT COUNT(1)
                FROM dbo.PlayerWildcards
                WHERE match_id = @MatchId
                  AND user_id = @UserId
                  AND wildcard_type_id = @WildcardTypeId;";

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

            public const string GET_PLAYER_WILDCARD_FOR_USE = @"
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
                WHERE   pw.player_wildcard_id = @PlayerWildcardId
                  AND   pw.match_id = @MatchId
                  AND   pw.user_id = @UserId
                  AND   pw.consumed_at IS NULL;";


            public const string INSERT_PLAYER_WILDCARD = @"
                INSERT INTO dbo.PlayerWildcards
                        (match_id, user_id, wildcard_type_id, granted_at)
                VALUES  (@MatchId, @UserId, @WildcardTypeId, SYSUTCDATETIME());";

            public const string CONSUME_PLAYER_WILDCARD = @"
                UPDATE dbo.PlayerWildcards
                SET consumed_at = SYSUTCDATETIME(),
                    consumed_in_round = @RoundNumber
                WHERE player_wildcard_id = @PlayerWildcardId
                  AND consumed_at IS NULL;";
        }
    }
}
