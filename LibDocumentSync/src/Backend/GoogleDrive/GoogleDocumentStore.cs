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

            // Prime Root.FullName & replace RootPath with Drive names
            RootPath = root.FullName;
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
                var credPath = Path.Combine(ApplicationPath, ".credentials/drive-dotnet-sync");

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
            GoogleDriveDocument document;
            if (Cache.TryGetValue(file.Id, out document)) {
                document.Document = file;
            }
            else {
                // Cache.AddDocument(this, file);
                document = new GoogleDriveDocument(this, file);
                Cache.Add(document);
            }
            return document;
        }

        public override IDocument Create(string path, DocumentType type) {
            var tail = System.IO.Path.GetFileName(path);
            var head = System.IO.Path.GetDirectoryName(path);

            Console.WriteLine("Create: {0} {1}", head, tail);
            return Create(GetByPath(head), tail, type);
        }

        public override IDocument Create(IDocument parent, string name, DocumentType type) {
            // TODO: Verify existence of Parent.
            // TODO: Throw error on duplicate files
            // TODO: Expand Creation options.
            // TODO: Atomic create. Creates, uploads content, and sets metadata in 1 operation.

            var file = new DriveFile();
            file.Name = name;
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

            return EncapsulateDocument(file);
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
            // Console.WriteLine(q);

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
            Cache.TryGetValue(id, out document);
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

        public override IDocument TryGetByPath(string path) {
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
                    // throw new FileNotFoundException("File not found: " + path);
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
            else
                return path.Substring(Root.FullName.Length);
        }

        public GoogleDriveChangesEnumerable GetChangeLog(string startPageToken = null) {
            string savedStartPageToken = startPageToken;

            if (startPageToken == null) {
                var response = DriveService.Changes.GetStartPageToken().Execute();
                // Console.WriteLine("Start token: " + response.StartPageTokenValue);
                savedStartPageToken = response.StartPageTokenValue;
            }

            return new GoogleDriveChangesEnumerable(this, savedStartPageToken);
        }


        public List<GoogleDriveDocument> Changes(ref string pageToken) {
            var list = new List<GoogleDriveDocument>();
            string newStartPageToken = null;
            while (pageToken != null) {
                //Console.WriteLine("PageToken: {0}", pageToken);
                var request = DriveService.Changes.List(pageToken);

                request.IncludeRemoved = true;
                request.Fields = String.Format("changes(fileId,kind,removed,time,file({0})),newStartPageToken,nextPageToken", RequiredFields);
                request.Spaces = "drive";

                var changes = request.Execute();
                foreach (var change in changes.Changes) {
                    // TODO: Special Handling of Delete event

                    GoogleDriveDocument document;
                    if (change.Removed.Value) {
                        Cache.TryGetValue(change.FileId, out document);
                        Cache.Remove(change.FileId);
                        if (document == null) {
                            var file = new DriveFile {
                                Id = change.FileId
                            };
                            // Need a Document instance for deleted file. That
                            // makes this the one exception to EncapsulateDocument
                            document = new GoogleDriveDocument(this, file);
                        }
                    }
                    else {
                        document = EncapsulateDocument(change.File);
                    }
                    Console.WriteLine("Change: {0} {1} {2} {3}", document.Id, 
                        document.FullName, document.ModifiedTime, change.Removed);

                    {
                        var parent = document;
                        while (parent != null) {
                            if (parent.Id == Root.Id) {
                                list.Add(document);
                                break;
                            }
                            parent = (GoogleDriveDocument)parent.Parent;
                        }
                    }
                }

                /*
                if (changes.NewStartPageToken != null) {
                    Console.WriteLine("NewStartPageToken: {0}", changes.NewStartPageToken);
                }

                if (changes.NextPageToken != null) {
                    Console.WriteLine("NextPageToken: {0}", changes.NextPageToken);
                }
                */
                newStartPageToken = changes.NewStartPageToken;
                pageToken = changes.NextPageToken;
            }
            pageToken = newStartPageToken;
            // Console.WriteLine("StartPageToken: {0}", pageToken);

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
}