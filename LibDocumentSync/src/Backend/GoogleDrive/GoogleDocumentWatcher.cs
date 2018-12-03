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