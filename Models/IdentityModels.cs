namespace PoliceBackend.Models;

public sealed record FaceCompareRequest(
    string CccdImage,
    string LiveImage);

public sealed record FaceCompareResponse(
    bool IsMatch,
    double Confidence,
    double Threshold,
    string RequestId);
