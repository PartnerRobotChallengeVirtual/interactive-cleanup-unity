using UnityEngine;
using UnityEngine.EventSystems;
using SIGVerse.RosBridge;
using SIGVerse.Common;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public interface IRosMsgSendHandler : IEventSystemHandler
	{
		void OnSendRosMessage(string message, string detail);
	}

	public class CleanupPubMessage : RosPubMessage<RosBridge.interactive_cleanup.InteractiveCleanupMsg>, IRosMsgSendHandler
	{
		public void OnSendRosMessage(string message, string detail)
		{
			SIGVerseLogger.Info("Sending message :" + message + ", " + detail);

			RosBridge.interactive_cleanup.InteractiveCleanupMsg cleanupMsg = new RosBridge.interactive_cleanup.InteractiveCleanupMsg();
			cleanupMsg.message = message;
			cleanupMsg.detail = detail;

			this.publisher.Publish(cleanupMsg);
		}
	}
}

