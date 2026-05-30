namespace PoliceBackend.Models;

public sealed record IdentityVerificationStateResponse(
    bool CccdVerified,
    bool FaceScanned,
    bool CccdSkipped,
    bool FaceSkipped,
    string? CccdImage,
    string? FaceImage,
    DateTimeOffset UpdatedAt);

public sealed record UpdateCccdVerificationRequest(
    string? CccdImage,
    bool CccdVerified,
    bool CccdSkipped);

public sealed record UpdateFaceVerificationRequest(
    string? FaceImage,
    bool FaceScanned,
    bool FaceSkipped);

public sealed record CreateDiditSessionRequest(
    string CallbackUrl,
    string? CallbackMethod = null,
    ClerkAccountSnapshot? Clerk = null);

public sealed record DiditSessionResponse(
    string SessionId,
    string Url);

public sealed record DiditDecisionResponse(
    string SessionId,
    string Status,
    bool IsApproved,
    string? Detail);
