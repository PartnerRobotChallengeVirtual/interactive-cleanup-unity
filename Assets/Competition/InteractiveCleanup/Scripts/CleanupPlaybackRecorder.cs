using UnityEngine;


namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupPlaybackCommon))]
	public class CleanupPlaybackRecorder : WorldPlaybackRecorder
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

		// Use this for initialization
		void Start()
		{
			CleanupPlaybackCommon common = this.GetComponent<CleanupPlaybackCommon>();

			this.targetTransforms = common.GetTargetTransforms();
		}
	}
}


