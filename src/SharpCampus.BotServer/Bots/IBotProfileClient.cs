namespace SharpCampus.BotServer.Bots;

public interface IBotProfileClient
{
    // Best effort: a bot plays exactly as well under the nickname its account id was given. Returns
    // whether the name stuck, so a slot stops asking once it has.
    Task<bool> RenameAsync(string accessToken, string nickname);
}
