namespace SharpCampus.BotServer.Configuration;

public sealed class BotServerOptions
{
    public const string SectionName = "BotServer";

    // A bot queues and polls exactly where a console client does. It is a client of the meta server,
    // never a peer of it.
    public string ApiServerAddress { get; set; } = "http://localhost:5001";

    public string SupabaseAuthUrl { get; set; } = "http://127.0.0.1:54321";

    // Printed by `supabase start`. Every local stack ships the same demo publishable key.
    public string SupabasePublishableKey { get; set; } = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";

    // How many bots can be playing at once, and so how many accounts the server signs in as.
    public int AccountPoolSize { get; set; } = 4;

    public string EmailPattern { get; set; } = "bot{0}@sharpcampus.dev";

    // Profile nicknames are unique across accounts, so any second process playing as its own pool has
    // to name its accounts something else.
    public string NicknamePattern { get; set; } = "bot{0}";

    public string Password { get; set; } = "sharpcampus-bot";

    // A ticket names the entry point as outside clients reach it, which is not an address that exists
    // inside the cluster. Set, this replaces the ticket's endpoint for the duel connection; the room-id
    // header still picks the room behind it. Empty means the ticket is followed as written.
    public string RoomEndpointOverride { get; set; } = "";

    public string RoomEndpoint(string ticketEndpoint)
        => RoomEndpointOverride.Length > 0 ? RoomEndpointOverride : ticketEndpoint;

    // Must stay below the ApiServer's SummonAfterSeconds: a bot nobody turned up for has to leave the
    // queue before it becomes the lone waiting account and draws a bot of its own. The two settings
    // live in different processes, so nothing at startup can check this for us.
    public int QueueTimeoutSeconds { get; set; } = 60;

    // One input per this many milliseconds. A room takes a limited number of inputs per tick, so a
    // whole plan sent at once would have its tail discarded.
    public int InputCadenceMilliseconds { get; set; } = 40;
}
