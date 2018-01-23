using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using SIGVerse.Common;
using System.Collections;
using SIGVerse.ToyotaHSR;
using System.Linq;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupAvatarMotionRecorder : MonoBehaviour, IAvatarMotionHandler
	{
		[TooltipAttribute("milliseconds")]
		public int recordInterval = 20;

		//-----------------------------------------------------
		private enum Step
		{
			Waiting,
			Initializing,
			Initialized, 
			Recording,
			Writing,
		}

		private Step step = Step.Waiting;

		private List<Transform> targetTransforms;

		private int numberOfTrials;

		private float elapsedTime = 0.0f;
		private float previousRecordedTime = 0.0f;

		private string filePath;

		private List<string> dataLines;


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

		// Use this for initialization
		void Start()
		{
			CleanupAvatarMotionCommon common = this.GetComponent<CleanupAvatarMotionCommon>();

			this.targetTransforms = common.GetTargetTransforms();
		}


		// Update is called once per frame
		void Update()
		{
			this.elapsedTime += Time.deltaTime;

			if (this.step == Step.Recording)
			{
				this.SaveMotions();
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

		public bool Record()
		{
			if (this.step == Step.Initialized)
			{
				this.StartRecording();
				return true;
			}

			return false;
		}

		public bool Stop()
		{
			if (this.step == Step.Recording)
			{
				this.StopRecording();
				return true;
			}

			return false;
		}

		private void StartInitializing(int numberOfTrials)
		{
			this.step = Step.Initializing;

			this.numberOfTrials = numberOfTrials;

			this.filePath = String.Format(Application.dataPath + CleanupAvatarMotionCommon.FilePath, this.numberOfTrials);

			SIGVerseLogger.Info("Output AvatarMotion file Path=" + this.filePath);

			// File open
			StreamWriter streamWriter = new StreamWriter(this.filePath, false);

			// Write header line and get transform instances
			string definitionLine = string.Empty;

			definitionLine += "0.0," + CleanupAvatarMotionCommon.DataType1Transform + "," + CleanupAvatarMotionCommon.DataType2TransformDef; // Elapsed time is dummy.

			foreach (Transform targetTransform in this.targetTransforms)
			{
				// Make a header line
				definitionLine += "\t" + CleanupAvatarMotionCommon.GetLinkPath(targetTransform);
			}

			streamWriter.WriteLine(definitionLine);

			streamWriter.Flush();
			streamWriter.Close();

			this.dataLines = new List<string>();

			this.step = Step.Initialized;
		}

		private void StartRecording()
		{
			SIGVerseLogger.Info("Start the avatar motion recording");

			this.step = Step.Recording;

			// Reset elapsed time
			this.elapsedTime = 0.0f;
			this.previousRecordedTime = 0.0f;
		}

		private void StopRecording()
		{
			SIGVerseLogger.Info("Stop the avatar motion recording");

			this.step = Step.Writing;

			Thread threadWriteData = new Thread(new ThreadStart(this.WriteDataToFile));
			threadWriteData.Start();
		}

		private void WriteDataToFile()
		{
			try
			{
				StreamWriter streamWriter = new StreamWriter(this.filePath, true);

				foreach (string dataLine in dataLines)
				{
					streamWriter.WriteLine(dataLine);
				}

				streamWriter.Flush();
				streamWriter.Close();

				this.step = Step.Waiting;
			}
			catch(Exception ex)
			{
				SIGVerseLogger.Error(ex.Message);
				SIGVerseLogger.Error(ex.StackTrace);
				Application.Quit();
			}
		}

		private void SaveMotions()
		{
			if (1000.0 * (this.elapsedTime - this.previousRecordedTime) < recordInterval) { return; }

			string dataLine = string.Empty;

			dataLine += Math.Round(this.elapsedTime, 4, MidpointRounding.AwayFromZero) + "," + CleanupAvatarMotionCommon.DataType1Transform + "," + CleanupAvatarMotionCommon.DataType2TransformVal;

			foreach (Transform transform in this.targetTransforms)
			{
				dataLine += "\t" +
					Math.Round(transform.position.x,    4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.position.y,    4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.position.z,    4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.eulerAngles.x, 4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.eulerAngles.y, 4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.eulerAngles.z, 4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.localScale.x,  4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.localScale.y,  4, MidpointRounding.AwayFromZero) + "," +
					Math.Round(transform.localScale.z,  4, MidpointRounding.AwayFromZero);
			}

			this.dataLines.Add(dataLine);

			this.previousRecordedTime = this.elapsedTime;
		}


		private string GetEventDataLine(string eventType)
		{
			return Math.Round(this.elapsedTime, 4, MidpointRounding.AwayFromZero) + "," + eventType + "," + CleanupAvatarMotionCommon.DataType2TransformVal + "\t";
		}

		public void OnAvatarPointByLeft()
		{
			if(this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft));
			}
		}
		
		public void OnAvatarPointByRight()
		{
			if(this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight));
			}
		}
		
		public void OnAvatarPressA()
		{
			if(this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPressA));
			}
		}
		
		public void OnAvatarPressX()
		{
			if(this.step == Step.Recording)
			{
				this.dataLines.Add(this.GetEventDataLine(CleanupAvatarMotionCommon.DataType1CleanupMsgPressX));
			}
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

