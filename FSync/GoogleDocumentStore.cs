using System;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;
using Mono.Data.Sqlite;

using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace FSync
{
	public class GoogleDriveDocument : IDocument
	{
		GoogleDriveDocumentStore Owner;
		internal DriveFile Document { get; set; }

		internal GoogleDriveDocument(GoogleDriveDocumentStore owner, DriveFile document)
		{
			Owner = owner;
			Document = document;

			FullName = Owner.GetPath(this);
		}

		public string Id { get { return Document.Id; } }
		public string Name { get { return Document.Name; } }
		public string FullName { get; private set; }
		public DateTime CreatedTime { get { return Document.CreatedTime.GetValueOrDefault(DateTime.Now); } }
		public DateTime ModifiedTime { get { return Document.ModifiedTime.GetValueOrDefault(DateTime.Now); } }
		public long Version { get { return Document.Version.GetValueOrDefault(); } }
		public bool Deleted { get; internal set; }

		public IDocument Parent {
			get {
				if (Document.Parents == null)
					return null;

				if (Document.Parents.Count > 1)
					throw new Exception("Unable to handle multiple Drive Parents");

				return Owner.GetById(Document.Parents[0]);
			}
		}

		public bool Trashed { get { return Document.Trashed.GetValueOrDefault(false); } }
		public bool Exists { get { return Owner.GetById(Document.Id) != null; } }
		public bool IsDirectory { get { return Document.MimeType == Owner.DirectoryType; } }
		public bool IsFile { get { return !IsDirectory; } }

		public string Md5Checksum { get { return Document.Md5Checksum; } }

		public System.Collections.IEnumerable Children {
			get {
				return Owner.GetContents(this);
			}
		}

		public void Update(System.IO.Stream stream)
		{
			Owner.Update(this, stream);
		}

		public void Delete()
		{
			Owner.Delete(this);
		}
	}

	public class GoogleDriveChangesEnumerable : IDocumentEnumerable
	{
		Queue<GoogleDriveDocument> Changes { get; set; }

		GoogleDriveDocumentStore Owner { get; set; }
		public string StartPageToken { get; internal set; }
		public string SavedPageToken { get; internal set; }

		internal GoogleDriveChangesEnumerable(GoogleDriveDocumentStore owner, string startPageToken)
		{
			Owner = owner;
			StartPageToken = startPageToken;
			SavedPageToken = StartPageToken;
			Changes = new Queue<GoogleDriveDocument>();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<IDocument> GetEnumerator()
		{
			if (Changes.Count <= 0) {
				var pageToken = StartPageToken;
				foreach (var change in Owner.Changes(ref pageToken)) {
					Changes.Enqueue(change);
				}
				StartPageToken = pageToken;
			}

			var changes = Changes;
			Changes = new Queue<GoogleDriveDocument>();

			return changes.GetEnumerator();
		}
	}

	public class GoogleDriveDocumentStore : IDocumentStore
	{
		static string[] Scopes = { DriveService.Scope.DriveFile, DriveService.Scope.DriveMetadata };
		static string ApplicationName = "DocumentSync - Google Drive Plugin";
		static string ApplicationPath;

		public readonly string DirectoryType = "application/vnd.google-apps.folder";
		public readonly string RequiredFields = "createdTime, id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";

		UserCredential credential;
		DriveService DriveService;
		string savedStartPageToken;

		GoogleDriveDocumentWatcher ChangeThread { get; set; }
		public GoogleDocumentCache Cache { get; private set; }
		// Drive Id Cache

		public GoogleDriveDocumentStore()
		{
			ApplicationPath = Path.Combine(
				System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
				System.Reflection.Assembly.GetExecutingAssembly().GetName().Name
			);
			Console.WriteLine("Application Path: {0}", ApplicationPath);

			// Authenticate
			using (var stream =
				   System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FSync.client_secret.json")) {
				var credPath = Path.Combine(ApplicationPath, ".credentials/drive-dotnet-sync.json");

				credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
					GoogleClientSecrets.Load(stream).Secrets,
					Scopes,
					"user",
					System.Threading.CancellationToken.None,
					new FileDataStore(credPath, true)).Result;
				Console.WriteLine("Credential file saved to: " + credPath);
			}

			DriveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer() {
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});

			// TODO: Encapsulate into Wrapper Class.
			// TODO: GoogleDocumentCache should interface with SQLite class and not expose it.
			var dbname = System.IO.Path.Combine(ApplicationPath, "dsync.db");
			var db = new System.Data.SQLite.SQLiteConnection("Data Source=" + dbname);
			Cache = new GoogleDocumentCache(db);

			ChangeThread = new GoogleDriveDocumentWatcher(this);
			ChangeThread.EnableRaisingEvents = true;
			// var root = new DriveFile();
			// root.Id = "";
			// root.Name = "/";
			//RootDocument = new GoogleDriveDocumentStor
		}

		public GoogleDriveDocumentStore(DriveService driveService)
		{
			DriveService = driveService;
		}

		protected GoogleDriveDocument EncapsulateDocument(DriveFile file)
		{
			// Update Cache
			// Update Index
			return new GoogleDriveDocument(this, file);
		}

		public IDocument Create(string path, DocumentType type)
		{
			var tail = System.IO.Path.GetFileName(path);
			var head = System.IO.Path.GetDirectoryName(path);

			return Create(GetByPath(head), tail, type);
		}

		public IDocument Create(IDocument parent, string name, DocumentType type)
		{
			// Verify existence of Parent.
			var file = new DriveFile();
			file.Name = name;
			// TODO: Expand Creation options.
			file.ModifiedTime = DateTime.Now;

			if (parent != null) {
				var pfile = GetById(parent.Id);
				file.Parents = new string[] { pfile.Id };
			}

			if (type == DocumentType.Directory)
				file.MimeType = DirectoryType;

			var createRequest = DriveService.Files.Create(file);
			createRequest.Fields = RequiredFields;
			file = createRequest.Execute();

			return new GoogleDriveDocument(this, file);
		}

		public void Update(IDocument document, Stream stream)
		{
			var updateRequest = DriveService.Files.Update(null, document.Id, stream, "");
			updateRequest.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";
			updateRequest.Upload();
			if (updateRequest.GetProgress().Status != Google.Apis.Upload.UploadStatus.Completed)
				throw updateRequest.GetProgress().Exception;

			// TODO: Update DriveFile Cache
			// Return new Document;
		}

		public void Delete(IDocument arg0)
		{
			var deleteRequest = DriveService.Files.Delete(arg0.Id);
			Console.WriteLine(deleteRequest.Execute());
		}

		public void MoveTo(IDocument src, IDocument dst)
		{
			throw new Exception("stub");
			if (dst.IsFile) {
				// Overwrite
			}
			// Update Parent
		}
		public void MoveTo(IDocument src, string name)
		{
			throw new Exception("stub");
			// Parse name as path
			// Does exist?
			// Is File?
			// Is Directory?
		}

		public IDocument GetById(string id)
		{
			// Check Cache First
			GoogleDriveDocument document;
			Cache.Documents.TryGetValue(id, out document);
			if (document == null) {
				var resource = DriveService.Files.Get(id);
				resource.Fields = RequiredFields;

				try {
					document = EncapsulateDocument(resource.Execute());
				} catch (Google.GoogleApiException e) {
					if (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
						return null;
				}
			}

			return document;
		}

		public IEnumerable<IDocument> GetContents(IDocument document)
		{
			FilesResource.ListRequest listRequest = DriveService.Files.List();
			listRequest.PageSize = 10;
			listRequest.Fields = String.Format("nextPageToken, files({0})", RequiredFields);
			listRequest.Q = String.Format("'{0}' in parents and trashed != true", document.Id);

			do {
				FileList files = listRequest.Execute();
				listRequest.PageToken = files.NextPageToken;
				foreach (var file in files.Files) {
					yield return new GoogleDriveDocument(this, file);
				}
			} while (!String.IsNullOrEmpty(listRequest.PageToken));
		}

		public IDocument GetByPath(string path)
		{
			IDocument dir = GetById("root");
			if (String.IsNullOrEmpty(path) || path == "/") {
				return dir;
			}

			var separators = new char[] {
				Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar
			};
			var paths = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);

			foreach (var folder in paths) {
				if (!dir.IsDirectory)
					throw new Exception("Path not a directory");

				bool found = false;
				foreach (IDocument file in dir.Children) {
					if (file.Name == folder) {
						found = true;
						dir = file;
						break;
					}
				}
				if (!found)
					throw new Exception("Path not found");
			}

			return dir;
		}

		/*
		private string GetPath(DriveFile file)
		{
		}
		*/

		public string GetPath(IDocument document)
		{
			List<string> paths = new List<string>();

			IDocument parent = document;
			do {
				paths.Insert(0, parent.Name);
				parent = parent.Parent;
			} while (parent != null);

			paths.Insert(0, "/");
			return System.IO.Path.Combine(paths.ToArray());
		}

		public GoogleDriveChangesEnumerable GetChangeLog(string startPageToken = null)
		{
			string savedStartPageToken = startPageToken;

			if (startPageToken == null) {
				var response = DriveService.Changes.GetStartPageToken().Execute();
				Console.WriteLine("Start token: " + response.StartPageTokenValue);
				savedStartPageToken = response.StartPageTokenValue;
			}

			return new GoogleDriveChangesEnumerable(this, savedStartPageToken);
		}


		public List<GoogleDriveDocument> Changes(ref string pageToken)
		{
			var list = new List<GoogleDriveDocument>();
			string newStartPageToken = null;
			while (pageToken != null) {
				Console.WriteLine("PageToken: {0}", pageToken);
				var request = DriveService.Changes.List(pageToken);

				request.IncludeRemoved = true;
				request.Fields = String.Format("changes(fileId,kind,removed,time,file({0})),newStartPageToken,nextPageToken", RequiredFields);
				request.Spaces = "drive";

				var changes = request.Execute();
				foreach (var change in changes.Changes) {
					// TODO: Special Handling of Delete event
					Console.WriteLine("Change: {0} {1} {2}", change.FileId, change.Removed, change.TimeRaw);
					GoogleDriveDocument document;
					if (change.Removed.Value) {
						Cache.Documents.TryGetValue(change.FileId, out document);
						Cache.Documents.Remove(change.FileId);
						if (document == null) {
							var file = new DriveFile {
								Id = change.FileId
							};
							document = new GoogleDriveDocument(this, file);
						}
					} else {
						document = new GoogleDriveDocument(this, change.File);
						Cache.Add(document);
					}
					list.Add(document);
				}

				if (changes.NewStartPageToken != null) {
					Console.WriteLine("NewStartPageToken: {0}", changes.NewStartPageToken);
				}

				if (changes.NextPageToken != null) {
					Console.WriteLine("NextPageToken: {0}", changes.NextPageToken);
				}
				newStartPageToken = changes.NewStartPageToken;
				pageToken = changes.NextPageToken;
			}
			pageToken = newStartPageToken;
			Console.WriteLine("StartPageToken: {0}", pageToken);

			return list;
		}
		// Whatever is returned by this, should be recallable.
		// Return Closure, to maintain StartPageToken
		/*
		public delegate  DocumentChangeLog();
		public DocumentChangeLog Changes(string startPageToken)
		{
			return delegate () {
				string pageToken = startPageToken;
				while (pageToken != null) {
					var request = DriveService.Changes.List(pageToken);

					request.IncludeRemoved = true;
					request.Fields = String.Format("changes(file({0}),newStartPageToken,nextPageToken", RequiredFields);
					request.Spaces = "drive";

					var changes = request.Execute();
					foreach (var change in changes.Changes) {
						yield return new GoogleDriveDocument(this, change.File);
					}
					if (changes.NewStartPageToken != null) {
						Console.WriteLine("NewStartPageToken: {0}", changes.NewStartPageToken);
						// Last page, save this token for the next polling interval
						startPageToken = changes.NewStartPageToken;
					}
					if (changes.NextPageToken != null) {
						Console.WriteLine("NextPageToken: {0}", changes.NextPageToken);
					}
					pageToken = changes.NextPageToken;
				}
				return null;
			};
		}
		*/

		public DocumentWatcher Watch()
		{
			return new GoogleDriveDocumentWatcher(this);
		}
	}

	public class GoogleDriveDocumentWatcher : DocumentWatcher
	{
		private DateTime begin;
		private System.Threading.Thread PollThread { get; set; }
		private GoogleDriveDocumentStore Owner { get; set; }

		private GoogleDriveChangesEnumerable ChangeLog { get; set; }

		internal GoogleDriveDocumentWatcher(GoogleDriveDocumentStore owner)
		{
			Owner = owner;
			ChangeLog = Owner.GetChangeLog();

			// Thread instead of Timer for precision
			PollThread = new System.Threading.Thread(() => {
				begin = DateTime.UtcNow;
				Check();
				System.Threading.Thread.Sleep((5000 - (DateTime.UtcNow - begin).Milliseconds));
			});
			PollThread.Start();
		}

		~GoogleDriveDocumentWatcher()
		{
			PollThread.Abort();
		}

		public override DocumentEventArgs Classify(IDocument change)
		{
			Console.WriteLine("{0} {1}", change.Id, change.FullName);
			Console.WriteLine("\t{0}\n\t{1}", change.CreatedTime, change.ModifiedTime);

			// Note: Is event time required for classification?

			// Deleted. Detected via Metdata.
			if (!change.Exists || change.Trashed) {
				//Console.WriteLine("Removed: {0} {1}", change.Id, change.FullName);
				return new DocumentEventArgs(DocumentChangeType.Deleted, change);
			} else {
				if (change.CreatedTime >= change.ModifiedTime)
					return new DocumentEventArgs(DocumentChangeType.Created, change);
				else
					return new DocumentEventArgs(DocumentChangeType.Changed, change);
				// This may require a cache.
				// events.Add(new DocumentEventArgs(DocumentChangeType.Renamed, change));
				/*
					Console.WriteLine("{0} {1} {2}", syncEvent, change.FileId, (change.File != null) ? change.File.Name : null);
					SyncQueue.Enqueue(syncEvent);
				*/
			}
		}

		private void Check()
		{
			var events = new List<DocumentEventArgs>();

			foreach (var change in ChangeLog) {
				events.Add(Classify(change));

				// Collapse Events
				if (!EnableRaisingEvents)
					return;

				// Dispatch Events
				foreach (var e in events) {
					switch (e.ChangeType) {
						case DocumentChangeType.Created:
							if (Created != null) {
								Created(this, e);
							}
							break;
						case DocumentChangeType.Changed:
							if (Changed != null) {
								Changed(this, e);
							}
							break;
						case DocumentChangeType.Deleted:
							if (Deleted != null) {
								Deleted(this, e);
							}
							break;
						case DocumentChangeType.Renamed:
							if (Renamed != null) {
								Renamed(this, e);
							}
							break;
					}
				}
			}
		}
	}
	}
