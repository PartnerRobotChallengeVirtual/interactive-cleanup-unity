using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Linq;

namespace SIGVerse.Competition.InteractiveCleanup
{
	[InitializeOnLoad]
	public class ConfigInitializer
	{
		static ConfigInitializer()
		{
			FileInfo configFileInfo = new FileInfo(Application.dataPath + CleanupConfig.FolderPath + "sample/" + CleanupConfig.ConfigFileName);

			if(!configFileInfo.Exists) { return; }

			DirectoryInfo sampleDirectoryInfo = new DirectoryInfo(Application.dataPath + CleanupConfig.FolderPath + "sample/");

			foreach (FileInfo fileInfo in sampleDirectoryInfo.GetFiles().Where(fileinfo => fileinfo.Name != ".gitignore"))
			{
				string destFilePath = Application.dataPath + CleanupConfig.FolderPath + fileInfo.Name;

				if (!File.Exists(destFilePath))
				{
					fileInfo.CopyTo(destFilePath);
				}
			}
		}
	}
}

