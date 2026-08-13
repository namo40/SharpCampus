using MagicOnion.Client;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Services;

namespace SharpCampus.RoomServer.Tests;

// These tests connect the same way the console client does, through pre-generated proxies rather than
// runtime code generation: the dynamic client cannot emit a receiver that carries a client result.
// Registering these takes the runtime-generated clients out of the picture, so both contract
// assemblies have to be covered.
[MagicOnionClientGeneration(typeof(IStatusService), typeof(IRoomControlService))]
internal partial class MagicOnionClientInitializer;
