using System;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;

using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace FSync
{
	public class GoogleDriveDocument : IDocument
	{
		GoogleDriveDocumentStore Owner;
		DriveFile Document;

		internal GoogleDriveDocument(GoogleDriveDocumentStore owner, DriveFile document)
		{
			Owner = owner;
			Document = document;

			FullName = Owner.GetPath(this);
		}

		public string Id { get { return Document.Id; } }
		public string Name { get { return Document.Name; } }
		public string FullName { get; private set; }

		public IDocument Parent {
			get {
				if (Document.Parents == null)
					return null;

				if (Document.Parents.Count > 1)
					throw new Exception("Unable to handle multiple Drive Parents");

				return Owner.GetById(Document.Parents[0]);
			}
		}

		public bool Exists { get {return Owner.GetById(Document.Id) != null;} }
		public bool IsDirectory { get { return Document.MimeType == Owner.DirectoryType; } }
		public bool IsFile { get { return !IsDirectory; } }

		public string Md5Checksum { get { return Document.Md5Checksum; } }

		public System.Collections.IEnumerable Children {
			get
			{
				return Owner.GetContents(this);
			}
		}
	}

	public class GoogleDriveDocumentStore : IDocumentStore
	{
		static string[] Scopes = { DriveService.Scope.DriveFile, DriveService.Scope.DriveMetadata };
		static string ApplicationName = "DocumentSync - Google Drive Plugin";

		public readonly string DirectoryType = "application/vnd.google-apps.folder";
		public readonly string RequiredFields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";

		UserCredential credential;
		DriveService DriveService;
		string savedStartPageToken;


		public GoogleDriveDocumentStore()
		{
			// Authenticate
			using (var stream =
				   System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FSync.client_secret.json")) {
				string credPath = System.Environment.GetFolderPath(
					System.Environment.SpecialFolder.Personal);
				credPath = Path.Combine(credPath, ".credentials/drive-dotnet-sync.json");

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

			// var root = new DriveFile();
			// root.Id = "";
			// root.Name = "/";
			//RootDocument = new GoogleDriveDocumentStor
		}

		public GoogleDriveDocumentStore(DriveService driveService) {
			DriveService = driveService;
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

		public void Delete(IDocument arg0) {
			var deleteRequest = DriveService.Files.Delete(arg0.Id);
			Console.WriteLine(deleteRequest.Execute());
		}
		public void MoveTo(IDocument src, IDocument dst) {
			throw new Exception("stub");
			if (dst.IsFile) {
				// Overwrite
			}
			// Update Parent
		}
		public void MoveTo(IDocument src, string name) {
			throw new Exception("stub");
			// Parse name as path
			// Does exist?
			// Is File?
			// Is Directory?
		}

		public IDocument GetById(string id)
		{
			var resource = DriveService.Files.Get(id);
			resource.Fields = RequiredFields;
			return new GoogleDriveDocument(this, resource.Execute());
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

		public IDocument GetByPath(string path) {
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

		public IEnumerable<IDocument> Changes(IDocument document)
		{
			string pageToken = savedStartPageToken;
			while (pageToken != null) {
				var request = DriveService.Changes.List(pageToken);

				request.IncludeRemoved = true;
				request.Fields = String.Format("changes(file({0}),newStartPageToken,nextPageToken", RequiredFields);
				request.Spaces = "drive";

				var changes = request.Execute();
				foreach (var change in changes.Changes) {
					yield return new GoogleDriveDocument(this, change.File);
					/*
						EventArgs syncEvent = null;

						if (change.Removed.GetValueOrDefault(false) || change.File.Trashed.GetValueOrDefault(false)) {
							Console.WriteLine("Removed: {0} {1}", change.FileId, change.File);
							syncEvent = new DriveDeletedEventArgs(GetUnifiedFile(change.FileId));
						} else {
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
					*/
				}
					if (changes.NewStartPageToken != null) {
					Console.WriteLine("NewStartPageToken: {0}", changes.NewStartPageToken);
					// Last page, save this token for the next polling interval
					savedStartPageToken = changes.NewStartPageToken;
				}
				if (changes.NextPageToken != null) {
					Console.WriteLine("NextPageToken: {0}", changes.NextPageToken);
				}
				pageToken = changes.NextPageToken;
			}
		}

	}

	public class GoogleDriveDocumentWatcher : DocumentWatcher
	{
		private GoogleDriveDocumentStore Owner;

		private void Check()
		{

		}
	}
}

