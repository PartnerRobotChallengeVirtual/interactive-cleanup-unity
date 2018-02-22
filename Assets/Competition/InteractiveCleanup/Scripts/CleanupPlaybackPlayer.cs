using UnityEngine;
using UnityEngine.UI;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[RequireComponent(typeof (CleanupPlaybackCommon))]
	public class CleanupPlaybackPlayer : TrialPlaybackPlayer
	{
		[HeaderAttribute("Interactive Cleanup Objects")]
		public CleanupScoreManager scoreManager;

		protected override void Awake()
		{
			base.isPlay = CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay;

			base.Awake();

			if(base.isPlay)
			{
				Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

//				robot.Find("CompetitionScripts").gameObject.SetActive(false);
				robot.Find("RosBridgeScripts")  .gameObject.SetActive(false);

				Transform moderator = GameObject.FindGameObjectWithTag("Moderator").transform;

				moderator.GetComponent<CleanupModerator>() .enabled = false;
				moderator.GetComponent<CleanupPubMessage>().enabled = false;
				moderator.GetComponent<CleanupSubMessage>().enabled = false;

				this.scoreManager.enabled = false;

				foreach(GameObject graspingCandidatePosition in GameObject.FindGameObjectsWithTag("GraspingCandidatesPosition"))
				{
					graspingCandidatePosition.SetActive(false);
				}
			}
		}


		public override void OnReadFileButtonClick()
		{
			base.trialNo = int.Parse(base.trialNoInputField.text);

			string filePath = string.Format(Application.dataPath + CleanupPlaybackCommon.FilePathFormat, base.trialNo);

			base.Initialize(filePath);
		}
	}
}

