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

			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.base_footprint      .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.arm_lift_link       .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.arm_flex_link       .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.arm_roll_link       .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.wrist_flex_link     .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.wrist_roll_link     .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.head_pan_link       .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.head_tilt_link      .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.torso_lift_link     .ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.hand_l_proximal_link.ToString()));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.Link.hand_r_proximal_link.ToString()));
		}
	}
}

