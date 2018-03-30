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
		TaskStart,
		WaitForIamReady, 
		SendingPickItUpMsg,
		SendingCleanUpMsg,
		WaitForObjectGrasped,
		WaitForTaskFinished,
		Judgement,
		WaitForNextTask,
	}

	public class CleanupModerator : MonoBehaviour, IRosMsgReceiveHandler, IAvatarMotionHandler, ITimeIsUpHandler, IGiveUpHandler
	{
		private const int SendingAreYouReadyInterval = 1000;

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
		private const string MsgObjectGrasped = "Object_grasped";
		private const string MsgTaskFinished  = "Task_finished";

		//-----------------------------

		public List<GameObject> environments;

		public CleanupScoreManager scoreManager;
		public GameObject avatarMotionPlayback;
		public GameObject playbackManager;

		public Laser laserLeft;
		public Laser laserRight;

		//-----------------------------

		private CleanupModeratorTool tool;
		private StepTimer stepTimer;

		private GameObject mainMenu;
		private PanelMainController mainPanelController;


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
				this.tool = new CleanupModeratorTool(this.environments, this.scoreManager, this.avatarMotionPlayback, this.playbackManager);

				this.stepTimer = new StepTimer();

				this.executionMode = this.tool.GetExecutionMode();

				this.mainMenu = GameObject.FindGameObjectWithTag("MainMenu");
				this.mainPanelController = mainMenu.GetComponent<PanelMainController>();
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

			if(this.executionMode==ExecutionMode.Competition)
			{
				this.mainPanelController.SetTaskMessageText(this.tool.GetTaskDetail());
			}

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
			this.mainPanelController.SetTrialNumberText(CleanupConfig.Instance.numberOfTrials);

			SIGVerseLogger.Info("##### " + this.mainPanelController.GetTrialNumberText() + " #####");

			this.mainPanelController.ResetTimeLeftText();


			this.receivedMessageMap = new Dictionary<string, bool>();
			this.receivedMessageMap.Add(MsgIamReady,      false);
			this.receivedMessageMap.Add(MsgIsThisCorrect, false);
			this.receivedMessageMap.Add(MsgObjectGrasped, false);
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

					this.SendPanelNotice("Failed\n"+ interruptedReason.Replace('_',' '), 100, PanelNoticeStatus.Red);

					this.GoToNextTaskTaskFailed(this.interruptedReason);
				}

				switch (this.step)
				{
					case ModeratorStep.Initialize:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 3000))
						{
							SIGVerseLogger.Info("Initialize");
							this.PreProcess();
							this.step++;
						}
						break;
					}
					case ModeratorStep.WaitForStart:
					{
						if(this.tool.IsPlaybackInitialized() && this.tool.IsConnectedToRos())
						{
							this.step++;
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
								this.mainPanelController.SetTaskMessageText(this.tool.GetTaskDetail());

								StartCoroutine(this.tool.SaveEnvironmentInfo());
							}

							this.step++;
						}
						break;
					}
					case ModeratorStep.WaitForObjectGrasped:
					{
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


						if (this.receivedMessageMap[MsgObjectGrasped])
						{
							// Check for grasping
							bool isSucceeded = this.tool.IsObjectGraspedSucceeded();

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgObjectGrasped + "'");
								this.SendPanelNotice("Good", 150, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.ObjectGraspedSuccess);
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgObjectGrasped + "'");
								this.SendPanelNotice("Failed\n" + MsgObjectGrasped.Replace('_', ' '), 100, PanelNoticeStatus.Red);
								this.GoToNextTaskTaskFailed("Failed " + MsgObjectGrasped);

								return;
							}

							this.step++;

							SIGVerseLogger.Info("Waiting for '" + MsgTaskFinished + "'");

							break;
						}

						break;
					}
					case ModeratorStep.WaitForTaskFinished:
					{
						if (this.receivedMessageMap[MsgTaskFinished])
						{
							StartCoroutine(this.tool.UpdatePlacementStatus(this));

							this.step++;
						}

						break;
					}
					case ModeratorStep.Judgement:
					{
						if (this.tool.IsPlacementCheckFinished())
						{
							bool isSucceeded = this.tool.IsPlacementSucceeded();

							if (CleanupConfig.Instance.configFileInfo.isAlwaysGoNext)
							{
								SIGVerseLogger.Warn("!!! DEBUG MODE !!! : always go next step : result=" + isSucceeded);
								isSucceeded = true;
							}

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgTaskFinished + "'");
								this.SendPanelNotice("Succeeded!", 150, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.CleanupSuccess);

								this.GoToNextTaskTaskSucceeded();
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgTaskFinished + "'");
								this.SendPanelNotice("Failed", 150, PanelNoticeStatus.Red);
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


		private void SendPanelNotice(string message, int fontSize, Color color)
		{
			PanelNoticeStatus noticeStatus = new PanelNoticeStatus(message, fontSize, color, 2.0f);

			// For changing the notice of a panel
			ExecuteEvents.Execute<IPanelNoticeHandler>
			(
				target: this.mainMenu, 
				eventData: null, 
				functor: (reciever, eventData) => reciever.OnPanelNoticeChange(noticeStatus)
			);

			// For recording
			ExecuteEvents.Execute<IPanelNoticeHandler>
			(
				target: this.playbackManager, 
				eventData: null, 
				functor: (reciever, eventData) => reciever.OnPanelNoticeChange(noticeStatus)
			);
		}


		public void OnReceiveRosMessage(RosBridge.interactive_cleanup.InteractiveCleanupMsg interactiveCleanupMsg)
		{
			if(this.receivedMessageMap.ContainsKey(interactiveCleanupMsg.message))
			{
				// Check message order
				if(interactiveCleanupMsg.message==MsgIamReady)
				{
					if(this.step!=ModeratorStep.WaitForIamReady) { SIGVerseLogger.Warn("Illegal timing. message : " + interactiveCleanupMsg.message); return; }
				}

				if(interactiveCleanupMsg.message==MsgIsThisCorrect)
				{
					if(this.step!=ModeratorStep.WaitForObjectGrasped) { SIGVerseLogger.Warn("Illegal timing. message : " + interactiveCleanupMsg.message); return; }
				}

				if(interactiveCleanupMsg.message==MsgObjectGrasped)
				{
					if(this.step!=ModeratorStep.WaitForObjectGrasped) { SIGVerseLogger.Warn("Illegal timing. message : " + interactiveCleanupMsg.message); return; }
				}

				if(interactiveCleanupMsg.message==MsgTaskFinished)
				{
					if(this.step!=ModeratorStep.WaitForTaskFinished) { SIGVerseLogger.Warn("Illegal timing. message : " + interactiveCleanupMsg.message); return; }
				}

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


		public void OnTimeIsUp()
		{
			this.interruptedReason = CleanupModerator.ReasonTimeIsUp;
		}

		public void OnGiveUp()
		{
			if(this.step > ModeratorStep.TaskStart && this.step < ModeratorStep.WaitForNextTask)
			{
				this.interruptedReason = CleanupModerator.ReasonGiveUp;
			}
			else
			{
				SIGVerseLogger.Warn("It is a timing not allowed to give up.");
			}
		}
	}
}

