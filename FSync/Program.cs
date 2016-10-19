using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.IO;
using System.Threading;

namespace FSync
{
	class FileSystemInode
	{
		public FileSystemInfo FileSystemInfo { get; set; }
		public FileInfo FileInfo { get { return FileSystemInfo as FileInfo; } set { FileSystemInfo = value; } }
		public DirectoryInfo DirectoryInfo { get { return FileSystemInfo as DirectoryInfo; } set { FileSystemInfo = value; } }

		public DateTime ModifiedTime { get { return FileSystemInfo.LastWriteTime; } }

		string mMd5Checksum;

		void CalculateMd5Checksum()
		{
			if (FileInfo == null)
				return;

			Debug.Assert(FileInfo == null, "Null FileInfo");
			using (var md5 = MD5.Create())
			{
				using (var stream = FileInfo.OpenRead())
				{
					mMd5Checksum = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
				}
			}
		}
		public FileSystemInode(FileSystemInfo fileSystemInfo)
		{
			FileSystemInfo = fileSystemInfo;
		}

		public bool IsDirectory
		{
			get { return DirectoryInfo != null; }
		}

		public bool IsFile
		{
			get { return FileInfo != null; }
		}
		public void MoveTo(string path)
		{
			if (IsFile)
				FileInfo.MoveTo(path);
			else if (IsDirectory)
				DirectoryInfo.MoveTo(path);
			else
				throw new ApplicationException("Shouldn't happen");
		}

		public string Md5Checksum
		{
			get
			{
				if (mMd5Checksum == null && DirectoryInfo == null)
					CalculateMd5Checksum();
				return mMd5Checksum;
			}
		}

		public string FullName
		{
			get
			{
				return FileSystemInfo.FullName;
			}
		}

	}

	class UnifiedFile
	{
		[Flags]
		public enum State
		{
			Dirty = -1,
			Synchronized = 0,
			Conflict = 1,

			RemoteRename = 1 << 1,
			RemoteUpdate = 1 << 2,
			RemoteDelete = 1 << 3,
			RemoteCreate = 1 << 4,

			LocalRename = 1 << 5,
			LocalUpdate = 1 << 6,
			LocalDelete = 1 << 7,
			LocalCreate = 1 << 8,

			Remote = RemoteRename | RemoteUpdate | RemoteDelete | RemoteCreate,
			Local = LocalRename | LocalUpdate | LocalDelete | LocalCreate,
			SwitchMask = Remote | Local,
		}
		State mStatus = State.Synchronized;
		public State Status { get { return mStatus; } }
		public string Path { get; set; }

		public Google.Apis.Drive.v3.Data.File File { get; set; }
		public FileSystemInode Inode { get; set; }

		public UnifiedFile(Google.Apis.Drive.v3.Data.File file, FileSystemInfo fileSystemInfo)
		{
			File = file;
			Inode = new FileSystemInode(fileSystemInfo);
		}

		public UnifiedFile(Google.Apis.Drive.v3.Data.File file, FileSystemInode inode)
		{
			File = file;
			Inode = inode;
		}

		// Compare File and Inode to generate state
		public void CalculateStatus()
		{
			if (File == null)
			{
				Console.WriteLine("CalculateStatus: LocalCreate");
				mStatus = State.LocalCreate;
			}
			else if (Inode == null)
			{
				Console.WriteLine("CalculateStatus: RemoteCreate");
				mStatus = State.RemoteCreate;
			}
			else
			{
				if (File.Md5Checksum != Inode.Md5Checksum)
				{
					Console.WriteLine("CalculateStatus: Conflict");
					mStatus = State.Conflict;
				}
				else
				{
					Console.WriteLine("CalculateStatus: Synchronized");
					mStatus = State.Synchronized;
				}
			}
		}

		public bool IsLocalUpdate
		{
			get
			{
				return (mStatus  & State.Local) > 0;
			}
		}

		public bool IsRemoteUpdate
		{
			get
			{
				return (mStatus & State.Remote) > 0;
			}
		}


		public bool HasConflict
		{
			get
			{
				return mStatus.HasFlag(State.Conflict);
			}

			set
			{
				mStatus |= State.Conflict;
			}
		}
		public bool Synchronized { get { return mStatus == State.Synchronized; } set { mStatus = State.Synchronized; } }

		public void Modify(Google.Apis.Drive.v3.Data.File file)
		{
			if (file.Version == File.Version)
				return;
			throw new NotImplementedException();
		}
		public void Modify(FileSystemInode file)
		{
			throw new NotImplementedException();
		}
		void Create(Google.Apis.Drive.v3.Data.File file)
		{
			mStatus = State.RemoteCreate;
		}
		void Rename(Google.Apis.Drive.v3.Data.File file)
		{
			mStatus = State.RemoteRename;
		}
		void Delete(Google.Apis.Drive.v3.Data.File file)
		{
			mStatus = State.RemoteDelete;
		}
		void Update(Google.Apis.Drive.v3.Data.File file)
		{
			Debug.Assert(mStatus == State.Synchronized, "mStatus not synchronized");
			Debug.Assert(File.Md5Checksum == Inode.Md5Checksum);

			if (File.ModifiedTime < Inode.ModifiedTime)
				mStatus |= State.Conflict;
			mStatus |= State.RemoteUpdate;
		}

		void Create(FileSystemInfo fileSystemInfo)
		{
			mStatus = State.LocalCreate;
		}
		void Rename(FileSystemInfo fileSystemInfo)
		{
			mStatus = State.LocalRename;
		}
		void Delete(FileSystemInfo fileSystemInfo)
		{
			mStatus = State.LocalDelete;
		}
		void Update(FileSystemInfo fileSystemInfo)
		{
			mStatus = State.LocalUpdate;
		}

		public string Md5Checksum
		{
			get
			{
				if (!Synchronized)
					throw new Exception("Checksum mismatch.  Requires Synchronization.");
				return File.Md5Checksum;
			}
		}
	}

	class UnifiedFileSystem
	{
		HashSet<string> PendingSync { get; set;}
		Dictionary<string, UnifiedFile> PathMap { get; set;}
		Dictionary<string, string> IdMap { get; set;}

		string savedStartPageToken;

		UnifiedFile Root { get; set;}
		DriveService DriveService { get; set;}

		public UnifiedFileSystem(DriveService driveService, Google.Apis.Drive.v3.Data.File file, FileSystemInfo path)
		{
			PendingSync = new HashSet<string>();
			PathMap = new Dictionary<string, UnifiedFile>();
			IdMap = new Dictionary<string, string>();

			DriveService = driveService;
			Root = new UnifiedFile(file, path);
			Root.Path = "/";
			SynchronizeFile(Root);
			AddFile(Root);

			// Change Tracking
			var response = DriveService.Changes.GetStartPageToken().Execute();
			Console.WriteLine("Start token: " + response.StartPageTokenValue);
			savedStartPageToken = response.StartPageTokenValue;
		}



		public Google.Apis.Drive.v3.Data.File GetDriveFileInfo(string id)
		{
			var resource = DriveService.Files.Get(id);
			Console.WriteLine(resource.FileId);
			resource.Fields = "explicitlyTrashed, id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size";
			return resource.Execute();
		}

		public Google.Apis.Drive.v3.Data.File GetDriveFileParent(Google.Apis.Drive.v3.Data.File file)
		{
			if (file.Parents == null)
				return null;
			if (file.Parents.Count > 1)
				throw new Exception("too many parents");
			var getRequest = DriveService.Files.Get(file.Parents[0]);
			getRequest.Fields = "id,name,parents";
			return getRequest.Execute();
		}

		public string GetDrivePath(Google.Apis.Drive.v3.Data.File file)
		{
			if (IdMap.ContainsKey(file.Id))
				return IdMap[file.Id];

			List<string> paths = new List<string>();

			Google.Apis.Drive.v3.Data.File parent = file;
			do
			{
				paths.Insert(0, parent.Name);
				parent = GetDriveFileParent(parent);
			} while (parent != null && parent.Id != Root.File.Id);
			paths.Insert(0, "/");

			var path = System.IO.Path.Combine(paths.ToArray()); 
			IdMap[file.Id] = path;

			return path;
		}

		public UnifiedFile GetUnifiedFile(params object[] args)
		{
			if (args.Length > 2)
				throw new ArgumentException("Too many arugments passed.");

			Google.Apis.Drive.v3.Data.File file = null;
			FileSystemInode fileSystemInode = null;

			foreach (var arg in args)
			{
				if (arg is Google.Apis.Drive.v3.Data.File)
				{
					if (file != null)
						throw new ArgumentException("Only one Drive File may be passed.");
					file = (Google.Apis.Drive.v3.Data.File)arg;
				}
				else if (arg is FileSystemInfo)
				{
					if (fileSystemInode != null)
						throw new ArgumentException("Multiple FileSystemInfo or FileSystemInode objects passed.");
					fileSystemInode = new FileSystemInode((FileSystemInfo)arg);
				}
				else if (arg is FileSystemInode)
				{
					if (fileSystemInode != null)
						throw new ArgumentException("Only one FileSystemInfo or FileSystemInode can be used.");
					fileSystemInode = (FileSystemInode)arg;
				}
				else
				{
					throw new ArgumentException("Argument must be Google.Apis.Drive.v3.Data.Files, FileSystemInfo, or FileSystemInode");
				}
			}

			string path = "";
			if (file != null)
			{
				path = GetDrivePath(file);
			}
			else if (fileSystemInode != null)
			{
				path = BuildRelativePath(fileSystemInode.FullName);
			}

			UnifiedFile unifiedFile;

			// TODO: This should be a query function.
			PathMap.TryGetValue(path, out unifiedFile);
			Console.WriteLine("GetUnifiedFile: {0} {1} {2} {3}", path,unifiedFile, file, fileSystemInode);

			if (unifiedFile == null)
			{
				unifiedFile = new UnifiedFile(file, fileSystemInode);
				unifiedFile.Path = path;
			}

			// TODO: Check if present and call Modify instead.
			if (file != null)
				unifiedFile.File = file;
			else if (fileSystemInode != null)
				unifiedFile.Inode = fileSystemInode;

			unifiedFile.CalculateStatus();

			return unifiedFile;
		}

		public string BuildRelativePath(string path)
		{
			return path.Substring(Root.Inode.FullName.Length);
		}

		public UnifiedFile AddFile(Google.Apis.Drive.v3.Data.File file)
		{
			return AddFile(GetUnifiedFile(file));
		}
		public UnifiedFile AddFile(FileSystemInfo fileSystemInfo)
		{
			return AddFile(GetUnifiedFile(fileSystemInfo));
		}
		public UnifiedFile AddFile(UnifiedFile file)
		{
			if (PathMap.ContainsKey(file.Path))
			{
			}
			else
			{
				PathMap.Add(file.Path, file);
			}
			return file;
		}

		public UnifiedFile GetFileByPath(string path)
		{ 
			UnifiedFile unifiedFile;
			PathMap.TryGetValue(path, out unifiedFile);
			return unifiedFile;
		}

		public void ScanDisk(UnifiedFile root)
		{
			DirectoryInfo path = root.Inode.DirectoryInfo;
			var folders = new List<UnifiedFile>();

			if (path.Attributes.HasFlag(FileAttributes.ReparsePoint))
				return; // Symbolic link

			foreach (var fi in path.EnumerateFiles())
			{
				Console.WriteLine("{0}", fi.FullName);
				AddFile(fi);
			}

			foreach (var di in path.EnumerateDirectories())
			{
				folders.Add(AddFile(di));
			}

			foreach (var folder in folders)
			{
				ScanDisk(folder);
			}
			
		}

		public void ScanDrive(UnifiedFile root) { } 
		public void FindFiles(UnifiedFile root)
		{
			FilesResource.ListRequest listRequest = DriveService.Files.List();
			listRequest.PageSize = 10;
			listRequest.Fields = "nextPageToken, files(id,kind,mimeType,md5Checksum,modifiedTime,name,parents,size)";
			listRequest.Q = "'" + root.File.Id + "' in parents and trashed != true";

			var folders = new List<UnifiedFile>();
			// Console.WriteLine("Files in " + root.File.Name);
			do
			{
				FileList files = listRequest.Execute();
				listRequest.PageToken = files.NextPageToken;
				foreach (var file in files.Files)
				{
					/*
					Console.WriteLine("  {0} ({1} - {2})", file.Name, file.Id, file.MimeType);
					Console.Write("  Parents:");
					foreach(var parent in file.Parents)
						Console.Write(" {0}", parent);
					Console.WriteLine();
					*/
					// Console.WriteLine(" {2} {3} {4}", file.Name, file.Id, file.Size, file.ModifiedTime, file.Md5Checksum)
					if (file.MimeType == "application/vnd.google-apps.folder")
					{
						folders.Add(AddFile(file));
					}
					else 
					{
						AddFile(file);
					}
				}

			} while (!String.IsNullOrEmpty(listRequest.PageToken));

			foreach (var folder in folders)
			{
				FindFiles(folder);
			}
		}

		public void Converge()
		{
			foreach (var item in PathMap)
			{
				SynchronizeFile(item.Value);
			}
		}

		void SynchronizeFile(UnifiedFile file)
		{
			if (file.Synchronized)
				return;

			Console.WriteLine("SynchronizeFile: {0}", file.Path);

			string fname = Root.Inode.FullName + file.Path;

			// Try to determine approriate flags.
			if (file.IsRemoteUpdate && file.IsLocalUpdate)
			{
				if (file.File.Md5Checksum != file.Inode.Md5Checksum)
				{
					file.HasConflict = true;
				}
			}

			if (file.HasConflict)
			{
				var conflictExt = ".conflict-" + file.File.ModifiedTime.GetValueOrDefault(DateTime.Now)
				                                     .ToUniversalTime().ToString("o");

				if (file.File.ModifiedTime > file.Inode.ModifiedTime)
				{
					file.Inode.MoveTo(file.Inode.FullName + conflictExt);
					file.Inode = null;
				}
				else
				{
					var conflict = new Google.Apis.Drive.v3.Data.File();
					conflict.Name = file.File.Name + conflictExt;
					DriveService.Files.Update(conflict, file.File.Id).Execute();
					file.File = null;
				}
				file.CalculateStatus();
			}

			if ((file.Status & (file.Status - 1)) != 0)
				throw new Exception("Multiple Flags set");
			Console.WriteLine(file.Status);

			Google.Apis.Drive.v3.Data.File gfile;
			UnifiedFile parent;

			switch (file.Status & UnifiedFile.State.SwitchMask)
			{
				case UnifiedFile.State.Synchronized:
					// Nothing to do here folks
					break;
				case UnifiedFile.State.LocalRename:
					Console.WriteLine("Local rename");
					break;
				case UnifiedFile.State.LocalCreate:
					gfile = new Google.Apis.Drive.v3.Data.File();
					parent = PathMap[System.IO.Path.GetDirectoryName(file.Path)];

					gfile.Name = System.IO.Path.GetFileName(file.Path);
					gfile.ModifiedTime = file.Inode.ModifiedTime;
					gfile.Parents = new string[] { parent.File.Id };
					if (file.Inode.IsDirectory)
						gfile.MimeType = "application/vnd.google-apps.folder";

					var createRequest = DriveService.Files.Create(gfile);
					createRequest.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";
					gfile = createRequest.Execute();
					file.File = gfile;
					goto case UnifiedFile.State.LocalUpdate;

				case UnifiedFile.State.LocalUpdate:
					Console.WriteLine("Local Update");

					gfile = new Google.Apis.Drive.v3.Data.File();
					gfile.ModifiedTime = file.Inode.ModifiedTime;

					if (file.Inode.IsFile)
					{
						Console.WriteLine("Uploading");
						var input = new System.IO.StreamReader(fname);
						var updateRequest = DriveService.Files.Update(null, file.File.Id, input.BaseStream, "");
						updateRequest.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";
						updateRequest.Upload();
						if (updateRequest.GetProgress().Status != Google.Apis.Upload.UploadStatus.Completed)
							throw new ApplicationException("Upload failed");
						file.File = updateRequest.ResponseBody;
						input.Close();
					}
					file.Synchronized = true;
					break;
				case UnifiedFile.State.LocalDelete:
					Console.WriteLine("Local Delete");
					/*
					Console.WriteLine("{0} {1} Sync - Remove", file.File.Id, file.Inode.FullName);
					//file.Deleted = true;
					PathMap.Remove(file.Path);
					var trash = new Google.Apis.Drive.v3.Data.File();
					Console.WriteLine(file.File.IsAppAuthorized);
					//if (Config.Trash)
					//DriveService.Files.Update(trash, file.File.Id).Execute();
					DriveService.Files.Delete(file.File.Id).Execute();
					// var request = DriveService.Files.Delete(file.File.Id);
					// request.Execute();
					*/
					break;
				default:
					Console.WriteLine("WTF BBQ!?");
					throw new ApplicationException("Unhandled Synchronization");
			}
		}

		public void DoSync()
		{
			FindFiles(Root);
			ScanDisk(Root);
			Watch();
			Converge();
			Console.WriteLine("Press 'q' to quit the sample.");
			while (Console.Read() != 'q') { };


			/*
			var file = PathMap["/badda"];
			Console.WriteLine("{0} {1}", Root.File.Id, Root.File.Name);
			Console.WriteLine("{0} {1}", file.File.Id, file.File.Name);
			foreach (var parent in file.File.Parents)
				Console.WriteLine("{0}", parent);
			*/
		}

		public void GetDriveChanges()
		{
			string pageToken = savedStartPageToken;
			while (pageToken != null)
			{
				var request = DriveService.Changes.List(pageToken);
				request.IncludeRemoved = true;
				request.Fields = "changes(file(id,md5Checksum,mimeType,modifiedTime,name,parents,size,trashed,version),fileId,removed,time),newStartPageToken,nextPageToken";
				request.Spaces = "drive";

				var changes = request.Execute();
				foreach (var change in changes.Changes)
				{
					Console.WriteLine("{0} {1} {2} {3}", change.File.Id, change.File.Name, change.File.Version, change.TimeRaw);
					// TODO: Handle folders
					if (change.File.MimeType == "application/vnd.google-apps.folder")
						continue;
					
					var path = GetDrivePath(change.File);

					UnifiedFile unifiedFile = null;
					foreach (var file in PathMap.Values)
					{
						if (file.File.Id == change.FileId)
						{
							unifiedFile = file;
							break;
						}
					}

					// New: fileId !in IdMap and DrivePath in Root.
					if (unifiedFile == null)
					{
						Console.WriteLine("{0} {1} Drive - Created", change.FileId, path);
						PendingSync.Add(AddFile(change.File).Path);
					}
					// Existing: fileId in IdMap.
					else
					{
						unifiedFile.Modify(change.File);

						// Delete: remove or trash set.
						if (change.Removed.GetValueOrDefault(false) || change.File.Trashed.GetValueOrDefault(false))
						{
							Console.WriteLine("{0} {1} Drive - Deleted", change.FileId, path);
							// System.IO.File.Delete(unifiedFile.FileSystemInfo.FullName);
							// PendingSync.Add(path);
						}

						// Rename: 
						else if (unifiedFile.File.Name != change.File.Name)
						{
							Console.WriteLine("DriveRenamed: {0} {1}", change.FileId, path);
							try
							{
								// unifiedFile.FileInfo.MoveTo(change.File.Name);
								Console.WriteLine(unifiedFile.Inode.FullName);
							}
							catch (Exception e)
							{
								Console.WriteLine(e);
							}
							// Does parent = parent.
							// Does name = name
						}

						// Change: md5sum mismatch
						else if (unifiedFile.File.Md5Checksum != change.File.Md5Checksum)
						{
							Console.WriteLine("{0} {1} Drive - Changed", change.FileId, path);
						}
						else
						{
							// New uploads fall into this category.
							Console.WriteLine("{0} {1} Drive - Unknown", change.FileId, path);							
						}
					}

					/*
					Console.WriteLine("Change found for file: " + change.FileId);
					Console.WriteLine("  Removed: {0}  Time: {1}", change.Removed, change.Time);
					Console.WriteLine("  Name: {0}  Size: {1}  MD5: {2}", change.File.Name, change.File.Size, change.File.Md5Checksum);
					Console.WriteLine("  MimeType: {0}", change.File.MimeType);
					Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(change));
					*/
				}
				if (changes.NewStartPageToken != null)
				{
					Console.WriteLine("NewStartPageToken: {0}", changes.NewStartPageToken);
					// Last page, save this token for the next polling interval
					savedStartPageToken = changes.NewStartPageToken;
				}
				if (changes.NextPageToken != null)
				{
					Console.WriteLine("NextPageToken: {0}", changes.NextPageToken);
				}
				pageToken = changes.NextPageToken;
			}
		}

		private void ProcessQueue(Object source, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				GetDriveChanges();

				var queue = PendingSync;
				PendingSync = new HashSet<string>();

				Console.WriteLine("Executing sync queue. {0}", queue.Count);
				foreach (var path in queue)
				{
					var unifiedFile = PathMap[path];
					// unifiedFile.Dirty();
					SynchronizeFile(unifiedFile);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		public void Watch()
		{
			var timer = new System.Timers.Timer(5000);
			timer.AutoReset = true;
			timer.Elapsed += ProcessQueue;
			timer.Start();

			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = Root.Inode.FullName;
			watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			watcher.Filter = "*";

			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.Created += new FileSystemEventHandler(OnChanged);
			watcher.Deleted += new FileSystemEventHandler(OnChanged);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);

			watcher.EnableRaisingEvents = true;
		}



		private void OnChanged(object source, FileSystemEventArgs e)
		{
			Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
			var inode = new FileSystemInode(e.FullPath);

			if (e.ChangeType == WatcherChangeTypes.Created)
				AddFile(new FileInfo(e.FullPath));

			var unifiedFile = PathMap[BuildRelativePath(e.FullPath)];
			unifiedFile.Modify(inode);
			PendingSync.Add(unifiedFile.Path);
			// unifiedFile.Dirty();
			// SyncFile(unifiedFile);
		}
		private void OnRenamed(object source, RenamedEventArgs e)
		{
			Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
		}			
	}

	class TreeNode<T>
	{
		List<TreeNode<T>> Children;

		T Item { get; set;}

		public TreeNode(T item)
		{
			Item = item;
		}
		public TreeNode<T> AddChild(T item)
		{
			TreeNode<T> nodeItem = new TreeNode<T>(item);
			Children.Add(nodeItem);
			return nodeItem;
		}
	}

	class MainClass
	{
		// TODO: Scope = DriveService.Scope.Drive? For full read/write.
		static string[] Scopes = { DriveService.Scope.DriveFile, DriveService.Scope.DriveMetadata};
		static string ApplicationName = "Drive API .NET sync";

		static DriveService driveService;
		static string driveFolder;
		static UnifiedFileSystem root;

		public static void Main(string[] args)
		{
			UserCredential credential;

			if (args.Length != 2)
			{
				Console.WriteLine("Usage: Watcher.exe <drive folder> <directory>");
				return;
			}

			// Authenticate
			using (var stream =
				   new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
			{
				string credPath = System.Environment.GetFolderPath(
					System.Environment.SpecialFolder.Personal);
				credPath = Path.Combine(credPath, ".credentials/drive-dotnet-sync.json");

				credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
					GoogleClientSecrets.Load(stream).Secrets,
					Scopes,
					"user",
					CancellationToken.None,
					new FileDataStore(credPath, true)).Result;
				Console.WriteLine("Credential file saved to: " + credPath);
			}

			// Create Service
			var service = new DriveService(new Google.Apis.Services.BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});

			// Get Folder List
			FilesResource.ListRequest listRequest = service.Files.List();
			listRequest.PageSize = 10;
			listRequest.Fields = "nextPageToken, files(explicitlyTrashed,id,kind,mimeType,md5Checksum,modifiedTime,name,size)";
			listRequest.Q = "mimeType='application/vnd.google-apps.folder' and trashed != true";

			// Console.WriteLine("Files:");
			do
			{
				FileList files = listRequest.Execute();
				listRequest.PageToken = files.NextPageToken;
				foreach (var file in files.Files)
				{
					// Console.WriteLine("{0} ({1} - {2})", file.Name, file.Id, file.MimeType);
					// " {2} {3} {4}", file.Name, file.Id, file.Size, file.ModifiedTime, file.Md5Checksum);
					if (file.Name == args[0])
						root = new UnifiedFileSystem(service, file, new System.IO.DirectoryInfo(args[1]));
				}
			} while (!String.IsNullOrEmpty(listRequest.PageToken));

			if (root != null)
			{
				System.IO.Directory.CreateDirectory(args[1]);
				root.DoSync();
			}
			else
			{
				Console.WriteLine("Unable to find Google Drive Folder");
			}
		}
	}
}
