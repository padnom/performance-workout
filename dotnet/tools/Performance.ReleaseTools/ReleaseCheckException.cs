namespace Performance.ReleaseTools;

/// <summary>A release check found a problem that must block the build or the distribution.</summary>
public sealed class ReleaseCheckException(string message) : Exception(message);
