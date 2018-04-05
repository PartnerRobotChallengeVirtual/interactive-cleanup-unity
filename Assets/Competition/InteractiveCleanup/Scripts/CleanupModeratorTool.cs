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

	public class CleanupModeratorTool
	{
		private const string EnvironmentInfoFileNameFormat = "/../SIGVerseConfig/InteractiveCleanup/EnvironmentInfo{0:D2}.json";

		private const string TagRobot                      = "Robot";
		private const string TagGraspingCandidates         = "GraspingCandidates";
		private const string TagDummyGraspingCandidates    = "DummyGraspingCandidates";
		private const string TagGraspingCandidatesPosition = "GraspingCandidatesPosition";
		private const string TagDestinationCandidates      = "DestinationCandidates";

		private const string JudgeTriggersName = "JudgeTriggers";

		private IRosConnection[] rosConnections;

		private string environmentName;
		private GameObject graspingTarget;
		private GameObject destination;

		private List<GameObject> graspables;
		private List<GameObject> destinationCandidates;

		private List<GameObject> graspingCandidatesPositions;

		private Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionsMap;   //key:GraspablePositionInfo, value:Graspables
		private Dictionary<RelocatableObjectInfo, GameObject> destinationsPositionsMap; //key:DestinationPositionInfo, value:Graspables

		private GameObject robot;
		private HSRGraspingDetector hsrGraspingDetector;

		private ExecutionMode executionMode;

		private bool hasPressedButtonForDataGeneration;
		private bool hasPointedTarget;
		private bool hasPointedDestination;
		private bool? isPlacementSucceeded;

		
		private CleanupAvatarMotionPlayer   avatarMotionPlayer;
		private CleanupAvatarMotionRecorder avatarMotionRecorder;

		private CleanupPlaybackRecorder playbackRecorder;


		public CleanupModeratorTool(List<GameObject> environments, CleanupScoreManager scoreManager, GameObject avatarMotionPlayback, GameObject worldPlayback)
		{
			CleanupConfig.Instance.InclementNumberOfTrials();

			this.executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			EnvironmentInfo environmentInfo = this.EnableEnvironment(environments);

			this.environmentName = environmentInfo.environmentName;

			this.GetGameObjects(avatarMotionPlayback, worldPlayback);

			this.Initialize(environmentInfo, scoreManager);
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
					GameObject activeEnvironment = (from environment in environments where environment.activeSelf==true select environment).FirstOrDefault();

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


		private void GetGameObjects(GameObject avatarMotionPlayback, GameObject worldPlayback)
		{
			this.robot = GameObject.FindGameObjectWithTag(TagRobot);

			this.hsrGraspingDetector = this.robot.GetComponentInChildren<HSRGraspingDetector>();

			
			// Get grasping candidates
			List<GameObject> graspingCandidates = GameObject.FindGameObjectsWithTag(TagGraspingCandidates).ToList<GameObject>();

			if (graspingCandidates.Count == 0)
			{
				throw new Exception("Count of GraspingCandidates is zero.");
			}

			List<GameObject> dummyGraspingCandidates = GameObject.FindGameObjectsWithTag(TagDummyGraspingCandidates).ToList<GameObject>();

			this.graspables = new List<GameObject>();

			this.graspables.AddRange(graspingCandidates);
			this.graspables.AddRange(dummyGraspingCandidates);

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


		public void Initialize(EnvironmentInfo environmentInfo, CleanupScoreManager scoreManager)
		{
			List<GameObject> objectCollisionDestinations = new List<GameObject>();
			objectCollisionDestinations.Add(scoreManager.gameObject);
			objectCollisionDestinations.Add(this.playbackRecorder.gameObject);

			foreach(GameObject graspable in this.graspables)
			{
				CollisionTransferer collisionTransferer = graspable.AddComponent<CollisionTransferer>();

				collisionTransferer.Initialize(objectCollisionDestinations, Score.GetObjectCollisionVeloticyThreshold());
			}


			this.graspablesPositionsMap   = null; //key:GraspablePositionInfo, value:Graspables
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
					Transform judgeTriggersTransform = this.destination.transform.Find(JudgeTriggersName);

					if (judgeTriggersTransform==null) { throw new Exception("No Judge Triggers object"); }

					judgeTriggersTransform.gameObject.AddComponent<PlacementChecker>();


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

			this.hasPressedButtonForDataGeneration = false;
			this.hasPointedTarget       = false;
			this.hasPointedDestination  = false;
			this.isPlacementSucceeded   = null;
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
			return "Target=" + this.graspingTarget.name + ",  Destination=" + this.destination.name;
		}


		public bool HasPressedButtonForDataGeneration()
		{
			return this.hasPressedButtonForDataGeneration;
		}

		public bool HasPointedTarget()
		{
			return this.hasPointedTarget;
		}

		public bool HasPointedDestination()
		{
			return this.hasPointedDestination;
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

		public void ClearRosConnections()
		{
			foreach(IRosConnection rosConnection in this.rosConnections)
			{
				rosConnection.Clear();
			}

			SIGVerseLogger.Info("Clear ROS connections");
		}

		public void CloseRosConnections()
		{
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

		public void StartPlayback()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStarted = this.playbackRecorder.Record();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the world playback recording"); }
			}

			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					bool isStarted = this.avatarMotionPlayer.Play();

					if(!isStarted) { SIGVerseLogger.Warn("Cannot start the avatar motion playing"); }
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					bool isStarted = this.avatarMotionRecorder.Record();

					if(!isStarted) { SIGVerseLogger.Warn("Cannot start the avatar motion recording"); }
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}


		public void StopPlayback()
		{
			if (CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStopped = this.playbackRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the world playback recording"); }
			}

			this.StopAvatarMotionPlayback();
		}

		private void StopAvatarMotionPlayback()
		{
			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					bool isStopped = this.avatarMotionPlayer.Stop();

					if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the avatar motion playing"); }
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					bool isStopped = this.avatarMotionRecorder.Stop();

					if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the avatar motion recording"); }
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}


		public bool IsPlaybackFinished()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsFinished()) { return false; }
			}

			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					if(!this.avatarMotionPlayer.IsFinished()) { return false; }
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					if(!this.avatarMotionRecorder.IsFinished()) { return false; }
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}

			return true;
		}


		public void PointObject(Laser laser, ModeratorStep step)
		{
			switch (this.executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					switch (step)
					{
						case ModeratorStep.SendingPickItUpMsg:
						{
							this.hasPointedTarget = true;
							break;
						}
						case ModeratorStep.SendingCleanUpMsg:
						{
							this.hasPointedDestination = true;
							break;
						}
						default:
						{
							SIGVerseLogger.Warn("This pointing by the avatar is an invalid timing. step=" + step);
							break;
						}
					}
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					switch (step)
					{
						case ModeratorStep.SendingPickItUpMsg:
						{
							this.hasPointedTarget = true;
							this.graspingTarget   = laser.nearestGraspingObject;

							break;
						}
						case ModeratorStep.SendingCleanUpMsg:
						{
							this.hasPointedDestination = true;
							this.destination           = laser.nearestDestination;

							// Add Placement checker to triggers
							Transform judgeTriggersTransform = this.destination.transform.Find(JudgeTriggersName);

							if (judgeTriggersTransform==null) { throw new Exception("No Judge Triggers object"); }

							judgeTriggersTransform.gameObject.AddComponent<PlacementChecker>();
					
							break;
						}
						default:
						{
							SIGVerseLogger.Warn("This pointing by the avatar is an invalid timing. step=" + step);
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
				case ModeratorStep.TaskStart:
				{
					this.hasPressedButtonForDataGeneration = true;
					break;
				}
				case ModeratorStep.WaitForTaskFinished:
				case ModeratorStep.Judgement:
				case ModeratorStep.WaitForNextTask:
				{
					this.StopAvatarMotionPlayback();
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

