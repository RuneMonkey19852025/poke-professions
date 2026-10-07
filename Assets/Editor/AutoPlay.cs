using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PokeProfessions.EditorTools
{
    /// <summary>
    /// One-shot auto-play hook used by launch.sh: the script drops an
    /// Assets/AutoPlay.request marker; the first time the editor notices it
    /// (domain load on a cold launch, or asset import when an already-running
    /// editor regains focus) the marker is consumed and Play mode starts.
    /// `./launch.sh --edit` opens the editor without auto-playing and clears
    /// any pending request. Markers older than 30 minutes are treated as
    /// stale leftovers and just deleted.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoPlay
    {
        internal const string MarkerPath = "Assets/AutoPlay.request";
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);
        private static bool scheduled;

        static AutoPlay()
        {
            // Defer so this never runs mid-import (same pattern as EditorSceneBootstrap).
            if (HasMarker()) Schedule();
        }

        /// <summary>Called by AutoPlayWatcher when the marker is imported while
        /// the editor is already open (launch.sh ran, editor was running).</summary>
        internal static void MarkerImported()
        {
            if (HasMarker()) Schedule();
        }

        private static bool HasMarker()
        {
            return File.Exists(MarkerPath);
        }

        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += Tick;
        }

        private static void Tick()
        {
            // Wait out initial import/compile so we don't play a half-built project.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Tick;
                return;
            }

            if (!File.Exists(MarkerPath))
            {
                scheduled = false;
                return;
            }

            // Stale request (launch failed, or the editor was focused much later)?
            // Clear it instead of surprising the user with a Play entry.
            var age = DateTime.Now - File.GetLastWriteTime(MarkerPath);
            if (age > MaxAge || age < TimeSpan.Zero)
            {
                Consume();
                scheduled = false;
                return;
            }

            // Already in Play mode: the request is satisfied — just clear it.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Consume();
                scheduled = false;
                return;
            }

            Consume();
            Debug.Log("[PokeProfessions] AutoPlay: entering Play mode (requested by launch.sh). " +
                      "Use ./launch.sh --edit to open the editor without playing.");
            EditorApplication.isPlaying = true;
        }

        private static void Consume()
        {
            // DeleteAsset also removes the .meta; fall back for a not-yet-imported file.
            if (!AssetDatabase.DeleteAsset(MarkerPath) && File.Exists(MarkerPath))
            {
                File.Delete(MarkerPath);
                if (File.Exists(MarkerPath + ".meta")) File.Delete(MarkerPath + ".meta");
            }
        }
    }

    /// <summary>
    /// The marker is a plain asset, so an import event is the signal that
    /// launch.sh requested Play in an editor that's already running.
    /// (On a cold launch the domain loads after the import, so AutoPlay's
    /// [InitializeOnLoad] constructor picks the marker up instead.)
    /// </summary>
    internal class AutoPlayWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                                            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path == AutoPlay.MarkerPath)
                {
                    AutoPlay.MarkerImported();
                    return;
                }
            }
        }
    }
}
