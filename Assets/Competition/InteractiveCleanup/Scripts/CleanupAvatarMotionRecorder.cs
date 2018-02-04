using UnityEngine;
using System;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupAvatarMotionCommon))]
	public class CleanupAvatarMotionRecorder : WorldPlaybackRecorder, IAvatarMotionHandler
	{
		void Awake()
		{
			ExecutionMode executionMode = (ExecutionMode)Enum.ToObject(typeof(ExecutionMode), CleanupConfig.Instance.configFileInfo.executionMode);

			switch (executionMode)
			{
				// For the competition. Read generated data.
				case ExecutionMode.Competition:
				{
					this.enabled = false;
					break;
				}
				// For data generation. 
				case ExecutionMode.DataGeneration:
				{
					break;
				}
				default:
				{
					throw new Exception("Illegal Execution mode. mode=" + CleanupConfig.Instance.configFileInfo.executionMode);
				}
			}
		}
		
		public bool Initialize(int numberOfTrials)
		{
			return this.Initialize(CleanupAvatarMotionCommon.GetFilePath(numberOfTrials));
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

