using BepInEx;
using BepInEx.Configuration;
using DynamicPanels;
using RuntimeInspectorNamespace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PassivePicasso.RuntimeInspectorPlugin
{
    [BepInPlugin("com.PassivePicasso.RuntimeInspectorPlugin", "RuntimeInspectorPlugin", "3.0.0")]
    public class RuntimeInspectorPlugin : BaseUnityPlugin
    {
        static FieldInfo m_typeField = typeof(VariableSet).GetField("m_type", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo m_variablesField = typeof(VariableSet).GetField("m_variables", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo m_hiddenVariablesField = typeof(RuntimeInspectorSettings).GetField("m_hiddenVariables", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo m_exposedVariablesField = typeof(RuntimeInspectorSettings).GetField("m_exposedVariables", BindingFlags.Instance | BindingFlags.NonPublic);
        private VariableSet[] hiddenVariables, exposedVariables;
        private Panel inspetorPanel;
        private Panel hierarchyPanel;
        private PanelTab inspectorTab;
        private PanelTab hierarchyTab;
        private AssetBundle sceneBundle, inspectorBundle;
        private string inspectorScenePath;
        private RuntimeInspector inspector;
        private RuntimeHierarchy hierarchy;
        private RuntimeInspectorSettings settings;
        private Canvas canvas;
        private DynamicPanelsCanvas dynamicPanelCanvas;
        public ConfigEntry<KeyCode> ShowInspectorKey { get; set; }
        public ConfigEntry<float> DefaultInspectorWidth { get; set; }
        public ConfigEntry<float> DefaultHierarchyWidth { get; set; }

        private void Awake()
        {
            ShowInspectorKey = Config.Bind("Key Bindings", "ShowInspector", KeyCode.I, "Keycode needed to press for runtime inspector window to appear");
            DefaultInspectorWidth = Config.Bind("Dock Settings", nameof(DefaultInspectorWidth), 300f, "Default width of the Inspector Panels");
            DefaultHierarchyWidth = Config.Bind("Dock Settings", nameof(DefaultHierarchyWidth), 300f, "Default width of the Hierarchy Panels");
        }

        private void Start()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var workingDirectory = Path.GetDirectoryName(assemblyLocation);
            var sceneBundlePath = Path.Combine(workingDirectory, "RIRSceneBundle");
            var inspectorBundlePath = Path.Combine(workingDirectory, "RuntimeInspector");

            inspectorBundle = AssetBundle.LoadFromFile(inspectorBundlePath);
            InitializeSettings();
            var allGameObjects = inspectorBundle.LoadAllAssets().OfType<GameObject>();

            RuntimeInspectorUtils.LoadObjRefPicker = () => Retrieve<ObjectReferencePicker>(allGameObjects, "ObjectReferencePicker");
            RuntimeInspectorUtils.LoadColorPicker = () => Retrieve<ColorPicker>(allGameObjects, "ColorPicker");
            RuntimeInspectorUtils.LoadDraggedReferenceItem = () => Retrieve<DraggedReferenceItem>(allGameObjects, "DraggedReferenceItem");
            RuntimeInspectorUtils.LoadTooltip = () => Retrieve<Tooltip>(allGameObjects, "Tooltip");
            PanelUtils.LoadPanel = () => Retrieve<Panel>(allGameObjects, "DynamicPanel");
            PanelUtils.LoadTab = () => Retrieve<PanelTab>(allGameObjects, "DynamicPanelTab");
            PanelUtils.LoadPanelPreview = () => Retrieve<RectTransform>(allGameObjects, "DynamicPanelPreview");
            //PanelNotificationCenter.OnPanelClosed += OnPanelClosed;
            //PanelNotificationCenter.OnTabClosed += OnTabClosed;
            sceneBundle = AssetBundle.LoadFromFile(sceneBundlePath);
            inspectorScenePath = sceneBundle.GetAllScenePaths()[0];
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(inspectorScenePath, LoadSceneMode.Additive);
        }

        bool initialized = false;
        private void Update()
        {
            if (canvas && canvas.gameObject.activeSelf && !initialized)
            {
                inspetorPanel.ResizeTo(new Vector2(DefaultInspectorWidth.Value, Screen.currentResolution.height));
                hierarchyPanel.ResizeTo(new Vector2(DefaultHierarchyWidth.Value, Screen.currentResolution.height));
                initialized = true;
            }
            if (Input.GetKeyDown(ShowInspectorKey.Value))
            {
                if (canvas)
                {
                    var inputIsFocused = canvas.GetComponentsInChildren<InputField>().Any(input => input.isFocused);
                    if (!inputIsFocused)
                        canvas.gameObject.SetActive(!canvas.gameObject.activeSelf);

                    if (!inspetorPanel)
                        inspetorPanel = ConfigureTab(inspector.gameObject, Direction.Right, "Inspector", out inspectorTab);
                    if (!hierarchyPanel)
                        hierarchyPanel = ConfigureTab(hierarchy.gameObject, Direction.Left, "Hierarchy", out hierarchyTab);

                }
            }
        }
        T Retrieve<T>(IEnumerable<GameObject> gameObjects, string name) where T : Component
            => gameObjects.FirstOrDefault(asset => asset.name == name && asset.GetComponent<T>())?.GetComponent<T>();
        IEnumerable<VariableSet> GetVariableSet(params (Type type, string[] variables)[] setData)
        {
            foreach (var variable in setData)
            {
                var vs = new VariableSet();
                m_typeField.SetValue(vs, variable.type.AssemblyQualifiedName);
                m_variablesField.SetValue(vs, variable.variables);
                yield return vs;
            }
        }
        void InitializeSettings()
        {
            settings = inspectorBundle.LoadAllAssets<RuntimeInspectorSettings>().FirstOrDefault() ?? ScriptableObject.CreateInstance<RuntimeInspectorSettings>();
            if (settings.HiddenVariables == null)
            {
                hiddenVariables = GetVariableSet(
                    (type: typeof(UnityEngine.Object),
                     variables: new string[] { "hideFlags", "useGUILayout", "runInEditMode", "m_CachedPtr", "m_InstanceID", "m_UnityRuntimeErrorString" }),
                    (type: typeof(Renderer), variables: new string[] { "material", "materials" }),
                    (type: typeof(MeshFilter), variables: new string[] { "mesh" }),
                    (type: typeof(Transform), variables: new string[] { "*" }),
                    (type: typeof(Component), variables: new string[] { "name", "tag" }),
                    (type: typeof(Collider), variables: new string[] { "material" }),
                    (type: typeof(Collider2D), variables: new string[] { "material" }),
                    (type: typeof(CanvasRenderer), variables: new string[] { "*" }),
                    (type: typeof(Animator), variables: new string[] { "bodyPosition", "bodyRotation", "playbackTime" })
                ).ToArray();
                m_hiddenVariablesField.SetValue(settings, hiddenVariables);
            }
            if (settings.ExposedVariables == null)
            {
                exposedVariables = GetVariableSet((type: typeof(Transform), variables: new string[] { "localPosition", "localEulerAngles", "localScale" })).ToArray();
                m_exposedVariablesField.SetValue(settings, exposedVariables);
            }
        }
        private void SceneManager_sceneLoaded(Scene loadedScene, LoadSceneMode arg1)
        {
            if (loadedScene.path != inspectorScenePath) return;

            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            inspector = loadedScene.GetRootGameObjects().SelectMany(rgo => rgo.GetComponentsInChildren<RuntimeInspector>()).First();
            hierarchy = loadedScene.GetRootGameObjects().SelectMany(rgo => rgo.GetComponentsInChildren<RuntimeHierarchy>()).First();
            canvas = loadedScene.GetRootGameObjects().SelectMany(rgo => rgo.GetComponentsInChildren<Canvas>()).First();
            dynamicPanelCanvas = canvas.GetComponent<DynamicPanelsCanvas>();
            //hierarchy.OnItemDoubleClicked += OnDoubleClickedEntry;

            inspetorPanel = ConfigureTab(inspector.gameObject, Direction.Right, "Inspector", out inspectorTab);
            inspetorPanel.ResizeTo(new Vector2(DefaultInspectorWidth.Value, Screen.currentResolution.height));

            hierarchyPanel = ConfigureTab(hierarchy.gameObject, Direction.Left, "Hierarchy", out hierarchyTab);
            hierarchyPanel.ResizeTo(new Vector2(DefaultHierarchyWidth.Value, Screen.currentResolution.height));

            DontDestroyOnLoad(canvas);
            InitializeSettings();
            hierarchy.Refresh();
            canvas.gameObject.SetActive(false);
        }
        private Panel ConfigureTab(GameObject target, Direction direction, string title, out PanelTab tab)
        {
            var panel = PanelUtils.CreatePanelFor(target.GetComponent<RectTransform>(), dynamicPanelCanvas);
            panel.DockToRoot(direction);

            tab = panel.GetTab(target.GetComponent<RectTransform>());
            tab.Label = title;
            return panel;
        }
        private void OnDoubleClickedEntry(Transform selection)
        {
            var inspectorPrefab = inspectorBundle.LoadAsset<GameObject>("RuntimeInspector");
            var focusedInspector = Instantiate(inspectorPrefab);
            var inspectorComp = focusedInspector.GetComponent<RuntimeInspector>();
            inspectorComp.Inspect(selection);
            ConfigureTab(focusedInspector, Direction.Right, selection.gameObject.name, out _);
        }
        private void OnPanelClosed(Panel panel)
        {
            if (panel == hierarchyPanel || panel == inspetorPanel) return;
            panel.Detach();
            Destroy(panel.gameObject);
        }
        private void OnTabClosed(PanelTab tab)
        {
            if (tab == inspectorTab || tab == hierarchyTab) return;
            if (tab.Panel.NumberOfTabs == 1)
            {
                if (tab.Panel == inspetorPanel || tab.Panel == hierarchyPanel) return;
                Destroy(tab.Panel.gameObject);
            }
            else
            {
                Destroy(tab.gameObject);
            }
        }
    }
}
