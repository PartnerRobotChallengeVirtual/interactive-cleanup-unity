using UnityEngine;
using UnityEngine.EventSystems;
using SIGVerse.Common;
using SIGVerse.RosBridge;
using System.Collections.Generic;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public interface IRosMsgReceiveHandler : IEventSystemHandler
	{
		void OnReceiveRosMessage(RosBridge.interactive_cleanup.InteractiveCleanupMsg interactiveCleanupMsg);
	}

	public class CleanupSubMessage : RosSubMessage<RosBridge.interactive_cleanup.InteractiveCleanupMsg>
	{
		public List<GameObject> destinations;

		protected override void SubscribeMessageCallback(RosBridge.interactive_cleanup.InteractiveCleanupMsg cleanupMsg)
		{
			SIGVerseLogger.Info("Received message :"+cleanupMsg.message);

			foreach(GameObject destination in this.destinations)
			{
				ExecuteEvents.Execute<IRosMsgReceiveHandler>
				(
					target: destination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnReceiveRosMessage(cleanupMsg)
				);
			}
		}
	}
}
