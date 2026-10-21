using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>
    /// Enforces the dependency direction the architecture relies on. The simulation must never
    /// depend on networking, rendering, UI or AI: that is what keeps it testable headlessly and
    /// keeps presentation from quietly becoming authoritative.
    /// </summary>
    public class AsmdefBoundaryTests
    {
        const string Scripts = "Assets/_Project/Scripts";

        [Serializable]
        class AsmDef
        {
            public string name;
            public string[] references;
        }

        static IEnumerable<TestCaseData> Rules()
        {
            yield return Rule("Content", "RushAhead.Sim", "RushAhead.AI", "RushAhead.Net", "RushAhead.Render", "RushAhead.UI");
            yield return Rule("Sim", "RushAhead.AI", "RushAhead.Net", "RushAhead.Render", "RushAhead.UI");
            yield return Rule("AI", "RushAhead.Net", "RushAhead.Render", "RushAhead.UI");
            yield return Rule("Net", "RushAhead.Render", "RushAhead.UI");
            yield return Rule("Render", "RushAhead.Net", "RushAhead.UI");
        }

        static TestCaseData Rule(string folder, params string[] forbidden) =>
            new TestCaseData($"{Scripts}/{folder}/RushAhead.{folder}.asmdef", forbidden).SetName($"{folder} stays independent");

        [TestCaseSource(nameof(Rules))]
        public void Assembly_DoesNotReferenceForbiddenAssemblies(string asmdefPath, string[] forbidden)
        {
            Assert.That(File.Exists(asmdefPath), Is.True, $"Missing {asmdefPath}");
            var asmdef = JsonUtility.FromJson<AsmDef>(File.ReadAllText(asmdefPath));

            var violations = (asmdef.references ?? Array.Empty<string>()).Intersect(forbidden).ToArray();

            Assert.That(violations, Is.Empty, $"{asmdef.name} must not reference: {string.Join(", ", violations)}");
        }
    }
}
