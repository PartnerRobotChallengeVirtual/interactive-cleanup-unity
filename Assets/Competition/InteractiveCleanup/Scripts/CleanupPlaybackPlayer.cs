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

		// Use this for initialization
		void Start()
		{
			CleanupPlaybackCommon common = this.GetComponent<CleanupPlaybackCommon>();

			this.targetTransforms = common.GetTargetTransforms();

			foreach (Transform targetTransform in targetTransforms)
			{
				this.targetObjectsPathMap.Add(CleanupPlaybackCommon.GetLinkPath(targetTransform), targetTransform);
			}
		}
	}
}

