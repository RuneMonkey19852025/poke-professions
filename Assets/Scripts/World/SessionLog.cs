using System;
using System.IO;
using UnityEngine;
using PokeProfessions.Core;

namespace PokeProfessions.World
{
    /// <summary>
    /// Lightweight crash telemetry. Appends session milestones and unhandled
    /// exceptions to session_log.txt next to the save file, and records a
    /// "clean exit" line on graceful shutdown. If the game dies silently
    /// (SIGKILL, driver kill, OOM), the absence of that line — plus the last
    /// milestone reached — narrows down what happened. Max 200 lines, local
    /// file, no network.
    /// </summary>
    public class SessionLog : MonoBehaviour
    {
        const string FileName = "session_log.txt";
        const int MaxEntries = 200;

        static string path;
        static bool active = true;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            path = Path.Combine(Application.persistentDataPath, FileName);
            Trim();
            Write("session start");
            Application.quitting += OnQuitting;
            Application.logMessageReceived += OnLogMessage;
        }

        void OnQuitting()
        {
            Write("clean exit");
            active = false;
        }

        void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            Write(type + ": " + condition +
                  (string.IsNullOrEmpty(stackTrace) ? "" : "\n    " + stackTrace.Replace("\n", "\n    ")));
        }

        /// <summary>Record a milestone from anywhere (e.g. "world built").</summary>
        public static void Write(string entry)
        {
            if (!active || string.IsNullOrEmpty(path)) return;
            try
            {
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + entry + "\n");
            }
            catch (Exception) { active = false; } // disk full / permissions: give up quietly
        }

        /// <summary>Keep only the most recent MaxEntries lines.</summary>
        static void Trim()
        {
            try
            {
                if (!File.Exists(path)) return;
                var lines = File.ReadAllLines(path);
                if (lines.Length <= MaxEntries) return;
                File.WriteAllLines(path, lines[^MaxEntries..]);
            }
            catch (Exception) { /* best effort */ }
        }
    }
}
