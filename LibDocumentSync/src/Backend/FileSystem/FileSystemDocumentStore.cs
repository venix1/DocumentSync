using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DocumentSync.Backend.FileSystem {

    public class FileSystemDocument : IDocument {
        public IDocumentStore Owner { get; private set; }
        // FileSystemDocumentStore Owner { get; set; }
        FileSystemInfo Document { get; set; }

        FileInfo FileInfo { get { return Document as FileInfo; } set { Document = value; } }
        DirectoryInfo DirectoryInfo { get { return Document as DirectoryInfo; } set { Document = value; } }

        internal FileSystemDocument(FileSystemDocumentStore owner, FileSystemInfo fsi) {
            Owner = owner;
            Document = fsi;
        }

        // IDocument Interface

        public string Id { get => Document.FullName; }
        public string Name { get => Document.Name; }
        public string FullName { get => ((FileSystemDocumentStore)Owner).MakeRelative(Document.FullName); }
        public long Size { get => IsFile ? FileInfo.Length : 0; }

        public DateTime CreatedTime => Document.CreationTime;
        public DateTime ModifiedTime {
            get => Document.LastWriteTime;
            set => UpdateLastWriteTime(value);
        }
        public IDocument Parent => throw new NotImplementedException();
        public long Version => throw new NotImplementedException();
        public bool Deleted => throw new NotImplementedException();
        public bool Exists => throw new NotImplementedException();
        public bool Trashed => throw new NotImplementedException();

        public StreamReader OpenText() {
            return FileInfo.OpenText();
        }

        public Stream OpenRead() {
            return FileInfo.OpenRead();
        }

        public bool IsDirectory {
            get {
                return DirectoryInfo != null;
            }
        }

        public bool IsFile {
            get {
                return FileInfo != null;
            }
        }

        public System.Collections.IEnumerable Children {
            get { throw new Exception("stub"); }
        }

        public string Md5Checksum {
            get { return CalculateMd5Sum(); }
        }

        private string _Md5Checksum;
        private string CalculateMd5Sum() {
            using (var md5hash = MD5.Create()) {
                if (IsFile) {
                    using (var stream = File.OpenRead(FileInfo.FullName)) {
                        var hash = md5hash.ComputeHash(stream);
                        _Md5Checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                else {
                    _Md5Checksum = "d41d8cd98f00b204e9800998ecf8427e";
                }
            }
            return _Md5Checksum;
        }

        public void Update(System.IO.Stream stream) {
            using (var fp = FileInfo.Create()) {
                stream.CopyTo(fp);
            }
        }

        public void UpdateLastWriteTime(DateTime value) {
            FileInfo.LastWriteTime = value;
        }

        public void Delete() {
            throw new NotImplementedException("File Modifications not implemented");
        }
    }

    [DocumentStore("fs")]
    public class FileSystemDocumentStore : DocumentStore {
        FileSystemDocument mRoot;
        public string RootPath { get; private set; }

        public override IDocument Root => mRoot;

        public FileSystemDocumentStore(string root) {
            if (System.IO.Directory.Exists(root)) {
                // TOOD: If not a document store get explicit permission
                // throw new Exception("Directory exists");
            }
            else {
                Directory.CreateDirectory(root);
            }
            RootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            mRoot = (FileSystemDocument)GetByPath(RootPath);
        }

        public override Stream Open(IDocument document, System.IO.FileMode mode) {
            throw new NotImplementedException();
        }

        public override IDocument Create(string path, DocumentType type) {
            path = Path.Combine(RootPath, path);
            var tail = System.IO.Path.GetFileName(path);
            var head = System.IO.Path.GetDirectoryName(path);

            return Create(GetByPath(head), tail, type);
        }

        public override IDocument Create(IDocument parent, string name, DocumentType type) {
            FileSystemInfo fsi;
            var path = MakeAbsolute(Path.Combine(parent.FullName, name));
            Console.WriteLine("Creating {0}", path);

            switch (type) {
                case DocumentType.File:
                    var fi = new FileInfo(path);
                    fi.Create().Dispose();
                    fsi = fi;
                    break;
                case DocumentType.Directory:
                    var di = new DirectoryInfo(path);
                    di.Create();
                    fsi = di;
                    break;
                default:
                    throw new Exception("DocumentType not implemented");
            }
            return new FileSystemDocument(this, fsi);
        }

        public override void Delete(IDocument arg0) {
            if (arg0.IsFile)
                File.Delete(arg0.FullName);

            if (arg0.IsDirectory)
                Directory.Delete(arg0.FullName, true);
        }

        public override void MoveTo(IDocument src, IDocument dst) {
            throw new NotImplementedException();
        }
        public override void MoveTo(IDocument src, string name) {
            throw new NotImplementedException();
        }

        public override IDocument GetById(string id) {
            // For now, id and Path are the same.
            return GetByPath(id);
        }

        public override IDocument GetByPath(string path) {
            var absPath = MakeAbsolute(path);
            FileSystemInfo fi;
            if (System.IO.Directory.Exists(absPath))
                fi = new DirectoryInfo(absPath);
            else if (System.IO.File.Exists(absPath))
                fi = new FileInfo(absPath);
            else
                return null;

            return new FileSystemDocument(this, fi);
        }

        public override DocumentWatcher Watch() {
            return new FileSystemDocumentWatcher(this);
        }

        /*
         * TODO: Likely to break on large file counts
         */
        public override IEnumerator<IDocument> GetEnumerator() {
            foreach (var item in Directory.EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories)) {
                yield return LoadDocument(item);
            }
        }

        public override IEnumerable<IDocument> EnumerateFiles(IDocument path, string filter = "*", SearchOption options = SearchOption.AllDirectories) {
            throw new NotImplementedException();
        }

        private FileSystemDocument LoadDocument(string path) {
            // Cache Lookups
            if (Directory.Exists(path)) {
                return new FileSystemDocument(this, new DirectoryInfo(path));

            }
            else if (File.Exists(path)) {
                return new FileSystemDocument(this, new FileInfo(path));
            }

            throw new NotImplementedException();
        }

        /**
         * Translates Document path to Filesystem Path
         */
        internal string MakeAbsolute(string path) {
            return Path.GetFullPath(Path.Combine(RootPath, "." + Path.DirectorySeparatorChar + path));
        }
        /**
         * Translates Filesystem path to Document path
         */
        internal string MakeRelative(string path) {
            return path.Substring(RootPath.Length);
        }

        public override void Update(IDocument src, Stream stream) {
            throw new NotImplementedException();
        }
        public override void Update(IDocument document) {
            // Does nothing? Answer the question Should Documents have
            // throw new NotImplementedException();
            // FileInfo.LastWriteTime = document.LastWriteTime;
        }
    }

    public class FileSystemDocumentWatcher : DocumentWatcher {
        private FileSystemDocumentStore Owner { get; set; }
        private List<DocumentEventArgs> Events { get; set; }
        private FileSystemWatcher Watcher { get; set; }

        internal FileSystemDocumentWatcher(FileSystemDocumentStore owner) {
            Owner = owner;
            Events = new List<DocumentEventArgs>();

            Watcher = new FileSystemWatcher();
            Watcher.Path = Owner.RootPath;
            Watcher.NotifyFilter = System.IO.NotifyFilters.LastWrite
                | System.IO.NotifyFilters.FileName
                | System.IO.NotifyFilters.DirectoryName;
            Watcher.Filter = "";
            Watcher.Changed += OnFileChanged;

            Watcher.Created += OnFileCreated;
            Watcher.Deleted += OnFileDeleted;
            Watcher.Renamed += OnFileRenamed;


            Watcher.EnableRaisingEvents = true;

            // Thread instead of Timer for precision
            PollThread = new System.Threading.Thread(() => {
                Console.WriteLine("Watching {0} Event Loop {1}", Owner, Watcher.Path);
                var begin = DateTime.UtcNow;
                while (true) {
                    Check();

                    var sleepTime = 5000 - (int)(DateTime.UtcNow - begin).TotalMilliseconds;
                    if (sleepTime > 0)
                        System.Threading.Thread.Sleep(sleepTime);
                    begin = DateTime.UtcNow;
                }

            });
            PollThread.Start();
        }
        ~FileSystemDocumentWatcher() {
            PollThread.Abort();
        }

        private void OnFileChanged(object source, FileSystemEventArgs e) {
            Console.WriteLine("{0} {1} {2}", this, e.ChangeType, e.FullPath);
            var path = e.FullPath.Substring(Owner.RootPath.Length);
            var document = Owner.GetByPath(path);
            var evt = new DocumentEventArgs(DocumentChangeType.Changed, document);
            Events.Add(evt);
        }
        private void OnFileCreated(object source, FileSystemEventArgs e) {
            Console.WriteLine("{0} {1} {2}", this, e.ChangeType, e.FullPath);
            var path = e.FullPath.Substring(Owner.RootPath.Length);
            var document = Owner.GetByPath(path);
            var evt = new DocumentEventArgs(DocumentChangeType.Created, document);
            Events.Add(evt);
        }
        private void OnFileDeleted(object source, FileSystemEventArgs e) {
            Console.WriteLine("{0} {1} {2}", this, e.ChangeType, e.FullPath);
            var path = e.FullPath.Substring(Owner.RootPath.Length);
            var document = Owner.GetByPath(path);
            var evt = new DocumentEventArgs(DocumentChangeType.Deleted, document);
            Events.Add(evt);
        }
        private void OnFileRenamed(object source, RenamedEventArgs e) {
            Console.WriteLine("{0} {1} {2}", this, e.ChangeType, e.FullPath);
            var path = e.FullPath.Substring(Owner.RootPath.Length);
            var document = Owner.GetByPath(path);
            var evt = new DocumentEventArgs(DocumentChangeType.Renamed, document);

            Events.Add(evt);
        }

        private void Check() {
            Console.WriteLine("{0} event check {1} {2}", this, EnableRaisingEvents, PauseRaisingEvents);
            if (!EnableRaisingEvents) {
                Events.Clear();
            }

            if (PauseRaisingEvents)
                return;

            Console.WriteLine("Dispatching {0} {1}", this, Events.Count);
            // Dispatch Events
            foreach (var e in Events) {
                switch (e.ChangeType) {
                    case DocumentChangeType.Created:
                        Created?.Invoke(Owner, e);
                        break;
                    case DocumentChangeType.Changed:
                        Changed?.Invoke(Owner, e);
                        break;
                    case DocumentChangeType.Deleted:
                        Deleted?.Invoke(Owner, e);
                        break;
                    case DocumentChangeType.Renamed:
                        Renamed?.Invoke(Owner, e);
                        break;
                }
            }
            Events.Clear();
        }

        public override DocumentEventArgs Classify(IDocument change) {
            throw new NotImplementedException();
        }
    }
}