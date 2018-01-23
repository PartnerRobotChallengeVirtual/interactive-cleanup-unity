using SIGVerse.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class ContactChecker : MonoBehaviour
	{
		private const float MaxWaitingTime = 3.0f;
		private const string JudgeTriggersName = "JudgeTriggers";

		private List<BoxCollider> triggers;

		private GameObject targetObject = null;

		private bool hasTargetCollided  = false;
		private bool shouldCheck = false;

		void Start ()
		{
			this.triggers = new List<BoxCollider>();

			this.triggers.AddRange(GetBoxColliders(this.transform));
		}

		public IEnumerator IsTargetContact(GameObject target)
		{
			this.targetObject = target;

			Rigidbody targetRigidbody = this.targetObject.GetComponent<Rigidbody>();

			float timeLimit = Time.time + MaxWaitingTime;

			while (!targetRigidbody.IsSleeping() && Time.time < timeLimit)
			{
				yield return null;
			}
		
			if(Time.time >= timeLimit)
			{
				SIGVerseLogger.Info("Target deployment failed: Time out.");

				yield return false;
			}
			else
			{
				this.shouldCheck = true;

				targetRigidbody.WakeUp();

				SIGVerseLogger.Info("Wakeup the target rigidbody");

				while (!this.hasTargetCollided && Time.time < timeLimit)
				{
					yield return null;
				}

				if(Time.time >= timeLimit)
				{
					SIGVerseLogger.Info("Target deployment failed: Time out.");
				}

				this.shouldCheck = false;

				yield return hasTargetCollided;
			}
		}

		public void ResetParam()
		{
			this.targetObject      = null;
			this.hasTargetCollided = false;
			this.shouldCheck       = false;
		}

		private static BoxCollider[] GetBoxColliders(Transform rootTransform)
		{
			Transform judgeTriggersTransform = rootTransform.transform.Find(JudgeTriggersName);

			if (judgeTriggersTransform==null)
			{
				throw new Exception("No Judge Triggers object.");
			}

			BoxCollider[] boxColliders = judgeTriggersTransform.GetComponents<BoxCollider>();
			
			if(boxColliders.Length==0)
			{
				throw new Exception("No Box colliders.");
			}
			
			return boxColliders;
		}


		private void OnTriggerStay(Collider other)
		{
			if (this.shouldCheck)
			{
				if (other.attachedRigidbody == null) { return; }

				if (other.attachedRigidbody.gameObject == this.targetObject)
				{
					Debug.Log("OnTriggerStay  time=" + Time.time + ", name=" + other.attachedRigidbody.gameObject.name);

					hasTargetCollided = true;
				}
			}
		}

		//private void OnTriggerEnter(Collider other)
		//{
		//	if (this.shouldCheck)
		//	{
		//		Debug.Log("in OnTriggerEnter");

		//		if (other.attachedRigidbody == null) { return; }

		//		if (other.attachedRigidbody.gameObject == this.targetObject)
		//		{
		//			Debug.Log("OnTriggerEnter  time=" + Time.time + ", name=" + other.attachedRigidbody.gameObject.name);

		//			hasTargetCollided = true;
		//		}
		//	}
		//}
	}
}


