using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    
        public static class MatchSql
        {
            public static class Text
            {
                public const string INSERT_MATCH = @"
                INSERT INTO dbo.Matches
                        (host_user_id,
                         state,
                         is_private,
                         access_code,
                         max_players,
                         created_at)
                OUTPUT INSERTED.match_id
                VALUES (@HostUserId,
                        @State,
                        @IsPrivate,
                        @AccessCode,
                        @MaxPlayers,
                        SYSUTCDATETIME());";

                public const string INSERT_MATCH_RULES = @"
                INSERT INTO dbo.MatchRules
                        (match_id,
                         starting_score,
                         max_score,
                         points_per_correct,
                         points_per_wrong,
                         points_per_elimination_gain,
                         allow_tiebreak_coinflip)
                VALUES (@MatchId,
                        @StartingScore,
                        @MaxScore,
                        @PointsPerCorrect,
                        @PointsPerWrong,
                        @PointsPerEliminationGain,
                        @AllowTiebreakCoinflip);";

                public const string INSERT_MATCH_PLAYER = @"
                INSERT INTO dbo.MatchPlayers
                        (match_id,
                         user_id,
                         joined_at,
                         is_eliminated)
                VALUES (@MatchId,
                        @UserId,
                        SYSUTCDATETIME(),
                        0);";

                public const string GET_MATCH_ID_BY_ACCESS_CODE = @"
                    SELECT TOP (1) m.match_id
                    FROM dbo.Matches m
                    WHERE m.access_code = @AccessCode;";

                public const string GET_MATCH_MAX_PLAYERS = @"
                    SELECT TOP (1) m.max_players
                    FROM dbo.Matches m
                    WHERE m.match_id = @MatchId;";

                public const string COUNT_MATCH_PLAYERS = @"
                    SELECT COUNT(1)
                    FROM dbo.MatchPlayers mp
                    WHERE mp.match_id = @MatchId;";

                public const string INSERT_MATCH_PLAYER_IF_NOT_EXISTS = @"
                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.MatchPlayers mp
                        WHERE mp.match_id = @MatchId
                          AND mp.user_id = @UserId
                    )
                    BEGIN
                        INSERT INTO dbo.MatchPlayers
                                (match_id,
                                 user_id,
                                 joined_at,
                                 is_eliminated)
                        VALUES  (@MatchId,
                                 @UserId,
                                 SYSUTCDATETIME(),
                                 0);
                    END";

            }
        }
    }
