-- SharpCampus game schema.
-- This script is the whole schema story: there is no migration tooling, so rerunning it drops and
-- recreates the tables. That is the reset procedure, and it discards every row.

DROP TABLE IF EXISTS profiles CASCADE;
DROP TABLE IF EXISTS match_records CASCADE;

-- No foreign key to auth.users: user_id carries the `sub` claim of the verified access token, which keeps
-- the account provider (Supabase today) replaceable without touching the game schema.
CREATE TABLE profiles (
    user_id           uuid        PRIMARY KEY,
    nickname          text        NOT NULL,
    coins             integer     NOT NULL DEFAULT 0,
    rating            integer     NOT NULL DEFAULT 1000,
    equipped_skin_id  text        NOT NULL DEFAULT 'CLASSIC',
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now()
);

-- Nicknames are claimed case-insensitively, so "Player" and "player" are the same name.
CREATE UNIQUE INDEX profiles_nickname_key ON profiles (lower(nickname));

-- One row per player per game, so a rematch adds two more. Outcome and reason are text rather than
-- codes so a row explains itself when it is read straight out of psql, and the ratings on both sides
-- of the match let the history be read without replaying every result before it.
CREATE TABLE match_records (
    match_id       text        NOT NULL,
    user_id        uuid        NOT NULL,
    outcome        text        NOT NULL,
    end_reason     text        NOT NULL,
    rating_before  integer     NOT NULL,
    rating_after   integer     NOT NULL,
    coins_awarded  integer     NOT NULL,
    lines_cleared  integer     NOT NULL,
    quads          integer     NOT NULL,
    garbage_sent   integer     NOT NULL,
    hard_drops     integer     NOT NULL,
    max_combo      integer     NOT NULL,
    duration_ticks integer     NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (match_id, user_id)
);

-- Every read of this table so far is one account's history, newest first.
CREATE INDEX match_records_user_history ON match_records (user_id, created_at DESC);
