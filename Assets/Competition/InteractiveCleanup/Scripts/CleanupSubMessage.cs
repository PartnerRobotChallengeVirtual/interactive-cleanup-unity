using UnityEngine;
using UnityEngine.EventSystems;
using SIGVerse.Common;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public interface IRosMsgReceiveHandler : IEventSystemHandler
	{
		void OnReceiveRosMessage(ROSBridge.interactive_cleanup.InteractiveCleanupMsg interactiveCleanupMsg);
	}

	public class CleanupSubMessage : RosSubMessage<ROSBridge.interactive_cleanup.InteractiveCleanupMsg>
	{
		override public void SubscribeMessageCallback(ROSBridge.interactive_cleanup.InteractiveCleanupMsg cleanupMsg)
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
