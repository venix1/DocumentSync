using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;

using DriveFile = Google.Apis.Drive.v3.Data.File;

using System.Reflection;

namespace DocumentSync.Backend.Google {

    public class GoogleDriveDocument : IDocument {
        public IDocumentStore Owner { get; private set; }
        internal DriveFile Document { get; set; }

        internal GoogleDriveDocument(GoogleDriveDocumentStore owner, DriveFile document) {
            Owner = owner;
            Document = document;

            FullName = owner.GetPath(this);
        }

        public string Id => Document.Id;
        public string Name => Document.Name;
        public string FullName { get; private set; }
        public long Size => Document.Size.GetValueOrDefault(0);
        public DateTime CreatedTime => Document.CreatedTime.GetValueOrDefault(DateTime.Now);
        public DateTime ModifiedTime {
            get => Document.ModifiedTime.GetValueOrDefault(DateTime.Now);
            set {
                Document.ModifiedTime = value; Owner.Update(this);
            }
        }
        public long Version => Document.Version.GetValueOrDefault();
        public bool Deleted {
            get {
                throw new System.NotImplementedException();
            }

            set {
                throw new System.NotImplementedException();
            }
        }

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
        //public bool IsDirectory { get { return Document.MimeType == Owner.DirectoryType; } }
        public bool IsDirectory => Document.MimeType == GoogleDriveDocumentStore.DirectoryType;
        public bool IsFile { get { return !IsDirectory; } }

        public string Md5Checksum { get { return Document.Md5Checksum; } }

        public System.Collections.IEnumerable Children {
            get {
                return Owner.List(this);
            }
        }

        public StreamReader OpenText() {
            throw new NotImplementedException();
        }

        public Stream OpenRead() {
            return Owner.Open(this, System.IO.FileMode.Open);
        }


        public void Delete() {
            Owner.Delete(this);
        }

        public void Update(Stream stream) {
            Owner.Update(this, stream);
        }
    }

    public class GoogleDriveChangesEnumerable : IDocumentEnumerable {
        Queue<GoogleDriveDocument> Changes { get; set; }

        GoogleDriveDocumentStore Owner { get; set; }
        public string StartPageToken { get; internal set; }
        public string SavedPageToken { get; internal set; }

        internal GoogleDriveChangesEnumerable(GoogleDriveDocumentStore owner, string startPageToken) {
            Owner = owner;
            StartPageToken = startPageToken;
            SavedPageToken = StartPageToken;
            Changes = new Queue<GoogleDriveDocument>();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<IDocument> GetEnumerator() {
            if (Changes.Count <= 0) {
                var pageToken = StartPageToken;
                foreach (var change in Owner.Changes(ref pageToken)) {
                    if (change.FullName.StartsWith(Owner.Root.FullName))
                        Changes.Enqueue(change);
                }
                StartPageToken = pageToken;
            }

            var changes = Changes;
            Changes = new Queue<GoogleDriveDocument>();

            return changes.GetEnumerator();
        }
    }

    [DocumentStore("google")]
    public class GoogleDriveDocumentStore : DocumentStore {
        static string[] Scopes = { DriveService.Scope.DriveFile, DriveService.Scope.DriveMetadata };
        static string ApplicationName = "DocumentSync - Google Drive Plugin";
        static string ApplicationPath;
        static string CachePath;

        public static readonly string DirectoryType = "application/vnd.google-apps.folder";
        public readonly string RequiredFields = "createdTime, id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";

        UserCredential credential;
        DriveService DriveService;
        string savedStartPageToken;

        GoogleDriveDocument mRoot;
        string RootPath;
        public override IDocument Root => mRoot;


        GoogleDriveDocumentWatcher ChangeThread { get; set; }
        public GoogleDocumentCache Cache { get; private set; }
        // Drive Id Cache

        private void Initialize(DriveService driveService, String rootFolder) {
            DriveService = driveService;

            Cache = new GoogleDocumentCache();

            ChangeThread = new GoogleDriveDocumentWatcher(this);
            ChangeThread.EnableRaisingEvents = true;

            RootPath = rootFolder;
            var root = GetById(rootFolder);
            if (root == null)
                root = GetByPath(rootFolder);

            if (root == null)
                throw new Exception("Unable to get root Folder");

            mRoot = (GoogleDriveDocument)root;
        }

        public GoogleDriveDocumentStore(String rootFolder) {
            ApplicationPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Name
            );
            CachePath = Path.Combine(ApplicationPath, "cache");
            System.IO.Directory.CreateDirectory(CachePath);
            Console.WriteLine("Application Path: {0}", ApplicationPath);

            // Authenticate
            using (var stream =
                   System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LibDocumentSync.client_secret.json")) {
                var credPath = Path.Combine(ApplicationPath, ".credentials/drive-dotnet-sync.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    System.Threading.CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            var driveService = new DriveService(new global::Google.Apis.Services.BaseClientService.Initializer() {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            Initialize(driveService, rootFolder);
        }

        public GoogleDriveDocumentStore(DriveService driveService, String rootFolder) {
            Initialize(driveService, rootFolder);
        }

        protected GoogleDriveDocument EncapsulateDocument(DriveFile file) {
            // Update Cache
            // Update Index
            var document = new GoogleDriveDocument(this, file);
            Cache.Add(document);
            return document;
        }

        public override IDocument Create(string path, DocumentType type) {
            var tail = System.IO.Path.GetFileName(path);
            var head = System.IO.Path.GetDirectoryName(path);

            Console.WriteLine("Create: {0} {1}", head, tail);
            return Create(GetByPath(head), tail, type);
        }

        public override IDocument Create(IDocument parent, string name, DocumentType type) {
            // Verify existence of Parent.
            var file = new DriveFile();
            file.Name = name;
            // TODO: Expand Creation options.
            file.ModifiedTime = DateTime.Now;

            if (parent != null) {
                file.Parents = new string[] { parent.Id };
            }
            else {
                file.Parents = new string[] { Root.Id };
            }

            if (type == DocumentType.Directory) {
                file.MimeType = DirectoryType;
            }

            var createRequest = DriveService.Files.Create(file);
            createRequest.Fields = RequiredFields;
            file = createRequest.Execute();

            return new GoogleDriveDocument(this, file);
        }

        public override void Update(IDocument src) {
            var fileMetadata = new DriveFile() {
                ModifiedTime = src.ModifiedTime
            };

            Console.WriteLine("Meta update {0} - ", src.FullName, src.Id);
            var updateRequest = DriveService.Files.Update(fileMetadata, src.Id);
            updateRequest.Fields = "id";
            updateRequest.Execute();

            // TODO: Update DriveFile Cache
            // Return new Document;
        }

        public override void Update(IDocument document, Stream stream) {
            Console.WriteLine("Updating {0}", document.FullName);
            var updateRequest = DriveService.Files.Update(null, document.Id, stream, "");
            updateRequest.Fields = "id, kind, mimeType, md5Checksum, modifiedTime, name, parents, size, version";
            updateRequest.Upload();
            if (updateRequest.GetProgress().Status != global::Google.Apis.Upload.UploadStatus.Completed)
                throw updateRequest.GetProgress().Exception;

            // TODO: Update DriveFile Cache
            // Return new Document;
        }

        public override void Delete(IDocument arg0) {
            var deleteRequest = DriveService.Files.Delete(arg0.Id);
            Console.WriteLine(deleteRequest.Execute());
        }

        public override Stream Open(IDocument document, System.IO.FileMode mode) {
            switch (mode) {
                case System.IO.FileMode.Open:
                    var tmpFile = Path.Combine(CachePath, document.Id);
                    var fStream = System.IO.File.Open(tmpFile, FileMode.Create);
                    Console.WriteLine("Opening {0}", tmpFile);
                    var downloadRequest = DriveService.Files.Get(document.Id);
                    var progress = downloadRequest.DownloadWithStatus(fStream);
                    if (progress.Status != global::Google.Apis.Download.DownloadStatus.Completed)
                        throw progress.Exception;
                    fStream.Close();
                    return System.IO.File.OpenRead(tmpFile);

                default:
                    throw new NotImplementedException();
            }
            throw new NotImplementedException();
        }

        public override void MoveTo(IDocument src, IDocument dst) {

            if (dst.IsFile) {
                // Overwrite
            }
            // Update Parent
            throw new NotImplementedException();
        }
        public override void MoveTo(IDocument src, string name) {
            throw new Exception("stub");
            // Parse name as path
            // Does exist?
            // Is File?
            // Is Directory?
        }

        internal IEnumerable<IDocument> GoogleFilesList(string q) {
            FilesResource.ListRequest listRequest = DriveService.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = String.Format("nextPageToken, files({0})", RequiredFields);
            listRequest.Q = q;
            Console.WriteLine(q);

            do {
                FileList files = listRequest.Execute();
                listRequest.PageToken = files.NextPageToken;
                foreach (var file in files.Files) {
                    yield return EncapsulateDocument(file);
                }
            } while (!String.IsNullOrEmpty(listRequest.PageToken));
        }

        internal IEnumerable<IDocument> ListDirectory(IDocument d) {
            return GoogleFilesList(String.Format("'{0}' in parents and trashed != true", d.Id));
        }

        public override IEnumerable<IDocument> EnumerateFiles(IDocument path, string filter = "8", SearchOption options = SearchOption.AllDirectories) {
            return GoogleFilesList(String.Format("'{0}' in parents and trashed != true", path.Id));
        }

        public override IEnumerator<IDocument> GetEnumerator() {
            return EnumerateFiles(Root).GetEnumerator();
        }

        public override IDocument GetById(string id) {
            // Check Cache First
            GoogleDriveDocument document;
            Cache.Documents.TryGetValue(id, out document);
            if (document == null) {
                var resource = DriveService.Files.Get(id);
                resource.Fields = RequiredFields;

                try {
                    document = EncapsulateDocument(resource.Execute());
                }
                catch (global::Google.GoogleApiException e) {
                    if (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                        return null;
                }
            }

            return document;
        }

        public override IDocument GetByPath(string path) {
            path = MakeAbsolutePath(path);
            Console.WriteLine(path);
            IDocument dir = GetById("root");
            if (String.IsNullOrEmpty(path) || path == "/") {
                return dir;
            }

            var separators = new char[] {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            };
            var paths = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);


            for (var i = 0; i < paths.Length; ++i) {
                var folder = paths[i];

                if (folder == "My Drive") {
                    continue;
                }

                var results = GoogleFilesList(String.Format("'{0}' in parents and name = '{1}' and trashed != true", dir.Id, folder)).ToArray();

                if (results.Length > 1) {
                    throw new NotImplementedException("Multiple files with same name");
                }
                if (results.Length != 1) {
                    return null;
                    throw new FileNotFoundException("File not found");
                }

                dir = results[0];
                Console.WriteLine("{0} {1} {2} {3}", folder, dir.IsDirectory, i, paths.Length);
                if (!dir.IsDirectory && i != (paths.Length - 1))
                    throw new DirectoryNotFoundException("File is not a directory");
            }

            return dir;
        }

        public string GetPath(IDocument document) {
            List<string> paths = new List<string>();

            IDocument parent = document;
            while (parent != null) {
                paths.Insert(0, parent.Name);
                parent = parent.Parent;
            }

            paths.Insert(0, "/");

            var path = System.IO.Path.Combine(paths.ToArray());
            if (Root == null || Root.FullName.Length > path.Length)
                return path;

            // Console.WriteLine("GetPath: {0} {1}", Root.FullName, path);
            return path.Substring(Root.FullName.Length);
        }

        public GoogleDriveChangesEnumerable GetChangeLog(string startPageToken = null) {
            string savedStartPageToken = startPageToken;

            if (startPageToken == null) {
                var response = DriveService.Changes.GetStartPageToken().Execute();
                Console.WriteLine("Start token: " + response.StartPageTokenValue);
                savedStartPageToken = response.StartPageTokenValue;
            }

            return new GoogleDriveChangesEnumerable(this, savedStartPageToken);
        }


        public List<GoogleDriveDocument> Changes(ref string pageToken) {
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
                    }
                    else {
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

        private string MakeAbsolutePath(string path) {
            if (Root is null)
                return path;
            return Path.GetFullPath(Root.FullName + "/" + path);
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

        public override DocumentWatcher Watch() {
            return new GoogleDriveDocumentWatcher(this);
        }
    }

    public class GoogleDriveDocumentWatcher : DocumentWatcher {
        private GoogleDriveDocumentStore Owner { get; set; }

        private GoogleDriveChangesEnumerable ChangeLog { get; set; }
        private List<DocumentEventArgs> Events { get; set; }

        internal GoogleDriveDocumentWatcher(GoogleDriveDocumentStore owner) {
            Owner = owner;
            ChangeLog = Owner.GetChangeLog();
            Events = new List<DocumentEventArgs>();

            // Thread instead of Timer for precision
            PollThread = new System.Threading.Thread(() => {
                Console.WriteLine("Starting {0} Event Loop", Owner);
                var begin = DateTime.UtcNow;
                while (true) {
                    Check();
                    var sleepTime = 25000 - (int)(DateTime.UtcNow - begin).TotalMilliseconds;
                    if (sleepTime > 0)
                        System.Threading.Thread.Sleep(sleepTime);
                    begin = DateTime.UtcNow;
                }
            });
            PollThread.Start();
        }

        ~GoogleDriveDocumentWatcher() {
            PollThread.Abort();
        }

        public override DocumentEventArgs Classify(IDocument change) {
            Console.WriteLine("{0} {1}", change.Id, change.FullName);
            Console.WriteLine("\t{0}\n\t{1}", change.CreatedTime, change.ModifiedTime);

            // Note: Is event time required for classification?

            // Deleted. Detected via Metdata.
            if (!change.Exists || change.Trashed) {
                //Console.WriteLine("Removed: {0} {1}", change.Id, change.FullName);
                return new DocumentEventArgs(DocumentChangeType.Deleted, change);
            }
            else {
                if (change.CreatedTime >= change.ModifiedTime)
                    return new DocumentEventArgs(DocumentChangeType.Created, change);
                else
                    return new DocumentEventArgs(DocumentChangeType.Changed, change);
                // This may require a cache.
                // events.Add(new DocumentEventArgs(DocumentChangeType.Renamed, changCopye));
                /*
                    Console.WriteLine("{0} {1} {2}", syncEvent, change.FileId, (change.File != null) ? change.File.Name : null);
                    SyncQueue.Enqueue(syncEvent);
                */
            }
        }

        private void Check() {
            var events = new List<DocumentEventArgs>();

            foreach (var change in ChangeLog) {
                events.Add(Classify(change));
            }

            if (!EnableRaisingEvents) {
                Events.Clear();
            }

            if (PauseRaisingEvents)
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