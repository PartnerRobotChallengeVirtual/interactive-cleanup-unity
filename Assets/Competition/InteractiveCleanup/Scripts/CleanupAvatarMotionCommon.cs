using System;
using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupAvatarMotionCommon : WorldPlaybackCommon
	{
		public const string FilePathFormat = "/../SIGVerseConfig/InteractiveCleanup/AvatarMotion{0:D2}.dat";

		// Events
		public const string DataType1CleanupMsgPointByLeft  = "101";
		public const string DataType1CleanupMsgPointByRight = "102";
		public const string DataType1CleanupMsgPressA       = "103";
		public const string DataType1CleanupMsgPressX       = "104";
	}
}

