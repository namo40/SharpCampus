using MagicOnion.Client;
using SharpCampus.Shared.Services;

namespace SharpCampus.Cli;

// Pre-generates client proxies and MessagePack formatters, so the client stays free of runtime code generation.
// The type is only a marker for the assembly to scan: every service contract in Shared gets a client.
[MagicOnionClientGeneration(typeof(IStatusService))]
internal partial class MagicOnionClientInitializer;
