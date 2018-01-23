using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using SIGVerse.Common;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupAvatarMotionPlayer : MonoBehaviour
	{
		private class UpdatingTransformData
		{
			public Transform UpdatingTransform { get; set; }
			public Vector3 Position { get; set; }
			public Vector3 Rotation { get; set; }
			public Vector3 Scale    { get; set; }

			public void UpdateTransform()
			{
				this.UpdatingTransform.position    = this.Position;
				this.UpdatingTransform.eulerAngles = this.Rotation;
				this.UpdatingTransform.localScale  = this.Scale;
			}
		}

		private class UpdatingTransformList
		{
			public float ElapsedTime { get; set; }
			private List<UpdatingTransformData> updatingTransformList;

			public UpdatingTransformList()
			{
				this.updatingTransformList = new List<UpdatingTransformData>();
			}

			public void AddUpdatingTransform(UpdatingTransformData updatingTransformData)
			{
				this.updatingTransformList.Add(updatingTransformData);
			}

			public List<UpdatingTransformData> GetUpdatingTransformList()
			{
				return this.updatingTransformList;
			}
		}

		private class EventData
		{
			public float  ElapsedTime { get; set; }
			public string EventTypeStr{ get; set; }

			public void SendEvent(GameObject sendingTarget)
			{
				// Send  Event data
				if(this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPointByLeft()
					);
				}
				else if(this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPointByRight()
					);
				}
				else if(this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPressA)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPressA()
					);
				}
				else if(this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPressX)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPressX()
					);
				}
			}
		}

		//-----------------------------------------------------

		private GameObject moderator;

		//-----------------------------------------------------
		private enum Step
		{
			Waiting,
			Initializing,
			Initialized, 
			Playing,
		}

		private Step step = Step.Waiting;

		private int numberOfTrials;

		private float elapsedTime = 0.0f;

		private List<Transform> targetTransforms;

		private Dictionary<string, Transform> targetObjectsPathMap  = new Dictionary<string, Transform>();

		private Queue<UpdatingTransformList> playingTransformQue;
		private Queue<EventData>             playingEventDataQue;


		void Awake()
		{
			ExecutionMode executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			switch (executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					this.enabled = false;
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}

		// Use this for initialization
		void Start()
		{
			CleanupAvatarMotionCommon common = this.GetComponent<CleanupAvatarMotionCommon>();

			this.moderator = GameObject.FindGameObjectWithTag("Moderator");

			this.targetTransforms = common.GetTargetTransforms();

			foreach (Transform targetTransform in this.targetTransforms)
			{
				this.targetObjectsPathMap.Add(CleanupAvatarMotionCommon.GetLinkPath(targetTransform), targetTransform);
			}
		}


		// Update is called once per frame
		void Update()
		{
			this.elapsedTime += Time.deltaTime;

			if (this.step == Step.Playing)
			{
				this.PlayMotions();
			}
		}

		public bool Initialize(int numberOfTrials)
		{
			if(this.step == Step.Waiting)
			{
				this.StartInitializing(numberOfTrials);

				return true;
			}

			return false;
		}

		public bool Play()
		{
			if (this.step == Step.Initialized)
			{
				this.StartPlaying();
				return true;
			}

			return false;
		}

		public bool Stop()
		{
			if (this.step == Step.Playing)
			{
				this.StopPlaying();
				return true;
			}

			return false;
		}

		private void StartInitializing(int numberOfTrials)
		{
			this.step = Step.Initializing;

			this.numberOfTrials = numberOfTrials;

			Thread threadWriteMotions = new Thread(new ParameterizedThreadStart(this.ReadDataFromFile));
			threadWriteMotions.Start(Application.dataPath);

			// Disable Rigidbodies and colliders
			foreach (Transform targetTransform in targetTransforms)
			{
				// Disable rigidbodies
				Rigidbody[] rigidbodies = targetTransform.GetComponentsInChildren<Rigidbody>(true);

				foreach (Rigidbody rigidbody in rigidbodies)
				{
					rigidbody.isKinematic     = true;
					rigidbody.velocity        = Vector3.zero;
					rigidbody.angularVelocity = Vector3.zero;
				}

				// Disable colliders
				Collider[] colliders = targetTransform.GetComponentsInChildren<Collider>(true);

				foreach (Collider collider in colliders)
				{
					collider.enabled = false;
				}
			}
		}

		private void ReadDataFromFile(object applicationDataPath)
		{
			try
			{
				string filePath = String.Format((string)applicationDataPath + CleanupAvatarMotionCommon.FilePath, this.numberOfTrials);

				if (!File.Exists(filePath))
				{
					SIGVerseLogger.Info("AvatarMotion file NOT found. Path=" + filePath);
					return;
				}

				// File open
				StreamReader streamReader = new StreamReader(filePath);

				this.playingTransformQue = new Queue<UpdatingTransformList>();
				this.playingEventDataQue = new Queue<EventData>();

				List<Transform> transformOrder = new List<Transform>();

				while (streamReader.Peek() >= 0)
				{
					string lineStr = streamReader.ReadLine();

					string[] columnArray = lineStr.Split(new char[] { '\t' }, 2);

					if (columnArray.Length < 2) { continue; }

					string headerStr = columnArray[0];
					string dataStr   = columnArray[1];

					string[] headerArray = headerStr.Split(',');

					// Motion data
					if (headerArray[1] == CleanupAvatarMotionCommon.DataType1Transform)
					{
						string[] dataArray = dataStr.Split('\t');

						// Definition
						if (headerArray[2] == CleanupAvatarMotionCommon.DataType2TransformDef)
						{
							transformOrder.Clear();

							SIGVerseLogger.Info("AvatarMotion player : transform data num=" + dataArray.Length);

							foreach (string transformPath in dataArray)
							{
								if (!this.targetObjectsPathMap.ContainsKey(transformPath))
								{
									SIGVerseLogger.Error("Couldn't find the object that path is " + transformPath);
								}

								transformOrder.Add(this.targetObjectsPathMap[transformPath]);
							}
						}
						// Value
						else if (headerArray[2] == CleanupAvatarMotionCommon.DataType2TransformVal)
						{
							if (transformOrder.Count == 0) { continue; }

							UpdatingTransformList timeSeriesMotionsData = new UpdatingTransformList();

							timeSeriesMotionsData.ElapsedTime = float.Parse(headerArray[0]);

							for (int i = 0; i < dataArray.Length; i++)
							{
								string[] transformValues = dataArray[i].Split(',');

								UpdatingTransformData transformPlayer = new UpdatingTransformData();
								transformPlayer.UpdatingTransform = transformOrder[i];

								transformPlayer.Position = new Vector3(float.Parse(transformValues[0]), float.Parse(transformValues[1]), float.Parse(transformValues[2]));
								transformPlayer.Rotation = new Vector3(float.Parse(transformValues[3]), float.Parse(transformValues[4]), float.Parse(transformValues[5]));

								if (transformValues.Length == 6)
								{
									transformPlayer.Scale = Vector3.one;
								}
								else if (transformValues.Length == 9)
								{
									transformPlayer.Scale = new Vector3(float.Parse(transformValues[6]), float.Parse(transformValues[7]), float.Parse(transformValues[8]));
								}

								timeSeriesMotionsData.AddUpdatingTransform(transformPlayer);
							}

							this.playingTransformQue.Enqueue(timeSeriesMotionsData);
						}
					}
					// Pointing Event data
					else if
					(
						headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft  || 
						headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight || 
						headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPressA       || 
						headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPressX
					){
						EventData eventData = new EventData();

						eventData.ElapsedTime  = float.Parse(headerArray[0]);
						eventData.EventTypeStr = headerArray[1];

						this.playingEventDataQue.Enqueue(eventData);
					}
				}

				streamReader.Close();

				SIGVerseLogger.Info("AvatarMotion player : File reading finished.");

				this.step = Step.Initialized;
			}
			catch (Exception ex)
			{
				SIGVerseLogger.Error(ex.Message);
				SIGVerseLogger.Error(ex.StackTrace);
				Application.Quit();
			}
		}


		private void StartPlaying()
		{
			SIGVerseLogger.Info("Start the avatar motion playing");

			this.step = Step.Playing;

			// Reset elapsed time
			this.elapsedTime = 0.0f;
		}

		private void StopPlaying()
		{
			SIGVerseLogger.Info("Stop the avatar motion playing");

			this.step = Step.Waiting;
		}

		void PlayMotions()
		{
			if (this.playingTransformQue.Count==0 && this.playingEventDataQue.Count==0)
			{
				this.Stop();
				return;
			}


			// Get Updating data for this frame
			Queue<UpdatingTransformList> updatingTransformQue = this.GetUpdatingTransformQueueInThisFrame();
			Queue<EventData>             sendingEventQue      = this.GetSendingEventQueueInThisFrame();

			while (updatingTransformQue.Count!=0 || sendingEventQue.Count!=0)
			{
				float transformTime = (updatingTransformQue.Count!=0)?  updatingTransformQue.Peek().ElapsedTime : float.MaxValue;
				float eventDataTime = (sendingEventQue     .Count!=0)?  sendingEventQue     .Peek().ElapsedTime : float.MaxValue;

				if(eventDataTime<=transformTime)
				{
					sendingEventQue.Dequeue().SendEvent(this.moderator);
				}
				else
				{
					UpdatingTransformList updatingTransformListInThisFrame = updatingTransformQue.Dequeue();

					foreach (UpdatingTransformData updatingTransformData in updatingTransformListInThisFrame.GetUpdatingTransformList())
					{
						updatingTransformData.UpdateTransform();
					}
				}
			}
		}

		private Queue<UpdatingTransformList> GetUpdatingTransformQueueInThisFrame()
		{
			// Use only latest transforms
			UpdatingTransformList updatingTransformList = null;

			while (this.playingTransformQue.Count!=0 && this.elapsedTime >= this.playingTransformQue.Peek().ElapsedTime)
			{
				updatingTransformList = this.playingTransformQue.Dequeue();
			}

			Queue<UpdatingTransformList> updatingTransformQue = new Queue<UpdatingTransformList>();

			if(updatingTransformList != null)
			{
				updatingTransformQue.Enqueue(updatingTransformList);
			}

			return updatingTransformQue;
		}

		private Queue<EventData> GetSendingEventQueueInThisFrame()
		{
			Queue<EventData> sendingEventQue = new Queue<EventData>();

			while (this.playingEventDataQue.Count!=0 && this.elapsedTime >= this.playingEventDataQue.Peek().ElapsedTime)
			{
				sendingEventQue.Enqueue(this.playingEventDataQue.Dequeue());
			}

			return sendingEventQue;
		}


		public bool IsInitialized()
		{
			return this.step == Step.Initialized;
		}

		public bool IsFinished()
		{
			return this.step == Step.Waiting;
		}
	}
}