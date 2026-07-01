/*
SPDX-License-Identifier: GPL-3.0-or-later
Copyright (c) 2023 RussDev7
This file is part of https://github.com/RussDev7/CMZ360ToPCWorldConverter - see LICENSE for details.
*/

using System.Collections.Generic;
using DNA.IO.Storage;
using System.IO;
using System;

namespace CMZ360ToPCWorldConverter.WorldData
{
	/// <summary>
	/// Lightweight wrapper around a CastleMiner Z world.info file.
	/// </summary>
	/// <remarks>
	/// The converter intentionally stores the original bytes instead of fully parsing
	/// the file. During save, it rewrites only the compatibility header and appends the
	/// PC server fields required by later world-info versions.
	/// </remarks>
	public class WorldInfo
	{
		#region Constants

		private const int    CurrentWorldInfoVersion = 5;
		private const int    PatchedWorldInfoVersion = 2;
		private const int    WorldInfoHeaderLength   = 4;
		private const string DefaultWorldName        = "World";
        private const string DefaultServerMessage    = "";
        private const string DefaultServerPassword   = "";

		private static readonly string BasePath = "OutputWorlds";
		private static readonly string FileName = "world.info";

		#endregion

		#region Public properties

		/// <summary>
		/// Latest world-info version known to this converter.
		/// </summary>
		public int Version
		{
			get
			{
				return CurrentWorldInfoVersion;
			}
		}

		/// <summary>
		/// Storage path for the world folder that owns this world.info file.
		/// </summary>
		public string SavePath
		{
			get
			{
				return this._savePath;
			}
			set
			{
				this._savePath = value;
			}
		}

		/// <summary>
		/// Display name placeholder retained from the game's original world-info type.
		/// </summary>
		public string Name
		{
			get
			{
				return this._name;
			}
			set
			{
				this._name = value;
			}
		}

		/// <summary>
		/// Last played date when available.
		/// </summary>
		public DateTime LastPlayedDate
		{
			get
			{
				return this._lastPlayedDate;
			}
			set
			{
				this._lastPlayedDate = value;
			}
		}
		#endregion

		#region Construction

		private WorldInfo()
		{
			this.ServerMessage  = DefaultServerMessage;
			this.ServerPassword = DefaultServerPassword;
		}
		#endregion

		#region Loading

		/// <summary>
		/// Loads every readable world.info file from the converter output folder.
		/// </summary>
		public static WorldInfo[] LoadWorldInfo(SaveDevice device)
		{
			try
			{
				CorruptWorlds.Clear();

				if (!device.DirectoryExists(BasePath))
				{
					return new WorldInfo[0];
				}

				List<WorldInfo> worlds = new List<WorldInfo>();

				foreach (string folder in device.GetDirectories(BasePath))
				{
					WorldInfo worldInfo = null;

					try
					{
						worldInfo = LoadFromStorage(folder, device);
					}
					catch (Exception exception)
					{
						worldInfo = null;
						CorruptWorlds.Add(folder);
						Console.WriteLine($"Could not load world.info from {folder}.");
						Console.WriteLine("  " + exception.Message);
					}

					if (worldInfo != null)
					{
						worlds.Add(worldInfo);
					}
				}

				return worlds.ToArray();
			}
			catch (Exception exception)
			{
				Console.WriteLine("Failed to enumerate converted worlds.");
				Console.WriteLine("  " + exception.Message);
				return new WorldInfo[0];
			}
		}

		/// <summary>
		/// Loads one world.info file from storage and remembers its folder path.
		/// </summary>
		private static WorldInfo LoadFromStorage(string folder, SaveDevice saveDevice)
		{
			WorldInfo info = new WorldInfo();

			saveDevice.Load(Path.Combine(folder, FileName), delegate(Stream stream)
			{
				info.Load(stream);
				info._savePath = folder;
			});

			return info;
		}

		/// <summary>
		/// Reads the original world.info bytes into memory.
		/// </summary>
		private void Load(Stream stream)
		{
			this.Bytes = this.ReadAllBytes(stream);
		}

		/// <summary>
		/// Copies all bytes from the stream.
		/// </summary>
		public byte[] ReadAllBytes(Stream stream)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				stream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}
		#endregion

		#region Saving

		/// <summary>
		/// Saves the patched world.info file back to the same storage path.
		/// </summary>
		public void SaveToStorage(SaveDevice saveDevice)
		{
			try
			{
				if (!saveDevice.DirectoryExists(this.SavePath))
				{
					saveDevice.CreateDirectory(this.SavePath);
				}

				string fileName = Path.Combine(this.SavePath, FileName);

				saveDevice.Save(fileName, false, false, delegate(Stream stream)
				{
					BinaryWriter binaryWriter = new BinaryWriter(stream);
					this.Save(binaryWriter);
					binaryWriter.Flush();
				});
			}
			catch (Exception exception)
			{
				Console.WriteLine($"Failed to save patched world.info for {this.SavePath}.");
				Console.WriteLine("  " + exception.Message);
			}
		}

		/// <summary>
		/// Writes a compatibility version header, copies the original remaining bytes,
		/// and appends server message/password fields.
		/// </summary>
		/// <remarks>
		/// NOTE: This method intentionally does not rebuild a full modern world-info file.
		/// The PC game should perform the real upgrade after it successfully loads and saves
		/// the converted world.
		/// </remarks>
		public void Save(BinaryWriter writer)
		{
			if (this.Bytes == null || this.Bytes.Length < WorldInfoHeaderLength)
			{
				throw new InvalidDataException("world.info is missing or shorter than the expected header.");
			}

			byte[] headerBytes = new byte[WorldInfoHeaderLength];
			headerBytes[0] = PatchedWorldInfoVersion;

			for (int i = 0; i < headerBytes.Length; i++)
			{
				writer.Write(headerBytes[i]);
			}

			for (int i = WorldInfoHeaderLength; i < this.Bytes.Length; i++)
			{
				writer.Write(this.Bytes[i]);
			}

			writer.Write(this.ServerMessage);
			writer.Write(this.ServerPassword);
		}
		#endregion

		#region Raw data and saved fields

		/// <summary>
		/// Original bytes loaded from world.info.
		/// </summary>
		public byte[] Bytes;

		/// <summary>
		/// World folders that failed to load during the latest scan.
		/// </summary>
		public static List<string> CorruptWorlds = new List<string>();

		private string   _savePath;
		private string   _name = DefaultWorldName;
		private DateTime _lastPlayedDate;

		public bool   InfiniteResourceMode;
		public int    HellBossesSpawned;
		public int    MaxHellBossSpawns;
		public string ServerMessage  = DefaultServerMessage;
		public string ServerPassword = DefaultServerPassword;

		#endregion

		#region Version metadata

		/// <summary>
		/// Vanilla world-info versions documented here for reference.
		/// </summary>
		private enum WorldInfoVersion
		{
			Initial = 1,
			Doors,
			Spawners,
			HellBosses,
			CurrentVersion
		}
		#endregion
	}
}
