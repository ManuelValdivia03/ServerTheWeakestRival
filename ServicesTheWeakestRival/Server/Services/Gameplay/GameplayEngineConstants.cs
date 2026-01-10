using System;

namespace ServicesTheWeakestRival.Server.Services
{
    internal static class GameplayEngineConstants
    {
        internal const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        internal const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        internal const string ERROR_DB = "DB_ERROR";
        internal const string ERROR_UNEXPECTED = "UNEXPECTED_ERROR";
        internal const string ERROR_MATCH_NOT_FOUND = "MATCH_NOT_FOUND";
        internal const string ERROR_NOT_PLAYER_TURN = "NOT_PLAYER_TURN";
        internal const string ERROR_DUEL_NOT_ACTIVE = "DUEL_NOT_ACTIVE";
        internal const string ERROR_NOT_WEAKEST_RIVAL = "NOT_WEAKEST_RIVAL";
        internal const string ERROR_INVALID_DUEL_TARGET = "INVALID_DUEL_TARGET";
        internal const string ERROR_MATCH_ALREADY_STARTED = "MATCH_ALREADY_STARTED";
        internal const string ERROR_NO_QUESTIONS = "NO_QUESTIONS";

        internal const string MESSAGE_DB_ERROR =
            "Ocurrió un error de base de datos. Intenta de nuevo más tarde.";

        internal const string MESSAGE_UNEXPECTED_ERROR =
            "Ocurrió un error inesperado. Intenta de nuevo más tarde.";

        internal const string FALLBACK_LOCALE_EN_US = "en-US";

        internal const string ERROR_MATCH_ALREADY_STARTED_MESSAGE =
            "Match already started. Joining is not allowed.";

        internal const string ERROR_MATCH_NOT_FOUND_MESSAGE = "Match not found.";
        internal const string ERROR_NOT_PLAYER_TURN_MESSAGE = "It is not the player turn.";
        internal const string ERROR_DUEL_NOT_ACTIVE_MESSAGE = "Duel is not active.";
        internal const string ERROR_NOT_WEAKEST_RIVAL_MESSAGE = "Only weakest rival can choose duel opponent.";
        internal const string ERROR_INVALID_DUEL_TARGET_MESSAGE = "Invalid duel opponent.";
        internal const string ERROR_NO_QUESTIONS_MESSAGE =
            "No se encontraron preguntas para la dificultad/idioma solicitados.";

        internal const int DEFAULT_MAX_QUESTIONS = 40;
        internal const int QUESTIONS_PER_PLAYER_PER_ROUND = 2;
        internal const int VOTE_PHASE_TIME_LIMIT_SECONDS = 30;
        internal const int MIN_PLAYERS_TO_CONTINUE = 2;

        internal const int COIN_FLIP_RANDOM_MIN_VALUE = 0;
        internal const int COIN_FLIP_RANDOM_MAX_VALUE = 100;
        internal const int COIN_FLIP_THRESHOLD_VALUE = 50;

        internal const int LIGHTNING_PROBABILITY_PERCENT = 20;
        internal const int LIGHTNING_TOTAL_QUESTIONS = 3;
        internal const int LIGHTNING_TOTAL_TIME_SECONDS = 30;
        internal const int LIGHTNING_RANDOM_MIN_VALUE = 0;
        internal const int LIGHTNING_RANDOM_MAX_VALUE = 100;

        internal const int EXTRA_WILDCARD_RANDOM_MIN_VALUE = 0;
        internal const int EXTRA_WILDCARD_RANDOM_MAX_VALUE = 100;
        internal const int EXTRA_WILDCARD_PROBABILITY_PERCENT = 20;

        internal const int BOMB_QUESTION_RANDOM_MIN_VALUE = 0;
        internal const int BOMB_QUESTION_RANDOM_MAX_VALUE = 100;
        internal const int BOMB_QUESTION_PROBABILITY_PERCENT = 20;

        internal const int SURPRISE_EXAM_RANDOM_MIN_VALUE = 0;
        internal const int SURPRISE_EXAM_RANDOM_MAX_VALUE = 100;
        internal const int SURPRISE_EXAM_PROBABILITY_PERCENT = 100;

        internal const int SURPRISE_EXAM_TIME_LIMIT_SECONDS = 20;

        internal const decimal SURPRISE_EXAM_SUCCESS_BONUS = 2.00m;
        internal const decimal SURPRISE_EXAM_FAILURE_PENALTY = 3.00m;

        internal const string SURPRISE_EXAM_RESOLVE_REASON_TIMEOUT = "TIMEOUT";
        internal const string SURPRISE_EXAM_RESOLVE_REASON_ALL_ANSWERED = "ALL_ANSWERED";

        internal const string SURPRISE_EXAM_BANKING_NOT_ALLOWED_MESSAGE =
            "Special event in progress. Banking is not allowed.";

        internal const decimal BOMB_BANK_DELTA = 0.50m;
        internal const decimal MIN_BANKED_POINTS = 0.00m;
        internal const decimal INITIAL_BANKED_POINTS = 5.00m;

        internal const int TURN_USER_ID_NONE = 0;

        internal const string SPECIAL_EVENT_LIGHTNING_WILDCARD_CODE = "LIGHTNING_WILDCARD_AWARDED";
        internal const string SPECIAL_EVENT_LIGHTNING_WILDCARD_DESCRIPTION_TEMPLATE =
            "El jugador {0} ha ganado un comodín relámpago.";

        internal const string SPECIAL_EVENT_EXTRA_WILDCARD_CODE = "EXTRA_WILDCARD_AWARDED";
        internal const string SPECIAL_EVENT_EXTRA_WILDCARD_DESCRIPTION_TEMPLATE =
            "El jugador {0} ha recibido un comodín extra.";

        internal const string SPECIAL_EVENT_BOMB_QUESTION_CODE = "BOMB_QUESTION";
        internal const string SPECIAL_EVENT_BOMB_QUESTION_DESCRIPTION_TEMPLATE =
            "Pregunta bomba para {0}. Acierto: +{1} a la banca. Fallo: -{1} de lo bancado.";

        internal const string SPECIAL_EVENT_BOMB_QUESTION_APPLIED_CODE = "BOMB_QUESTION_APPLIED";
        internal const string SPECIAL_EVENT_BOMB_QUESTION_APPLIED_DESCRIPTION_TEMPLATE =
            "Pregunta bomba resuelta por {0}. Cambio en banca: {1}. Banca actual: {2}.";

        internal const string SPECIAL_EVENT_SURPRISE_EXAM_STARTED_CODE = "SURPRISE_EXAM_STARTED";
        internal const string SPECIAL_EVENT_SURPRISE_EXAM_STARTED_DESCRIPTION =
            "¡Examen sorpresa! Cada jugador debe responder su pregunta.";

        internal const string SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_CODE = "SURPRISE_EXAM_RESOLVED";
        internal const string SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_ALL_CORRECT = "Todos acertaron";
        internal const string SPECIAL_EVENT_SURPRISE_EXAM_OUTCOME_SOME_FAILED = "Al menos uno falló";

        internal const string SPECIAL_EVENT_SURPRISE_EXAM_RESOLVED_DESCRIPTION_TEMPLATE =
            "Examen sorpresa resuelto. {0} ({1}/{2}). Cambio en banca: {3}. Banca actual: {4}.";

        internal const int DARK_MODE_RANDOM_MIN_VALUE = 0;
        internal const int DARK_MODE_RANDOM_MAX_VALUE = 100;
        internal const int DARK_MODE_PROBABILITY_PERCENT = 20;

        internal const string SPECIAL_EVENT_DARK_MODE_STARTED_CODE = "DARK_MODE_STARTED";
        internal const string SPECIAL_EVENT_DARK_MODE_STARTED_DESCRIPTION =
            "¡A oscuras! Los lugares se han revuelto y la identidad de los jugadores está oculta.";

        internal const string SPECIAL_EVENT_DARK_MODE_ENDED_CODE = "DARK_MODE_ENDED";
        internal const string SPECIAL_EVENT_DARK_MODE_ENDED_DESCRIPTION =
            "Las luces vuelven. Ahora puedes ver por quién votaste.";

        internal const string SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_CODE = "DARK_MODE_VOTE_REVEAL";
        internal const string SPECIAL_EVENT_DARK_MODE_VOTE_REVEAL_DESCRIPTION_TEMPLATE =
            "Votaste por {0}.";

        internal const string DARK_MODE_NO_VOTE_DISPLAY_NAME = "Nadie";
        internal const string DARK_MODE_FALLBACK_PLAYER_NAME_TEMPLATE = "Jugador {0}";

        internal const string WILDCARD_CHANGE_Q = "CHANGE_Q";
        internal const string WILDCARD_PASS_Q = "PASS_Q";
        internal const string WILDCARD_SHIELD = "SHIELD";
        internal const string WILDCARD_FORCED_BANK = "FORCED_BANK";
        internal const string WILDCARD_DOUBLE = "DOUBLE";
        internal const string WILDCARD_BLOCK = "BLOCK";
        internal const string WILDCARD_SABOTAGE = "SABOTAGE";
        internal const string WILDCARD_EXTRA_TIME = "EXTRA_TIME";

        internal const string TURN_REASON_TIME_DELTA_PREFIX = "TIME_DELTA:";
        internal const string TURN_REASON_WILDCARD_PASS_Q = "WILDCARD_PASS_Q";

        internal const int WILDCARD_TIME_BONUS_SECONDS = 5;
        internal const int WILDCARD_TIME_PENALTY_SECONDS = 5;

        internal const string SPECIAL_EVENT_WILDCARD_USED_CODE_TEMPLATE = "WILDCARD_USED_{0}";
        internal const string SPECIAL_EVENT_WILDCARD_USED_DESCRIPTION_TEMPLATE = "{0} usó el comodín {1}.";

        internal const string SPECIAL_EVENT_SHIELD_TRIGGERED_CODE = "WILDCARD_SHIELD_TRIGGERED";
        internal const string SPECIAL_EVENT_SHIELD_TRIGGERED_DESCRIPTION_TEMPLATE =
            "El escudo de {0} evitó su eliminación.";

        internal const string SPECIAL_EVENT_TIME_BONUS_CODE = "WILDCARD_EXTRA_TIME";
        internal const string SPECIAL_EVENT_TIME_BONUS_DESCRIPTION_TEMPLATE =
            "{0} obtuvo +{1} segundos.";

        internal const string SPECIAL_EVENT_TIME_PENALTY_CODE = "WILDCARD_SABOTAGE";
        internal const string SPECIAL_EVENT_TIME_PENALTY_DESCRIPTION_TEMPLATE =
            "{0} tendrá -{1} segundos.";

        internal const string ERROR_WILDCARD_INVALID_TIMING = "WILDCARD_INVALID_TIMING";
        internal const string ERROR_WILDCARD_INVALID_TIMING_MESSAGE =
            "No puedes usar comodines en este momento.";

        internal const string ERROR_WILDCARDS_BLOCKED = "WILDCARDS_BLOCKED";
        internal const string ERROR_WILDCARDS_BLOCKED_MESSAGE =
            "Tus comodines están bloqueados por esta ronda.";

        internal const string ERROR_INVALID_ROUND = "INVALID_ROUND";
        internal const string ERROR_INVALID_ROUND_MESSAGE =
            "La ronda del cliente no coincide con la del servidor.";

        internal const int LOCALE_CODE_MAX_LENGTH = 10;

        internal static readonly decimal[] CHAIN_STEPS =
        {
            0.10m,
            0.20m,
            0.30m,
            0.40m,
            0.50m
        };
    }
}
