// Copyright (c) 2024 Luke Shires
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[CustomPropertyDrawer(typeof(ScenePickerAttribute))]
public class ScenePickerAttributePropertyDrawer : PropertyDrawer
{
	private const int c_buttonWidth = 23;
	private static GUIContent s_pickerButtonIcon = EditorGUIUtility.IconContent("d_scenepicking_pickable_hover");


	// picking mode control
	private static bool s_pickingActive = false;
	private static int s_keyboardControlID = 0;

	// serialized property tracking
	private static Object s_activeSelection = null;
	private static SerializedProperty s_activeProperty = null;
	private static string s_activePropertyPath = string.Empty;
	private static SerializedObject s_activeSerializedObject = null;

	// scene view tracking
	private static bool s_mouseOverSceneView = false;
	private static SceneView s_activeSceneView = null;
	private EditorWindow s_currentEditorWindow = null;

	// scene view highlight
	private static Material s_material = new Material(Shader.Find("Hidden/ScenePickerShader"));
	private static CommandBuffer s_commandBuffer = new CommandBuffer();
	private static List<Renderer> s_activeRenderers = new List<Renderer>(16);


	private System.Type ElementType => fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType;
	private bool IsActiveProperty(SerializedProperty property) => property.serializedObject == s_activeSerializedObject && s_activePropertyPath == property.propertyPath;


	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		// ignore non object refernce properties
		if (property.propertyType != SerializedPropertyType.ObjectReference)
		{
			EditorGUI.PropertyField(position, property, label);
			return;
		}


		// handle selection mode escape events
		if (s_pickingActive && IsActiveProperty(property))
		{
			int defaultControlID = EditorGUIUtility.GetControlID(FocusType.Keyboard, new Rect(0, 0, int.MaxValue, int.MaxValue));
			EditorGUIUtility.AddCursorRect(new Rect(0, 0, int.MaxValue, int.MaxValue), MouseCursor.Zoom, defaultControlID);
			if (s_mouseOverSceneView == false) GUIUtility.hotControl = defaultControlID;

			if (s_currentEditorWindow != EditorWindow.focusedWindow ||  // focused on a different editor window
				s_keyboardControlID != GUIUtility.keyboardControl ||    // keyboard control moved to a different GUI element
				Event.current.type == EventType.MouseDown)				// mouse was pressed in the current editor window
			{
				ExitPickingMode();
			}

			// any key press should exit picking mode. eat event to prevent further key down action
			if (Event.current.type == EventType.KeyDown)
			{
				Event.current.Use();
				ExitPickingMode();
			}

			// any mouse down event should exit picking mode. eat event to prevent further key down action
			if (Event.current.type == EventType.MouseDown)
			{
				Event.current.Use();
				ExitPickingMode();
			}

			// repaint inspector on mouse move as this may change the active selection
			if (Event.current.type == EventType.MouseMove)
			{
				UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
			}
		}


		// tint the GUI to blue if this property is in active selection mode
		Color prevGuiColour = GUI.color;
		if (IsActiveProperty(property)) GUI.color = EditorStyles.label.focused.textColor * 1.3f;


		// work around wide mode editor gui drawing issues
		if (EditorGUIUtility.wideMode)
		{
			position.x += 0.5f;
			position.width -= 0.5f;
		}

		// calculate element positions
		Rect objectFieldPosition = new Rect(position) { width = position.width - (c_buttonWidth + 3) };
		objectFieldPosition.width = Mathf.Max(EditorGUIUtility.labelWidth + (c_buttonWidth - 2), objectFieldPosition.width);
		Rect buttonPosition = new Rect(position) { x = position.x + objectFieldPosition.width + 2, width = c_buttonWidth };


		// draw object field GUI element
		if (s_pickingActive && s_mouseOverSceneView && IsActiveProperty(property))
		{
			// update object field value as preview from scene mouse hover, but dont update the serialized object
			EditorGUI.ObjectField(objectFieldPosition, label, s_activeSelection, ElementType, true);
		}
		else
		{
			// normal handling of object field outside of selection mode
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
			Object chosenObject = EditorGUI.ObjectField(objectFieldPosition, label, property.objectReferenceValue, ElementType, true);
			if (EditorGUI.EndChangeCheck())
			{
				property.objectReferenceValue = chosenObject;
				property.serializedObject.ApplyModifiedProperties();
			}
			EditorGUI.showMixedValue = false;
		}
		int lastControlID = GUIUtility.GetControlID(FocusType.Passive); // create an invisible control to intercept keyboard events


		// scene picker button
		EditorGUIUtility.SetIconSize(new Vector2(16, 16));
		if (GUI.Button(buttonPosition, s_pickerButtonIcon))
		{
			s_mouseOverSceneView = false;
			s_activeSceneView = null;
			s_activeRenderers.Clear();

			GUIUtility.hotControl = 0;
			GUIUtility.keyboardControl = lastControlID;
			s_keyboardControlID = GUIUtility.keyboardControl;

			s_currentEditorWindow = EditorWindow.focusedWindow;
			s_activeProperty = property;
			s_activeSerializedObject = property.serializedObject;
			s_activePropertyPath = property.propertyPath;
			s_pickingActive = true;

			// register to GUI event callbacks
			SceneView.beforeSceneGui -= OnBeforeSceneGUI;
			SceneView.beforeSceneGui += OnBeforeSceneGUI;
			SceneView.duringSceneGui -= OnDuringSceneGUI;
			SceneView.duringSceneGui += OnDuringSceneGUI;
			Selection.selectionChanged -= OnSelectionChanged;
			Selection.selectionChanged += OnSelectionChanged; // any selection change should imidiately exit picking mode

			SceneView.RepaintAll();
		}
		EditorGUIUtility.SetIconSize(Vector2.zero);


		// restore GUI tint colour
		GUI.color = prevGuiColour;
	}

	private void OnBeforeSceneGUI(SceneView sceneView)
	{
		// this should be unreachable, but safer to handle it anyway
		if (s_pickingActive == false)
		{
			ExitPickingMode();
			return;
		}


		// create hot control to prevent other gui script reciving events
		int defaultControlID = GUIUtility.GetControlID(FocusType.Passive);
		HandleUtility.AddDefaultControl(defaultControlID);
		if (s_activeSceneView == sceneView) GUIUtility.hotControl = defaultControlID;


		// display cursor icon in scene view when in selection mode
		MouseCursor cursor = s_activeSelection == null ? MouseCursor.Zoom : MouseCursor.Link;
		EditorGUIUtility.AddCursorRect(new Rect(0, 0, 10000, 1000), cursor);

		// handle GUI event from the scene view
		switch (Event.current.type)
		{
			case EventType.MouseEnterWindow:
				{
					s_mouseOverSceneView = true;
					s_activeSceneView = sceneView;

					// update active selection based on mouse position
					GameObject activeGo = HandleUtility.PickGameObject(Event.current.mousePosition, false);
					Object selection = ExtractTargetValue(activeGo);
					if (s_activeSelection != selection)
					{
						s_activeSelection = selection;
						ExtractRenderer(s_activeSelection);
					}
					UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
				}
				break;

			case EventType.MouseLeaveWindow:
				{
					s_mouseOverSceneView = false;
					s_activeSceneView = null;

					// clear ative selection
					s_activeSelection = null;
					s_activeRenderers.Clear();
					UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
				}
				break;

			case EventType.MouseMove:
				{
					// update active selection based on mouse position
					GameObject activeGo = HandleUtility.PickGameObject(Event.current.mousePosition, false);
					Object selection = ExtractTargetValue(activeGo);
					if (s_activeSelection != selection)
					{
						s_activeSelection = selection;
						ExtractRenderer(s_activeSelection);
						UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
					}
				}
				break;

			case EventType.MouseDown:
				{ 
					if (Event.current.button == 0) // left mouse confirms, all other buttons cancels picking
					{
						GameObject activeGo = HandleUtility.PickGameObject(Event.current.mousePosition, false);
						Object selection = ExtractTargetValue(activeGo);

						// apply the current value to the serialized object
						s_activeProperty.objectReferenceValue = selection;
						s_activeProperty.serializedObject.ApplyModifiedProperties();
					}
					Event.current.Use();
					ExitPickingMode();
				}
				break;
		}
	}

	private void OnDuringSceneGUI(SceneView sceneView)
	{
		if (s_activeRenderers.Count == 0) return;

		if (Event.current.type == EventType.Repaint)
		{
			// draw selected objects renderer with the highlight material
			s_commandBuffer.name = "Scene Picking Overlay";
			foreach (Renderer renderer in s_activeRenderers)
			{
				s_commandBuffer.DrawRenderer(renderer, s_material);
			}
			Graphics.ExecuteCommandBuffer(s_commandBuffer);
			s_commandBuffer.Clear();
		}
	}

	private Object ExtractTargetValue(GameObject go)
	{
		if (go == null) return null;

		if (ElementType == typeof(GameObject)) // special case where element type is not a Component but a GameObject
		{
			return go;
		}
		else
		{
			if ((attribute as ScenePickerAttribute).searchInParents)
			{
				return go.GetComponentInParent(ElementType, true);
			}
			else
			{
				go.TryGetComponent(ElementType, out Component component);
				return component;
			}
		}
	}

	private void ExtractRenderer(Object selection)
	{
		s_activeRenderers.Clear();

		if (selection == null)
		{
			return;
		}
		else
		{
			GameObject go = selection as GameObject;
			if (go == null)
			{
				go = (selection as Component).gameObject;
			}
			go.GetComponentsInChildren(false, s_activeRenderers);
		}
	}

	private void OnSelectionChanged()
	{
		ExitPickingMode();
	}

	private void ExitPickingMode()
	{
		GUIUtility.hotControl = 0;

		s_activeSerializedObject = null;
		s_activePropertyPath = null;
		s_activeProperty = null;
		s_pickingActive = false;

		s_activeRenderers.Clear();

		SceneView.beforeSceneGui -= OnBeforeSceneGUI;
		SceneView.duringSceneGui -= OnDuringSceneGUI;
		Selection.selectionChanged -= OnSelectionChanged;
	}
}