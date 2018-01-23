using UnityEngine;
using UnityEngine.EventSystems;
using SIGVerse.ROSBridge;
using SIGVerse.Common;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public interface IRosMsgSendHandler : IEventSystemHandler
	{
		void OnSendRosMessage(string message, string detail);
	}

	public class CleanupPubMessage : RosPubMessage<ROSBridge.interactive_cleanup.InteractiveCleanupMsg>, IRosMsgSendHandler
	{
		public void OnSendRosMessage(string message, string detail)
		{
			SIGVerseLogger.Info("Sending message :" + message + ", " + detail);

			ROSBridge.interactive_cleanup.InteractiveCleanupMsg cleanupMsg = new ROSBridge.interactive_cleanup.InteractiveCleanupMsg();
			cleanupMsg.message = message;
			cleanupMsg.detail = detail;

			this.publisher.Publish(cleanupMsg);
		}
	}
}

