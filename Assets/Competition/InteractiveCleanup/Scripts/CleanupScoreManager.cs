using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using SIGVerse.Common;
using SIGVerse.ToyotaHSR;
using UnityEngine.EventSystems;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public static class Score
	{
		public const int MaxScore = +999;
		public const int MinScore = -999;

		public enum Type
		{
			CleanupSuccess,
			AskedCorrectOrNot,
			HsrCollisionEnter,
			ObjectCollisionEnter,
		}

		public static int GetScore(Type scoreType, params object[] args)
		{
			switch(scoreType)
			{
				case Score.Type.CleanupSuccess      : { return +100; }
				case Score.Type.AskedCorrectOrNot   : { return - 10; }
				case Score.Type.HsrCollisionEnter   : { return - 10; }
				case Score.Type.ObjectCollisionEnter: { return GetObjectCollisionScore((Collision)args[0]); }
			}

			throw new Exception("Illegal score type. Type = " + (int)scoreType + ", method name=(" + System.Reflection.MethodBase.GetCurrentMethod().Name + ")");
		}

		public static float GetObjectCollisionVeloticyThreshold()
		{
			return 1.0f;
		}

		private static int GetObjectCollisionScore(Collision collision)
		{
			return Mathf.FloorToInt((collision.relativeVelocity.magnitude - 1.0f) * -10);
		}
	}

	public class CleanupScoreManager : MonoBehaviour, IHSRCollisionHandler, ITransferredCollisionHandler
	{
		private const float DefaultTimeScale = 1.0f;

		public List<GameObject> scoreNotificationDestinations;

		//---------------------------------------------------
		private GameObject mainMenu;

		private int score;


		void Awake()
		{
			this.mainMenu = GameObject.FindGameObjectWithTag("MainMenu");
		}

		// Use this for initialization
		void Start()
		{
			this.UpdateScoreText(0, CleanupConfig.Instance.GetTotalScore());

			this.score = 0;

			Time.timeScale = 0.0f;
		}


		public void AddScore(Score.Type scoreType, params object[] args)
		{
			int additionalScore = Score.GetScore(scoreType, args);

			this.score = Mathf.Clamp(this.score + additionalScore, Score.MinScore, Score.MaxScore);

			this.UpdateScoreText(this.score);

			SIGVerseLogger.Info("Score add [" + additionalScore + "], Challenge " + CleanupConfig.Instance.numberOfTrials + " Score=" + this.score);

			// Send the Score Notification
			ScoreStatus scoreStatus = new ScoreStatus(additionalScore, this.score, CleanupConfig.Instance.GetTotalScore());

			foreach(GameObject scoreNotificationDestination in this.scoreNotificationDestinations)
			{
				ExecuteEvents.Execute<IScoreHandler>
				(
					target: scoreNotificationDestination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnScoreChange(scoreStatus)
				);
			}
		}

		public void TaskStart()
		{
			this.UpdateScoreText(this.score);

			Time.timeScale = CleanupScoreManager.DefaultTimeScale;
		}

		public void TaskEnd()
		{
			Time.timeScale = 0.0f;

			CleanupConfig.Instance.AddScore(this.score);

			this.UpdateScoreText(this.score, CleanupConfig.Instance.GetTotalScore());

			SIGVerseLogger.Info("Total Score=" + CleanupConfig.Instance.GetTotalScore().ToString());

			CleanupConfig.Instance.RecordScoreInFile();
		}


		private void UpdateScoreText(float score)
		{
			ExecuteEvents.Execute<IPanelScoreHandler>
			(
				target: this.mainMenu,
				eventData: null,
				functor: (reciever, eventData) => reciever.OnScoreChange(score)
			);
		}

		private void UpdateScoreText(float score, float total)
		{
			ExecuteEvents.Execute<IPanelScoreHandler>
			(
				target: this.mainMenu,
				eventData: null,
				functor: (reciever, eventData) => reciever.OnScoreChange(score, total)
			);
		}


		public void OnHsrCollisionEnter(Collision collision)
		{
			this.AddScore(Score.Type.HsrCollisionEnter);
		}

		public void OnTransferredCollisionEnter(Collision collision)
		{
			foreach(ContactPoint contactPoint in collision.contacts)
			{
				if(contactPoint.otherCollider.CompareTag("NoDeductionCollider")){ return; }
			}

			SIGVerseLogger.Info("Object collision occurred. name=" + collision.contacts[0].thisCollider.name);

			this.AddScore(Score.Type.ObjectCollisionEnter, collision);
		}
	}
}
