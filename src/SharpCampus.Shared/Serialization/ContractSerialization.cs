using Cysharp.Serialization.MessagePack;
using MagicOnion.Serialization;
using MagicOnion.Serialization.MessagePack;
using MessagePack;
using MessagePack.Resolvers;

namespace SharpCampus.Shared.Serialization;

/// <summary>
/// MessagePack configuration every process that speaks these contracts has to install.
/// </summary>
public static class ContractSerialization
{
    /// <summary>Standard MessagePack options plus the formatters the contracts need.</summary>
    // RoomId wraps a Ulid, and the value object's generated formatter asks the resolver for one, which
    // the standard resolver does not carry.
    public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(
            [new UlidMessagePackFormatter()],
            [StandardResolver.Instance]));

    /// <summary>The MagicOnion serializer built on <see cref="Options"/>.</summary>
    public static IMagicOnionSerializerProvider Provider { get; } =
        MessagePackMagicOnionSerializerProvider.Default.WithOptions(Options);
}
