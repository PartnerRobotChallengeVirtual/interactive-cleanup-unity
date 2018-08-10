using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupAvatarMotionCommon))]
	public class CleanupAvatarMotionPlayer : WorldPlaybackPlayer
	{
		private PlaybackPointingEventController pointingController; // Pointing


		protected override void Awake()
		{
			ExecutionMode executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			this.isPlay = executionMode == ExecutionMode.Competition;

			base.Awake();
		}

		// Use this for initialization
		protected override void Start()
		{
			base.Start();  // Avatar motion data

//			this.transformController.IsRigidbodiesDisable = false;
			this.transformController.IsCollidersDisable   = false;

			GameObject moderator = GameObject.FindGameObjectWithTag("Moderator");

			this.pointingController = new PlaybackPointingEventController(moderator); // Pointing
		}


		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + CleanupAvatarMotionCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}


		protected override void ReadData(string[] headerArray, string dataStr)
		{
			base.ReadData(headerArray, dataStr);  // Avatar motion data

			this.pointingController.ReadEvents(headerArray, dataStr); // Pointing
		}


		protected override void StartInitializing()
		{
			base.StartInitializing();  // Avatar motion data

			this.pointingController.StartInitializingEvents(); // Pointing
		}


		protected override void UpdateIndexAndElapsedTime(float elapsedTime)
		{
			base.UpdateIndexAndElapsedTime(elapsedTime);  // Avatar motion data

			this.pointingController.UpdateIndex(elapsedTime); // Pointing
		}


		protected override void UpdateData()
		{
			base.UpdateData();  // Avatar motion data

			this.pointingController.ExecutePassedAllEvents(this.elapsedTime, this.deltaTime); // Pointing
		}


		protected override float GetTotalTime()
		{
			return Mathf.Max(base.GetTotalTime(), this.pointingController.GetTotalTime());
		}


		public bool IsInitialized()
		{
			return this.isInitialized;
		}

		public bool IsFinished()
		{
			return this.step == Step.Waiting;
		}


		public void ApplyFirstPostureOfAvatar()
		{
			base.transformController.ExecuteFirstEvent();
		}

		public List<PlaybackTransformEvent> GetFirstPostureOfAvatar()
		{
			return base.transformController.eventLists[0].EventList;
		}
	}
}

