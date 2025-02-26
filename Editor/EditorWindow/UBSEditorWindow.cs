﻿using System;
using UnityEngine;
using System.Collections;
using UnityEditor;
using UBS;
using UnityEngine.Serialization;

namespace UBS
{
	internal class UBSEditorWindow : UBSWindowBase
	{
		#region window creation
		const int kMinWidth = 600;
		const int kMinHeight = 400;
		const int kListWidth = 250;



		public static void Init (BuildCollection collection) 
		{
			var window = EditorWindow.GetWindow<UBSEditorWindow>("Build System",true);
			window.data = collection;
			window.position = new Rect(50,50, kMinWidth + 50 + kListWidth,kMinHeight + 50);
		}


		#endregion


		#region gui rendering
		string _searchContent = "";
		Vector2[] _scrollPositions;
		BuildProcessEditor _editor = new BuildProcessEditor();
        UnityEditor.IMGUI.Controls.SearchField _searchField;
        void SearchField()
		{
            _searchContent = _searchField.OnGUI(_searchContent);
        }
        void OnEnable()
		{
			Initialize();
        }
		protected override void OnGUI()
		{
			base.OnGUI ();

			Initialize();
			if(!_initialized)
				return;
				
			GUILayout.BeginVertical();
			_scrollPositions[0] = GUILayout.BeginScrollView(_scrollPositions[0]);

			
			GUILayout.BeginHorizontal();
			//
			// selectable Build Processes
			//
			GUILayout.BeginVertical("GameViewBackground",GUILayout.MaxWidth(kListWidth));
			SearchField();
            GUILayout.Space(4);
			_scrollPositions[1] = GUILayout.BeginScrollView(_scrollPositions[1], GUILayout.ExpandWidth(true));
			bool odd = true;
			if(data != null)
			{
				foreach(var process in data.Processes)
				{
					if(process == null)
					{
						data.Processes.Remove(process);
						GUIUtility.ExitGUI();
						return;
					}
					if( string.IsNullOrEmpty(_searchContent) || process.Name.StartsWith(_searchContent))
					{
						RenderSelectableBuildProcess(process,odd);
						odd = !odd;
					}
				}
			}
            GUILayout.FlexibleSpace();
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
			GUILayout.BeginVertical(GUILayout.Width(32));
			{
				if(GUILayout.Button(new GUIContent("+","Add"),UBS.Styles.ToolButton))
				{
					var el = new BuildProcess();
					Undo.RecordObject(data, "Add Build Process");
					data.Processes.Add(el);
				}
				GUI.enabled = _selectedBuildProcess != null;
				if(GUILayout.Button(new GUIContent("-","Remove"),UBS.Styles.ToolButton))
				{
					Undo.RecordObject(data, "Add Build Process");
					data.Processes.Remove(_selectedBuildProcess);
				}
				if(GUILayout.Button(new GUIContent(('\u274f').ToString(),"Duplicate"),UBS.Styles.ToolButton))
				{
					Undo.RecordObject(data, "Duplicate Build Process");
					BuildProcess bp = new BuildProcess(_selectedBuildProcess);
					data.Processes.Add(bp);
				}
				if(GUILayout.Button(new GUIContent(('\u21e1').ToString(),"Move Up"),UBS.Styles.ToolButton))
				{
					if(_selectedBuildProcess != null)
					{
						int idx = data.Processes.IndexOf( _selectedBuildProcess );
						if(idx > 0)
						{
							Undo.RecordObject(data, "Sort Build Processes");
							data.Processes.Remove(_selectedBuildProcess);
							data.Processes.Insert(idx-1,_selectedBuildProcess);
						}
					}
				}
				if(GUILayout.Button(new GUIContent(('\u21e3').ToString(), "Move Down"),UBS.Styles.ToolButton))
				{
					if(_selectedBuildProcess != null)
					{
						int idx = data.Processes.IndexOf( _selectedBuildProcess );
						if(idx < data.Processes.Count-1)
						{
							Undo.RecordObject(data, "Sort Build Processes");
							data.Processes.Remove(_selectedBuildProcess);
							data.Processes.Insert(idx+1,_selectedBuildProcess);
						}
					}
				}
				GUI.enabled = true;
			}
			GUILayout.EndVertical();
			Styles.VerticalLine();
			//
			// selected Build Process
			//
			_scrollPositions[2] = GUILayout.BeginScrollView(_scrollPositions[2],UBS.Styles.BuildProcessEditorBackground);

			_editor.OnGUI(_selectedBuildProcess, data);

			GUILayout.EndScrollView();

			GUILayout.EndHorizontal();

			Styles.HorizontalLine();
			RenderBuildVersion();
			Styles.HorizontalLine();
			
			GUILayout.EndVertical();

			GUILayout.EndScrollView();

		}

		void RenderSelectableBuildProcess (BuildProcess pProcess, bool pOdd)
        {
            bool selected = false;
			if(_selectedBuildProcessIndex == data.Processes.IndexOf(pProcess))
            {
                selected = true;
                DoSelectBuildProcess( pProcess );
                DrawBuildProcessEntry(data, pProcess, pOdd, ref _selectedCount, true, ref selected);
            }else
			{
				DrawBuildProcessEntry(data, pProcess, pOdd, ref _selectedCount, true, ref selected);
			    if(selected)
			    {
				    DoSelectBuildProcess( pProcess );
			    }
			}


		}

		void RenderBuildVersion()
		{
			GUILayout.BeginHorizontal();
            GUILayout.Label("UBS version " + UBSVersion.version.ToString(), EditorStyles.miniLabel);
			GUILayout.FlexibleSpace();
			GUILayout.Label("Build version:");

			int v;

			v = EditorGUILayout.IntField( data.version.major, GUILayout.Width(50));
			if(v != data.version.major)
			{
				data.version.major = v;
				data.SaveVersion();
			}

			v = EditorGUILayout.IntField( data.version.minor, GUILayout.Width(50));
			if(v != data.version.minor)
			{
				data.version.minor = v;
				data.SaveVersion();
			}

			v = EditorGUILayout.IntField( data.version.build, GUILayout.Width(50));
			if(v != data.version.build)
			{
				data.version.build = v;
				data.SaveVersion();
			}

			BuildVersion.BuildType type = (BuildVersion.BuildType)EditorGUILayout.EnumPopup( data.version.type, GUILayout.Width(50));
			if(type != data.version.type)
			{
				data.version.type = type;
				data.SaveVersion();
			}

			GUILayout.Label("Revision:");
			v = EditorGUILayout.IntField( data.version.revision, GUILayout.Width(80));
			if(v != data.version.revision)
			{
				data.version.revision = v;
				data.SaveVersion();
			}

			GUILayout.EndHorizontal();
		}
#endregion


#region data handling
		
		[FormerlySerializedAs("mData")] [SerializeField]
		BuildCollection data;

        
		[System.NonSerialized]
		bool _initialized = false;
		
		[System.NonSerialized]
		BuildProcess _selectedBuildProcess;

        [SerializeField]
        private int _selectedBuildProcessIndex;

        [System.NonSerialized]
        private int _selectedCount;

        void Initialize()
		{
			if(_initialized || data == null)
				return;

			_scrollPositions = new Vector2[3];

			_initialized = true;

            _searchField = new UnityEditor.IMGUI.Controls.SearchField();
            
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            EditorWindow.FocusWindowIfItsOpen<UBSEditorWindow>();
        }

		void OnUndoRedoPerformed()
		{
			this.Repaint();
		}

		void DoSelectBuildProcess (BuildProcess process)
        {
            _selectedBuildProcessIndex = data.Processes.IndexOf(process);
			_selectedBuildProcess = process;
		}

		void OnDestroy()
		{
			if(_editor != null)
				_editor.OnDestroy();

			if (data)
				EditorUtility.SetDirty(data);

			AssetDatabase.SaveAssets();

			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			data = null;
			_initialized = false;
		}

#endregion
    }
}