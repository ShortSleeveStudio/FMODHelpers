using System;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace FMODHelpers.Editor
{
    public static class FMODEventReferenceUpdater
    {
        #region Constants
        const string EditorBankPrefix = "bank:/";
        const string EditorEventPrefix = "event:/";
        const string EditorFMODPath = "Assets/FMOD";
        const string EditorFMODBanksName = "Banks";
        const string EditorFMODBanksPath = EditorFMODPath + "/" + EditorFMODBanksName;
        const string EditorFMODBanksTempPath = EditorFMODTempPath + "/" + EditorFMODBanksName;
        const string EditorFMODBanksBrokenPath = EditorFMODBrokenPath + "/" + EditorFMODBanksName;
        const string EditorFMODEventsName = "Events";
        const string EditorFMODEventsPath = EditorFMODPath + "/" + EditorFMODEventsName;
        const string EditorFMODEventsTempPath = EditorFMODTempPath + "/" + EditorFMODEventsName;
        const string EditorFMODEventsBrokenPath = EditorFMODBrokenPath + "/" + EditorFMODEventsName;
        const string EditorFMODTempName = "_TEMP";
        const string EditorFMODTempPath = EditorFMODPath + "/" + EditorFMODTempName;
        const string EditorFMODBrokenName = "_BROKEN";
        const string EditorFMODBrokenPath = EditorFMODPath + "/" + EditorFMODBrokenName;
        #endregion

        #region Static State
        static DateTime LastUpdate;
        #endregion

        #region Unity Lifecycle
        [InitializeOnLoadMethod]
        static void RegisterForEditorUpdate()
        {
            LastUpdate = DateTime.Now;
            EditorApplication.update += Update;
        }

        static void Update()
        {
            // Ensure event manager is initialized
            if (!EventManager.IsInitialized)
                return;

            // Check our last update time
            if (DateTime.Compare(EventManager.CacheTime, LastUpdate) > 0)
            {
                Debug.Log("Updating FMOD references...");
                GenerateReferences();
                LastUpdate = EventManager.CacheTime;
            }
        }
        #endregion

        #region Private API
        static void GenerateReferences()
        {
            if (!EventManager.IsInitialized)
            {
                Debug.LogError("Event cache isn't ready");
                return;
            }

            // Setup
            int progressId = 0;

            // Generate References
            try
            {
                // Start reporting progress
                progressId = Progress.Start("Generating FMOD References");

                // Ensure directories don't already exist
                if (!AssetDatabase.AssetPathExists(EditorFMODPath))
                    throw new Exception("FMOD folder doesn't exist");
                if (AssetDatabase.AssetPathExists(EditorFMODTempPath))
                    throw new Exception($"Temporary folder from a previous import still exists: {EditorFMODTempPath}");
                if (AssetDatabase.AssetPathExists(EditorFMODBrokenPath))
                    throw new Exception($"Broken references folder from a previous import still exists: {EditorFMODBrokenPath}");

                // Create temporary directory
                AssetDatabase.CreateFolder(EditorFMODPath, EditorFMODTempName);

                // Generate References
                ReferenceGenerateResult bankResult = GenerateBankReferences(
                    progressId,
                    EditorFMODTempPath,
                    EditorFMODBanksName,
                    EditorFMODBanksTempPath
                );
                ReferenceGenerateResult eventResult = GenerateEventReferences(
                    progressId,
                    EditorFMODTempPath,
                    EditorFMODEventsName,
                    EditorFMODEventsTempPath
                );

                // Create broken directories as needed and delete old directories
                if (bankResult.BrokenReferences)
                {
                    EnsureDirectory(EditorFMODPath, EditorFMODBrokenPath, EditorFMODBrokenName);
                    Debug.LogWarning(
                        "Found bank references that no longer exist. They will be left in the "
                            + $"following directory for inspection: {EditorFMODBanksBrokenPath}"
                    );
                    AssetDatabase.MoveAsset(EditorFMODBanksPath, EditorFMODBanksBrokenPath);
                }
                else if (AssetDatabase.AssetPathExists(EditorFMODBanksPath))
                    AssetDatabase.DeleteAsset(EditorFMODBanksPath);

                if (eventResult.BrokenReferences)
                {
                    EnsureDirectory(EditorFMODPath, EditorFMODBrokenPath, EditorFMODBrokenName);
                    Debug.LogWarning(
                        "Found bank references that no longer exist. They will be left in the "
                            + $"following directory for inspection: {EditorFMODEventsBrokenPath}"
                    );
                    AssetDatabase.MoveAsset(EditorFMODEventsPath, EditorFMODEventsBrokenPath);
                }
                else if (AssetDatabase.AssetPathExists(EditorFMODEventsPath))
                {
                    AssetDatabase.DeleteAsset(EditorFMODEventsPath);
                }

                // Make the temp directories, the new main directories
                AssetDatabase.MoveAsset(EditorFMODBanksTempPath, EditorFMODBanksPath);
                AssetDatabase.MoveAsset(EditorFMODEventsTempPath, EditorFMODEventsPath);

                // The temporary directories become the new references directories
                AssetDatabase.DeleteAsset(EditorFMODTempPath);

                // Save assets
                AssetDatabase.SaveAssets();

                // Report progress
                Progress.Report(progressId, 1f, "Done");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (AssetDatabase.AssetPathExists(EditorFMODTempPath))
                    AssetDatabase.DeleteAsset(EditorFMODTempPath);
            }
            finally
            {
                Progress.Remove(progressId);
            }
        }

        static ReferenceGenerateResult GenerateBankReferences(int progressId, string tempDir, string bankFolderName, string bankTempDir)
        {
            Progress.Report(progressId, 0.25f, "Generating bank references");

            // Create temporary lookup table
            Dictionary<object, AssetInfo<FMODBankRef>> bankIdToBank = LoadReferenceMap<FMODBankRef>(typeof(FMODBankRef));

            // Create temporary directory
            AssetDatabase.CreateFolder(tempDir, bankFolderName);

            // Construct references
            foreach (EditorBankRef editorBank in EventManager.Banks)
            {
                // Replicate FMOD bank heirarchy
                string fmodBankPath = editorBank.StudioPath.Substring(EditorBankPrefix.Length);
                string newBankAsset;
                RecreatePath(bankTempDir, fmodBankPath, out newBankAsset);

                // Attempt to create the bank reference or move it if it already exists
                MoveOrCreateAsset(
                    bankIdToBank,
                    editorBank.StudioPath,
                    newBankAsset,
                    (FMODBankRef bankRef) =>
                    {
                        bankRef.Name = editorBank.Name;
                        bankRef.StudioPath = editorBank.StudioPath;
                    }
                );
            }

            return new() { BrokenReferences = bankIdToBank.Count > 0 };
        }

        static ReferenceGenerateResult GenerateEventReferences(int progressId, string tempDir, string eventDirName, string eventTempDir)
        {
            Progress.Report(progressId, 0.50f, "Generating event references");

            // Create temporary lookup tables
            Dictionary<object, AssetInfo<FMODBankRef>> bankIdToBank = LoadReferenceMap<FMODBankRef>(typeof(FMODBankRef));
            Dictionary<object, AssetInfo<FMODEventRef>> eventIdToEvent = LoadReferenceMap<FMODEventRef>(typeof(FMODEventRef));

            // Create temporary directory
            AssetDatabase.CreateFolder(tempDir, eventDirName);

            // Construct references
            foreach (EditorEventRef editorEvent in EventManager.Events)
            {
                // Replicate FMOD event heirarchy
                string fmodEventPath = editorEvent.Path.Substring(EditorEventPrefix.Length);
                string newEventAsset;
                RecreatePath(eventTempDir, fmodEventPath, out newEventAsset);

                // Attempt to create the bank reference or move it if it already exists
                MoveOrCreateAsset(
                    eventIdToEvent,
                    editorEvent.Guid,
                    newEventAsset,
                    (FMODEventRef eventRef) =>
                    {
                        eventRef.Guid = editorEvent.Guid;
                        eventRef.Path = editorEvent.Path;
                        eventRef.Banks = new FMODBankRef[editorEvent.Banks.Count];
                        for (int i = 0; i < editorEvent.Banks.Count; i++)
                            eventRef.Banks[i] = bankIdToBank[editorEvent.Banks[i].StudioPath].Asset;
                    }
                );
            }

            return new ReferenceGenerateResult() { BrokenReferences = eventIdToEvent.Count > 0 };
        }

        static void RecreatePath(string baseDir, string path, out string assetPath)
        {
            string[] pathSegments = path.Split('/');
            string containingDir = baseDir;
            for (int i = 0; i < pathSegments.Length - 1; i++) // -1 to skip the event/bank name
            {
                string pathSegment = SanitizeFileName(pathSegments[i]);
                if (string.IsNullOrEmpty(pathSegment))
                    throw new Exception($"Path contained empty segments: {path}");

                string newDir = PathCombine(containingDir, pathSegment);
                EnsureDirectory(containingDir, newDir, pathSegment);
                containingDir = newDir;
            }
            assetPath = containingDir + "/" + pathSegments[pathSegments.Length - 1] + ".asset";
        }

        static string PathCombine(params string[] paths) => string.Join('/', paths);

        static void EnsureDirectory(string baseDir, string path, string name)
        {
            if (!AssetDatabase.AssetPathExists(path))
                AssetDatabase.CreateFolder(baseDir, name);
        }

        static Dictionary<object, AssetInfo<T>> LoadReferenceMap<T>(Type t)
            where T : FMODRef
        {
            Dictionary<object, AssetInfo<T>> map = new();
            string[] GUIDs = AssetDatabase.FindAssets($"t:{t.Name}");
            for (int i = 0; i < GUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(GUIDs[i]);
                T assetRef = AssetDatabase.LoadAssetAtPath<T>(path);
                map[assetRef.ID] = new AssetInfo<T>(assetRef, path);
            }
            return map;
        }

        static void MoveOrCreateAsset<T>(Dictionary<object, AssetInfo<T>> map, object assetId, string outputPath, Action<T> initializer)
            where T : FMODRef
        {
            AssetInfo<T> refInfo;
            map.TryGetValue(assetId, out refInfo);
            if (refInfo != null)
            {
                AssetDatabase.MoveAsset(refInfo.AssetPath, outputPath);
                initializer(refInfo.Asset);
            }
            else
            {
                T asset = ScriptableObject.CreateInstance<T>();
                initializer(asset);
                AssetDatabase.CreateAsset(asset, outputPath);
            }

            // Clear out the map as we go so we know about references that no longer exist at the
            // end
            map.Remove(assetId);
        }

        static string SanitizeFileName(string name)
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        #endregion

        #region Helper Classes
        struct ReferenceGenerateResult
        {
            public bool BrokenReferences;
        }

        class AssetInfo<T>
            where T : FMODRef
        {
            public AssetInfo(T asset, string assetPath)
            {
                Asset = asset;
                AssetPath = assetPath;
            }

            public T Asset;
            public string AssetPath;
        }
        #endregion
    }
}
