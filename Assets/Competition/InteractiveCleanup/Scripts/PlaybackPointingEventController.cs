using UnityEngine;
using System;
using System.Collections.Generic;
using SIGVerse.Competition.InteractiveCleanup;
using UnityEngine.EventSystems;

namespace SIGVerse.Competition
{
	public class PlaybackPointingEvent : PlaybackEventBase
	{
		public string EventTypeStr { get; set; }
		public GameObject Destination{ get; set; }

		public void Execute()
		{
			// Send Pointing event
			if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft)
			{
				ExecuteEvents.Execute<IAvatarMotionHandler>
				(
					target: this.Destination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnAvatarPointByLeft()
				);
			}
			else if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight)
			{
				ExecuteEvents.Execute<IAvatarMotionHandler>
				(
					target: this.Destination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnAvatarPointByRight()
				);
			}
			else if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPressA)
			{
				ExecuteEvents.Execute<IAvatarMotionHandler>
				(
					target: this.Destination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnAvatarPressA()
				);
			}
			else if (this.EventTypeStr == CleanupAvatarMotionCommon.DataType1CleanupMsgPressX)
			{
				ExecuteEvents.Execute<IAvatarMotionHandler>
				(
					target: this.Destination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnAvatarPressX()
				);
			}
		}
	}


	public class PlaybackPointingEventList : PlaybackEventListBase<PlaybackPointingEvent>
	{
		public PlaybackPointingEventList()
		{
			this.EventList = new List<PlaybackPointingEvent>();
		}
	}

	// ------------------------------------------------------------------

	public class PlaybackPointingEventController : PlaybackEventControllerBase<PlaybackPointingEventList, PlaybackPointingEvent>
	{
		private GameObject destination;

		public PlaybackPointingEventController(GameObject destination)
		{
			this.destination = destination;
		}

		public override void StartInitializingEvents()
		{
			this.eventLists = new List<PlaybackPointingEventList>();
		}

		public override bool ReadEvents(string[] headerArray, string dataStr)
		{
			// Pointing event
			if(
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByLeft ||
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPointByRight ||
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPressA ||
				headerArray[1] == CleanupAvatarMotionCommon.DataType1CleanupMsgPressX
			){
				PlaybackPointingEvent PointingEvent = new PlaybackPointingEvent();

				PointingEvent.EventTypeStr = headerArray[1];
				PointingEvent.Destination  = this.destination;

				PlaybackPointingEventList PointingEventList = new PlaybackPointingEventList();
				PointingEventList.ElapsedTime = float.Parse(headerArray[0]);
				PointingEventList.EventList.Add(PointingEvent);

				this.eventLists.Add(PointingEventList);

				return true;
			}

			return false;
		}
	}
}

