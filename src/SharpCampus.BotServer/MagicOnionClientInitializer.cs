using MagicOnion.Client;
using SharpCampus.Shared.Services;

namespace SharpCampus.BotServer;

// The bot connects through pre-generated proxies rather than runtime code generation: the dynamic
// client cannot emit a receiver that carries a client result, and the duel hub's rematch offer is one.
// The type is only a marker for the assembly to scan: every contract in Shared gets a client.
[MagicOnionClientGeneration(typeof(IStatusService))]
internal partial class MagicOnionClientInitializer;
