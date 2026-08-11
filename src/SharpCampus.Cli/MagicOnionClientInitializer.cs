using MagicOnion.Client;
using SharpCampus.Shared.Services;

namespace SharpCampus.Cli;

// Pre-generates client proxies and MessagePack formatters, so the client stays free of runtime code generation.
[MagicOnionClientGeneration(typeof(IStatusService))]
internal partial class MagicOnionClientInitializer;
