using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using SIGVerse.Common;
using System.Linq;

namespace SIGVerse.Competition.InteractiveCleanup
{ 
	[System.Serializable]
	public class CleanupConfigFileInfo
	{
		public string teamName;
		public int  sessionTimeLimit;
		public int  maxNumberOfTrials;
		public bool isScoreFileRead;
		public int  executionMode;
		public bool isAlwaysGoNext;
		public int  playbackType;
	}

	public enum ExecutionMode
	{
		Competition,
		DataGeneration,
	}

	public class CleanupConfig : Singleton<CleanupConfig>
	{
		public const string FolderPath     = "/../SIGVerseConfig/InteractiveCleanup/";

		public const string ConfigFileName = "InteractiveCleanupConfig.json";
		public const string ScoreFileName  = "InteractiveCleanupScore.txt";

		private string configFilePath;
		private string scoreFilePath;

		protected CleanupConfig() { } // guarantee this will be always a singleton only - can't use the constructor!

		public CleanupConfigFileInfo configFileInfo;

		public int numberOfTrials;

		public List<int> scores;


		void Awake()
		{
			// Read Config file
			this.configFilePath = Application.dataPath + CleanupConfig.FolderPath + CleanupConfig.ConfigFileName;

			this.configFileInfo = new CleanupConfigFileInfo();

			if (File.Exists(configFilePath))
			{
				// File open
				StreamReader streamReader = new StreamReader(configFilePath, Encoding.UTF8);

				this.configFileInfo = JsonUtility.FromJson<CleanupConfigFileInfo>(streamReader.ReadToEnd());

				streamReader.Close();
			}
			else
			{
#if UNITY_EDITOR
				SIGVerseLogger.Warn("Interactive Cleanup config file does not exists.");

				this.configFileInfo.teamName          = "XXXX";
				this.configFileInfo.sessionTimeLimit  = 360;
				this.configFileInfo.maxNumberOfTrials = 15;
				this.configFileInfo.isScoreFileRead   = false;
				this.configFileInfo.executionMode     = (int)ExecutionMode.Competition;
				this.configFileInfo.isAlwaysGoNext    = false;
				this.configFileInfo.playbackType      = CleanupPlaybackCommon.PlaybackTypeRecord;

				this.SaveConfig();
#else
				SIGVerseLogger.Error("Interactive Cleanup config file does not exists.");
				Application.Quit();
#endif
			}

			// Initialize common parameter
			this.scoreFilePath = Application.dataPath + CleanupConfig.FolderPath + CleanupConfig.ScoreFileName;

			this.scores = new List<int>();

			if (this.configFileInfo.isScoreFileRead)
			{
				// File open
				StreamReader streamReader = new StreamReader(scoreFilePath, Encoding.UTF8);

				string line;

				while ((line = streamReader.ReadLine()) != null)
				{
					string scoreStr = line.Trim();

					if (scoreStr == string.Empty) { continue; }

					this.scores.Add(Int32.Parse(scoreStr));
				}

				streamReader.Close();

				this.numberOfTrials = this.scores.Count;

				if (this.numberOfTrials >= this.configFileInfo.maxNumberOfTrials)
				{
					SIGVerseLogger.Error("this.numberOfTrials >= this.configFileInfo.maxNumberOfTrials");
					Application.Quit();
				}
			}
			else
			{
				this.numberOfTrials = 0;
			}
		}

		public void SaveConfig()
		{
			StreamWriter streamWriter = new StreamWriter(configFilePath, false, Encoding.UTF8);

			SIGVerseLogger.Info("Save Cleanup config : " + JsonUtility.ToJson(CleanupConfig.Instance.configFileInfo));

			streamWriter.WriteLine(JsonUtility.ToJson(CleanupConfig.Instance.configFileInfo, true));

			streamWriter.Flush();
			streamWriter.Close();
		}

		public void InclementNumberOfTrials()
		{
			this.numberOfTrials++; 
		}

		public void AddScore(int score)
		{
			this.scores.Add(score);
		}

		public int GetTotalScore()
		{
			return this.scores.Where(score => score > 0).Sum();
		}

		public void RecordScoreInFile()
		{
			string filePath = Application.dataPath + CleanupConfig.FolderPath + CleanupConfig.ScoreFileName;

			bool append = true;

			if(this.numberOfTrials==1) { append = false; }

			StreamWriter streamWriter = new StreamWriter(filePath, append, Encoding.UTF8);

			SIGVerseLogger.Info("Record the socre in a file. path=" + filePath);

			streamWriter.WriteLine(this.scores[this.scores.Count - 1]);

			streamWriter.Flush();
			streamWriter.Close();
		}
	}
}

