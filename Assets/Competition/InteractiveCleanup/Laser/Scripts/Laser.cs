using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class Laser : MonoBehaviour
	{
		public string   hitTargetName;
		public Vector3  hitPoint;

		public GameObject nearestGraspingObject;
		public GameObject nearestDestination;

		public Transform lookAtObject;

		// ---------------------------
		private List<GameObject> graspingCandidates;
		private List<GameObject> destinationCandidates;

		private LineRenderer lineRenderer;

		private float defaultWidthMultiplier;

		private bool isDeactivating = false;
		private bool isPointing     = false;

		void Awake()
		{
			this.lineRenderer = GetComponentInChildren<LineRenderer>();

			this.defaultWidthMultiplier = this.lineRenderer.widthMultiplier;

			this.graspingCandidates = new List<GameObject>();
			this.graspingCandidates.AddRange(GameObject.FindGameObjectsWithTag("GraspingCandidates"));
//			this.graspingCandidates.AddRange(GameObject.FindGameObjectsWithTag("DummyGraspingCandidates"));

			this.destinationCandidates = new List<GameObject>();
			this.destinationCandidates.AddRange(GameObject.FindGameObjectsWithTag("DestinationCandidates"));
		}

		void Start()
		{
			this.hitTargetName = string.Empty;
			this.hitPoint      = Vector3.zero;

			this.nearestGraspingObject = null;
			this.nearestDestination    = null;
		}

		void Update()
		{
			if(this.lookAtObject!=null)
			{
				this.transform.LookAt(lookAtObject);
			}

			RaycastHit hit;

			if (Physics.Raycast(transform.position, transform.forward, out hit))
			{
				if (hit.collider)
				{
					this.lineRenderer.SetPosition(1, new Vector3(0, 0, hit.distance));

					this.hitTargetName = hit.transform.name;
					this.hitPoint      = hit.point;

					float nearestDist;
					
					nearestDist = float.MaxValue;

					foreach (GameObject graspingCandidate in this.graspingCandidates)
					{
						float dist = Vector3.Distance(this.hitPoint, graspingCandidate.transform.position);

						if (dist < nearestDist)
						{
							this.nearestGraspingObject = graspingCandidate;
							nearestDist = dist;
						}
					}

					nearestDist = float.MaxValue;

					foreach (GameObject destinationCandidate in this.destinationCandidates)
					{
						float dist = Vector3.Distance(this.hitPoint, destinationCandidate.transform.position);

						if (dist < nearestDist)
						{
							this.nearestDestination = destinationCandidate;
							nearestDist = dist;
						}
					}
				}
			}
			else
			{
				this.lineRenderer.SetPosition(1, new Vector3(0, 0, 5000));
			}
		}

		public void Activate()
		{
			if(this.gameObject.activeSelf) { return; }

			this.gameObject.SetActive(true);

			this.lineRenderer.widthMultiplier = this.defaultWidthMultiplier;

			this.isDeactivating = false;
			this.isPointing     = false;
		}


		public void Deactivate()
		{
			if(!this.gameObject.activeSelf) { return; }

			if(!this.isDeactivating)
			{
				this.isDeactivating = true;

				StartCoroutine(this.DeactivateCoroutine());
			}
		}

		private IEnumerator DeactivateCoroutine()
		{
//			yield return new WaitForSeconds(0.1f);
			yield return null;

			if(this.gameObject.activeSelf)
			{
				this.isDeactivating = false;

				this.gameObject.SetActive(false);
			}
		}


		public string Point(bool isDeactivatedWhenFinished)
		{
			if(!this.gameObject.activeSelf) { return string.Empty; }

			if(!this.isPointing)
			{
				this.isPointing = true;
	
				StartCoroutine(this.PointCoroutine(isDeactivatedWhenFinished));

				return hitTargetName;
			}
			else
			{
				return string.Empty;
			}
		}

		private IEnumerator PointCoroutine(bool isDeactivatedWhenFinished)
		{
			this.lineRenderer.widthMultiplier = this.defaultWidthMultiplier * 5.0f;

			yield return new WaitForSeconds(0.5f);

			if(this.gameObject.activeSelf)
			{
				this.lineRenderer.widthMultiplier = this.defaultWidthMultiplier;

				this.isPointing = false;

				if(isDeactivatedWhenFinished)
				{
					this.gameObject.SetActive(false);
				}
			}
		}
	}
}
