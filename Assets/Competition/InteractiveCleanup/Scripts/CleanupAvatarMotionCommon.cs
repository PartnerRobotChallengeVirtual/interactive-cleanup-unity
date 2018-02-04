using System;
using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupAvatarMotionCommon : WorldPlaybackCommon
	{
		private const string FilePathFormat = "/../SIGVerseConfig/InteractiveCleanup/AvatarMotion{0:D2}.dat";

		// Events
		public const string DataType1CleanupMsgPointByLeft  = "21";
		public const string DataType1CleanupMsgPointByRight = "22";
		public const string DataType1CleanupMsgPressA       = "23";
		public const string DataType1CleanupMsgPressX       = "24";

		public static string GetFilePath(int numberOfTrials)
		{
			return string.Format(Application.dataPath + FilePathFormat, numberOfTrials);
		}
	}
}

