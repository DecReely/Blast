using Blast.EditorTools;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>
    /// Bridges a <see cref="VerificationReport"/> into an NUnit assertion.
    ///
    /// The suites run every check before returning, so a failure message lists all of them rather
    /// than only the first — which is what makes a red test actionable without a second run.
    /// </summary>
    internal static class VerificationAssert
    {
        public static void Passed(VerificationReport report)
        {
            if (report.Passed)
            {
                return;
            }

            Assert.Fail($"{report}\n{report.Failures.Count} failure(s):\n  " +
                        string.Join("\n  ", report.Failures));
        }
    }
}
