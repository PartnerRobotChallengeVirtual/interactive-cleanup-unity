using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using SIGVerse.Common;
using SIGVerse.Competition;
using SIGVerse.ToyotaHSR;
using UnityEngine.UI;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public enum ModeratorStep
	{
		Initialize,
		WaitForStart,
		WaitForIamReady, 
		TaskStart,
		SendingPickItUpMsg,
		SendingCleanUpMsg,
		WaitForTaskFinished,
		Judgement,
		WaitForNextTask,
	}

	public class CleanupModerator : MonoBehaviour, IRosMsgReceiveHandler, IAvatarMotionHandler
	{
		private const int SendingAreYouReadyInterval = 1000;

		private readonly Color GreenColor = new Color(  0/255f, 143/255f, 36/255f, 255/255f);
		private readonly Color RedColor   = new Color(255/255f,   0/255f,  0/255f, 255/255f);

		private const string MsgAreYouReady     = "Are_you_ready?";
		private const string MsgPickItUp        = "Pick_it_up!";
		private const string MsgCleanUp         = "Clean_up!";
		private const string MsgYes             = "Yes";
		private const string MsgNo              = "No";
		private const string MsgTaskSucceeded   = "Task_succeeded";
		private const string MsgTaskFailed      = "Task_failed";
		private const string MsgMissionComplete = "Mission_complete";

		private const string ReasonTimeIsUp = "Time_is_up";
		private const string ReasonGiveUp   = "Give_up";

		private const string MsgIamReady      = "I_am_ready";
		private const string MsgIsThisCorrect = "Is_this_correct?";
		private const string MsgTaskFinished  = "Task_finished";

		//-----------------------------

		public List<GameObject> environments;

		public GameObject avatarMotionPlayback;
		public GameObject worldPlayback;

		public Laser laserLeft;
		public Laser laserRight;

		//-----------------------------

		private CleanupModeratorTool tool;
		private StepTimer stepTimer;

		private CleanupMenu cleanupMenu;
		private CleanupScoreManager scoreManager;


		private ExecutionMode executionMode;
		private ModeratorStep step;

		private Dictionary<string, bool> receivedMessageMap;

		private bool isAllTaskFinished;
		private string interruptedReason;

		private float noticeHideTime;


		void Awake()
		{
			try
			{
				this.tool = new CleanupModeratorTool(this.environments);

				this.stepTimer = new StepTimer();

				this.tool.InitPlaybackVariables(this.avatarMotionPlayback, this.worldPlayback);

				this.executionMode = this.tool.GetExecutionMode();

				GameObject mainMenu = GameObject.FindGameObjectWithTag("MainMenu");

				this.cleanupMenu  = mainMenu.GetComponent<CleanupMenu>();
				this.scoreManager = mainMenu.GetComponent<CleanupScoreManager>();

				if(this.executionMode==ExecutionMode.Competition)
				{
					this.scoreManager.SetTaskMessageText(this.tool.GetTaskDetail());
				}
			}
			catch (Exception exception)
			{
				Debug.LogError(exception);
				SIGVerseLogger.Error(exception.Message);
				SIGVerseLogger.Error(exception.StackTrace);
				this.ApplicationQuitAfter1sec();
			}
		}


		// Use this for initialization
		void Start()
		{
			this.step = ModeratorStep.Initialize;

			this.isAllTaskFinished = false;
			this.interruptedReason = string.Empty;
			this.noticeHideTime    = 0.0f;


			List<GameObject> graspables = this.tool.GetGraspables();

			for (int i=0; i<graspables.Count; i++)
			{
				Rigidbody rigidbody = graspables[i].GetComponent<Rigidbody>();

				rigidbody.constraints
					= RigidbodyConstraints.FreezeRotation |
					  RigidbodyConstraints.FreezePositionX |
					  RigidbodyConstraints.FreezePositionZ;

				rigidbody.maxDepenetrationVelocity = 0.5f;

				StartCoroutine(this.tool.LoosenRigidbodyConstraints(rigidbody));
			}
		}


		private void PreProcess()
		{
			this.scoreManager.SetChallengeInfoText();

			SIGVerseLogger.Info("##### " + this.scoreManager.GetChallengeInfoText() + " #####");

			this.scoreManager.ResetTimeLeftText();


			this.receivedMessageMap = new Dictionary<string, bool>();
			this.receivedMessageMap.Add(MsgIamReady,      false);
			this.receivedMessageMap.Add(MsgIsThisCorrect, false);
			this.receivedMessageMap.Add(MsgTaskFinished,  false);

			this.tool.InitializePlayback();

			SIGVerseLogger.Info("End of PreProcess. Trial No=" + CleanupConfig.Instance.numberOfTrials);
		}


		private void PostProcess()
		{
			SIGVerseLogger.Info("Task end");

			if (CleanupConfig.Instance.numberOfTrials == CleanupConfig.Instance.configFileInfo.maxNumberOfTrials)
			{
				this.SendRosMessage(MsgMissionComplete, "");

				SIGVerseLogger.Info("All tasks finished.");
				this.isAllTaskFinished = true;
			}
		}

		// Update is called once per frame
		void Update ()
		{
			try
			{
				if(this.isAllTaskFinished) { return; }

				if(this.interruptedReason!=string.Empty && this.step != ModeratorStep.WaitForNextTask)
				{
					SIGVerseLogger.Info("Failed '" + this.interruptedReason + "'");

					this.ShowNotice("Failed\n"+ interruptedReason.Replace('_',' '), 100, RedColor);

					this.GoToNextTaskTaskFailed(this.interruptedReason);
				}

				switch (this.step)
				{
					case ModeratorStep.Initialize:
					{
						SIGVerseLogger.Info("Initialize");
						this.PreProcess();
						this.step++;
						break;
					}
					case ModeratorStep.WaitForStart:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 3000))
						{
							if(!this.tool.IsPlaybackInitialized()) { break; }

							this.step++;
						}

						break;
					}
					case ModeratorStep.WaitForIamReady:
					{
						if (this.receivedMessageMap[MsgIamReady])
						{
							this.step++;
							break;
						}

						if (this.stepTimer.IsTimePassed((int)this.step, SendingAreYouReadyInterval))
						{
							this.SendRosMessage(MsgAreYouReady, "");
						}

						break;
					}
					case ModeratorStep.TaskStart:
					{
						// Wait for button pressing when the data generation
						if(this.executionMode == ExecutionMode.DataGeneration)
						{
							if(!this.tool.HasPressedButtonForDataGeneration()) { break; }
						}

						SIGVerseLogger.Info("Task start!");

						this.scoreManager.TaskStart();
						
						this.tool.StartPlayback();

						this.step++;

						break;
					}
					case ModeratorStep.SendingPickItUpMsg:
					{
						if (this.tool.HasPointedTarget())
						{
							this.SendRosMessage(MsgPickItUp, "");

							SIGVerseLogger.Info("Sent '" + MsgPickItUp + "'");

							this.step++;
						}
						break;
					}
					case ModeratorStep.SendingCleanUpMsg:
					{
						if (this.tool.HasPointedDestination())
						{
							this.SendRosMessage(MsgCleanUp, "");

							SIGVerseLogger.Info("Sent '" + MsgCleanUp + "'");
							
							if(this.executionMode == ExecutionMode.DataGeneration)
							{
								this.scoreManager.SetTaskMessageText(this.tool.GetTaskDetail());

								StartCoroutine(this.tool.SaveEnvironmentInfo());
							}

							this.step++;
						}
						break;
					}
					case ModeratorStep.WaitForTaskFinished:
					{
						if (this.receivedMessageMap[MsgTaskFinished])
						{
							StartCoroutine(this.tool.UpdateDeploymentStatus(this));

							this.step++;
						}

						// Respond to confirmation of correctness
						if(this.receivedMessageMap[MsgIsThisCorrect])
						{
							// Reset a flag
							this.receivedMessageMap[MsgIsThisCorrect] = false;

							// Check for grasped object
							bool isSucceeded = this.tool.IsCorrectObject();

							if (isSucceeded)
							{
								this.SendRosMessage(MsgYes, "");
								SIGVerseLogger.Info("It is the correct target.");
							}
							else
							{
								this.SendRosMessage(MsgNo, "");
								SIGVerseLogger.Info("It is the INCORRECT target.");
							}

							this.scoreManager.AddScore(Score.Type.AskedCorrectOrNot);
						}

						break;
					}
					case ModeratorStep.Judgement:
					{
						if (this.tool.IsDeploymentCheckFinished())
						{
							bool isSucceeded = this.tool.IsDeploymentSucceeded();

							if (CleanupConfig.Instance.configFileInfo.isAlwaysGoNext)
							{
								SIGVerseLogger.Warn("!!! DEBUG MODE !!! : always go next step : result=" + isSucceeded);
								isSucceeded = true;
							}

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgTaskFinished + "'");
								this.ShowNotice("Succeeded!", 150, GreenColor);
								this.scoreManager.AddScore(Score.Type.CleanupSuccess);

								this.GoToNextTaskTaskSucceeded();
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgTaskFinished + "'");
								this.ShowNotice("Failed", 150, RedColor);
								this.GoToNextTaskTaskFailed("Failed " + MsgTaskFinished);
							}
						}

						break;
					}
					case ModeratorStep.WaitForNextTask:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 5000))
						{
							if(!this.tool.IsPlaybackFinished()) { break; }

							SceneManager.LoadScene(SceneManager.GetActiveScene().name);
						}

						break;
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogError(exception);
				SIGVerseLogger.Error(exception.Message);
				SIGVerseLogger.Error(exception.StackTrace);
				this.ApplicationQuitAfter1sec();
			}
		}

		private void ApplicationQuitAfter1sec()
		{
			Thread.Sleep(1000);
			Application.Quit();
		}

		public void InterruptTimeIsUp()
		{
			this.interruptedReason = CleanupModerator.ReasonTimeIsUp;
		}

		public void InterruptGiveUp()
		{
			this.interruptedReason = CleanupModerator.ReasonGiveUp;
		}

		private void GoToNextTaskTaskSucceeded()
		{
			this.GoToNextTask(MsgTaskSucceeded, "");
		}

		private void GoToNextTaskTaskFailed(string detail)
		{
			this.GoToNextTask(MsgTaskFailed, detail);
		}

		private void GoToNextTask(string message, string detail)
		{
			this.tool.StopPlayback();

			this.scoreManager.TaskEnd();

			this.SendRosMessage(message, detail);

			this.PostProcess();

			this.step = ModeratorStep.WaitForNextTask;
		}


		private void SendRosMessage(string message, string detail)
		{
			ExecuteEvents.Execute<IRosMsgSendHandler>
			(
				target: this.gameObject, 
				eventData: null, 
				functor: (reciever, eventData) => reciever.OnSendRosMessage(message, detail)
			);
		}



		private void ShowNotice(string message, int fontSize, Color color)
		{
			this.cleanupMenu.notice.SetActive(true);

			Text noticeText = this.cleanupMenu.notice.GetComponentInChildren<Text>();

			noticeText.text     = message;
			noticeText.fontSize = fontSize;
			noticeText.color    = color;

			this.noticeHideTime = UnityEngine.Time.time + 2.0f;

			StartCoroutine(this.HideNotice()); // Hide after 2[s]
		}

		private IEnumerator HideNotice()
		{
			while(UnityEngine.Time.time < this.noticeHideTime)
			{
				yield return null;
			}

			this.cleanupMenu.notice.SetActive(false);
		}


		public void OnReceiveRosMessage(ROSBridge.interactive_cleanup.InteractiveCleanupMsg interactiveCleanupMsg)
		{
			if(this.receivedMessageMap.ContainsKey(interactiveCleanupMsg.message))
			{
				this.receivedMessageMap[interactiveCleanupMsg.message] = true;
			}
			else
			{
				SIGVerseLogger.Warn("Received Illegal message : " + interactiveCleanupMsg.message);
			}
		}


		public void OnAvatarPointByLeft()
		{
			this.tool.PointObject(this.laserLeft, this.step);
		}

		public void OnAvatarPointByRight()
		{
			this.tool.PointObject(this.laserRight, this.step);
		}

		public void OnAvatarPressA()
		{
			this.tool.PressAorX(this.step);
		} 

		public void OnAvatarPressX()
		{
			this.tool.PressAorX(this.step);
		} 
	}
}

