﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Editor.Controls;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUIUtility;
using Debug = UnityEngine.Debug;

namespace ThunderKit.Core.Editor
{
    [CustomEditor(typeof(ComposableObject), true)]
    public class ComposableObjectEditor : UnityEditor.Editor
    {
        public class StepData
        {
            public SerializedProperty step;
            public SerializedProperty dataArray;
            public int index;
        }

        static ComposableElement ClipboardItem;
        Dictionary<UnityEngine.Object, UnityEditor.Editor> Editors;
        SerializedProperty dataArray;

        private void OnEnable()
        {
            try
            {
                var targetObject = target as ComposableObject;
                Editors = new Dictionary<UnityEngine.Object, UnityEditor.Editor>();
            }
            catch
            {
            }
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "Data");
            GUILayout.Space(2);

            dataArray = serializedObject.FindProperty(nameof(ComposableObject.Data));
            CleanDataArray();
            for (int i = 0; i < dataArray.arraySize; i++)
            {
                var step = dataArray.GetArrayElementAtIndex(i);
                var stepSo = new SerializedObject(step.objectReferenceValue);
                var stepType = step.objectReferenceValue.GetType();
                var isSingleLine = stepType.GetCustomAttributes<SingleLineAttribute>().Any();

                UnityEditor.Editor editor;
                if (Editors.ContainsKey(step.objectReferenceValue))
                    editor = Editors[step.objectReferenceValue];
                else
                    Editors[step.objectReferenceValue] = editor = CreateEditor(step.objectReferenceValue);

                try
                {
                    var title = ObjectNames.NicifyVariableName(stepType.Name);
                    var foldoutRect = GUILayoutUtility.GetRect(currentViewWidth, singleLineHeight + 3);

                    GUI.Box(new Rect(foldoutRect.x - 14, foldoutRect.y - 1, foldoutRect.width + 20, foldoutRect.height + 1), string.Empty);

                    var standardSize = singleLineHeight + standardVerticalSpacing;

                    Rect deleteRect = new Rect(foldoutRect.x + 1 + foldoutRect.width - standardSize, foldoutRect.y + 1, standardSize, standardSize);

                    var popupIcon = IconContent("_Popup");
                    if (Event.current.type == EventType.Repaint)
                        GUIStyle.none.Draw(deleteRect, popupIcon, false, false, false, false);

                    if (Event.current.type == EventType.MouseUp && deleteRect.Contains(Event.current.mousePosition))
                        ShowContextMenu(i, step);

                    if (isSingleLine)
                    {
                        var so = new SerializedObject(step.objectReferenceValue);
                        var iter = so.GetIterator().Copy();
                        iter.NextVisible(true);
                        if ("m_script".Equals(iter.name, System.StringComparison.OrdinalIgnoreCase))
                            iter.NextVisible(false);

                        EditorHelpers.AddField(new Rect(foldoutRect.x, foldoutRect.y + 1, foldoutRect.width - 20, foldoutRect.height - 1),
                                                 iter,
                                                 ObjectNames.NicifyVariableName(stepType.Name));
                    }
                    else
                    {
                        step.isExpanded = EditorGUI.Foldout(foldoutRect, step.isExpanded, title);
                        if (step.isExpanded)
                        {
                            editor.serializedObject.Update();
                            editor.OnInspectorGUI();
                            if (GUI.changed)
                            {
                                EditorUtility.SetDirty(editor.serializedObject.targetObject);
                                editor.serializedObject.ApplyModifiedProperties();
                                Repaint();
                            }
                            Repaint();
                            editor.serializedObject.ApplyModifiedProperties();
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            var composableObject = target as ComposableObject;
            var size = AddScriptWindow.Styles.addButtonStyle.CalcSize(new GUIContent($"Add {ObjectNames.NicifyVariableName(composableObject.ElementType.Name)}"));
            var rect = GUILayoutUtility.GetRect(size.x, size.y);
            rect.width = size.x;
            rect.y += standardVerticalSpacing;
            rect.x = (currentViewWidth / 2) - (rect.width / 2);
            OnAddElementGUI(rect, composableObject);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(serializedObject.targetObject);
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            Repaint();
            serializedObject.ApplyModifiedProperties();
        }
        private void ShowContextMenu(int i, SerializedProperty step)
        {
            var menu = new GenericMenu();
            var stepData = new StepData { step = step, index = i, dataArray = dataArray };
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(Remove))), false, Remove, stepData);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(Duplicate))), false, Duplicate, stepData);
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(Copy))), false, Copy, stepData);

            var currentroot = step.serializedObject.targetObject as ComposableObject;
            
            if (ClipboardItem && currentroot.ElementType.IsAssignableFrom(ClipboardItem.GetType()))
            {
                menu.AddItem(new GUIContent($"Paste new {ObjectNames.NicifyVariableName(ClipboardItem?.name)} above"), false, PasteNewAbove, stepData);
                menu.AddItem(new GUIContent($"Paste new {ObjectNames.NicifyVariableName(ClipboardItem?.name)}"), false, PasteNew, stepData);
            }
            else
                menu.AddDisabledItem(new GUIContent($"Paste"));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(MoveToTop))), false, MoveToTop, stepData);
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(MoveUp))), false, MoveUp, stepData);
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(MoveDown))), false, MoveDown, stepData);
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(MoveToBottom))), false, MoveToBottom, stepData);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(nameof(EditScript))), false, EditScript, stepData);
            menu.ShowAsContext();
        }
        private void CleanDataArray()
        {
            for (int i = 0; i < dataArray.arraySize; i++)
            {
                SerializedProperty step = null;
                do
                {
                    step = dataArray.GetArrayElementAtIndex(i);
                    if (!step.objectReferenceValue)
                    {
                        for (int x = i; x < dataArray.arraySize - 1; x++)
                            dataArray.MoveArrayElement(x + 1, x);
                    }
                }
                while (step == null);
            }
        }
        static void EditScript(object data)
        {
            if (data is StepData stepData
             && stepData.step.objectReferenceValue is ScriptableObject scriptableObject)
                ScriptEditorHelper.EditScript(scriptableObject);
        }
        static void Duplicate(object data)
        {
            var stepData = data as StepData;
            if (stepData.index == 0) return;

            var instance = (ComposableElement)Instantiate(stepData.step.objectReferenceValue);

            var dataArray = stepData.step.serializedObject.FindProperty(nameof(ComposableObject.Data));
            dataArray.InsertArrayElementAtIndex(stepData.index);
            var property = dataArray.GetArrayElementAtIndex(stepData.index);
            var target = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(stepData.step.serializedObject.targetObject));
            AddSubAsset(instance, property, target);
        }
        static void PasteNewAbove(object data)
        {
            var stepData = data as StepData;

            if (ClipboardItem)
                InsertClipboard(stepData, -1);
        }
        static void PasteNew(object data)
        {
            var stepData = data as StepData;

            if (ClipboardItem)
                InsertClipboard(stepData, 0);
        }
        private static void InsertClipboard(StepData stepData, int offset)
        {
            var dataArray = stepData.step.serializedObject.FindProperty(nameof(ComposableObject.Data));
            dataArray.InsertArrayElementAtIndex(stepData.index + offset);
            var property = dataArray.GetArrayElementAtIndex(stepData.index + offset);
            var target = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(stepData.step.serializedObject.targetObject));
            AddSubAsset(ClipboardItem, property, target);
            ClipboardItem = null;
        }
        static void Copy(object data)
        {
            var stepData = data as StepData;
            if (ClipboardItem) DestroyImmediate(ClipboardItem);
            ClipboardItem = (ComposableElement)Instantiate(stepData.step.objectReferenceValue);
            ClipboardItem.name = ClipboardItem.name.Replace("(Clone)", "");
        }
        static void MoveToTop(object data)
        {
            var stepData = data as StepData;
            if (stepData.index == 0) return;
            stepData.dataArray.MoveArrayElement(stepData.index, 0);
            stepData.dataArray.serializedObject.SetIsDifferentCacheDirty();
            stepData.dataArray.serializedObject.ApplyModifiedProperties();
        }
        static void MoveToBottom(object data)
        {
            var stepData = data as StepData;
            if (stepData.index == stepData.dataArray.arraySize - 1) return;
            stepData.dataArray.MoveArrayElement(stepData.index, stepData.dataArray.arraySize - 1);
            stepData.dataArray.serializedObject.SetIsDifferentCacheDirty();
            stepData.dataArray.serializedObject.ApplyModifiedProperties();
        }
        static void MoveUp(object data)
        {
            var stepData = data as StepData;
            if (stepData.index == 0) return;
            stepData.dataArray.MoveArrayElement(stepData.index, stepData.index - 1);
            stepData.dataArray.serializedObject.SetIsDifferentCacheDirty();
            stepData.dataArray.serializedObject.ApplyModifiedProperties();
        }
        static void MoveDown(object data)
        {
            var stepData = data as StepData;
            if (stepData.index == stepData.dataArray.arraySize - 1) return;
            stepData.dataArray.MoveArrayElement(stepData.index, stepData.index + 1);
            stepData.dataArray.serializedObject.SetIsDifferentCacheDirty();
            stepData.dataArray.serializedObject.ApplyModifiedProperties();
        }
        static void Remove(object data)
        {
            var stepData = data as StepData;
            AssetDatabase.RemoveObjectFromAsset(stepData.step.objectReferenceValue);
            stepData.dataArray.DeleteArrayElementAtIndex(stepData.index);
            for (int x = stepData.index; x < stepData.dataArray.arraySize; x++)
                stepData.dataArray.MoveArrayElement(x + 1, x);
            stepData.dataArray.arraySize--;

            stepData.dataArray.serializedObject.SetIsDifferentCacheDirty();
            stepData.dataArray.serializedObject.ApplyModifiedProperties();
        }
        private static void AddSubAsset(ComposableElement instance, SerializedProperty stepField, UnityEngine.Object target)
        {
            AssetDatabase.AddObjectToAsset(instance, target);

            stepField.objectReferenceValue = instance;
            stepField.serializedObject.SetIsDifferentCacheDirty();
            stepField.serializedObject.ApplyModifiedProperties();

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(instance));
            AssetDatabase.SaveAssets();
        }
        void OnAddElementGUI(Rect rect, ComposableObject composableObject)
        {
            bool Filter(MonoScript script)
            {
                var scriptClass = script.GetClass();
                if (scriptClass == null)
                {
                    return false;
                }
                return !scriptClass.IsAbstract && scriptClass.IsSubclassOf(composableObject.ElementType);
            }
            ScriptableObject CreateFromScript(MonoScript script)
            {
                if (!script) return null;
                if (script.GetClass() == null) return null;

                var instance = (ComposableElement)CreateInstance(script.GetClass());
                instance.name = script.GetClass().Name;
                var prop = dataArray.GetArrayElementAtIndex(dataArray.arraySize++);
                AddSubAsset(instance, prop, target);
                return instance;
            }
            AddScriptWindow.Show(rect, composableObject.ElementType, CreateFromScript, Filter, composableObject.ElementTemplate);
        }
    }
}