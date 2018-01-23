using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupLookAtAvatar : MonoBehaviour
	{
		public Transform avatarTransform;

		private Vector3 adjustPos;
		
		// Use this for initialization
		void Start ()
		{
			this.adjustPos = new Vector3(0, 1.0f, 0);
		}
	
		// Update is called once per frame
		void Update ()
		{
			this.transform.LookAt(this.avatarTransform.position + this.adjustPos);
		}
	}
}
