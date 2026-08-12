using MasterMemory;

// The generator emits MemoryDatabase, DatabaseBuilder and the per-table classes into this namespace,
// next to the table records themselves.
[assembly: MasterMemoryGeneratorOptions(Namespace = "SharpCampus.Shared.MasterData")]
