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
using UnityEngine.UI;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public enum ModeratorStep
	{
		Initialize,
		WaitForStart,
		TaskStart,
		WaitForIamReady, 
		WaitForObjectGrasped,
		WaitForTaskFinished,
		Judgement,
		WaitForNextTask,
	}

	public enum AvatarStateStep
	{
		Initialize,
		WaitForPickItUp,
		WaitForCleanUp,
		WaitForReturnToInitialPosition,
		ReturnToInitialPosition,
		WaitForNextPointing,
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

		private const string MsgIamReady      = "I_am_ready";
		private const string MsgIsThisCorrect = "Is_this_correct?";
		private const string MsgObjectGrasped = "Object_grasped";
		private const string MsgTaskFinished  = "Task_finished";
		private const string MsgPointItAgain  = "Point_it_again";
		private const string MsgGiveUp        = "Give_up";

		private const string ReasonTimeIsUp = "Time_is_up";
		private const string ReasonGiveUp   = MsgGiveUp;

		//-----------------------------

		public List<GameObject> environments;

		public CleanupScoreManager scoreManager;
		public GameObject avatarMotionPlayback;
		public GameObject playbackManager;

		public AudioSource objectCollisionAudioSource;

		public Laser laserLeft;
		public Laser laserRight;

		//-----------------------------

		private CleanupModeratorTool tool;
		private StepTimer stepTimer;

		private GameObject mainMenu;
		private PanelMainController mainPanelController;


		private ExecutionMode   executionMode;
		private ModeratorStep   step;
		private AvatarStateStep avatarStateStep;

		private Dictionary<string, bool> receivedMessageMap;

		private bool isAllTaskFinished;
		private string interruptedReason;

		private float noticeHideTime;


		void Awake()
		{
			try
			{
				this.tool = new CleanupModeratorTool(this);

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
			this.step            = ModeratorStep.Initialize;
			this.avatarStateStep = AvatarStateStep.Initialize;

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

			this.scoreManager.ResetTimeLeftText();


			this.receivedMessageMap = new Dictionary<string, bool>();
			this.receivedMessageMap.Add(MsgIamReady,      false);
			this.receivedMessageMap.Add(MsgIsThisCorrect, false);
			this.receivedMessageMap.Add(MsgObjectGrasped, false);
			this.receivedMessageMap.Add(MsgTaskFinished,  false);
			this.receivedMessageMap.Add(MsgPointItAgain,  false);
			this.receivedMessageMap.Add(MsgGiveUp,        false);

			this.tool.InitializePlayback();

			SIGVerseLogger.Info("End of PreProcess. Trial No=" + CleanupConfig.Instance.numberOfTrials);
		}


		private void PostProcess()
		{
			SIGVerseLogger.Info("Task end");

			if (CleanupConfig.Instance.numberOfTrials == CleanupConfig.Instance.configFileInfo.maxNumberOfTrials)
			{
				this.SendRosMessage(MsgMissionComplete, string.Empty);

				SIGVerseLogger.Info("All tasks finished.");

				StartCoroutine(this.tool.CloseRosConnections());

				this.isAllTaskFinished = true;
			}
			else
			{
				StartCoroutine(this.tool.ClearRosConnections());

				this.step = ModeratorStep.WaitForNextTask;
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
					this.SendPanelNotice("Failed\n"+ this.interruptedReason.Replace('_',' '), 100, PanelNoticeStatus.Red);

					if(this.interruptedReason==ReasonTimeIsUp)
					{
						this.tool.AddSpeechQueModerator(ReasonTimeIsUp);
					}
					else if(this.interruptedReason==ReasonGiveUp)
					{
						this.tool.AddSpeechQueHsr(ReasonGiveUp);
					}
					
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
							if(this.tool.IsPlaybackInitialized() && this.tool.IsConnectedToRos())
							{
								this.tool.ApplyFirstPostureOfAvatar();
								this.step++;
							}
						}

						break;
					}
					case ModeratorStep.TaskStart:
					{
						SIGVerseLogger.Info("Task start!");
						this.tool.AddSpeechQueModerator("Task start!");

						this.scoreManager.TaskStart();
						
						this.tool.StartPlaybackRecorder();

						this.step++;

						break;
					}
					case ModeratorStep.WaitForIamReady:
					{
						// Wait for button pressing when the data generation mode. And wait for receiving I_am_ready when the competition mode.
						if ((this.executionMode == ExecutionMode.DataGeneration && this.tool.HasPressedButtonToStartRecordingAvatarMotion()) || 
						    (this.executionMode == ExecutionMode.Competition    && this.receivedMessageMap[MsgIamReady]))
						{
							StartCoroutine(this.StartAvatarStateController());

							this.step++;
							break;
						}

						if (this.stepTimer.IsTimePassed((int)this.step, SendingAreYouReadyInterval))
						{
							this.SendRosMessage(MsgAreYouReady, string.Empty);
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
								this.SendRosMessage(MsgYes, string.Empty);
								this.tool.AddSpeechQueModerator("Yes");
								SIGVerseLogger.Info("It is the correct target.");
							}
							else
							{
								this.SendRosMessage(MsgNo, string.Empty);
								this.tool.AddSpeechQueModerator("No");
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
								this.SendPanelNotice("Good Job", 150, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.ObjectGraspedSuccess);
								this.tool.AddSpeechQueModeratorGood();
							}
							else
							{
								string detail = "Failed to grasp";
								SIGVerseLogger.Info("Failed '" + MsgObjectGrasped + "'");
								this.SendPanelNotice("Failed\n" + detail, 100, PanelNoticeStatus.Red);
								this.scoreManager.AddScore(Score.Type.ObjectGraspedFailure);
								this.tool.AddSpeechQueModeratorFailed();

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
								this.SendPanelNotice("Task Completed", 120, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.CleanupSuccess);
								this.tool.AddSpeechQueModerator("Excellent!");

								this.GoToNextTaskTaskSucceeded();
							}
							else
							{
								string detail = "You didn't complete";
								SIGVerseLogger.Info("Failed '" + MsgTaskFinished + "'");
								this.SendPanelNotice("Failed\n" + detail, 100, PanelNoticeStatus.Red);
								this.scoreManager.AddScore(Score.Type.CleanupFailure);
								this.tool.AddSpeechQueModeratorFailed();

								this.GoToNextTaskTaskFailed("Failed " + MsgTaskFinished);
							}
						}

						break;
					}
					case ModeratorStep.WaitForNextTask:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 5000) && !this.tool.IsSpeaking())
						{
							if(!this.tool.IsPlaybackFinished()) { break; }

							SceneManager.LoadScene(SceneManager.GetActiveScene().name);
						}

						break;
					}
				}

				this.tool.ControlSpeech(this.step==ModeratorStep.WaitForNextTask); // Speech
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
			this.GoToNextTask(MsgTaskSucceeded, string.Empty);
		}

		private void GoToNextTaskTaskFailed(string detail)
		{
			this.GoToNextTask(MsgTaskFailed, detail);
		}

		private void GoToNextTask(string message, string detail)
		{
			this.tool.AddSpeechQueModerator("Let's go to the next session");

			this.tool.StopPlayback();

			this.scoreManager.TaskEnd();

			this.SendRosMessage(message, detail);

			this.PostProcess();
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



		private IEnumerator StartAvatarStateController()
		{
			bool isAvatarControlling = true;

			int numberOfPointing = 1;

			while(isAvatarControlling)
			{
				if(this.step==ModeratorStep.WaitForNextTask){ isAvatarControlling = false; }

				switch (this.avatarStateStep)
				{
					case AvatarStateStep.Initialize:
					{
						this.tool.StartAvatarMotionPlaybackPlayer();
						this.tool.StartAvatarMotionPlaybackRecorder();

						this.avatarStateStep++;

						break;
					}
					case AvatarStateStep.WaitForPickItUp:
					{
						if (this.tool.HasPointedTarget())
						{
							this.SendRosMessage(MsgPickItUp, string.Empty);
							this.tool.AddSpeechQueModerator("Please pick it up");
							SIGVerseLogger.Info("Sent '" + MsgPickItUp + "'");

							this.avatarStateStep++;
						}
						break;
					}
					case AvatarStateStep.WaitForCleanUp:
					{
						if (this.tool.HasPointedDestination())
						{
							this.SendRosMessage(MsgCleanUp, string.Empty);
							this.tool.AddSpeechQueModerator("Please clean up");
							SIGVerseLogger.Info("Sent '" + MsgCleanUp + "'");
							
							if(this.executionMode == ExecutionMode.DataGeneration && numberOfPointing==1)
							{
								this.mainPanelController.SetTaskMessageText(this.tool.GetTaskDetail());

								StartCoroutine(this.tool.SaveEnvironmentInfo());
							}

							this.avatarStateStep++;
						}
						break;
					}
					case AvatarStateStep.WaitForReturnToInitialPosition:
					{
						if((this.executionMode == ExecutionMode.DataGeneration && this.tool.HasPressedButtonToStopRecordingAvatarMotion()) ||
						   (this.executionMode == ExecutionMode.Competition    && this.tool.IsAvatarMotionPlaybackPlayerFinished()))
						{
							this.tool.StopAvatarMotionPlaybackRecorder();

							this.avatarStateStep++;
						}
						break;
					}
					case AvatarStateStep.ReturnToInitialPosition:
					{
						if(this.executionMode == ExecutionMode.Competition)
						{
							IEnumerator makeAvatarInitialPosture = this.tool.MakeAvatarInitialPosture();

							yield return StartCoroutine(makeAvatarInitialPosture);
						}
							
						this.avatarStateStep++;

						break;
					}
					case AvatarStateStep.WaitForNextPointing:
					{
						if (this.executionMode == ExecutionMode.Competition && this.receivedMessageMap[MsgPointItAgain])
						{
							this.receivedMessageMap[MsgPointItAgain] = false;

							this.scoreManager.AddScore(Score.Type.PointItAgain);

							this.tool.ResetPointingStatus();

							numberOfPointing++;

							this.avatarStateStep = AvatarStateStep.Initialize;
						}

						break;
					}
				}

				yield return null;
			}
		}



		public void OnReceiveRosMessage(RosBridge.interactive_cleanup.InteractiveCleanupMsg interactiveCleanupMsg)
		{
			if(this.receivedMessageMap.ContainsKey(interactiveCleanupMsg.message))
			{
				// Check message order
				if(interactiveCleanupMsg.message==MsgIamReady)
				{
					if(this.step!=ModeratorStep.WaitForIamReady) { this.LogMsgAtIllegalTiming(interactiveCleanupMsg.message); return; }
				}

				if(interactiveCleanupMsg.message==MsgIsThisCorrect)
				{
					if(this.step!=ModeratorStep.WaitForObjectGrasped) { this.LogMsgAtIllegalTiming(interactiveCleanupMsg.message); return; }
					this.tool.AddSpeechQueHsr(MsgIsThisCorrect);
				}

				if(interactiveCleanupMsg.message==MsgObjectGrasped)
				{
					if(this.step!=ModeratorStep.WaitForObjectGrasped) { this.LogMsgAtIllegalTiming(interactiveCleanupMsg.message); return; }
					this.tool.AddSpeechQueHsr("I grasped the object");
				}

				if(interactiveCleanupMsg.message==MsgTaskFinished)
				{
					if(this.step!=ModeratorStep.WaitForTaskFinished) { this.LogMsgAtIllegalTiming(interactiveCleanupMsg.message); return; }
					this.tool.AddSpeechQueHsr(MsgTaskFinished);
				}

				if(interactiveCleanupMsg.message==MsgPointItAgain)
				{
					if(this.executionMode == ExecutionMode.DataGeneration) { SIGVerseLogger.Warn("In the data generation mode, can not request a re-pointing"); return; }

					if(this.avatarStateStep!=AvatarStateStep.WaitForNextPointing) { this.LogMsgAtIllegalTiming(interactiveCleanupMsg.message); return; }

					this.tool.AddSpeechQueHsr("Could you point it again?");
				}

				if(interactiveCleanupMsg.message==MsgGiveUp)
				{
					this.OnGiveUp();
				}

				this.receivedMessageMap[interactiveCleanupMsg.message] = true;
			}
			else
			{
				SIGVerseLogger.Warn("Received Illegal message : " + interactiveCleanupMsg.message);
			}
		}

		private void LogMsgAtIllegalTiming(string message)
		{
			SIGVerseLogger.Warn("Illegal timing. message : " + message);
		}


		public void OnAvatarPointByLeft()
		{
			this.tool.PointObject(this.laserLeft, this.avatarStateStep);
		}

		public void OnAvatarPointByRight()
		{
			this.tool.PointObject(this.laserRight, this.avatarStateStep);
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
			this.interruptedReason = ReasonTimeIsUp;
		}

		public void OnGiveUp()
		{
			if(this.step > ModeratorStep.TaskStart && this.step < ModeratorStep.WaitForNextTask)
			{
				this.interruptedReason = ReasonGiveUp;
			}
			else
			{
				SIGVerseLogger.Warn("It is a timing not allowed to give up.");
			}
		}
	}
}

