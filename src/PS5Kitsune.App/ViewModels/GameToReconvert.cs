namespace PS5Kitsune.App.ViewModels;

/// <summary>A row of Home's "Games to reconvert".</summary>
/// <param name="Detail">"Converted with {label} · in use: {label}".</param>
public sealed record GameToReconvert(string TitleId, string Name, string Detail);
