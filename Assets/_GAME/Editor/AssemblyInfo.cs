using System.Runtime.CompilerServices;

// The verification suites are driven by both the Dream Games menu and the test runner. The scene
// scaffolding they share — the scratchpad, the fixed-step ticker, the headless scene runner — stays
// internal rather than being widened into a public API just so tests can reach it.
[assembly: InternalsVisibleTo("Blast.Tests.Editor")]
