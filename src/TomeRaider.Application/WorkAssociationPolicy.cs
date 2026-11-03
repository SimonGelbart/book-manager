namespace TomeRaider.Application;

public static class WorkAssociationPolicy
{
    public static ImportAssociationDecision Decide(
        EpubMetadata metadata,
        IReadOnlyList<ImportReviewCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return new ImportAssociationDecision(null, null, candidates);
        }

        if (candidates.Count == 1
            && string.Equals(metadata.Ao3WorkId, candidates[0].Ao3WorkId, StringComparison.Ordinal)
            && HasCompatibleIdentity(metadata, candidates[0]))
        {
            return new ImportAssociationDecision(candidates[0].WorkId, null, candidates);
        }

        var sourceMatches = candidates.Count(candidate =>
            string.Equals(metadata.Ao3WorkId, candidate.Ao3WorkId, StringComparison.Ordinal));
        var reason = sourceMatches > 1
            ? "More than one existing work has this source identifier."
            : sourceMatches == 1
                ? "The source identifier matches an existing work, but title or author metadata conflicts."
                : "Available metadata suggests an existing work, but source identity is not established.";
        return new ImportAssociationDecision(null, reason, candidates);
    }

    private static bool HasCompatibleIdentity(
        EpubMetadata metadata,
        ImportReviewCandidate candidate)
    {
        if (!string.Equals(
                NormalizeIdentityText(metadata.Title),
                NormalizeIdentityText(candidate.Title),
                StringComparison.Ordinal))
        {
            return false;
        }

        return metadata.Author is null
            || candidate.Author is null
            || string.Equals(
                NormalizeIdentityText(metadata.Author),
                NormalizeIdentityText(candidate.Author),
                StringComparison.Ordinal);
    }

    private static string NormalizeIdentityText(string value)
    {
        return string.Join(
                " ",
                value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }
}
