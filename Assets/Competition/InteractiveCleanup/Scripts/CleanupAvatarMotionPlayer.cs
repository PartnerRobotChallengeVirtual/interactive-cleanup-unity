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
	[RequireComponent(typeof (CleanupAvatarMotionCommon))]
	public class CleanupAvatarMotionPlayer : WorldPlaybackPlayer
	{
		private class EventData
		{
			public float ElapsedTime { get; set; }
			public string EventTypeStr { get; set; }

			public void SendEvent(GameObject sendingTarget)
			{
				// Send  Event data
				if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPointByLeft()
					);
				}
				else if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPointByRight()
					);
				}
				else if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPressA)
				{
					ExecuteEvents.Execute<IAvatarMotionHandler>
					(
						target: sendingTarget,
						eventData: null,
						functor: (reciever, eventData) => reciever.OnAvatarPressA()
					);
				}
				else if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPressX)
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

		private Queue<EventData> playingEventDataQue;

		//-----------------------------------------------------

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

		public bool Initialize(int numberOfTrials)
		{
			return this.Initialize(CleanupAvatarMotionCommon.GetFilePath(numberOfTrials));
		}

		// Use this for initialization
		protected override void Start()
		{
			base.Start();

			this.moderator = GameObject.FindGameObjectWithTag("Moderator");

			this.playingEventDataQue = new Queue<EventData>();
		}


		protected override void ReadData(string[] headerArray, string dataStr)
		{
			// Motion data
			if (headerArray[1] == WorldPlaybackCommon.DataType1Transform)
			{
				base.ReadTransforms(headerArray, dataStr);
			}
			// Pointing Event data
			else
			{
				this.ReadEvent(headerArray, dataStr);
			}
		}

		private bool ReadEvent(string[] headerArray, string dataStr)
		{
			if
			(
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft ||
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight ||
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPressA ||
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPressX
			)
			{
				EventData eventData = new EventData();

				eventData.ElapsedTime = float.Parse(headerArray[0]);
				eventData.EventTypeStr = headerArray[1];

				this.playingEventDataQue.Enqueue(eventData);

				return true;
			}

			return false;
		}


		protected override void UpdateData()
		{
			if (this.playingTransformQue.Count == 0 && this.playingEventDataQue.Count == 0)
			{
				this.Stop();
				return;
			}

			// Get Updating data for this frame
			Queue<UpdatingTransformList> updatingTransformQue = this.GetUpdatingTransformQueueInThisFrame();
			Queue<EventData> sendingEventQue = this.GetSendingEventQueueInThisFrame();

			while (updatingTransformQue.Count != 0 || sendingEventQue.Count != 0)
			{
				float transformTime = (updatingTransformQue.Count != 0) ? updatingTransformQue.Peek().ElapsedTime : float.MaxValue;
				float eventDataTime = (sendingEventQue.Count != 0) ? sendingEventQue.Peek().ElapsedTime : float.MaxValue;

				if (eventDataTime <= transformTime)
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

			while (this.playingTransformQue.Count != 0 && this.elapsedTime >= this.playingTransformQue.Peek().ElapsedTime)
			{
				updatingTransformList = this.playingTransformQue.Dequeue();
			}

			Queue<UpdatingTransformList> updatingTransformQue = new Queue<UpdatingTransformList>();

			if (updatingTransformList != null)
			{
				updatingTransformQue.Enqueue(updatingTransformList);
			}

			return updatingTransformQue;
		}

		private Queue<EventData> GetSendingEventQueueInThisFrame()
		{
			Queue<EventData> sendingEventQue = new Queue<EventData>();

			while (this.playingEventDataQue.Count != 0 && this.elapsedTime >= this.playingEventDataQue.Peek().ElapsedTime)
			{
				sendingEventQue.Enqueue(this.playingEventDataQue.Dequeue());
			}

			return sendingEventQue;
		}
	}
}

