namespace Configurator.Core.Profiles;

/// <summary>One fixed authenticated container profile; implementations own cryptographic composition.</summary>
public interface IProfileContainerCodec
{
    /// <summary>
    /// Optional device recipient at index zero. This opaque routing identifier is not authenticated
    /// identity or recovery evidence. Without it, all recipients are explicitly supplied portable recipients.
    /// </summary>
    Guid? DeviceRecipientId { get; }

    /// <summary>
    /// Snapshots payload and portable recipient materials and returns caller-owned restricted ciphertext.
    /// The configured device recipient, when present, precedes portable recipients and counts toward the limit.
    /// </summary>
    ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
        IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials,
        CancellationToken cancellationToken = default);

    /// <summary>Authenticates one selected recipient before admitting an owned application payload.</summary>
    ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
        ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default);
}
