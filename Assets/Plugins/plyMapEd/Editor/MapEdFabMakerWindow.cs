using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Rendering;

namespace PLY.MapEd
{
	public class MapEdFabMakerWindow : EditorWindow
	{
		// ------------------------------------------------------------------------------------------------------------
		#region vars

		[System.NonSerialized] private Label srcLabel;
		[System.NonSerialized] private Label dstLabel;

		[SerializeField] private string srcRoot;
		[SerializeField] private string dstRoot;

		[SerializeField] private int setLayer = 0;
		[SerializeField] private ShadowCastingMode setCastShadows = ShadowCastingMode.On;
		[SerializeField] private bool setStaticShadowCaster = false;

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region system

		[MenuItem("Window/PrefabMaker")]
		public static void Open()
		{
			GetWindow<MapEdFabMakerWindow>("PrefabMaker");
		}

		protected void OnEnable()
		{
			MapEdSettings.Init();

			var srcButton = new Button { text = "Choose Source Folder" };
			var dstButton = new Button { text = "Choose Destination Folder" };
			var prcButton = new Button { text = "Process" };

			srcButton.clicked += OnSrcButtonClicked;
			dstButton.clicked += OnDstButtonClicked;
			prcButton.clicked += OnPrcButtonClicked;

			srcLabel = new Label($"Models: {srcLabel}");
			dstLabel = new Label($"Prefabs: {dstLabel}");

			var setLayerEle = new LayerField("Set Layer", setLayer);
			var setCastShadowsEle = new EnumField("Cast Shadows", setCastShadows);
			var setStaticShadowCasterEle = new Toggle("Static Shadow Caster") { value = setStaticShadowCaster };
			
			setLayerEle.RegisterValueChangedCallback(ev => setLayer = ev.newValue);
			setCastShadowsEle.RegisterValueChangedCallback(ev => setCastShadows = (ShadowCastingMode)ev.newValue);
			setStaticShadowCasterEle.RegisterValueChangedCallback(ev => setStaticShadowCaster = ev.newValue);

			// --

			InsertSpacer();
			rootVisualElement.Add(srcButton);
			rootVisualElement.Add(srcLabel);

			InsertSpacer();
			rootVisualElement.Add(dstButton);
			rootVisualElement.Add(dstLabel);

			InsertSpacer();
			rootVisualElement.Add(prcButton);

			InsertSpacer();
			rootVisualElement.Add(new Label("Options"));
			rootVisualElement.Add(setLayerEle);
			rootVisualElement.Add(setCastShadowsEle);
			rootVisualElement.Add(setStaticShadowCasterEle);
		}

		protected void OnDisable()
		{ }

		private void InsertSpacer()
		{
			var spacer = new VisualElement();
			spacer.style.height = 10;
			rootVisualElement.Add(spacer);
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region ui callbacks

		private void OnSrcButtonClicked()
		{
			var path = EditorUtility.OpenFolderPanel("Select Source Root", Application.dataPath, "");
			if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
			{
				srcRoot = FullPathToRelativePath(path);
				srcLabel.text = $"Models: {srcRoot}";
			}
		}

		private void OnDstButtonClicked()
		{
			var path = EditorUtility.OpenFolderPanel("Select Destination Root", Application.dataPath, "");
			if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
			{
				dstRoot = FullPathToRelativePath(path);
				dstLabel.text = $"Prefabs: {dstRoot}";
			}
		}

		private void OnPrcButtonClicked()
		{
			if (string.IsNullOrWhiteSpace(srcRoot) || string.IsNullOrWhiteSpace(dstRoot))
			{
				return;
			}
			
			Process();
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region process

		private void Process()
		{
			EditorUtility.DisplayProgressBar("Make prefabs", "", 0f);

			var guids = AssetDatabase.FindAssets("t:model", new[] { srcRoot });
			var step = 1f / guids.Length;
			var progress = 0f;
			foreach (var guid in guids)
			{
				try
				{
					progress += step;
					if (EditorUtility.DisplayCancelableProgressBar("Make prefabs", "", progress))
					{
						break;
					}

					// load the model asset
					var modelPath = AssetDatabase.GUIDToAssetPath(guid);
					var model = AssetDatabase.LoadMainAssetAtPath(modelPath);

					if (model == null)
					{
						Debug.LogWarning($"Failed to load: {modelPath}");
						continue;
					}

					// process folder and file names
					var prefabPath = dstRoot + modelPath.Substring(srcRoot.Length);
					var prefabFile = Path.GetFileNameWithoutExtension(prefabPath) + ".prefab";
					prefabPath = Path.GetDirectoryName(prefabPath).Replace("\\", "/");

					CheckPath(prefabPath);
					prefabPath = $"{prefabPath}/{prefabFile}";

					// create instance to be saved as prefab
					var obj = PrefabUtility.InstantiatePrefab(model);
					var fab = obj as GameObject;
					if (fab == null)
					{
						DestroyImmediate(obj);
						Debug.LogWarning($"Failed to save: {prefabPath} from {modelPath}");
						continue;
					}

					// set layer options on object
					SetLayersRecursively(fab.transform, setLayer);

					// set rendering options on object
					var rens = fab.GetComponentsInChildren<Renderer>();
					foreach (var ren in rens)
					{
						ren.shadowCastingMode = setCastShadows;
						ren.staticShadowCaster = setStaticShadowCaster;
					}

					// save new prefab asset
					PrefabUtility.SaveAsPrefabAsset(fab, prefabPath, out bool success);
					if (!success)
					{
						Debug.LogWarning($"Failed to save: {prefabPath} from {modelPath}");
					}

					// done, destroy instantiated object
					DestroyImmediate(obj);
				}
				catch (System.Exception ex)
				{
					Debug.LogException(ex);
				}
			}

			EditorUtility.ClearProgressBar();
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region misc

		private static string FullPathToRelativePath(string path)
		{
			return "Assets" + path.Substring(Application.dataPath.Length);
		}

		private static void CheckPath(string path)
		{
			var folders = path.Split('/');
			var checkedPath = "Assets";

			// start 1: skip assets
			for (int i = 1; i < folders.Length; i++)
			{
				if (!AssetDatabase.IsValidFolder($"{checkedPath}/{folders[i]}"))
				{
					AssetDatabase.CreateFolder(checkedPath, folders[i]);
				}
				checkedPath = $"{checkedPath}/{folders[i]}";
			}

			AssetDatabase.Refresh();
		}

		private static void SetLayersRecursively(Transform transform, int layer)
		{
			transform.gameObject.layer = layer;
			int count = transform.childCount;
			for (int i = 0; i < count; i++)
			{
				SetLayersRecursively(transform.GetChild(i), layer);
			}
		}

		#endregion
		// ============================================================================================================
	}
}