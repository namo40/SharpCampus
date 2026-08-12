-- SharpCampus game schema.
-- This script is the whole schema story: there is no migration tooling, so rerunning it drops and
-- recreates the tables. That is the reset procedure, and it discards every row.

DROP TABLE IF EXISTS profiles CASCADE;

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
