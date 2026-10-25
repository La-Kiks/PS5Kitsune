using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Diagnostics;

namespace PS5Kitsune.App.ViewModels;

/// <summary>What the Game page needs to diagnose an exit and apply its fix. See the 7c spec, "Game page".</summary>
/// <param name="ReportUrl">The page Report opens after copying its text; null copies only.</param>
public sealed record DiagnosisServices(CrashDiagnoser Diagnoser, LibsRepair Libs, CrashReporter Reporter, IDialogService Dialogs, Uri? ReportUrl);
