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

using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace FSync
{
	public abstract class DriveSyncEventArgs : EventArgs
	{
		public UnifiedFile UnifiedFile { get; set; }
		public GoogleFile OldFile { get; set;}
		public GoogleFile NewFile { get; set;}

		public DriveSyncEventArgs(UnifiedFile unifiedFile)
		{
			UnifiedFile = unifiedFile;
		}
	}

	public class DriveCreatedEventArgs : DriveSyncEventArgs
	{
		public DriveCreatedEventArgs(UnifiedFile unifiedFile) : base(unifiedFile)
		{
		}
	}

	public class DriveChangedEventArgs : DriveSyncEventArgs
	{
		public DriveChangedEventArgs(UnifiedFile unifiedFile) : base(unifiedFile)
		{
		}

		public DriveChangedEventArgs(UnifiedFile unifiedFile, GoogleFile file) : base(unifiedFile)
		{
		}
	}

	public class DriveDeletedEventArgs : DriveSyncEventArgs
	{
		public DriveDeletedEventArgs(UnifiedFile unifiedFile) : base(unifiedFile)
		{
		}
	}

	public class DriveRenamedEventArgs : DriveSyncEventArgs
	{
		public UnifiedFile OldFile;

		public DriveRenamedEventArgs(UnifiedFile oldFile, UnifiedFile newFile) : base(newFile)
		{
			OldFile = oldFile;
		}
	}

	public abstract class FileSyncEventArgs : EventArgs
	{
		public UnifiedFile UnifiedFile { get; set;}
		public FileSystemInode Inode;

		public FileSyncEventArgs(UnifiedFile unifiedFile)
		{
			UnifiedFile = unifiedFile;
		}
	}

	public class FileChangedEventArgs : FileSyncEventArgs
	{
		public FileChangedEventArgs(UnifiedFile unifiedFile) : base(unifiedFile)
		{ }
	}

	public class FileCreatedEventArgs : FileSyncEventArgs
	{
		public FileCreatedEventArgs(UnifiedFile unifiedFile) : base(unifiedFile)
		{ }
	}

	public class FileDeletedEventArgs : FileSyncEventArgs
	{
		public FileDeletedEventArgs(UnifiedFile unifiedFile) : base(unifiedFile)
		{ }
	}

	public class FileRenamedEventArgs : FileSyncEventArgs
	{
		public string OldFullPath { get; set;}

		public FileRenamedEventArgs(string oldPath, UnifiedFile unifiedFile) : base(null)
		{
			OldFullPath = oldPath;
		}
	}
	/*
	public struct ChangeEvent
	{
		public string source;
		public string path;
		public ChangeEventTypes type;
		public Google.Apis.Drive.v3.Data.File file;
		public FileSystemInode inode;
	}
	*/

	public class FileSystemInode
	{
		public FileSystemInfo FileSystemInfo { get; set; }
		public FileInfo FileInfo { get { return FileSystemInfo as FileInfo; } set { FileSystemInfo = value; } }
		public DirectoryInfo DirectoryInfo { get { return FileSystemInfo as DirectoryInfo; } set { FileSystemInfo = value; } }

		public DateTime ModifiedTime { get { return FileSystemInfo.LastWriteTime; } }

		string mMd5Checksum;

		void CalculateMd5Checksum()
		{
			if (FileInfo == null || !FileInfo.Exists)
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
		public FileSystemInode(string path)
		{
			if (System.IO.Directory.Exists(path))
				FileSystemInfo = new DirectoryInfo(path);
			else if (System.IO.File.Exists(path))
				FileSystemInfo = new FileInfo(path);
		}

		public FileSystemInode(FileSystemInfo fileSystemInfo)
		{
			FileSystemInfo = fileSystemInfo;
		}

		public void Create()
		{
			throw new NotImplementedException();
		}

		public bool IsDirectory
		{
			get { return DirectoryInfo != null; }
		}

		public bool Exists {
			get {
				if (FileSystemInfo == null)
					return false;

				return FileSystemInfo.Exists;
			}
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
				if (mMd5Checksum == null && IsFile)
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

	public class UnifiedFile
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

			Deleted = LocalDelete | RemoteDelete,
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

		public bool IsDeleted
		{
			get
			{
				return (mStatus & State.Deleted) > 0;
			}
		}

		// Compare File and Inode to generate state
		public void CalculateStatus()
		{
			if (File == null && Inode == null)
			{
				Console.WriteLine("CalculateStatus: Delete");
			}
			else if (File == null)
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
				// Console.WriteLine("Exists: {0} {1} {2}", File.Md5Checksum, Inode, Inode.FileSystemInfo);
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

		public bool IsDirectory
		{
			get
			{
				if (Inode != null && Inode.IsDirectory)
					return true;
				if (File != null && File.MimeType == "application/vnd.google-apps.folder")
					return true;
				return false;
			}
		}

		public bool IsFile
		{
			get
			{
				return !IsDirectory;
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

		public void Dirty(Google.Apis.Drive.v3.Data.File file)
		{
			if (file == null || File == null)
				return;
			if (file.Version == File.Version)
				return;
			if (file.Trashed.GetValueOrDefault(false))
			{
				mStatus = State.RemoteDelete;
			}
			else if (file.Md5Checksum != File.Md5Checksum)
			{
				mStatus = State.RemoteUpdate;
			}
			else
			{
				throw new NotImplementedException();
			}
		}
		public void Dirty(FileSystemInode file)
		{
			if (file == null)
				return;

			if (File == null)
				mStatus = State.LocalCreate;
			else if (file.FileSystemInfo == null)
				mStatus = State.LocalDelete;
			else if (file.FullName != Inode.FullName)
				mStatus = State.LocalRename;
			else
			{
				if (File.Md5Checksum != file.Md5Checksum)
					mStatus = State.LocalUpdate;
			}
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

	public class UnifiedFileSystem
	{
		IDictionary<Type, EventHandler> mEventHandlers;
		Queue<EventArgs> SyncQueue { get; set; }
		Dictionary<string, UnifiedFile> PathMap { get; set;}
		Dictionary<string, string> IdMap { get; set;}

		string savedStartPageToken;

		UnifiedFile Root { get; set;}
		DriveService DriveService { get; set;}

		public UnifiedFileSystem(DriveService driveService, Google.Apis.Drive.v3.Data.File file, FileSystemInfo path)
		{
			SyncQueue = new Queue<EventArgs>();
			PathMap = new Dictionary<string, UnifiedFile>();
			IdMap = new Dictionary<string, string>();
			mEventHandlers = new Dictionary<Type, EventHandler>();

			// mEventHandlers[typeof(DriveChangedEventArgs)] = OnDriveChanged;

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

			string path;
			if (parent.Id == Root.File.Id)
			{
				paths.Insert(0, "/");
				path = System.IO.Path.Combine(paths.ToArray());
				IdMap[file.Id] = path;
				return path;
			}
			else
			{
				return System.IO.Path.Combine(paths.ToArray());
			}
		}

		public UnifiedFile GetUnifiedFile(params object[] args)
		{
			if (args.Length > 2)
				throw new ArgumentException("Too many arugments passed.");

			Google.Apis.Drive.v3.Data.File file = null;
			FileSystemInode fileSystemInode = null;
			string path = null;

			foreach (var arg in args)
			{
				if (arg is Google.Apis.Drive.v3.Data.File) {
					if (file != null)
						throw new ArgumentException("Only one Drive File may be passed.");
					file = (Google.Apis.Drive.v3.Data.File)arg;
				} else if (arg is FileSystemInfo) {
					if (fileSystemInode != null)
						throw new ArgumentException("Multiple FileSystemInfo or FileSystemInode objects passed.");
					fileSystemInode = new FileSystemInode((FileSystemInfo)arg);
				} else if (arg is FileSystemInode) {
					if (fileSystemInode != null)
						throw new ArgumentException("Only one FileSystemInfo or FileSystemInode can be used.");
					fileSystemInode = (FileSystemInode)arg;
				} else if (arg is string) {
					path = (string)arg;
					if (path[0] == '/')
						fileSystemInode = new FileSystemInode(path);
					else if (path.StartsWith("0B_")) {
						foreach (var f in PathMap.Values) {
							if (f.File != null && f.File.Id == path) {
								file = f.File;
								path = null;
								break;
							}
						}
						if (file == null) {
							Console.WriteLine("File not monitored. {0}", path);
							// throw new DriveFileNotMonitoredException(path);
							return null;
						}
					}
					else
						throw new Exception("Only paths supported");
				}
				else if (arg == null)
				{ }
				else
				{
					throw new ArgumentException("Argument must be Google.Apis.Drive.v3.Data.Files, FileSystemInfo, or FileSystemInode");
				}
			}



			if (path != null)
				path = BuildRelativePath(path);
			else if (file != null)
				path = GetDrivePath(file);
			else if (fileSystemInode != null)
				path = BuildRelativePath(fileSystemInode.FullName);

			UnifiedFile unifiedFile;

			// TODO: This should be a query function.
			PathMap.TryGetValue(path, out unifiedFile);
			Console.WriteLine("GetUnifiedFile: {0} {1} {2}:{3}", path, unifiedFile, file, fileSystemInode);

			if (unifiedFile == null)
			{
				unifiedFile = new UnifiedFile(file, fileSystemInode);
				unifiedFile.Path = path;
				PathMap[path] = unifiedFile;
			}

			// TODO: Check if present and call Modify instead.
			if (file != null)
				unifiedFile.File = file;
			else if (fileSystemInode != null)
				unifiedFile.Inode = fileSystemInode;

			return unifiedFile;
		}
		public UnifiedFile GetUnifiedFileById(string id)
		{
			foreach (var file in PathMap.Values)
			{
				if (file.File.Id == id)
					return file;
			}
			return null;
		}

		public string BuildLocalPath(string path)
		{
			return Root.Inode.FullName + path;
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
				AddFile(fi).CalculateStatus();
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

		public void ScanDrive(UnifiedFile root) 
		{
			FilesResource.ListRequest listRequest = DriveService.Files.List();
			listRequest.PageSize = 10;
			listRequest.Fields = "nextPageToken, files(id,kind,mimeType,md5Checksum,modifiedTime,name,parents,size)";
			listRequest.Q = "'" + root.File.Id + "' in parents and trashed != true";

			var folders = new List<UnifiedFile>();
			do
			{
				FileList files = listRequest.Execute();
				listRequest.PageToken = files.NextPageToken;
				foreach (var file in files.Files)
				{
					if (file.MimeType == "application/vnd.google-apps.folder")
					{
						folders.Add(AddFile(file));
					}
					else 
					{
						AddFile(file).CalculateStatus();
					}
				}

			} while (!String.IsNullOrEmpty(listRequest.PageToken));

			foreach (var folder in folders)
			{
				ScanDrive(folder);
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

			Console.WriteLine("SynchronizeFile: {0} {1}", file.Path, file.Status);

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

			Console.WriteLine("Status: {0}", file.Status);
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
							throw updateRequest.GetProgress().Exception;
						file.File = updateRequest.ResponseBody;
						input.Close();
					}
					file.Synchronized = true;
					break;
				case UnifiedFile.State.LocalDelete:
					Console.WriteLine("Local Delete");
					// file.Delete();

					//if (Config.Trash)
					// var trash = new Google.Apis.Drive.v3.Data.File();
					// DriveService.Files.Update(trash, file.File.Id).Execute();
					DriveService.Files.Delete(file.File.Id).Execute();
					PathMap.Remove(file.Path);
					file.File = null;
					file.Inode = null;
					break;
				case UnifiedFile.State.RemoteCreate:
					FileSystemInfo fileSystemInfo;

					if (file.IsDirectory)
					{
						var path = BuildLocalPath(file.Path);
						fileSystemInfo = System.IO.Directory.CreateDirectory(path);
					}
					else if (file.IsFile)
					{
						var path = BuildLocalPath(file.Path);
						Console.WriteLine(path);
						using (System.IO.File.Create(path))
						{ }
						fileSystemInfo = new FileInfo(path);
					}
					else
					{
						throw new ApplicationException("");
					}

					file.Inode = new FileSystemInode(fileSystemInfo);
					// Set metadata

					goto case UnifiedFile.State.RemoteUpdate;
				case UnifiedFile.State.RemoteUpdate:
					if (file.Inode.IsFile)
					{
						var output = new System.IO.StreamWriter(file.Inode.FullName);
						var download = DriveService.Files.Get(file.File.Id);
						download.Download(output.BaseStream);
						output.Close();
					}
					break;
				case UnifiedFile.State.RemoteRename:
					break;
				case UnifiedFile.State.RemoteDelete:
					break;
				default:
					Console.WriteLine("WTF BBQ!?");
					throw new ApplicationException("Unhandled Synchronization: " + file.Status);
			}
			file.CalculateStatus();
		}

		public void DoSync()
		{
			ScanDrive(Root);
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
					EventArgs syncEvent = null;

					if (change.Removed.GetValueOrDefault(false) || change.File.Trashed.GetValueOrDefault(false))
					{
						Console.WriteLine("Removed: {0} {1}", change.FileId, change.File);
						syncEvent = new DriveDeletedEventArgs(GetUnifiedFile(change.FileId));
					}
					else
					{
						UnifiedFile unifiedFile = GetUnifiedFileById(change.FileId);

						if (unifiedFile == null)
							syncEvent = new DriveCreatedEventArgs(unifiedFile);
						else if (unifiedFile.File.Name != change.File.Name)
							syncEvent = new DriveRenamedEventArgs(unifiedFile, GetUnifiedFile(change.File));
						else
							syncEvent = new DriveChangedEventArgs(unifiedFile);
					}
					Console.WriteLine("{0} {1} {2}", syncEvent, change.FileId, (change.File != null) ? change.File.Name : null);
					SyncQueue.Enqueue(syncEvent);
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

		void ExecuteDriveChangedEvent(DriveChangedEventArgs e)
		{
			Console.WriteLine("Executing Drive Changed Event");
			if (e.UnifiedFile.File.Version == e.UnifiedFile.File.Version)
				return;

			e.UnifiedFile.File = e.NewFile;
			if (e.UnifiedFile.Inode.IsFile && e.NewFile.Md5Checksum != e.UnifiedFile.Inode.Md5Checksum)
			{
				var output = new System.IO.StreamWriter(e.UnifiedFile.Inode.FullName);
				var download = DriveService.Files.Get(e.UnifiedFile.File.Id);
				download.Download(output.BaseStream);

				output.Close();
			}
		}

		void ExecuteDriveCreatedEvent(DriveCreatedEventArgs e)
		{
			Console.WriteLine("Executing Drive Created Event");
			FileSystemInfo fileSystemInfo;

			if (!e.UnifiedFile.Inode.Exists)
				return;

			if (e.UnifiedFile.IsDirectory)
			{
				var path = BuildLocalPath(e.UnifiedFile.Path);
				fileSystemInfo = System.IO.Directory.CreateDirectory(path);
			}
			else if (e.UnifiedFile.IsFile)
			{
				var path = BuildLocalPath(e.UnifiedFile.Path);
				Console.WriteLine(path);
				using (System.IO.File.Create(path))
				{ }
				fileSystemInfo = new FileInfo(path);
			}
			else
			{
				throw new ApplicationException("");
			}

			e.UnifiedFile.Inode = new FileSystemInode(fileSystemInfo);
		}

		void ExecuteDriveDeletedEvent(DriveDeletedEventArgs e)
		{
			Console.WriteLine("Executing Drive Deleted event");
		}

		void ExecuteDriveRenamedEvent(DriveRenamedEventArgs e)
		{
			Console.WriteLine("Executing Drive Renamed Event");
		}

		void ExecuteFileChangedEvent(FileChangedEventArgs e)
		{
			Console.WriteLine(e.UnifiedFile);
			Console.WriteLine(e.UnifiedFile.Path);
			Console.WriteLine(e.UnifiedFile.Inode);
			if (!e.UnifiedFile.Inode.Exists)
				return;

			if (e.UnifiedFile.Inode.Md5Checksum == e.UnifiedFile.File.Md5Checksum)
				return;
			
			Console.WriteLine("Executing File Changed event");
			var gfile = new Google.Apis.Drive.v3.Data.File();
			gfile.ModifiedTime = e.UnifiedFile.Inode.ModifiedTime;

			if (e.UnifiedFile.IsFile)
			{
				string fname = Root.Inode.FullName + e.UnifiedFile.Path;

				var input = new System.IO.StreamReader(fname);
				var updateRequest = DriveService.Files.Update(null, e.UnifiedFile.File.Id, input.BaseStream, "");
				updateRequest.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";
				updateRequest.Upload();
				if (updateRequest.GetProgress().Status != Google.Apis.Upload.UploadStatus.Completed)
					throw updateRequest.GetProgress().Exception;
				e.UnifiedFile.File = updateRequest.ResponseBody;
				input.Close();
			}
		}

		void ExecuteFileCreatedEvent(FileCreatedEventArgs e)
		{
			if (!e.UnifiedFile.Inode.Exists)
				return;
			
			Console.WriteLine("Executing File created event");
			var gfile = new Google.Apis.Drive.v3.Data.File();
			var parent = PathMap[System.IO.Path.GetDirectoryName(e.UnifiedFile.Path)];

			gfile.Name = System.IO.Path.GetFileName(e.UnifiedFile.Path);
			gfile.ModifiedTime = e.UnifiedFile.Inode.ModifiedTime;
			gfile.Parents = new string[] { parent.File.Id };
			if (e.UnifiedFile.Inode.IsDirectory)
				gfile.MimeType = "application/vnd.google-apps.folder";
			else
			{
				// System.Web.MimeMapping.GetMimeMapping(e.UnifiedFile.Inode.FullName);
			}
			var createRequest = DriveService.Files.Create(gfile);
			createRequest.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";
			gfile = createRequest.Execute();
			e.UnifiedFile.File = gfile;
		}

		void ExecuteFileDeletedEvent(FileDeletedEventArgs e)
		{
			Console.WriteLine("Executing File Deleted Event");
			if (e.UnifiedFile.File == null)
				return;
			DriveService.Files.Delete(e.UnifiedFile.File.Id).Execute();
			PathMap.Remove(e.UnifiedFile.Path);
		}

		void ExecuteFileRenamedEvent(FileRenamedEventArgs e)
		{
			Console.WriteLine("Executing File Renamed Event");
			// Parent changed?
		}

		private void ProcessQueue(Object source, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				GetDriveChanges();

				var queue = SyncQueue;
				SyncQueue = new Queue<EventArgs>();

				Console.WriteLine("Executing sync queue. {0}", queue.Count);
				foreach (var change in queue)
				{
					Console.WriteLine("Change: {0}", change.GetType());

					if (change is DriveChangedEventArgs)
						ExecuteDriveChangedEvent((DriveChangedEventArgs)change);
					else if (change is DriveCreatedEventArgs)
						ExecuteDriveCreatedEvent((DriveCreatedEventArgs)change);
					else if (change is DriveDeletedEventArgs)
						ExecuteDriveDeletedEvent((DriveDeletedEventArgs)change);
					else if (change is DriveRenamedEventArgs)
						ExecuteDriveRenamedEvent((DriveRenamedEventArgs)change);

					else if (change is FileChangedEventArgs)
						ExecuteFileChangedEvent((FileChangedEventArgs)change);
					else if (change is FileCreatedEventArgs)
						ExecuteFileCreatedEvent((FileCreatedEventArgs)change);
					else if (change is FileDeletedEventArgs)
						ExecuteFileDeletedEvent((FileDeletedEventArgs)change);
					else if(change is FileRenamedEventArgs)
						ExecuteFileRenamedEvent((FileRenamedEventArgs)change);
					else
					{ } // throw new Exception("Unknown Event type");

					/*
					if (!PathMap.ContainsKey(change.path))
						AddFile(GetUnifiedFile(change.file, change.inode));
					var unifiedFile = PathMap[change.path];

					unifiedFile.Dirty(change.file);
					unifiedFile.Dirty(change.inode);
					SynchronizeFile(unifiedFile);
					*/
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
			watcher.Filter = "";

			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.Created += new FileSystemEventHandler(OnChanged);
			watcher.Deleted += new FileSystemEventHandler(OnChanged);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);

			watcher.EnableRaisingEvents = true;
		}

		private void OnChanged(object source, FileSystemEventArgs e)
		{
			lock(SyncQueue)
			{
				try
				{
					Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
					EventArgs syncEvent = null;
					UnifiedFile unifiedFile = GetUnifiedFile(e.FullPath);
					switch (e.ChangeType)
					{
						case WatcherChangeTypes.Changed:
							if (!System.IO.File.Exists(e.FullPath)) {
								Console.WriteLine("Out of Order event, Discarding");
								break;
							}
							syncEvent = new FileChangedEventArgs(unifiedFile);
							break;
						case WatcherChangeTypes.Created:
							if (!System.IO.File.Exists(e.FullPath)) {
								Console.WriteLine("Out of Order event, Discarding");
								break;
							}
							syncEvent = new FileCreatedEventArgs(unifiedFile);
							break;
						case WatcherChangeTypes.Deleted:
							if (System.IO.File.Exists(e.FullPath)) {
								Console.WriteLine("Out of Order event, Discarding");
								break;
							}
							syncEvent = new FileDeletedEventArgs(unifiedFile);
							break;
						default:
							throw new Exception("unhandled ChangeType");
					}
					if (syncEvent != null)
						SyncQueue.Enqueue(syncEvent);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
			}
			}
		}

		private void OnRenamed(object source, RenamedEventArgs e)
		{
			lock(SyncQueue)
			{
				Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
				SyncQueue.Enqueue(new FileRenamedEventArgs(e.OldFullPath, GetUnifiedFile(e.FullPath)));
			}
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
