using UnityEngine;
using SIGVerse.ToyotaHSR;
using SIGVerse.Common;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupPlaybackCommon : TrialPlaybackCommon
	{
		public const string FilePathFormat = "/../SIGVerseConfig/InteractiveCleanup/Playback{0:D2}.dat";

		protected override void Awake()
		{
			if(CleanupConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay)
			{
				// Activate all grasping candidates
				GameObject graspingCandidatesObj = GameObject.Find("GraspingCandidates");

				foreach (Transform graspingCandidate in graspingCandidatesObj.transform)
				{
					graspingCandidate.gameObject.SetActive(true);

					graspingCandidate.position = new Vector3(0.0f, -5.0f, 0.0f); // Wait in the ground

					// Disable rigidbodies
					Rigidbody[] rigidbodies = graspingCandidate.GetComponentsInChildren<Rigidbody>(true);
					foreach (Rigidbody rigidbody in rigidbodies) { rigidbody.isKinematic = true; }

					// Disable colliders
					Collider[] colliders = graspingCandidate.GetComponentsInChildren<Collider>(true);
					foreach (Collider collider in colliders) { collider.enabled = false; }
				}
			}

			base.Awake();

			// Robot
			Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

			this.targetTransforms.Add(robot);

			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.BaseFootPrintName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.ArmLiftLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.ArmFlexLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.ArmRollLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.WristFlexLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.WristRollLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HeadPanLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HeadTiltLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.TorsoLiftLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HandLProximalLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HandRProximalLinkName));
		}
	}
}

