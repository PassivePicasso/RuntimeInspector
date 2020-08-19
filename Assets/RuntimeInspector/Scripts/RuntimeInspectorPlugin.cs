﻿using BepInEx;
using BepInEx.Logging;
using RoR2;
using RuntimeInspectorNamespace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PassivePicasso.RuntimeInspector
{
    using Path = System.IO.Path;

    [BepInDependency("com.PassivePicasso.RainOfStages.Shared")]
    [BepInPlugin("com.PassivePicasso.RuntimeInspector", "RuntimeInspector", "2020.1.0")]
    public class RuntimeInspectorPlugin : BaseUnityPlugin
    {
        public static readonly VariableSet[] HiddenVariables = new[]
        {
            new VariableSet { type = "Object", variables = new string[] { "hideFlags", "useGUILayout", "runInEditMode", "m_CachedPtr", "m_InstanceID", "m_UnityRuntimeErrorString" } },
            new VariableSet { type = "Renderer", variables = new string[] { "material", "materials" } },
            new VariableSet { type = "MeshFilter", variables = new string[] { "mesh" } },
            new VariableSet { type = "Transform", variables = new string[] { "*" } },
            new VariableSet { type = "Component", variables = new string[] { "name","tag" } },
            new VariableSet { type = "Collider", variables = new string[] { "material" } },
            new VariableSet { type = "Collider2D", variables = new string[] { "material" } },
            new VariableSet { type = "CanvasRenderer", variables = new string[] { "*" } },
            new VariableSet { type = "Animator", variables = new string[] { "bodyPosition", "bodyRotation", "playbackTime" } }
        };
        public static readonly VariableSet[] ExposedVariables = new[]
        {
            new VariableSet { type = "Transform", variables = new string[] { "localPosition", "localEulerAngles", "localScale" } }
        };

        class ManifestMap
        {
            public FileInfo File;
            public string[] Content;
        }

        public static IReadOnlyList<UnityEngine.Object> Assets => instance.assets.AsReadOnly();

        static RuntimeInspectorPlugin instance;

        private List<GameObject> prefabs = new List<GameObject>();
        private List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private string workingDirectory;

        private void Awake()
        {
            var assemblyLocation = typeof(RuntimeInspectorPlugin).Assembly.Location;
            workingDirectory = Path.GetDirectoryName(assemblyLocation);
            Logger.LogInfo(workingDirectory);

            instance = this;
            Extensions.logger = Logger;
            RoR2Application.onLoad += Initialize;
        }

        void Initialize()
        {
            try
            {
                Logger.LogMessage("Initializing Runtime Inspector");

                LoadAssetBundles();

                InjectPanel("RuntimeInspectorPanel");
            }
            catch { }
            finally
            {
                Logger.LogInfo($"Finished setting up RI panel");
            }
        }

        private void InjectPanel(string prefabName)
        {
            GameObject prefab = prefabs.FirstOrDefault(go => go.name.Equals(prefabName));
            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);
            Logger.LogInfo($"Acquired Integration Panel: {instance?.name ?? "Not Found"}");

            var safeArea = RoR2Application.instance.mainCanvas.transform;//.transform.Find("SafeArea");

            instance.GetComponent<RectTransform>().SetParent(safeArea, false);

            var settings = instance.GetComponentInChildren<RuntimeInspectorNamespace.RuntimeInspector>().settings.First();
            settings.HiddenVariables = HiddenVariables;
            settings.ExposedVariables = ExposedVariables;
            instance.SetActive(true);
        }

        private void LoadAssetBundles()
        {
            var dir = new DirectoryInfo(workingDirectory);

            prefabs.Clear();
            assets.Clear();
            var manifestMaps = dir.GetFiles("*.manifest", SearchOption.AllDirectories)
                                  .Select(manifestFile => new ManifestMap { File = manifestFile, Content = File.ReadAllLines(manifestFile.FullName) })
                                  .Where(mfm => mfm.Content.Any(line => line.StartsWith("AssetBundleManifest:")))
                                  .ToArray();

            Logger.LogInfo($"Loading RuntimeInspector AssetBundles");
            foreach (var mfm in manifestMaps)
            {
                try
                {
                    var directory = mfm.File.DirectoryName;
                    var filename = Path.GetFileNameWithoutExtension(mfm.File.FullName);
                    var abmPath = Path.Combine(directory, filename);
                    var namedBundle = AssetBundle.LoadFromFile(abmPath);
                    var manifest = namedBundle.LoadAsset<AssetBundleManifest>("assetbundlemanifest");
                    Logger.LogInfo($"Loaded RuntimeInspector AssetBundleManifest");

                    var dependentBundles = manifest.GetAllAssetBundles();
                    Logger.LogInfo($"Loading RuntimeInspector AssetBundleManifest dependent bundles");

                    foreach (var definitionBundle in dependentBundles)
                        try
                        {
                            Logger.LogInfo($"Loading RuntimeInspector AssetBundle: {definitionBundle}");
                            var bundlePath = Path.Combine(directory, definitionBundle);
                            var bundle = AssetBundle.LoadFromFile(bundlePath);
                            Logger.LogInfo($"Loaded RuntimeInspector AssetBundle: {bundle.name} with {bundle.GetAllAssetNames().Aggregate((a, b) => $"{a}, {b}")}");
                            assets.AddRange(bundle.LoadAllAssets());
                            prefabs.AddRange(assets.OfType<GameObject>());
                            Logger.LogInfo($"Loaded RuntimeInspector AssetBundle Prefabs: {assets.Select(bf => bf.name).Aggregate((a, b) => $"{a}, {b}")}");
                        }
                        catch (Exception e) { Logger.LogError(e); }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }
        }
    }

    internal static class Extensions
    {
        public static ManualLogSource logger;
        public static IEnumerable<T> Flatten<T>(this IEnumerable<T> e, Func<T, IEnumerable<T>> f) => e.SelectMany(c => f(c).Flatten(f)).Concat(e);
        public static IEnumerable<Transform> Flatten(this IEnumerable<Transform> transforms)
        {
            foreach (var transform in transforms)
            {
                yield return transform;
                Transform[] childTransforms = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++)
                    childTransforms[i] = transform.GetChild(i);

                foreach (var childTransform in childTransforms.Flatten())
                    yield return childTransform;
            }
        }
        public static IEnumerable<String> GetHierarchy(this IEnumerable<Transform> transforms, int indent = 0)
        {
            var indentString = indent == 0 ? "" : Enumerable.Repeat("--", indent).Aggregate((a, b) => $"{a}{b}");
            foreach (var transform in transforms)
            {
                yield return $"{indentString}{transform.name}";

                Transform[] childTransforms = new Transform[transform.childCount];

                for (int i = 0; i < transform.childCount; i++)
                    childTransforms[i] = transform.GetChild(i);

                foreach (var childName in childTransforms.GetHierarchy(indent + 1))
                    yield return childName;
            }
        }
        public static void PrintHierarchy(this IEnumerable<Transform> transforms, int indent = 0)
        {
            var indentString = indent == 0 ? "" : Enumerable.Repeat("--", indent).Aggregate((a, b) => $"{a}{b}");
            foreach (var transform in transforms)
            {
                if (!transform.gameObject.activeInHierarchy)
                    logger.LogDebug($"{indentString}{transform.name}");
                else
                    logger.LogMessage($"{indentString}{transform.name}");

                Transform[] childTransforms = new Transform[transform.childCount];

                for (int i = 0; i < transform.childCount; i++)
                    childTransforms[i] = transform.GetChild(i);

                childTransforms.PrintHierarchy(indent + 1);
            }
        }

        public static void PrintHierarchy(this Transform transform)
        {
            Transform[] childTransforms = new Transform[transform.childCount];

            for (int i = 0; i < transform.childCount; i++)
                childTransforms[i] = transform.GetChild(i);

            var hierarchy = childTransforms.GetHierarchy(1);

            logger.LogDebug($"{transform.name}");
            foreach (var line in hierarchy) logger.LogDebug($"{line}");
        }
    }
}