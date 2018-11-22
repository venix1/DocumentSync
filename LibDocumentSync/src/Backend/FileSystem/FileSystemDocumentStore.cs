using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DocumentSync {
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

    public class FileSystemDocumentStore : DocumentStore {
        string Root { get; set; }

        public FileSystemDocumentStore(string root) {
            if (System.IO.Directory.Exists(root)) {
                // TOOD: If not a document store get explicit permission
                // throw new Exception("Directory exists");
            }
            else {
                Directory.CreateDirectory(root);
            }
            Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        }

        public override IDocument Create(string path, DocumentType type) {
            path = Path.Combine(Root, path);
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

        public override void Copy(IDocument src, IDocument dst) {
            // TODO: Set attributes

            using (var fp = src.OpenRead()) {
                dst.Update(fp);
            }
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
                throw new Exception("File does not exist.");

            return new FileSystemDocument(this, fi);
        }

        public override DocumentWatcher Watch() {
            return new FileSystemDocumentWatcher();
        }

        /*
         * TODO: Likely to break on large file counts
         */
        public override IEnumerator<IDocument> GetEnumerator() {
            foreach (var item in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories)) {
                yield return LoadDocument(item);
            }
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
            return Path.GetFullPath(Path.Combine(Root, "." + Path.DirectorySeparatorChar + path));
        }
        /**
         * Translates Filesystem path to Document path
         */
        internal string MakeRelative(string path) {
            return path.Substring(Root.Length);
        }
    }

    public class FileSystemDocumentWatcher : DocumentWatcher {
        Queue<EventArgs> SyncQueue { get; set; }

        internal FileSystemDocumentWatcher() {
            /*
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
            */
        }

        public override DocumentEventArgs Classify(IDocument change) {
            throw new NotImplementedException();
        }
        private void OnChanged(object source, FileSystemEventArgs e) {
            lock (SyncQueue) {
                /*
                try {
                    Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
                    EventArgs syncEvent = null;
                    UnifiedFile unifiedFile = GetUnifiedFile(e.FullPath);
                    switch (e.ChangeType) {
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
                } catch (Exception ex) {
                    Console.WriteLine(ex);
                }
                */
            }
        }

        private void OnRenamed(object source, RenamedEventArgs e) {
            /*
            lock (SyncQueue) {
                Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
                SyncQueue.Enqueue(new FileRenamedEventArgs(e.OldFullPath, GetUnifiedFile(e.FullPath)));
            }
            */
        }
    }
}
