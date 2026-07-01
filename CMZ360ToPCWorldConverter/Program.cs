/*
SPDX-License-Identifier: GPL-3.0-or-later
Copyright (c) 2023 RussDev7
This file is part of https://github.com/RussDev7/CMZ360ToPCWorldConverter - see LICENSE for details.
*/

using CMZ360ToPCWorldConverter.WorldData;
using System.Collections.Generic;
using DNA.Security.Cryptography;
using DNA.IO.Storage;
using System.Text;
using System.IO;
using System;

namespace CMZ360ToPCWorldConverter
{
	/// <summary>
	/// Console entry point for decrypting CastleMiner Z world files into an unprotected
	/// output folder that the PC game can read.
	/// </summary>
	internal class Program
	{
		#region Constants

		private const string InputWorldsFolderName  = "Worlds";
		private const string OutputWorldsFolderName = "OutputWorlds";

		// CastleMiner Z save-device key suffix used by this converter's original logic.
		private const string SaveKeySuffix = "CMZ778";

		// SteamID64 values are normally 17 digits. The original converter used this
		// length check to decide whether the entered value was probably an Xbox gamertag.
		private const int SteamId64Length = 17;

		#endregion

		#region Entry point

		/// <summary>
		/// Prompts for the original owner identifier, decrypts files from Worlds,
		/// and patches world.info files when the input looks like an Xbox gamertag.
		/// </summary>
		private static void Main()
		{
			string currentDirectory = Directory.GetCurrentDirectory();
			SaveDevice device = new FileSystemSaveDevice(currentDirectory, null);

			Console.WriteLine("Enter the gamertag or SteamID64 that the map(s) came from:");
			string ownerId = Console.ReadLine() ?? string.Empty;

			bool shouldPatchWorldInfo = UsesGamertagKey(ownerId);

			DecryptSaves(ownerId, currentDirectory);

			if (shouldPatchWorldInfo)
			{
				UpdateSaves(device);
			}

			Console.WriteLine("Done. Press any key to close.");
			Console.ReadKey();
		}
		#endregion

		#region World info patching

		/// <summary>
		/// Re-saves every readable OutputWorlds/*/world.info file using the
		/// converter's compatibility writer.
		/// </summary>
		/// <remarks>
		/// NOTE: This is not a full world upgrade. It only rewrites the world-info header
		/// and appends PC server fields so the game can attempt to load the world.
		/// </remarks>
		private static void UpdateSaves(SaveDevice device)
		{
			foreach (WorldInfo worldInfo in WorldInfo.LoadWorldInfo(device))
			{
				Console.WriteLine("Saving patched world.info for " + worldInfo.SavePath);
				worldInfo.SaveToStorage(device);
			}
		}
		#endregion

		#region Save decryption

		/// <summary>
		/// Decrypts every file under the local Worlds folder and writes the
		/// unprotected copy to the matching path under OutputWorlds.
		/// </summary>
		/// <param name="ownerId">Original Xbox gamertag or SteamID64 used to derive the save key.</param>
		/// <param name="directory">Working directory that contains the input and output folders.</param>
		private static void DecryptSaves(string ownerId, string directory)
		{
			string inputDirectory = Path.Combine(directory, InputWorldsFolderName);
			string outputDirectory = Path.Combine(directory, OutputWorldsFolderName);

			EnsureDirectoryExists(inputDirectory);
			EnsureDirectoryExists(outputDirectory);

			byte[] encryptionKey = new MD5HashProvider().Compute(Encoding.UTF8.GetBytes(ownerId + SaveKeySuffix)).Data;

			SaveDevice encryptedSaveDevice = new FileSystemSaveDevice(inputDirectory, encryptionKey);
			SaveDevice outputSaveDevice = new FileSystemSaveDevice(outputDirectory, null);

			List<string> files = new List<string>();
			GetFiles(inputDirectory, files);

			foreach (string inputFile in files)
			{
				byte[] dataToSave;

				try
				{
					Console.WriteLine("Loading " + inputFile.Replace(directory, string.Empty));
					dataToSave = encryptedSaveDevice.LoadData(inputFile);
				}
				catch (Exception exception)
				{
					Console.WriteLine("Failed to load " + inputFile.Replace(directory, string.Empty));
					Console.WriteLine("  " + exception.Message);
					continue;
				}

				string outputFile = inputFile.Replace(inputDirectory, outputDirectory);
				string outputFolder = Path.GetDirectoryName(outputFile);

				if (!string.IsNullOrEmpty(outputFolder))
				{
					EnsureDirectoryExists(outputFolder);
				}

				Console.WriteLine("Saving " + outputFile.Replace(directory, string.Empty));
				outputSaveDevice.Save(outputFile, dataToSave, false, false);
			}
		}
		#endregion

		#region Helpers

		/// <summary>
		/// Preserves the original converter heuristic for detecting gamertag-based saves.
		/// </summary>
		private static bool UsesGamertagKey(string ownerId)
		{
			return ownerId.Length < SteamId64Length;
		}

		/// <summary>
		/// Creates the directory when it is missing.
		/// </summary>
		private static void EnsureDirectoryExists(string path)
		{
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
		}

		/// <summary>
		/// Recursively collects all files below <paramref name="path"/>.
		/// </summary>
		private static void GetFiles(string path, List<string> returnedFiles)
		{
			string[] directories = Directory.GetDirectories(path);

			for (int i = 0; i < directories.Length; i++)
			{
				GetFiles(directories[i], returnedFiles);
			}

			foreach (string file in Directory.GetFiles(path))
			{
				returnedFiles.Add(file);
			}
		}
		#endregion
	}
}
