namespace Analysis.Api;

public sealed record ApiMetadataResponse(
    string Service,
    string Milestone,
    string Description);
