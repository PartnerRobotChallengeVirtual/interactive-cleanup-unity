using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using SIGVerse.Common;
using System.Collections;
using SIGVerse.ToyotaHSR;
using SIGVerse.RosBridge;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[Serializable]
	public class RelocatableObjectInfo
	{
		public string name;
		public Vector3 position;
		public Vector3 eulerAngles;
	}

	[Serializable]
	public class EnvironmentInfo
	{
		public string taskMessage;
		public string environmentName;
		public string graspingTargetName;
		public string destinationName;
		public List<RelocatableObjectInfo> graspablesPositions;
		public List<RelocatableObjectInfo> destinationsPositions; 
	}

	public class SpeechInfo
	{
		public string message;
		public string gender;
		public bool   canCancel;

		public SpeechInfo(string message, string gender, bool canCancel)
		{
			this.message   = message;
			this.gender    = gender;
			this.canCancel = canCancel;
		}
	}


	public class CleanupModeratorTool
	{
		private const string EnvironmentInfoFileNameFormat = "/../SIGVerseConfig/InteractiveCleanup/EnvironmentInfo{0:D2}.json";

		private const string TagRobot                      = "Robot";
		private const string TagGraspingCandidates         = "GraspingCandidates";
//		private const string TagDummyGraspingCandidates    = "DummyGraspingCandidates";
		private const string TagGraspingCandidatesPosition = "GraspingCandidatesPosition";
		private const string TagDestinationCandidates      = "DestinationCandidates";

		private const string JudgeTriggerNameOn = "JudgeTriggerOn";
		private const string JudgeTriggerNameIn = "JudgeTriggerIn";

		public const string SpeechExePath  = "../TTS/ConsoleSimpleTTS.exe";
		public const string SpeechLanguage = "409";
		public const string SpeechGenderModerator = "Male";
		public const string SpeechGenderHsr       = "Female";


		private IRosConnection[] rosConnections;

		private string taskMessage;
		private string environmentName;
		private GameObject graspingTarget;
		private GameObject destination;

		private List<GameObject> graspables;
		private List<GameObject> destinationCandidates;

		private List<GameObject> graspingCandidatesPositions;

		private Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionsMap;   //key:GraspablePositionInfo, value:Graspables
		private Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap; //key:DestinationPositionInfo, value:Graspables

		private GameObject robot;
		private GraspingDetector hsrGraspingDetector;

		private ExecutionMode executionMode;

		private bool hasPressedButtonToStartRecordingAvatarMotion;
		private bool hasPressedButtonToStopRecordingAvatarMotion;

		private bool hasPointedTarget;
		private bool hasPointedDestination;
		private bool? isPlacementSucceeded;

		
		private CleanupAvatarMotionPlayer   avatarMotionPlayer;
		private CleanupAvatarMotionRecorder avatarMotionRecorder;

		private CleanupPlaybackRecorder playbackRecorder;

		private System.Diagnostics.Process speechProcess;

		private Queue<SpeechInfo> speechInfoQue;
		private SpeechInfo latestSpeechInfo;

		private bool isSpeechUsed;


		public CleanupModeratorTool(CleanupModerator moderator)
		{
			CleanupConfig.Instance.InclementNumberOfTrials();

			this.executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			EnvironmentInfo environmentInfo = this.EnableEnvironment(moderator.environments);

			this.taskMessage     = environmentInfo.taskMessage;
			this.environmentName = environmentInfo.environmentName;

			this.GetGameObjects(environmentInfo, moderator.avatarMotionPlayback, moderator.playbackManager);

			this.Initialize(environmentInfo, moderator.scoreManager, moderator.objectCollisionAudioSource);
		}


		private EnvironmentInfo EnableEnvironment(List<GameObject> environments)
		{
			if(environments.Count != (from environment in environments select environment.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of environments.");
			}


			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					environmentInfo = this.GetEnvironmentInfo();
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					GameObject activeEnvironment = (from environment in environments where environment.activeSelf==true select environment).SingleOrDefault();

					if(activeEnvironment!=null)
					{
						environmentInfo.environmentName = activeEnvironment.name;

						SIGVerseLogger.Warn("Selected an active environment. name=" + activeEnvironment.name);
					}
					else
					{
						environmentInfo.environmentName = environments[UnityEngine.Random.Range(0, environments.Count)].name;
					}
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}

			foreach (GameObject environment in environments)
			{
				if(environment.name==environmentInfo.environmentName)
				{
					environment.SetActive(true);
				}
				else
				{
					environment.SetActive(false);
				}
			}

			return environmentInfo;
		}


		private void GetGameObjects(EnvironmentInfo environmentInfo, GameObject avatarMotionPlayback, GameObject worldPlayback)
		{
			this.robot = GameObject.FindGameObjectWithTag(TagRobot);

			this.hsrGraspingDetector = this.robot.GetComponentInChildren<GraspingDetector>();

			// Get grasping candidates
			List<GameObject> graspingCandidates = ExtractGraspingCandidates(environmentInfo);

//			List<GameObject> dummyGraspingCandidates = GameObject.FindGameObjectsWithTag(TagDummyGraspingCandidates).ToList<GameObject>();

			this.graspables = new List<GameObject>();

			this.graspables.AddRange(graspingCandidates);
//			this.graspables.AddRange(dummyGraspingCandidates);

			// Check the name conflict of graspables.
			if(this.graspables.Count != (from graspable in this.graspables select graspable.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of graspable objects.");
			}

			SIGVerseLogger.Info("Count of Graspables   = " + this.graspables.Count);

			// Get grasping candidates positions
			this.graspingCandidatesPositions = GameObject.FindGameObjectsWithTag(TagGraspingCandidatesPosition).ToList<GameObject>();

			if (graspables.Count > this.graspingCandidatesPositions.Count)
			{
				throw new Exception("graspables.Count > graspingCandidatesPositions.Count.");
			}
			else
			{
				SIGVerseLogger.Info("Count of GraspingCandidatesPosition = " + this.graspingCandidatesPositions.Count);
			}

			this.destinationCandidates = GameObject.FindGameObjectsWithTag(TagDestinationCandidates).ToList<GameObject>();

			if(this.destinationCandidates.Count == 0)
			{
				throw new Exception("Count of DestinationCandidates is zero.");
			}

			// Check the name conflict of destination candidates.
			if(this.destinationCandidates.Count != (from destinations in this.destinationCandidates select destinations.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of destination candidates objects.");
			}

			SIGVerseLogger.Info("Count of Destinations = " + this.destinationCandidates.Count);

			this.avatarMotionPlayer   = avatarMotionPlayback.GetComponent<CleanupAvatarMotionPlayer>();
			this.avatarMotionRecorder = avatarMotionPlayback.GetComponent<CleanupAvatarMotionRecorder>();

			this.playbackRecorder = worldPlayback.GetComponent<CleanupPlaybackRecorder>();
		}

		public List<GameObject> ExtractGraspingCandidates(EnvironmentInfo environmentInfo)
		{
			// Temporarily activate all grasping candidates
			if(this.executionMode==ExecutionMode.Competition)
			{
				GameObject graspingCandidatesObj = GameObject.Find("GraspingCandidates");

				foreach (Transform graspingCandidate in graspingCandidatesObj.transform)
				{
					graspingCandidate.gameObject.SetActive(true);
				}
			}

			// Get grasping candidates from the tag
			List<GameObject> graspingCandidates = GameObject.FindGameObjectsWithTag(TagGraspingCandidates).ToList<GameObject>();

			if(this.executionMode==ExecutionMode.Competition)
			{
				// Confirm the graspable objects inconsistency
				List<RelocatableObjectInfo> graspablesOnlyInFile = (from graspablePosition in environmentInfo.graspablesPositions where graspingCandidates.All(graspingcandidate => graspingcandidate.name!=graspablePosition.name) select graspablePosition).ToList();

				if(graspablesOnlyInFile.Count!=0)
				{
					SIGVerseLogger.Error("Following objects do not exist in the scene");
					foreach(RelocatableObjectInfo inFileWithoutScen in graspablesOnlyInFile){ SIGVerseLogger.Error("name=" + inFileWithoutScen.name); }
					throw new Exception("Some objects do not exist in the scene");
				}

				// Deactivate unused objects
				List<GameObject> graspablesOnlyInScene = (from graspingcandidate in graspingCandidates where environmentInfo.graspablesPositions.All(graspablePosition => graspablePosition.name!=graspingcandidate.name) select graspingcandidate).ToList();

				foreach(GameObject graspableOnlyInScene in graspablesOnlyInScene)
				{
					graspableOnlyInScene.SetActive(false);
				}

				// Extract the effective graspable objects
				graspingCandidates = (from graspingcandidate in graspingCandidates where environmentInfo.graspablesPositions.Any(graspablePosition => graspablePosition.name==graspingcandidate.name) select graspingcandidate).ToList();
			}

			if (graspingCandidates.Count == 0)
			{
				throw new Exception("Count of GraspingCandidates is zero.");
			}

			return graspingCandidates;
		}

		public void Initialize(EnvironmentInfo environmentInfo, CleanupScoreManager scoreManager, AudioSource objectCollisionAudioSource)
		{
			List<GameObject> objectCollisionDestinations = new List<GameObject>();
			objectCollisionDestinations.Add(scoreManager.gameObject);
			objectCollisionDestinations.Add(this.playbackRecorder.gameObject);

			foreach(GameObject graspable in this.graspables)
			{
				CollisionTransferer collisionTransferer = graspable.AddComponent<CollisionTransferer>();

				collisionTransferer.Initialize(objectCollisionDestinations, Score.GetObjectCollisionVeloticyThreshold(), 0.1f, objectCollisionAudioSource);
			}


			this.graspablesPositionsMap   = null; //key:GraspablePositionInfo,   value:Graspables
			this.destinationsPositionsMap = null; //key:DestinationPositionInfo, value:DestinationCandidate

			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					this.DeactivateGraspingCandidatesPositions();

					// Grasping target object
					this.graspingTarget = (from graspable in this.graspables where graspable.name == environmentInfo.graspingTargetName select graspable).First();

					if (this.graspingTarget == null) { throw new Exception("Grasping target not found. name=" + environmentInfo.graspingTargetName); }

					// Graspables positions map
					this.graspablesPositionsMap = new Dictionary<RelocatableObjectInfo, GameObject>();

					foreach (RelocatableObjectInfo graspablePositionInfo in environmentInfo.graspablesPositions)
					{
						GameObject graspableObj = (from graspable in this.graspables where graspable.name == graspablePositionInfo.name select graspable).First();

						if (graspableObj == null) { throw new Exception("Graspable object not found. name=" + graspablePositionInfo.name); }

						this.graspablesPositionsMap.Add(graspablePositionInfo, graspableObj);
					}

					// Destination object
					this.destination = (from destinationCandidate in this.destinationCandidates where destinationCandidate.name == environmentInfo.destinationName select destinationCandidate).First();

					if (this.destination == null) { throw new Exception("Destination not found. name=" + environmentInfo.destinationName); }

					// Add Placement checker to triggers
					this.AddPlacementChecker(this.destination);


					// Destination candidates position map
					this.destinationsPositionsMap = new Dictionary<RelocatableObjectInfo, GameObject>();

					foreach (RelocatableObjectInfo destinationPositionInfo in environmentInfo.destinationsPositions)
					{
						GameObject destinationObj = (from destinationCandidate in this.destinationCandidates where destinationCandidate.name == destinationPositionInfo.name select destinationCandidate).First();

						if (destinationObj == null) { throw new Exception("Destination candidate not found. name=" + destinationPositionInfo.name); }

						this.destinationsPositionsMap.Add(destinationPositionInfo, destinationObj);
					}

					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
//					this.graspingTarget     = CleanupModeratorTools.GetGraspingTargetObject();
//					this.cleanupDestination = CleanupModeratorTools.GetDestinationObject();

					this.graspablesPositionsMap   = this.CreateGraspablesPositionsMap();
					this.destinationsPositionsMap = this.CreateDestinationsPositionsMap();

					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}

			foreach (KeyValuePair<RelocatableObjectInfo, GameObject> pair in this.graspablesPositionsMap)
			{
				pair.Value.transform.position    = pair.Key.position;
				pair.Value.transform.eulerAngles = pair.Key.eulerAngles;

//				Debug.Log(pair.Key.name + " : " + pair.Value.name);
			}

			foreach (KeyValuePair<RelocatableObjectInfo, GameObject> pair in this.destinationsPositionsMap)
			{
				pair.Value.transform.position    = pair.Key.position;
				pair.Value.transform.eulerAngles = pair.Key.eulerAngles;

//				Debug.Log(pair.Key.name + " : " + pair.Value.name);
			}

			this.rosConnections = SIGVerseUtils.FindObjectsOfInterface<IRosConnection>();

			SIGVerseLogger.Info("ROS connection : count=" + this.rosConnections.Length);


			// Set up the voice (Using External executable file)
			this.speechProcess = new System.Diagnostics.Process();
			this.speechProcess.StartInfo.FileName = Application.dataPath + "/" + SpeechExePath;
			this.speechProcess.StartInfo.CreateNoWindow = true;
			this.speechProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

			this.isSpeechUsed = System.IO.File.Exists(this.speechProcess.StartInfo.FileName);

			this.speechInfoQue = new Queue<SpeechInfo>();

			SIGVerseLogger.Info("Text-To-Speech: " + Application.dataPath + "/" + SpeechExePath);


			this.hasPressedButtonToStartRecordingAvatarMotion = false;
			this.hasPressedButtonToStopRecordingAvatarMotion  = false;

			this.isPlacementSucceeded   = null;

			this.ResetPointingStatus();
		}

		private void AddPlacementChecker(GameObject destination)
		{
			// Add Placement checker to triggers
			Transform judgeTriggerOn = destination.transform.Find(JudgeTriggerNameOn);
			Transform judgeTriggerIn = destination.transform.Find(JudgeTriggerNameIn);

			if (judgeTriggerOn == null && judgeTriggerIn == null) { throw new Exception("No JudgeTrigger. name=" + destination.name); }
			if (judgeTriggerOn != null && judgeTriggerIn != null) { throw new Exception("Too many JudgeTrigger. name=" + destination.name); }

			if (judgeTriggerOn != null)
			{
				PlacementChecker placementChecker = judgeTriggerOn.gameObject.AddComponent<PlacementChecker>();
				placementChecker.Initialize(PlacementChecker.JudgeType.On);
			}
			if (judgeTriggerIn != null)
			{
				PlacementChecker placementChecker = judgeTriggerIn.gameObject.AddComponent<PlacementChecker>();
				placementChecker.Initialize(PlacementChecker.JudgeType.In);
			}
		}


		public List<GameObject> GetGraspables()
		{
			return this.graspables;
		}

		public ExecutionMode GetExecutionMode()
		{
			return this.executionMode;
		}


		public IEnumerator LoosenRigidbodyConstraints(Rigidbody rigidbody)
		{
			while(!rigidbody.IsSleeping())
			{
				yield return null;
			}

			rigidbody.constraints = RigidbodyConstraints.None;
		}


		public void DeactivateGraspingCandidatesPositions()
		{
			foreach (GameObject graspingCandidatesPosition in this.graspingCandidatesPositions)
			{
				graspingCandidatesPosition.SetActive(false);
			}
		}

		public Dictionary<RelocatableObjectInfo, GameObject> CreateGraspablesPositionsMap()
		{
			this.DeactivateGraspingCandidatesPositions();

			// Shuffle the grasping candidates list
			this.graspables = this.graspables.OrderBy(i => Guid.NewGuid()).ToList();

			// Shuffle the grasping candidates position list
			this.graspingCandidatesPositions  = this.graspingCandidatesPositions.OrderBy(i => Guid.NewGuid()).ToList();


			Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionsMap = new Dictionary<RelocatableObjectInfo, GameObject>();

			for (int i=0; i<this.graspables.Count; i++)
			{
				RelocatableObjectInfo graspablePositionInfo = new RelocatableObjectInfo();

				graspablePositionInfo.name        = this.graspables[i].name;
				graspablePositionInfo.position    = this.graspingCandidatesPositions[i].transform.position - new Vector3(0, this.graspingCandidatesPositions[i].transform.localScale.y * 0.49f, 0);
				graspablePositionInfo.eulerAngles = this.graspingCandidatesPositions[i].transform.eulerAngles;

				graspablesPositionsMap.Add(graspablePositionInfo, this.graspables[i]);
			}

			return graspablesPositionsMap;
		}

		public Dictionary<RelocatableObjectInfo, GameObject> CreateDestinationsPositionsMap()
		{
			Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap = new Dictionary<RelocatableObjectInfo, GameObject>();

			for (int i=0; i<this.destinationCandidates.Count; i++)
			{
				RelocatableObjectInfo destinationPositionInfo = new RelocatableObjectInfo();

				destinationPositionInfo.name        = this.destinationCandidates[i].name;
				destinationPositionInfo.position    = this.destinationCandidates[i].transform.position;
				destinationPositionInfo.eulerAngles = this.destinationCandidates[i].transform.eulerAngles;

				destinationsPositionsMap.Add(destinationPositionInfo, this.destinationCandidates[i]);
			}

			return destinationsPositionsMap;
		}


		public string GetTaskDetail()
		{
			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					return this.taskMessage;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					return "Target=" + this.graspingTarget.name + ",  Destination=" + this.destination.name;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}


		public void ControlSpeech(bool isTaskFinished)
		{
			if(!this.isSpeechUsed){ return; }

			// Cancel current speech that can be canceled when task finished
			try
			{
				if (isTaskFinished && this.latestSpeechInfo!=null && this.latestSpeechInfo.canCancel && !this.speechProcess.HasExited)
				{
					this.speechProcess.Kill();
				}
			}
			catch (Exception)
			{
				SIGVerseLogger.Warn("Couldn't terminate the speech process, but do nothing.");
				// Do nothing even if an error occurs
			}


			if (this.speechInfoQue.Count <= 0){ return; }

			// Return if the current speech is not over
			if (this.latestSpeechInfo!=null && !this.speechProcess.HasExited){ return; }


			SpeechInfo speechInfo = this.speechInfoQue.Dequeue();

			if(isTaskFinished && speechInfo.canCancel){ return; }

			this.latestSpeechInfo = speechInfo;

			string message = this.latestSpeechInfo.message.Replace("_", " "); // Remove "_"

			this.speechProcess.StartInfo.Arguments = "\"" + message + "\" \"Language=" + SpeechLanguage + "; Gender=" + this.latestSpeechInfo.gender + "\"";

			try
			{
				this.speechProcess.Start();

				SIGVerseLogger.Info("Spoke :" + message);
			}
			catch (Exception)
			{
				SIGVerseLogger.Warn("Could not speak :" + message);
			}
		}


		public void AddSpeechQue(string message, string gender, bool canCancel = false)
		{
			if(!this.isSpeechUsed){ return; }

			this.speechInfoQue.Enqueue(new SpeechInfo(message, gender, canCancel));
		}

		public void AddSpeechQueModerator(string message, bool canCancel = false)
		{
			this.AddSpeechQue(message, SpeechGenderModerator, canCancel);
		}

		public void AddSpeechQueModeratorGood(bool canCancel = false)
		{
			this.AddSpeechQue("Good job", SpeechGenderModerator, canCancel);
		}

		public void AddSpeechQueModeratorFailed(bool canCancel = false)
		{
			this.AddSpeechQue("That's too bad", SpeechGenderModerator, canCancel);
		}

		public void AddSpeechQueHsr(string message, bool canCancel = false)
		{
			this.AddSpeechQue(message, SpeechGenderHsr, canCancel);
		}

		public bool IsSpeaking()
		{
			return this.speechInfoQue.Count != 0 || (this.latestSpeechInfo!=null && !this.speechProcess.HasExited);
		}


		public bool HasPressedButtonToStartRecordingAvatarMotion()
		{
			return this.hasPressedButtonToStartRecordingAvatarMotion;
		}

		public bool HasPressedButtonToStopRecordingAvatarMotion()
		{
			return this.hasPressedButtonToStopRecordingAvatarMotion;
		}

		public bool HasPointedTarget()
		{
			return this.hasPointedTarget;
		}

		public bool HasPointedDestination()
		{
			return this.hasPointedDestination;
		}

		public void ResetPointingStatus()
		{
			this.hasPointedTarget      = false;
			this.hasPointedDestination = false;
		}

		public bool IsObjectGraspedSucceeded()
		{
			if (this.hsrGraspingDetector.GetGraspedObject() != null)
			{
				return this.graspingTarget == this.hsrGraspingDetector.GetGraspedObject();
			}

			return false;
		}

		public bool IsPlacementCheckFinished()
		{
			return isPlacementSucceeded != null;
		}

		public bool IsPlacementSucceeded()
		{
			return (bool)isPlacementSucceeded;
		}


		public bool IsCorrectObject()
		{
			return IsObjectGraspedSucceeded();
		}


		public IEnumerator UpdatePlacementStatus(MonoBehaviour moderator)
		{
			if(this.graspingTarget.transform.root == this.robot.transform.root)
			{
				this.isPlacementSucceeded = false;

				SIGVerseLogger.Info("Target placement failed: HSR has the grasping target.");
			}
			else
			{
				PlacementChecker placementChecker = this.destination.GetComponentInChildren<PlacementChecker>();

				IEnumerator<bool?> isPlaced = placementChecker.IsPlaced(this.graspingTarget);

				yield return moderator.StartCoroutine(isPlaced);

				this.isPlacementSucceeded = (bool)isPlaced.Current;
			}
		}


		public void InitializePlayback()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				this.playbackRecorder.Initialize(CleanupConfig.Instance.numberOfTrials);
			}

			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					this.avatarMotionPlayer.Initialize(CleanupConfig.Instance.numberOfTrials);
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					this.avatarMotionRecorder.Initialize(CleanupConfig.Instance.numberOfTrials);
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}

		public bool IsConnectedToRos()
		{
			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				if(!rosConnection.IsConnected())
				{
					return false;
				}
			}
			return true;
		}

		public IEnumerator ClearRosConnections()
		{
			yield return new WaitForSecondsRealtime (1.5f);

			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				rosConnection.Clear();
			}

			SIGVerseLogger.Info("Clear ROS connections");
		}

		public IEnumerator CloseRosConnections()
		{
			yield return new WaitForSecondsRealtime (1.5f);

			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				rosConnection.Close();
			}

			SIGVerseLogger.Info("Close ROS connections");
		}

		public bool IsPlaybackInitialized()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsInitialized()) { return false; }
			}

			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					if(!this.avatarMotionPlayer.IsInitialized()) { return false; }
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					if(!this.avatarMotionRecorder.IsInitialized()) { return false; }
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}

			return true;
		}

		public void ApplyFirstPostureOfAvatar()
		{
			if (this.executionMode == ExecutionMode.Competition)
			{
				this.avatarMotionPlayer.ApplyFirstPostureOfAvatar();
			}
		}

		public IEnumerator MakeAvatarInitialPosture()
		{
			const float TransitionSpeed = 1.0f;

			List<PlaybackTransformEvent> firstPosture = this.avatarMotionPlayer.GetFirstPostureOfAvatar();

			List<Transform>                   transforms  = new List<Transform>();
			Dictionary<Transform, Vector3>    startPosMap = new Dictionary<Transform, Vector3>();
			Dictionary<Transform, Quaternion> startRotMap = new Dictionary<Transform, Quaternion>();
			Dictionary<Transform, Vector3>    endPosMap   = new Dictionary<Transform, Vector3>();
			Dictionary<Transform, Quaternion> endRotMap   = new Dictionary<Transform, Quaternion>();

			foreach(PlaybackTransformEvent playbackTransformEvent in firstPosture)
			{
				Transform targetTransform = playbackTransformEvent.TargetTransform;

				transforms .Add(targetTransform);
				startPosMap.Add(targetTransform, playbackTransformEvent.TargetTransform.position);
				startRotMap.Add(targetTransform, playbackTransformEvent.TargetTransform.rotation);
				endPosMap  .Add(targetTransform, playbackTransformEvent.Position);
				endRotMap  .Add(targetTransform, Quaternion.Euler(playbackTransformEvent.Rotation.x, playbackTransformEvent.Rotation.y, playbackTransformEvent.Rotation.z));
			}

			float ratio = 0;

			while(ratio <= 1.0f)
			{
				foreach(Transform targetTransform in transforms)
				{
					targetTransform.position = Vector3   .Lerp (startPosMap[targetTransform], endPosMap[targetTransform], ratio);
					targetTransform.rotation = Quaternion.Slerp(startRotMap[targetTransform], endRotMap[targetTransform], ratio);
				}

				ratio += Time.deltaTime * TransitionSpeed;

				yield return null;
			}
		}

		public void StartPlaybackRecorder()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStarted = this.playbackRecorder.Record();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the world playback recording"); }
			}
		}

		public void StartAvatarMotionPlaybackPlayer()
		{
			// For the competition. Read generated data.
			if (this.executionMode == ExecutionMode.Competition)
			{
				bool isStarted = this.avatarMotionPlayer.Play();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the avatar motion playing"); }
			}
		}

		public void StartAvatarMotionPlaybackRecorder()
		{
			// For data generation. 
			if (this.executionMode == ExecutionMode.DataGeneration)
			{
				bool isStarted = this.avatarMotionRecorder.Record();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the avatar motion recording"); }
			}
		}


		public void StopPlayback()
		{
			this.StopPlaybackRecorder();
			this.StopAvatarMotionPlaybackPlayer();
			this.StopAvatarMotionPlaybackRecorder();
		}

		public void StopPlaybackRecorder()
		{
			if (CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStopped = this.playbackRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the world playback recording"); }
			}
		}

		public void StopAvatarMotionPlaybackPlayer()
		{
			// For the competition. Read generated data.
			if (this.executionMode == ExecutionMode.Competition)
			{
				bool isStopped = this.avatarMotionPlayer.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the avatar motion playing"); }
			}
		}

		public void StopAvatarMotionPlaybackRecorder()
		{
			// For data generation. 
			if (this.executionMode == ExecutionMode.DataGeneration)
			{
				bool isStopped = this.avatarMotionRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the avatar motion recording"); }
			}
		}


		public bool IsPlaybackFinished()
		{
			if(!this.IsPlaybackRecorderFinished()){ return false; }

			if(!this.IsAvatarMotionPlaybackPlayerFinished()){ return false; }

			if(!this.IsAvatarMotionPlaybackRecorderFinished()){ return false; }

			return true;
		}

		public bool IsPlaybackRecorderFinished()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				return this.playbackRecorder.IsFinished();
			}

			return true;
		}

		public bool IsAvatarMotionPlaybackPlayerFinished()
		{
			// For the competition. Read generated data.
			if (this.executionMode == ExecutionMode.Competition)
			{
				return this.avatarMotionPlayer.IsFinished();
			}

			return true;
		}

		public bool IsAvatarMotionPlaybackRecorderFinished()
		{
			// For data generation. 
			if (this.executionMode == ExecutionMode.DataGeneration)
			{
				return this.avatarMotionRecorder.IsFinished();
			}

			return true;
		}


		public void PointObject(Laser laser, AvatarStateStep avatarStateStep)
		{
			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					switch (avatarStateStep)
					{
						case AvatarStateStep.WaitForPickItUp:
						{
							this.hasPointedTarget = true;
							break;
						}
						case AvatarStateStep.WaitForCleanUp:
						{
							this.hasPointedDestination = true;
							break;
						}
						default:
						{
							SIGVerseLogger.Warn("This pointing by the avatar is an invalid timing. step=" + avatarStateStep);
							break;
						}
					}
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					switch (avatarStateStep)
					{
						case AvatarStateStep.WaitForPickItUp:
						{
							this.hasPointedTarget = true;
							this.graspingTarget   = laser.nearestGraspingObject;

							break;
						}
						case AvatarStateStep.WaitForCleanUp:
						{
							this.hasPointedDestination = true;
							this.destination           = laser.nearestDestination;

							// Add Placement checker to triggers
							if(this.destination.GetComponentInChildren<PlacementChecker>()==null)
							{
								this.AddPlacementChecker(this.destination);
							}
					
							break;
						}
						default:
						{
							SIGVerseLogger.Warn("This pointing by the avatar is an invalid timing. step=" + avatarStateStep);
							break;
						}
					}
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}


		public void PressAorX(ModeratorStep step)
		{
			switch (step)
			{
				case ModeratorStep.WaitForIamReady:
				{
					this.hasPressedButtonToStartRecordingAvatarMotion = true;
					break;
				}
				case ModeratorStep.WaitForObjectGrasped:
				case ModeratorStep.WaitForTaskFinished:
				case ModeratorStep.Judgement:
				case ModeratorStep.WaitForNextTask:
				{
					this.hasPressedButtonToStopRecordingAvatarMotion = true;
					break;
				}
				default:
				{
					SIGVerseLogger.Warn("This pressing A or X by the avatar is an invalid timing. step=" + step);
					break;
				}
			}
		}


		public EnvironmentInfo GetEnvironmentInfo()
		{
			string filePath = String.Format(Application.dataPath + EnvironmentInfoFileNameFormat, CleanupConfig.Instance.numberOfTrials);

			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			if (File.Exists(filePath))
			{
				// File open
				StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8);

				environmentInfo = JsonUtility.FromJson<EnvironmentInfo>(streamReader.ReadToEnd());

				streamReader.Close();
			}
			else
			{
				throw new Exception("Environment info file does not exist. filePath=" + filePath);
			}

			return environmentInfo;
		}


		public IEnumerator SaveEnvironmentInfo()
		{
			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			environmentInfo.taskMessage        = this.GetTaskDetail();
			environmentInfo.environmentName    = this.environmentName;
			environmentInfo.graspingTargetName = this.graspingTarget.name;
			environmentInfo.destinationName    = this.destination.name;

			List<RelocatableObjectInfo> graspablesPositions = new List<RelocatableObjectInfo>();

			foreach(KeyValuePair<RelocatableObjectInfo, GameObject> graspablePositionPair in this.graspablesPositionsMap)
			{
				RelocatableObjectInfo graspableInfo = new RelocatableObjectInfo();
				graspableInfo.name        = graspablePositionPair.Value.name;
				graspableInfo.position    = graspablePositionPair.Key.position;
				graspableInfo.eulerAngles = graspablePositionPair.Key.eulerAngles;

				graspablesPositions.Add(graspableInfo);
			}

			environmentInfo.graspablesPositions = graspablesPositions;

			yield return null;

			List<RelocatableObjectInfo> destinationsPositions = new List<RelocatableObjectInfo>();

			foreach(KeyValuePair<RelocatableObjectInfo, GameObject> destinationPositionPair in this.destinationsPositionsMap)
			{
				RelocatableObjectInfo destinationInfo = new RelocatableObjectInfo();
				destinationInfo.name        = destinationPositionPair.Value.name;
				destinationInfo.position    = destinationPositionPair.Key.position;
				destinationInfo.eulerAngles = destinationPositionPair.Key.eulerAngles;

				destinationsPositions.Add(destinationInfo);
			}

			environmentInfo.destinationsPositions = destinationsPositions;

			yield return null;

			object[] args = new object[] { environmentInfo, Application.dataPath};

			Thread threadWritingFile = new Thread(new ParameterizedThreadStart(this.SaveEnvironmentInfo));
			threadWritingFile.Start(args);
		}

		private void SaveEnvironmentInfo(object args)
		{
			object[] argsArray = (object[])args;

			EnvironmentInfo environmentInfo = (EnvironmentInfo)argsArray[0];
			string applicationDataPath      = (string)argsArray[1];

			string filePath = String.Format(applicationDataPath + EnvironmentInfoFileNameFormat, CleanupConfig.Instance.numberOfTrials);

			StreamWriter streamWriter = new StreamWriter(filePath, false);

			SIGVerseLogger.Info("Save Environment info. path=" + filePath);

			streamWriter.WriteLine(JsonUtility.ToJson(environmentInfo, true));

			streamWriter.Flush();
			streamWriter.Close();
		}
	}
}

