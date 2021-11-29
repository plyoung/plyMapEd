using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;

namespace PLY.MapEd
{
	[EditorTool("MapEd Transform Tool")] //, componentToolTarget: null, editorToolContext: typeof(GameObjectToolContext))] //, typeof(GameObject))]
	public class MapEdTransformTool : EditorTool
	{
		[SerializeField] private Texture2D toolIcon;

		public override GUIContent toolbarIcon => iconContent;

		private static MapEdTransformTool instance;
		private static GameObject prefab;
		private static Transform previewObject;
		private static Transform container;

		private static GUIContent iconContent;
		private static GameObject gridFab;
		private static Texture2D[] gridTextures;

		private static float gridLevel;
		private static float rotation;

		private SceneView activeSceneView;
		private int unityGridState = -1; // -1: unknown, 0: was off, 1: was on

		private Plane plane;
		private Transform grid;
		private Renderer gridArt;

		private static int MatProp_MainTex = Shader.PropertyToID("_MainTex");
		private static int MatProp_TintColor = Shader.PropertyToID("_Tint");
		private static int MatProp_BackColor = Shader.PropertyToID("_Back");

		// ------------------------------------------------------------------------------------------------------------
		#region shortcuts

		public static void SetActiveObject(string prefabGuid, Transform container = null)
		{
			MapEdTransformTool.container = container;		
			prefab = null;

			if (previewObject != null)
			{
				if (Selection.activeTransform == previewObject)
				{
					Selection.activeObject = null;
				}

				DestroyImmediate(previewObject.gameObject);
				previewObject = null;
			}

			if (string.IsNullOrEmpty(prefabGuid))
			{
				return;
			}

			prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
			if (prefab != null)
			{
				CreatePreviewObject(prefab);
			}

			if (previewObject != null)
			{
				if (instance == null)
				{
					// need to delay the call else there will be error about invalid selection for tool type
					EditorApplication.delayCall += () => ToolManager.SetActiveTool<MapEdTransformTool>();
				}
				else if (!ToolManager.IsActiveTool(instance))
				{
					ToolManager.SetActiveTool(instance);
				}
			}

			// focus scene view
			EditorWindow.FocusWindowIfItsOpen<SceneView>();
		}

		[Shortcut("plyMapEd/Reset Rotation", typeof(SceneView), KeyCode.R, ShortcutModifiers.Alt)] //[MenuItem("Tools/plyMapEd/Reset Rotation of Selected &r")]
		protected static void Shortcut_ResetRotation()
		{
			if (instance != null) instance.ResetRotation();
		}

		[Shortcut("plyMapEd/Rotate (Snapped)", typeof(SceneView), KeyCode.Space)]
		protected static void Shortcut_RotateSnapped()
		{
			if (instance != null) instance.RotateObjects(1f);
		}

		[Shortcut("plyMapEd/Rotate (½ Snapped)", typeof(SceneView), KeyCode.Space, ShortcutModifiers.Action)]
		protected static void Shortcut_RotateSnappedHalf()
		{
			if (instance != null) instance.RotateObjects(0.5f);
		}

		[Shortcut("plyMapEd/Rotate (¼ Snapped)", typeof(SceneView), KeyCode.Space, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
		protected static void Shortcut_RotateSnappedQuarter()
		{
			if (instance != null) instance.RotateObjects(0.25f);
		}

		[Shortcut("plyMapEd/Reset Grid Level", typeof(SceneView), KeyCode.Home)]
		protected static void Shortcut_ResetGridLevel()
		{
			if (instance != null) instance.ResetGridLevel();
		}

		[Shortcut("plyMapEd/Move Grid Up", typeof(SceneView), KeyCode.PageUp)]
		protected static void Shortcut_MoveGridUp()
		{
			if (instance != null) instance.ChangeGridLevel(1f, 0);
		}

		[Shortcut("plyMapEd/Move Grid Up (½ Snapped)", typeof(SceneView), KeyCode.PageUp, ShortcutModifiers.Action)]
		protected static void Shortcut_MoveGridUpHalf()
		{
			if (instance != null) instance.ChangeGridLevel(1f, 1);
		}

		[Shortcut("plyMapEd/Move Grid Up (¼ Snapped)", typeof(SceneView), KeyCode.PageUp, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
		protected static void Shortcut_MoveGridUpQuarter()
		{
			if (instance != null) instance.ChangeGridLevel(1f, 2);
		}

		[Shortcut("plyMapEd/Move Grid Down", typeof(SceneView), KeyCode.PageDown)]
		protected static void Shortcut_MoveGridDown()
		{
			if (instance != null) instance.ChangeGridLevel(-1f, 0);
		}

		[Shortcut("plyMapEd/Move Grid Down (½ Snapped)", typeof(SceneView), KeyCode.PageDown, ShortcutModifiers.Action)]
		protected static void Shortcut_MoveGridDownHalf()
		{
			if (instance != null) instance.ChangeGridLevel(-1f, 1);
		}

		[Shortcut("plyMapEd/Move Grid Down (¼ Snapped)", typeof(SceneView), KeyCode.PageDown, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
		protected static void Shortcut_MoveGridDownQuarter()
		{
			if (instance != null) instance.ChangeGridLevel(-1f, 2);
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region actions

		private void ResetRotation()
		{
			rotation = 0f;

			if (instance == null || !ToolManager.IsActiveTool(instance) || Selection.transforms.Length == 0)
			{
				return;
			}

			Undo.RecordObjects(Selection.transforms, "MapEd Reset Rotation");
			foreach (var transform in Selection.transforms)
			{
				transform.rotation = Quaternion.identity;
			}
		}

		private void RotateObjects(float frac)
		{
			var delta = MapEdSettings.RotateSnap * frac;
			rotation += delta;

			if (instance == null || !ToolManager.IsActiveTool(instance) || Selection.transforms.Length == 0)
			{
				return;
			}

			Undo.RecordObjects(Selection.transforms, "MapEd Rotate");
			foreach (var transform in Selection.transforms)
			{
				var rot = transform.rotation.eulerAngles;
				rot.y += delta;
				transform.rotation = Quaternion.Euler(rot);
			}
		}

		private float CalculateSnappedValue(float value, float snapValue, out float remainder)
		{
			remainder = 0f;
			if (snapValue <= 0f) return value;

			float currentAmountAbs = Mathf.Abs(value);
			if (currentAmountAbs > snapValue)
			{
				remainder = currentAmountAbs % snapValue;
				return snapValue * (Mathf.Sign(value) * Mathf.Floor(currentAmountAbs / snapValue));
			}

			return 0f;
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region tool

		protected void OnEnable()
		{
			instance = this;

			MapEdSettings.Init();

			if (iconContent == null)
			{
				iconContent = new GUIContent()
				{
					image = toolIcon,
					text = "MapEd Transform Tool",
					tooltip = "MapEd Transform Tool"
				};
			}

			if (gridFab == null)
			{
				gridFab = Resources.Load<GameObject>("plyMapEd_GridObject");
			}

			if (gridTextures == null || gridTextures.Length == 0)
			{
				gridTextures = new[]
				{
					Resources.Load<Texture2D>("plyMapEd_GridDashed1"),
					Resources.Load<Texture2D>("plyMapEd_GridDashed2"),
					Resources.Load<Texture2D>("plyMapEd_GridSolid1"),
					Resources.Load<Texture2D>("plyMapEd_GridSolid2"),
				};
			}
		}

		protected void OnDisable()
		{
			HideGrid();
			SetActiveObject(null);
			instance = null;
			//gridFab = null;
			//gridTextures = null;
		}

		public override void OnActivated()
		{
			base.OnActivated();

			// remove, just in case
			MapEdSettings.GridStyleChanged -= UpdateGridStyle;
			MapEdSettings.GridFrontColorChanged -= UpdateGridFrontColour;
			MapEdSettings.GridBackColorChanged -= UpdateGridBackColour;
			MapEdSettings.GridFillColorChanged -= UpdateGridFillColour;

			// add callbacks
			MapEdSettings.GridStyleChanged += UpdateGridStyle;
			MapEdSettings.GridFrontColorChanged += UpdateGridFrontColour;
			MapEdSettings.GridBackColorChanged += UpdateGridBackColour;
			MapEdSettings.GridFillColorChanged += UpdateGridFillColour;
		}

		public override void OnWillBeDeactivated()
		{
			base.OnWillBeDeactivated();

			if (activeSceneView != null && unityGridState == 1)
			{   // restore grid state
				activeSceneView.showGrid = true;
			}

			MapEdSettings.GridStyleChanged -= UpdateGridStyle;
			MapEdSettings.GridFrontColorChanged -= UpdateGridFrontColour;
			MapEdSettings.GridBackColorChanged -= UpdateGridBackColour;
			MapEdSettings.GridFillColorChanged -= UpdateGridFillColour;

			unityGridState = -1;
			activeSceneView = null;

			SetActiveObject(null);
			HideGrid();
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region scene view/ ui

		public override void OnToolGUI(EditorWindow window)
		{
			var sceneView = window as SceneView;
			if (sceneView == null) return;

			if (activeSceneView != sceneView)
			{
				if (activeSceneView != null && unityGridState == 1)
				{   // restore grid state
					activeSceneView.showGrid = true;
				}

				activeSceneView = sceneView;
				unityGridState = sceneView.showGrid ? 1 : 0;
				sceneView.showGrid = false;
			}

			ShowGrid();

			if (previewObject != null && prefab != null)
			{
				MovePreviewObject();
			}
			else
			{
				MoveSelectedObjects();
			}
		}

		private void MoveSelectedObjects()
		{
			var ev = Event.current;

			EditorGUI.BeginChangeCheck();

			var position = Tools.handlePosition;
			var rotation = Tools.handleRotation;

			position = Handles.PositionHandle(position, rotation);
			Handles.DrawWireDisc(position, Vector3.up, 0.5f);

			if (EditorGUI.EndChangeCheck())
			{
				var snapValue = MapEdSettings.MoveSnap;

				if (ev.shift && ev.control && ev.alt)
				{
					snapValue *= 0.25f; // ¼ Snapped
				}
				else if (ev.control)
				{
					snapValue *= 0.5f; // ½ Snapped
				}

				var delta = position - Tools.handlePosition;
				delta.x = CalculateSnappedValue(delta.x, snapValue, out _);
				delta.y = CalculateSnappedValue(delta.y, snapValue, out _);
				delta.z = CalculateSnappedValue(delta.z, snapValue, out _);

				Undo.RecordObjects(Selection.transforms, "MapEd Move");

				foreach (var transform in Selection.transforms)
				{
					transform.position += delta;
				}
			}

			// move grid
			if (grid != null && ev.type == EventType.Repaint)
			{
				var snapping = MapEdSettings.MoveSnap;
				var ray = activeSceneView.camera.ScreenPointToRay(HandleUtility.GUIPointToScreenPixelCoordinate(ev.mousePosition));
				if (snapping > 0.0f)
				{
					grid.position = new Vector3(Mathf.Round(ray.origin.x / snapping) * snapping, gridLevel, Mathf.Round(ray.origin.z / snapping) * snapping);
				}
			}
		}

		private void MovePreviewObject()
		{
			HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

			var ev = Event.current;
			var doRepaint = false;

			//if (Selection.activeTransform != previewObject)
			//{
			//	Selection.activeTransform = previewObject;
			//}

			// remove preview object when Esc rpessed
			if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
			{
				SetActiveObject(null);
				return;
			}

			// move preview object and grid
			if (ev.type == EventType.MouseMove || ev.type == EventType.Repaint)
			{
				var snapping = MapEdSettings.MoveSnap;
				var ray = activeSceneView.camera.ScreenPointToRay(HandleUtility.GUIPointToScreenPixelCoordinate(ev.mousePosition));
				if (plane.Raycast(ray, out float distance))
				{
					var snapValue = snapping;
					if (ev.shift && ev.control && ev.alt) snapValue *= 0.25f; // ¼ Snapped
					else if (ev.control) snapValue *= 0.5f; // ½ Snapped

					var worldPos = ray.GetPoint(distance);
					worldPos.x = Mathf.Round(worldPos.x / snapValue) * snapValue;
					worldPos.z = Mathf.Round(worldPos.z / snapValue) * snapValue;
					worldPos.y = gridLevel;

					previewObject.position = worldPos;

					if (ev.type == EventType.MouseMove)
					{
						doRepaint = true;
					}
				}

				if (grid != null && ev.type == EventType.Repaint)
				{
					grid.position = new Vector3(Mathf.Round(ray.origin.x / snapping) * snapping, gridLevel, Mathf.Round(ray.origin.z / snapping) * snapping);
				}
			}

			// place new object when mouse clicked
			if (ev.type == EventType.MouseDown && ev.button == 0 && ev.modifiers != EventModifiers.Alt)
			{
				ev.Use();
				PlaceNewObject();
				doRepaint = true;
			}

			if (doRepaint)
			{
				activeSceneView.Repaint();
			}
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region grid

		private void ShowGrid()
		{
			if (grid == null && gridFab != null)
			{
				var gridPos = new Vector3(0f, gridLevel, 0f);
				plane = new Plane(Vector3.up, gridPos);

				var go = Instantiate(gridFab, gridPos, Quaternion.identity);
				go.name = "_PLYMAPED_GRID_";
				go.tag = "EditorOnly";

				grid = go.GetComponent<Transform>();
				gridArt = go.GetComponentInChildren<Renderer>();

				go.hideFlags = gridArt.gameObject.hideFlags = HideFlags.HideAndDontSave; // | HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

				UpdateGridStyle(MapEdSettings.GridStyle);
				UpdateGridFrontColour(MapEdSettings.GridFrontColor);
				UpdateGridBackColour(MapEdSettings.GridBackColor);
				UpdateGridFillColour(MapEdSettings.GridFillColor);
			}
		}

		private void HideGrid()
		{
			if (grid != null)
			{
				DestroyImmediate(grid.gameObject);
				grid = null;
			}
		}

		private void ResetGridLevel()
		{
			gridLevel = 0f;
			plane = new Plane(Vector3.up, Vector3.zero);

			if (grid != null)
			{
				var pos = grid.position;
				pos.y = gridLevel;
				grid.position = pos;
			}
		}

		private void ChangeGridLevel(float dir, int snapping)
		{
			float gridCellSize = MapEdSettings.MoveSnap;

			if (snapping == 0)
			{
				// reset to nearest full snap
				var level = gridLevel;
				level += (dir * gridCellSize);
				gridLevel = Mathf.Round(level / gridCellSize) * gridCellSize;
			}
			else if (snapping == 1)
			{
				// calculate an offset from current level and snap to that
				var offs = (dir * gridCellSize) * 0.5f;
				gridLevel += offs;
			}
			else if (snapping == 2)
			{
				// calculate an offset from current level and snap to that
				var offs = (dir * gridCellSize) * 0.25f;
				gridLevel += offs;
			}

			plane = new Plane(Vector3.up, new Vector3(0f, gridLevel, 0f));

			if (grid != null)
			{
				var pos = grid.position;
				pos.y = gridLevel;
				grid.position = pos;
			}
		}

		private void UpdateGridStyle(int val)
		{
			if (gridArt == null || val < 0 || val >= gridTextures.Length) return;

			gridArt.sharedMaterials[0].SetTexture(MatProp_MainTex, gridTextures[val]);
			gridArt.sharedMaterials[1].SetTexture(MatProp_MainTex, gridTextures[val]);
		}

		private void UpdateGridFrontColour(Color val)
		{
			if (gridArt != null)
			{
				// material 1 is the front grid material
				gridArt.sharedMaterials[0].SetColor(MatProp_TintColor, val);
			}
		}

		private void UpdateGridBackColour(Color val)
		{
			if (gridArt != null)
			{
				// material 2 is the back grid material
				gridArt.sharedMaterials[1].SetColor(MatProp_TintColor, val);
			}
		}

		private void UpdateGridFillColour(Color val)
		{
			if (gridArt != null)
			{
				// material 1 also handles the grid fill colour
				gridArt.sharedMaterials[0].SetColor(MatProp_BackColor, val);
			}
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region preview and object

		private static void PlaceNewObject()
		{
			GameObject go;
			
			if (MapEdSettings.KeepPrefabLink)
			{
				go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			}
			else
			{
				go = Instantiate(prefab);
			}

			go.transform.parent = container;
			go.transform.SetPositionAndRotation(previewObject.position, previewObject.rotation);

			Undo.RegisterCreatedObjectUndo(go, "MapEd Place Object");
		}

		private static void CreatePreviewObject(GameObject prefab)
		{
			var go = Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, rotation, 0f));
			go.name = "_PLYMAPED_PREVIEW_OBJECT_";
			go.tag = "EditorOnly";
			go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

			previewObject = go.transform;
			Selection.activeObject = go;

			// disable some things not needed in the preview
			ClearStaticFlagsRecursive(previewObject);

			//var colliders = go.GetComponentsInChildren<Collider>();
			//var lights = go.GetComponentsInChildren<Light>();
			//foreach (var c in colliders) DestroyImmediate(c); //c.enabled = false;
			//foreach (var l in lights) DestroyImmediate(l); // l.enabled = false;
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------
		#region misc

		private static void ClearStaticFlagsRecursive(Transform transform)
		{
			GameObjectUtility.SetStaticEditorFlags(transform.gameObject, 0);

			int count = transform.childCount;
			for (int i = 0; i < count; i++)
			{
				ClearStaticFlagsRecursive(transform.GetChild(i));
			}
		}

		#endregion
		// ============================================================================================================
	}
}