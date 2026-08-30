using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// The outcome of one verification suite: every check it ran, and which of them failed.
    ///
    /// The suites have two consumers with different needs — the Dream Games menu wants a readable
    /// log of everything that was checked, and the test runner wants a pass or a fail with enough
    /// detail to act on. Returning a report rather than logging directly serves both from one run,
    /// so a check can never pass under the menu and fail under the tests.
    /// </summary>
    public sealed class VerificationReport
    {
        private readonly List<string> _lines = new();
        private readonly List<string> _failures = new();

        public VerificationReport(string title)
        {
            Title = title;
        }

        public string Title { get; }

        /// <summary>Description of each failed check, empty when the suite passed.</summary>
        public IReadOnlyList<string> Failures => _failures;

        public bool Passed => _failures.Count == 0;

        /// <summary>Records a check that has already been evaluated.</summary>
        public void Check(string name, bool passed, string detail = null)
        {
            var described = string.IsNullOrEmpty(detail) ? name : $"{name} ({detail})";

            _lines.Add($"  {(passed ? "PASS" : "FAIL")} {described}");

            if (!passed)
            {
                _failures.Add(described);
            }
        }

        /// <summary>Records a check comparing an observed value against the expected one.</summary>
        public void Expect<T>(string name, T actual, T expected)
        {
            Check(name, Equals(actual, expected), $"was {actual}, expected {expected}");
        }

        /// <summary>Adds context that is not a pass or a fail, such as the setup a run used.</summary>
        public void Note(string line)
        {
            _lines.Add($"  {line}");
        }

        /// <summary>Merges another suite's results in, keeping its failures.</summary>
        public void Absorb(VerificationReport other)
        {
            _lines.AddRange(other._lines);
            _failures.AddRange(other._failures);
        }

        /// <summary>
        /// Writes the report to the console, as an error when anything failed so it cannot be
        /// scrolled past.
        /// </summary>
        public void Log()
        {
            if (Passed)
            {
                Debug.Log(ToString());
                return;
            }

            Debug.LogError($"{this}\n{_failures.Count} failure(s).");
        }

        public override string ToString()
        {
            var text = new StringBuilder(Title).AppendLine(":");

            foreach (var line in _lines)
            {
                text.AppendLine(line);
            }

            return text.ToString();
        }
    }
}
