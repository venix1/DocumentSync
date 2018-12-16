using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DocumentSync.Backend.FileSystem {
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

                    var sleepTime = 15000 - (int)(DateTime.UtcNow - begin).TotalMilliseconds;
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
            Console.WriteLine("-- {0} {1}", path, document.FullName);
            var evt = new DocumentEventArgs(DocumentChangeType.Changed, document);
            Events.Add(evt);
        }
        private void OnFileCreated(object source, FileSystemEventArgs e) {
            Console.WriteLine("{0} {1} {2}", this, e.ChangeType, e.FullPath);
            var path = e.FullPath.Substring(Owner.RootPath.Length);
            Console.WriteLine("-- {0}", path);
            var document = Owner.GetByPath(path);
            var evt = new DocumentEventArgs(DocumentChangeType.Created, document);
            Events.Add(evt);
        }
        private void OnFileDeleted(object source, FileSystemEventArgs e) {
            Console.WriteLine("{0} {1} {2}", this, e.ChangeType, e.FullPath);
            var path = e.FullPath.Substring(Owner.RootPath.Length);
            var document = Owner.GetByPath(path);
            if (document == null)
                throw new DocumentException("Unexpected event");
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

            if (Events.Count > 0)
                Console.WriteLine("Dispatching {0} {1}", this, Events.Count);

            var events = Events;
            Events = new List<DocumentEventArgs>();
            // Dispatch Events
            foreach (var e in events) {
                switch (e.ChangeType) {
                    case DocumentChangeType.Created:
                        if (e.Document.Exists)
                            Created?.Invoke(Owner, e);
                        else
                            Console.WriteLine("Document deleted ignoring Created event");
                        break;
                    case DocumentChangeType.Changed:
                        if (e.Document.Exists)
                            Changed?.Invoke(Owner, e);
                        else
                            Console.WriteLine("Document deleted ignoring Changed event");
                        break;
                    case DocumentChangeType.Deleted:
                        Deleted?.Invoke(Owner, e);
                        break;
                    case DocumentChangeType.Renamed:
                        Renamed?.Invoke(Owner, e);
                        break;
                }
            }
        }

        public override DocumentEventArgs Classify(IDocument change) {
            throw new NotImplementedException();
        }
    }
}