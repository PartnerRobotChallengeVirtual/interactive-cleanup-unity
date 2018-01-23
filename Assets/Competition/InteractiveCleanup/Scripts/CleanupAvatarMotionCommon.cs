using System;
using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupAvatarMotionCommon : MonoBehaviour
	{
		public const string FilePath = "/../SIGVerseConfig/InteractiveCleanup/AvatarMotion{0:D2}.dat";

		// Status
		public const string DataType1Transform = "11";

		// Events
		public const string DataType1CleanupMsgPointByLeft  = "21";
		public const string DataType1CleanupMsgPointByRight = "22";
		public const string DataType1CleanupMsgPressA       = "23";
		public const string DataType1CleanupMsgPressX       = "24";

		//public const string DataType1CleanupMsgPickUp  = "21";
		//public const string DataType1CleanupMsgCleanUp = "22";

		public const string DataType2TransformDef = "0";
		public const string DataType2TransformVal = "1";

		private List<Transform> targetTransforms;

		void Awake()
		{
			this.targetTransforms = new List<Transform>();
			
			Transform moderator = GameObject.FindGameObjectWithTag("Moderator").transform;

			Transform[] avatarTransforms = moderator.GetComponentsInChildren<Transform>(true);

			foreach (Transform avatarTransform in avatarTransforms)
			{
				this.targetTransforms.Add(avatarTransform);
			}
		}

		public List<Transform> GetTargetTransforms()
		{
			return this.targetTransforms;
		}


		public static string GetLinkPath(Transform transform)
		{
			string path = transform.name;

			while (transform.parent != null)
			{
				transform = transform.parent;
				path = transform.name + "/" + path;
			}

			return path;
		}
	}
}

