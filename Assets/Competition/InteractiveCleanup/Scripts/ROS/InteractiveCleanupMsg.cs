// Generated by gencs from interactive_cleanup/InteractiveCleanupMsg.msg
// DO NOT EDIT THIS FILE BY HAND!

using System;
using System.Collections;
using System.Collections.Generic;
using SIGVerse.RosBridge;
using UnityEngine;


namespace SIGVerse.RosBridge
{
	namespace interactive_cleanup
	{

		[System.Serializable]
		public class InteractiveCleanupMsg : RosMessage
		{
			public string message;
			public string detail;


			public InteractiveCleanupMsg()
			{
				this.message = "";
				this.detail  = "";

			}

			public InteractiveCleanupMsg(string message, string detail)
			{
				this.message = message;
				this.detail  = detail;
			}

			new public static string GetMessageType()
			{
				return "interactive_cleanup/InteractiveCleanupMsg";
			}

			new public static string GetMD5Hash()
			{
				return "83c3ad4b113aebdb7a85eba9ba595d50";
			}

		} // class InteractiveCleanupMsg

	} // namespace interactive_cleanup

} // namespace ROSBridgeLib
