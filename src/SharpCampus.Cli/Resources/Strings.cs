using System.Resources;

namespace SharpCampus.Cli.Resources;

// Hand written rather than generated: MSBuild puts the strongly typed output in the intermediate
// folder, where the IDE's project model does not pick it up. Member names are the resource keys.
internal static class Strings
{
    private static readonly ResourceManager _resources =
        new("SharpCampus.Cli.Resources.Strings", typeof(Strings).Assembly);

    public static string BoardHeaderRival => Get(nameof(BoardHeaderRival));
    public static string BoardHeaderYou => Get(nameof(BoardHeaderYou));
    public static string HudCombo => Get(nameof(HudCombo));
    public static string HudGarbage => Get(nameof(HudGarbage));
    public static string HudHold => Get(nameof(HudHold));
    public static string HudLevel => Get(nameof(HudLevel));
    public static string HudNext => Get(nameof(HudNext));
    public static string HudPing => Get(nameof(HudPing));
    public static string KeyMap => Get(nameof(KeyMap));
    public static string LoggedIn => Get(nameof(LoggedIn));
    public static string LoggedOut => Get(nameof(LoggedOut));
    public static string LoginFailed => Get(nameof(LoginFailed));
    public static string MatchResumed => Get(nameof(MatchResumed));
    public static string Matched => Get(nameof(Matched));
    public static string NicknameRejected => Get(nameof(NicknameRejected));
    public static string NicknameRule => Get(nameof(NicknameRule));
    public static string NicknameTaken => Get(nameof(NicknameTaken));
    public static string NicknameUpdated => Get(nameof(NicknameUpdated));
    public static string NotLoggedIn => Get(nameof(NotLoggedIn));
    public static string NotLoggedInShort => Get(nameof(NotLoggedInShort));
    public static string OpponentDisconnected => Get(nameof(OpponentDisconnected));
    public static string OpponentNeverArrived => Get(nameof(OpponentNeverArrived));
    public static string ProfileSummary => Get(nameof(ProfileSummary));
    public static string QueueLeft => Get(nameof(QueueLeft));
    public static string QueueReleased => Get(nameof(QueueReleased));
    public static string Queued => Get(nameof(Queued));
    public static string ReasonAborted => Get(nameof(ReasonAborted));
    public static string ReasonDisconnect => Get(nameof(ReasonDisconnect));
    public static string ReasonForfeit => Get(nameof(ReasonForfeit));
    public static string ReasonTopOut => Get(nameof(ReasonTopOut));
    public static string RematchDeclined => Get(nameof(RematchDeclined));
    public static string RematchNoAnswer => Get(nameof(RematchNoAnswer));
    public static string RematchPrompt => Get(nameof(RematchPrompt));
    public static string RematchWaiting => Get(nameof(RematchWaiting));
    public static string ReplBanner => Get(nameof(ReplBanner));
    public static string ReplicaStats => Get(nameof(ReplicaStats));
    public static string ResultAbandoned => Get(nameof(ResultAbandoned));
    public static string ResultDraw => Get(nameof(ResultDraw));
    public static string ResultLose => Get(nameof(ResultLose));
    public static string ResultWin => Get(nameof(ResultWin));
    public static string SeatRejected => Get(nameof(SeatRejected));
    public static string Seated => Get(nameof(Seated));
    public static string ServerUnreachable => Get(nameof(ServerUnreachable));
    public static string ServerUnreachableDetail => Get(nameof(ServerUnreachableDetail));
    public static string ServerVersionLine => Get(nameof(ServerVersionLine));
    public static string SessionExpired => Get(nameof(SessionExpired));
    public static string SignUpFailed => Get(nameof(SignUpFailed));
    public static string SignedUp => Get(nameof(SignedUp));
    public static string StateFinished => Get(nameof(StateFinished));
    public static string StateGetReady => Get(nameof(StateGetReady));
    public static string WaitingForOpponent => Get(nameof(WaitingForOpponent));
    public static string WindowTooSmall => Get(nameof(WindowTooSmall));

    private static string Get(string name) => _resources.GetString(name)!;
}
