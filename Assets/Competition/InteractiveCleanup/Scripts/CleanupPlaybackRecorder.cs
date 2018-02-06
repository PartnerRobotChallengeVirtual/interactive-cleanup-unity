using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupPlaybackCommon))]
	public class CleanupPlaybackRecorder : TrialPlaybackRecorder
	{
		void Awake()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == CleanupPlaybackCommon.PlaybackTypeRecord)
			{
			}
			else
			{
				this.enabled = false;
			}
		}
		
		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + CleanupPlaybackCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}
	}
}


