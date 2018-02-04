using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupPlaybackCommon))]
	public class CleanupPlaybackPlayer : WorldPlaybackPlayer
	{
		void Awake()
		{
			if (CleanupConfig.Instance.configFileInfo.playbackType == CleanupPlaybackCommon.PlaybackTypePlay)
			{
				Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

				robot.Find("CompetitionScripts").gameObject.SetActive(false);
				robot.Find("RosBridgeScripts")  .gameObject.SetActive(false);

				GameObject mainMenu = GameObject.FindGameObjectWithTag("MainMenu");

				mainMenu.GetComponentInChildren<CleanupScoreManager>().enabled = false;

				foreach(GameObject graspingCandidatePosition in GameObject.FindGameObjectsWithTag("GraspingCandidatesPosition"))
				{
					graspingCandidatePosition.SetActive(false);
				}
			}
			else
			{
				this.enabled = false;
			}
		}

		public bool Initialize()
		{
			string filePath = string.Format(Application.dataPath + CleanupPlaybackCommon.FilePathFormat, 0);

			return this.Initialize(filePath);
		}
	}
}

