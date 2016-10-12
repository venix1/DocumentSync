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
	class UnifiedFile
	{
		public List<UnifiedFile> Children { get; set;}
		public string Path { get; set;}
		public Google.Apis.Drive.v3.Data.File File { get; set; }
		public FileSystemInfo FileSystemInfo { get; set;}
		public FileInfo FileInfo { get { return FileSystemInfo as FileInfo; } set { FileSystemInfo = value; } }
		public DirectoryInfo DirectoryInfo { get { return FileSystemInfo as DirectoryInfo ; } set { FileSystemInfo = value; } }
		public bool Complete { get; set; }

		public UnifiedFile(Google.Apis.Drive.v3.Data.File file, FileSystemInfo fileSystemInfo)
		{
			Children = new List<UnifiedFile>();
			Complete = false;
				
			File = file;
			FileSystemInfo = fileSystemInfo;
		}

		public void Dirty()
		{
			Complete = false;
		}

		public string FileInfoMd5Checksum
		{
			get
			{
				Console.WriteLine(FileInfo);
				Debug.Assert(FileInfo == null, "Null FileInfo");
				using (var md5 = MD5.Create())
				{
					Console.WriteLine(FileInfo.FullName);
					using (var stream = FileInfo.OpenRead())
					{
						return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
					}
				}
			}
		}
		public string Md5Checksum
		{
			get
			{
				if (Complete || File.Md5Checksum == FileInfoMd5Checksum)
					return File.Md5Checksum;
				else
					throw new Exception("Checksum mismatch");
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
			Root.Complete = true;
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

		public UnifiedFile GetUnifiedFile(Google.Apis.Drive.v3.Data.File file)
		{
			string path = GetDrivePath(file);
			UnifiedFile unifiedFile = GetFileByPath(path);

			if (unifiedFile != null)
			{
				if (unifiedFile.File == null)
					unifiedFile.File = file;
				return unifiedFile;
			}

			unifiedFile = new UnifiedFile(file, null);
			unifiedFile.Path = path;

			if (System.IO.Directory.Exists(unifiedFile.Path))
				unifiedFile.FileSystemInfo = new System.IO.DirectoryInfo(unifiedFile.Path);
			else if (System.IO.File.Exists(unifiedFile.Path))
				unifiedFile.FileSystemInfo = new System.IO.FileInfo(unifiedFile.Path);

			return unifiedFile;
		}

		public string BuildRelativePath(string path)
		{
			return path.Substring(Root.FileSystemInfo.FullName.Length);
		}

		public UnifiedFile GetUnifiedFile(FileSystemInfo fileSystemInfo)
		{
			UnifiedFile unifiedFile;
			string path = BuildRelativePath(fileSystemInfo.FullName);

			PathMap.TryGetValue(path, out unifiedFile);

			if (unifiedFile != null)
			{
				if (unifiedFile.FileSystemInfo == null)
					unifiedFile.FileSystemInfo = fileSystemInfo;
				return unifiedFile;
			}

			unifiedFile = new UnifiedFile(null, fileSystemInfo);
			unifiedFile.Path = path;

			return unifiedFile;
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
			DirectoryInfo path = root.DirectoryInfo;
			var folders = new List<UnifiedFile>();

			if (path.Attributes.HasFlag(FileAttributes.ReparsePoint))
				return; // Symbolic link

			foreach (var fi in path.EnumerateFiles())
			{
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
					// Console.WriteLine(" {2} {3} {4}", file.Name, file.Id, file.Size, file.ModifiedTime, file.Md5Checksum);
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
				SyncFile(item.Value);
			}
		}

		public void SyncFile(UnifiedFile file)
		{
			if (file.Complete)
				return;
			
			string fname = Root.DirectoryInfo.FullName + file.Path;

			if (file.File != null && file.FileInfo != null)
			{
				if (!System.IO.File.Exists(file.FileInfo.FullName))
				{
					Console.WriteLine("{0} {1} Sync - Remove", file.File.Id, file.FileInfo.FullName);
					//file.Deleted = true;
					PathMap.Remove(file.Path);
					var trash = new Google.Apis.Drive.v3.Data.File();
					Console.WriteLine(file.File.IsAppAuthorized);
					DriveService.Files.Update(trash, file.File.Id).Execute();

					return;
				}
				if (file.File.Md5Checksum != file.FileInfoMd5Checksum)
				{
					// Save drive checksums, for conflicts.
					// Console.WriteLine("{0} {1}", file.File.Md5Checksum, file.FileInfoMd5Checksum);
					// Console.WriteLine("{0} {1}", file.File.ModifiedTime, file.FileInfo.LastWriteTime);
					Debug.Assert(file.File.ModifiedTime.HasValue, "No modification time for Google Drive File");

					if (file.File.ModifiedTime > file.FileInfo.LastWriteTime)
					{
						Console.WriteLine("{0} {1} Sync - Update Local", file.File.Id, file.FileInfo.FullName);
						var conflict = ".conflict-" + file.FileInfo.LastWriteTimeUtc.ToString("o");
						Console.WriteLine(conflict);
						// Console.WriteLine("Removing local file");
						file.FileInfo.MoveTo(file.FileInfo.FullName + conflict);
						file.FileInfo = null;
					}
					else
					{
						Console.WriteLine("{0} {1} Sync - Update Drive", file.File.Id, file.FileInfo.FullName);
						var conflict = ".conflict-" + file.File.ModifiedTime.GetValueOrDefault(DateTime.Now).ToUniversalTime().ToString("o");
						// Console.WriteLine("Removing Drive file");
						var trash = new Google.Apis.Drive.v3.Data.File();
						trash.Name = file.File.Name + conflict;
						DriveService.Files.Update(trash, file.File.Id).Execute();

						/*
						var request = DriveService.Files.Delete(file.File.Id);
						request.Execute();
						*/
						file.File = null;
					}
					SyncFile(file);
					return;
				}
				else
				{
					Console.WriteLine("{0} {1} Sync - Metadata", file.File.Id, file.FileInfo.FullName);
					// Update modification time to match Google Drive.
					// Console.WriteLine("{0} {1}", file.FileInfo.LastWriteTime, file.File.ModifiedTime.GetValueOrDefault(file.FileInfo.LastAccessTime));
					/*
					if (file.FileInfo.LastWriteTime != file.File.ModifiedTime.GetValueOrDefault(file.FileInfo.LastAccessTime))
						file.FileInfo.LastWriteTime = file.File.ModifiedTime.GetValueOrDefault(file.FileInfo.LastAccessTime);
					*/
					file.Complete = true;
				}
			}
			else if (file.File != null)
			{
				if (file.File.MimeType == "application/vnd.google-apps.folder")
				{
					System.IO.Directory.CreateDirectory(fname);
					file.DirectoryInfo = new DirectoryInfo(fname);
				}
				else
				{
					var output = new System.IO.StreamWriter(fname);
					var download = DriveService.Files.Get(file.File.Id);
					download.Download(output.BaseStream);
					output.Close();
					file.FileInfo = new FileInfo(fname);
				}
				file.Complete = true;
				Console.WriteLine(file.FileInfo);
			}
			else
			{
				var gfile = new Google.Apis.Drive.v3.Data.File();
				var parent = PathMap[System.IO.Path.GetDirectoryName(file.Path)];
				gfile.Name = System.IO.Path.GetFileName(file.Path);
				gfile.ModifiedTime = file.FileSystemInfo.LastWriteTime;
				gfile.Parents = new string[] { parent.File.Id };
				if (file.DirectoryInfo != null)
					gfile.MimeType = "application/vnd.google-apps.folder";

				if (true)
				{
					var request = DriveService.Files.Create(gfile);
					request.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size";
					gfile = request.Execute();
					Console.WriteLine("{0} {1}", gfile.Id, gfile.Name);
				}
				if (file.FileInfo != null)
				{
					Console.WriteLine("Uploading");
					var input = new System.IO.StreamReader(fname);
					var request = DriveService.Files.Update(null, gfile.Id, input.BaseStream, "");
					request.Upload();
					input.Close();
				}
				file.File = gfile;
				file.Complete = true;
			}
		}

		public void DoSync()
		{
			FindFiles(Root);
			ScanDisk(Root);
			Converge();
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
					Console.WriteLine("{0} {1} {2}", change.File.Name, change.File.Version, change.TimeRaw);
					// TODO: Handle folders
					if (change.File.MimeType == "application/vnd.google-apps.folder")
						continue;
					
					var path = GetDrivePath(change.File);
					// Console.WriteLine("{0} {1}", change.FileId, path);
					UnifiedFile unifiedFile = null;
					foreach (var file in PathMap.Values)
					{
						/*
						Console.WriteLine(file.File.Id);
						Console.WriteLine(change.FileId);
						Console.WriteLine("---");
						*/
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
								Console.WriteLine(unifiedFile.FileInfo.FullName);
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
			GetDriveChanges();

			var queue = PendingSync;
			PendingSync = new HashSet<string>();

			Console.WriteLine("Executing sync queue. {0}", queue.Count);
			foreach (var path in queue)
			{
				var unifiedFile = PathMap[path];
				unifiedFile.Dirty();
				SyncFile(unifiedFile);
			}
		}

		public void Watch()
		{
			var timer = new System.Timers.Timer(5000);
			timer.AutoReset = true;
			timer.Elapsed += ProcessQueue;
			timer.Start();

			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = Root.DirectoryInfo.FullName;
			watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			watcher.Filter = "*";

			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.Created += new FileSystemEventHandler(OnChanged);
			watcher.Deleted += new FileSystemEventHandler(OnChanged);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);

			watcher.EnableRaisingEvents = true;
			Console.WriteLine("Press 'q' to quit the sample.");
			while (Console.Read() != 'q') { };
		}



		private void OnChanged(object source, FileSystemEventArgs e)
		{
			Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
			if (e.ChangeType == WatcherChangeTypes.Created)
				AddFile(new FileInfo(e.FullPath));

			var unifiedFile = PathMap[BuildRelativePath(e.FullPath)];
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
				root.Watch();
			}
			else
			{
				Console.WriteLine("Unable to find Google Drive Folder");
			}
		}
	}
}
