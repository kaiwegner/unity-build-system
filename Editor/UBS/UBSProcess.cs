using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Serialization;

namespace UBS
{
    [Serializable]
    public class UBSProcess : ScriptableObject
    {
        const string processPath = "Assets/UBSProcess.asset";
        const string processPathKey = "UBSProcessPath";


		#region data
        

        [SerializeField]
        BuildConfiguration
            mCurrentBuildConfiguration;
        BuildConfiguration CurrentBuildConfiguration
        {
            get { return mCurrentBuildConfiguration;}

        }

        [FormerlySerializedAs("mBuildAndRun")] [SerializeField]
        bool
            _buildAndRun;

        [FormerlySerializedAs("mBatchMode")] [SerializeField]
        bool
            _batchMode;
        public bool IsInBatchMode
        {
            get { return _batchMode; }
        }

        [FormerlySerializedAs("mCollection")] [SerializeField]
        BuildCollection
            _collection;
        public BuildCollection BuildCollection
        {
            get { return _collection; }
        }

        [FormerlySerializedAs("mSelectedProcesses")] [SerializeField]
        List<BuildProcess>
            _selectedProcesses;


        [FormerlySerializedAs("mCurrentBuildProcessIndex")] [SerializeField]
        int
            _currentBuildProcessIndex;

        [FormerlySerializedAs("mCurrent")] [SerializeField]
        int
            _current;

        [FormerlySerializedAs("mCurrentState")] [SerializeField]
        UBSState
            _currentState = UBSState.invalid;

        [FormerlySerializedAs("mPreStepWalker")] [SerializeField]
        UBSStepListWalker
            _preStepWalker = new UBSStepListWalker();

        [FormerlySerializedAs("mPostStepWalker")] [SerializeField]
        UBSStepListWalker
            _postStepWalker = new UBSStepListWalker();

        public UBSState CurrentState
        {
            get { return _currentState; }
        }

        public UBSStepListWalker SubPreWalker
        {
            get
            {
                return _preStepWalker;
            }
        }

        public UBSStepListWalker SubPostWalker
        {
            get
            {
                return _postStepWalker;
            }
        }

        public float Progress
        {
            get
            {

                return ((SubPreWalker.Progress + SubPostWalker.Progress) / 2.0f
                    + System.Math.Max(0, _currentBuildProcessIndex - 1)) / (float)_selectedProcesses.Count;
            }
        }

        public string CurrentProcessName
        {
            get
            {
                if(CurrentProcess != null)
                {
                    return CurrentProcess.Name;
                }
                return "N/A";
            }
        }

        BuildProcess CurrentProcess
        {
            get
            {
                if(_selectedProcesses == null || _currentBuildProcessIndex >= _selectedProcesses.Count)
                {
                    return null;
                }
                return _selectedProcesses[_currentBuildProcessIndex];
            }
        }

        public bool IsDone { get
            {
                return CurrentState == UBSState.done || CurrentState == UBSState.aborted;
            }
        }

        #endregion

        #region public interface

        public BuildProcess GetCurrentProcess()
        {
            return CurrentProcess;
        }

        public static string GetProcessPath()
        {
            return EditorPrefs.GetString(processPathKey, processPath);
        }
        /// <summary>
        /// You can overwrite where to store the build process. 
        /// </summary>
        /// <param name="pPath">P path.</param>
        public static void SetProcessPath(string pPath)
        {
            EditorPrefs.GetString(processPathKey, processPath);
        }

		#region command line options

        /// <summary>
        /// Builds a given build collection from command line. Call this method directly from the command line using Unity in headless mode. 
        /// <https://docs.unity3d.com/Documentation/Manual/CommandLineArguments.html>
        /// 
        /// Provide `collection` parameter to your command line build to specify the collection you want to build. 
        /// All selected build processes within the collection will be build. 
        /// 
        /// Example: -collection=Assets/New\ BuildCollection.asset
        /// </summary>
        public static void BuildFromCommandLine()
        {
            
            string[] arguments = System.Environment.GetCommandLineArgs();
            CommandLineArgsParser parser = new CommandLineArgsParser(arguments);
            
            string[] availableArgs = { 
                "-batchmode", 
                "-collection=", 
                "-android-sdk=", 
                "-android-ndk=", 
                "-jdk-path=", 
                "-buildTag=", 
                "-buildAll", 
                "-commitID=", 
                "-tagName=", 
                "-buildProcessByNames="
                
            };
            
            bool batchMode = parser.Collection.HasArgument("batchmode");
            string collectionPath = parser.Collection.GetValue<string>("collection");
			string androidSdkPath = parser.Collection.GetValue<string>("android-sdk");
			string buildTag = parser.Collection.GetValue<string>("buildTag");            
            string commitID = parser.Collection.GetValue<string>("commitID") ;
			string tagName = parser.Collection.GetValue<string>("tagName");
			string androidNdkPath = parser.Collection.GetValue<string>("android-ndk");
			string jdkPath = parser.Collection.GetValue<string>("jdk-path");
            bool buildAll = parser.Collection.HasArgument("buildAll");
            
            string startBuildProcessByNames = parser.Collection.GetValue<string>("buildProcessByNames");
			
			if(collectionPath == null)
			{
				Debug.LogError("NO BUILD COLLECTION SET");
                return;
			}
			
			if(!string.IsNullOrEmpty(androidSdkPath))
			{
				EditorPrefs.SetString("AndroidSdkRoot", androidSdkPath);
				Debug.Log("Set Android SDK root to: " + androidSdkPath);
			}

            if (!string.IsNullOrEmpty(androidNdkPath))
            {
                EditorPrefs.SetString("AndroidNdkRoot", androidNdkPath);
                Debug.Log("Set Android NDK root to: " + androidNdkPath);
            }

            if (!string.IsNullOrEmpty(jdkPath))
            {
                EditorPrefs.SetString("JdkPath", jdkPath);
                Debug.Log("Set JDK-Path root to: " + jdkPath);
            }
            
            if(!string.IsNullOrEmpty(commitID))
            {
                EditorPrefs.SetString("commitID", commitID);
                Debug.Log("Set commitID to: " + commitID);
            }
            
            if(!string.IsNullOrEmpty(tagName))
            {
                EditorPrefs.SetString("tagName", tagName);
                Debug.Log("Set tagName to: " + tagName);
            }

			Debug.Log("Loading Build Collection: " + collectionPath);

			// Load Build Collection
			BuildCollection collection = AssetDatabase.LoadAssetAtPath(collectionPath, typeof(BuildCollection)) as BuildCollection;
			// Run Create Command

            if (!String.IsNullOrEmpty(startBuildProcessByNames))
            {
                string[] buildProcessNameList = startBuildProcessByNames.Split(',');
                var lowerCaseTrimmedBuildProcessNameList = buildProcessNameList.Select(x => x.ToLower()).Select(x => x.Trim()).ToArray();
                Create(collection, false, lowerCaseTrimmedBuildProcessNameList, batchMode, buildTag);
            }
            else
            {
                Create(collection, false, buildAll, batchMode, buildTag);
            }
			
			
			UBSProcess process = LoadUBSProcess();

			try
			{
				while(true)
				{
					process.MoveNext();
					Debug.Log("Wait..");
					Debug.Log ("Process state: " + process.CurrentState);
					if(process.CurrentState == UBSState.done)
					{
						return;
					}
				}
			}catch(Exception pException)
			{
				Debug.LogError("Build failed due to exception: ");
				Debug.LogException(pException);
				EditorApplication.Exit(1);
			}
		}

		public static string AddBuildTag(string pOutputPath, string pTag)
		{
			List<string> splittedPath = new List<string>(pOutputPath.Split('/'));

			if(splittedPath[splittedPath.Count - 1].Contains("."))
			{

				splittedPath.Insert(splittedPath.Count - 2, pTag);
			}
			else
			{
				splittedPath.Add(pTag);
			}

			splittedPath.RemoveAll((str) => {
				return string.IsNullOrEmpty(str);
			});

			return string.Join("/", splittedPath.ToArray());
		}

#endregion

		public static void Create(BuildCollection collection, bool buildAndRun, bool pBatchMode = false, bool pBuildAll = false, string buildTag = "")
		{
			UBSProcess p = ScriptableObject.CreateInstance<UBSProcess>();
			p._buildAndRun = buildAndRun;
			p._batchMode = pBatchMode;
			p._collection = collection;
			if(!pBuildAll)
			{
				p._selectedProcesses = p._collection.Processes.FindAll( obj => obj.Selected );
			}
			else
			{
				p._selectedProcesses = p._collection.Processes;
			}
			p._currentState = UBSState.invalid;

			if(!string.IsNullOrEmpty(buildTag))
			{
				foreach(var sp in p._selectedProcesses)
				{
					sp.OutputPath = AddBuildTag(sp.OutputPath, buildTag);
				}
			}

			AssetDatabase.CreateAsset( p, GetProcessPath());
			AssetDatabase.SaveAssets();
		}

        /// <summary>
        /// Builds a buildcollection by using an array of build process names (',' seperated!)
        /// By using a list of build process names, we reconfigure and retarget the actual build collection.
        /// </summary>
        public static void Create(BuildCollection collection, bool buildAndRun, string[] namesToBuild, bool batchMode = false, string buildTag = "")
        {
            UBSProcess p = ScriptableObject.CreateInstance<UBSProcess>();
            p._buildAndRun = buildAndRun;
            p._batchMode = batchMode;
            p._collection = collection;
            if (namesToBuild != null && namesToBuild.Length > 0)
            {
                var selectedProcesses = p._collection.Processes
                    .Where(buildProcess => namesToBuild.Contains(buildProcess.Name.ToLower())).ToList();
                p._selectedProcesses = selectedProcesses;
            }
            else
            {
                p._selectedProcesses = p._collection.Processes;
            }
            p._currentState = UBSState.invalid;

            if (!string.IsNullOrEmpty(buildTag))
            {
                foreach (var sp in p._selectedProcesses)
                {
                    sp.OutputPath = AddBuildTag(sp.OutputPath, buildTag);
                }
            }

            AssetDatabase.CreateAsset(p, GetProcessPath());
            AssetDatabase.SaveAssets();
        }

		public static bool IsUBSProcessRunning()
		{
			var asset = AssetDatabase.LoadAssetAtPath( GetProcessPath(), typeof(UBSProcess) );
			return asset != null;
		}
		public static UBSProcess LoadUBSProcess()
		{
			var process = AssetDatabase.LoadAssetAtPath( GetProcessPath(), typeof(UBSProcess));
			return process as UBSProcess;
		}
		

		public void MoveNext()
		{

			switch(CurrentState)
			{
				case UBSState.setup: DoSetup(); break;
				case UBSState.preSteps: DoPreSteps(); break;
				case UBSState.building: DoBuilding(); break;
				case UBSState.postSteps: DoPostSteps(); break;
				case UBSState.invalid: NextBuild(); break;
				case UBSState.done: OnDone(); break;
			}
		}
		
		public void Cancel(string pMessage)
		{
			if(pMessage.Length > 0)
			{
				EditorUtility.DisplayDialog("UBS: Error occured!", pMessage, "Ok - my fault.");
			}
			Cancel();
		}

		public void Cancel()
		{
			_currentState = UBSState.aborted;
			_preStepWalker.Clear();
			_postStepWalker.Clear();
			Save();
		}

		#endregion


		#region build process state handling
		void OnDone()
		{

		}
		void NextBuild()
		{

			if(_currentBuildProcessIndex >= _selectedProcesses.Count)
			{
				_currentState = UBSState.done;
				Save();
			}else
			{
				_currentState = UBSState.setup;
				Save ();
			}
		}

		void DoSetup()
		{
			mCurrentBuildConfiguration = new BuildConfiguration();
            mCurrentBuildConfiguration.Initialize();

			if(!CheckOutputPath(CurrentProcess))
				return;
            
			if (!EditorUserBuildSettings.SwitchActiveBuildTarget (Helpers.GroupFromBuildTarget(CurrentProcess.Platform), CurrentProcess.Platform)) {
                Cancel();
				throw new Exception("Could not switch to build target: " + CurrentProcess.Platform);
			}
			
			var scenes = new EditorBuildSettingsScene[CurrentProcess.Scenes.Count];
			for(int i = 0;i< scenes.Length;i++)
			{
				EditorBuildSettingsScene ebss = new EditorBuildSettingsScene( CurrentProcess.Scenes[i] ,true );
				scenes[i] = ebss;
			}
			EditorBuildSettings.scenes = scenes;


			_preStepWalker.Init( CurrentProcess.PreBuildSteps, mCurrentBuildConfiguration );

			_postStepWalker.Init(CurrentProcess.PostBuildSteps, mCurrentBuildConfiguration );

			_currentState = UBSState.preSteps;
			
			Save();
		}

		void DoPreSteps()
		{
			_preStepWalker.MoveNext();

			if(_currentState == UBSState.aborted)
				return;

			if(_preStepWalker.IsDone())
			{
				_currentState = UBSState.building;
			}
			Save();
		}

		void DoBuilding()
		{
            
			List<string> scenes = new List<string>();

			foreach(var scn in EditorBuildSettings.scenes)
			{
				if(scn.enabled)
					scenes.Add(scn.path);
			}
			BuildOptions bo = CurrentProcess.Options;
			if(_buildAndRun)
				bo = bo | BuildOptions.AutoRunPlayer;

			if(!CurrentProcess.Pretend)
			{
				BuildPipeline.BuildPlayer(
					scenes.ToArray(),
					CurrentProcess.OutputPath,
					CurrentProcess.Platform,
					bo );
			}

			OnBuildDone ();
		}

		void OnBuildDone() 
		{
			_currentState = UBSState.postSteps;
			Save();
		}
        
		void DoPostSteps()
		{
			_postStepWalker.MoveNext();
			
			if(_currentState == UBSState.aborted)
				return;

			if(_postStepWalker.IsDone())
			{
				_currentState = UBSState.invalid;
				_currentBuildProcessIndex++;
			}
			Save();
		}
		#endregion

		void Save()
		{
			if(this != null) {
				EditorUtility.SetDirty(this);
				AssetDatabase.SaveAssets();
			}
		}

		bool CheckOutputPath(BuildProcess pProcess)
		{
			string error = "";
			
			
			if(pProcess.OutputPath.Length == 0) {
				error = "Please provide an output path.";
				Cancel(error);
				return false;
			}
			
			try
			{
				DirectoryInfo dir;
				if(pProcess.Platform == BuildTarget.Android
				   || pProcess.Platform == BuildTarget.StandaloneWindows
				   || pProcess.Platform == BuildTarget.StandaloneWindows64)
					dir = new DirectoryInfo(Path.GetDirectoryName(UBS.Helpers.GetAbsolutePathRelativeToProject( pProcess.OutputPath )));
				else
					dir = new DirectoryInfo(pProcess.OutputPath);
				dir.Create();

				if(!dir.Exists)
					error = "The given output path is invalid.";
			}
			catch (Exception e)
			{
				error = e.ToString();
			}
			
			if(error.Length > 0)
			{
				Cancel(error);
				return false;
			}
			return true;

		}
	}
    
	public enum UBSState
	{
		invalid,
		setup,
		preSteps,
		building,
		postSteps,
		done,
		aborted
	}



	[Serializable]
	public class UBSStepListWalker
	{
        [field: FormerlySerializedAs("mIndex")]
        [field: SerializeField]
        public int Index { get; private set; } = 0;

        [field: FormerlySerializedAs("mSteps")]
        [field: SerializeField]
        public List<BuildStep> Steps { get; private set; }


        IBuildStepProvider _currentStep;

        [field: FormerlySerializedAs("mConfiguration")]
        [field: SerializeField]
        public BuildConfiguration Configuration { get; private set; }

        public UBSStepListWalker()
		{

		}

		public void Init ( List<BuildStep> steps, BuildConfiguration configuration)
		{
			Index = 0;
			Steps = steps;
			Configuration = configuration;
		}
		public void Clear()
		{
			Index = 0;
			Steps = null;
			Configuration = null;
		}

		public void MoveNext()
		{
			if(_currentStep == null || _currentStep.IsBuildStepDone())
			{
				NextStep();
			}else
			{
				_currentStep.BuildStepUpdate();
			}
		}
		
		void NextStep()
		{
			if(Steps == null)
				return;
			if(IsDone())
			{
				return;
			}

			if(_currentStep != null)
				Index++;
            
			if(Index >= Steps.Count)
				return;
            
            // skips disabled steps
            while (Index < Steps.Count-1)
            {
                if (Steps[Index].Enabled)
                    break;
                Index++;
            }
            
			Steps[Index].InferType();

			if (Steps [Index].StepType != null) 
			{
				_currentStep = System.Activator.CreateInstance( Steps[Index].StepType ) as IBuildStepProvider;
				Configuration.SetParams( Steps[Index].Parameters );
			} 
			else 
			{
				_currentStep = new EmptyBuildStep();
			}			

			_currentStep.BuildStepStart(Configuration);			
		}

		public bool IsDone()
		{
			if(Steps != null)
				return Index == Steps.Count;
			else
				return false;
		}

		public float Count {
			get
			{
				if(Steps == null || Steps.Count == 0)
					return 0;
				return (float)Steps.Count;
			}
		}

		public float Progress
		{
			get
			{
				if(Index == 0 && Count == 0)
					return 1;
				return Index / Count;
			}
		}
		public string Step
		{
			get
			{
				if(Steps == null || Index >= Steps.Count)
					return "N/A";

				return Steps[Index].TypeName;
			}
		}
	}



}

