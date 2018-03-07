using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupPlaybackCommon))]
	public class CleanupPlaybackRecorder : TrialPlaybackRecorder
	{
		protected override void Awake()
		{
			this.isRecord = CleanupConfig.Instance.configFileInfo.playbackType == CleanupPlaybackCommon.PlaybackTypeRecord;

			base.Awake();
		}
		
		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + CleanupPlaybackCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}
	}
}


