using UnityEngine;
using System;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupAvatarMotionCommon))]
	public class CleanupAvatarMotionRecorder : WorldPlaybackRecorder, IAvatarMotionHandler
	{
		protected override void Awake()
		{
			ExecutionMode executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			base.isRecord = executionMode == ExecutionMode.DataGeneration;

			base.Awake();
		}
		
		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + CleanupAvatarMotionCommon.FilePathFormat, numberOfTrials);

			return base.Initialize(filePath);
		}

		private string GetEventDataLine(string eventType)
		{
			return Math.Round(this.elapsedTime, 4, MidpointRounding.AwayFromZero) + "," + eventType + "," + CleanupAvatarMotionCommon.DataType2TransformVal + "\t";
		}

		public void OnAvatarPointByLeft()
		{
			if (this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft));
			}
		}

		public void OnAvatarPointByRight()
		{
			if (this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight));
			}
		}

		public void OnAvatarPressA()
		{
			if (this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPressA));
			}
		}

		public void OnAvatarPressX()
		{
			if (this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPressX));
			}
		}
	}
}

