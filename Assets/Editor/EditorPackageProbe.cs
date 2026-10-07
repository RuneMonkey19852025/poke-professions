using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
// "PackageInfo" alone is ambiguous: UnityEditor has one too (the asset-store kind).
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PokeProfessions.EditorTools
{
    /// <summary>
    /// Diagnostic probe: the editor's "This project contains one or more
    /// packages with errors" dialog names no package, and the editor log stays
    /// silent about it — the errors only exist in the Package Manager's own
    /// state. This dumps that state (name, version, source, path and every
    /// <c>PackageInfo.errors</c> entry) to <c>Logs/packages.txt</c>, which is
    /// the list the dialog is built from.
    ///
    ///   ALSA_CONFIG_PATH="$PWD/Tools/alsa-null.conf" \
    ///     "$HOME/Unity/Hub/Editor/6000.6.3f1/Editor/Unity" \
    ///     -batchMode -quit -nographics -projectPath "$PWD" \
    ///     -executeMethod PokeProfessions.EditorTools.EditorPackageProbe.Run
    /// </summary>
    public static class EditorPackageProbe
    {
        const string ReportPath = "Logs/packages.txt";

        public static void Run()
        {
            var sb = new StringBuilder();
            var pkgs = PackageInfo.GetAllRegisteredPackages();
            var broken = new List<PackageInfo>();

            sb.AppendLine("== registered packages: " + pkgs.Length + " ==");
            foreach (var p in pkgs)
            {
                bool err = p.errors != null && p.errors.Length > 0;
                if (err) broken.Add(p);
                sb.AppendLine((err ? "ERROR " : "ok    ") + p.name + "@" + p.version +
                              "  source=" + p.source +
                              "  direct=" + p.isDirectDependency +
                              "  path=" + (string.IsNullOrEmpty(p.resolvedPath) ? "(none)" : p.resolvedPath));
                if (err)
                    foreach (var e in p.errors) sb.AppendLine("        " + e);
            }

            sb.AppendLine();
            sb.AppendLine("== " + broken.Count + " package(s) with errors ==");
            foreach (var p in broken)
                sb.AppendLine(p.name + "@" + p.version);

            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[PackageProbe] " + broken.Count + " package(s) with errors -> " + ReportPath);

            EditorApplication.Exit(broken.Count == 0 ? 0 : 1);
        }
    }
}
